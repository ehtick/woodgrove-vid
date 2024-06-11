using System.Net;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using WoodgroveDemo.Helpers;
using WoodgroveDemo.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.VerifiedID.Presentation;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Identity.VerifiedID;

namespace WoodgroveDemo.Controllers.Presentation;

[ApiController]
[Route("[controller]")]
public class PresentController : ControllerBase
{

    protected readonly IConfiguration _Configuration;
    protected TelemetryClient _Telemetry;
    protected IMemoryCache _Cache;
    protected AppSettings _AppSettings { get; set; }
    protected readonly IHttpClientFactory _HttpClientFactory;
    public ResponseToClient _Response { get; set; } = new ResponseToClient();

    public PresentController(TelemetryClient telemetry, IConfiguration configuration, IMemoryCache cache, IHttpClientFactory httpClientFactory)
    {
        _Configuration = configuration;
        _Cache = cache;
        _Telemetry = telemetry;
        _HttpClientFactory = httpClientFactory;

        // Load the settings of this demo
        _AppSettings = new AppSettings(configuration, "Presentation", true);
    }

    [AllowAnonymous]
    [HttpGet("/api/Presentation/Present")]

    public async Task<ResponseToClient> Get()
    {
        // Clear session
        this.HttpContext.Session.Clear();

        // Initiate the status object
        Status status = new Status("Presentation", "Present");

        try
        {
            // Create a presentation request object
            PresentationRequest request = RequestHelper.CreatePresentationRequest(_AppSettings, this.Request);

            // Serialize the request object to JSON HTML format
            _Response.RequestPayload = request.ToHtml();

            // Prepare the HTTP request with the Bearer access token and the request body
            var client = _HttpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await MsalAccessTokenHandler.AcquireToken(_AppSettings, _Cache));

            // Call the Microsoft Entra ID request endpoint            
            HttpResponseMessage res = await client.PostAsync(
                _AppSettings.RequestUrl,
                new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

            _Response.ResponseBody = await res.Content.ReadAsStringAsync();
            HttpStatusCode statusCode = res.StatusCode;

            if (statusCode == HttpStatusCode.Created)
            {
                PresentationResponse presentationResponse = PresentationResponse.Parse(_Response.ResponseBody);
                _Response.ResponseBody = presentationResponse.ToHtml();

                _Response.QrCodeUrl = presentationResponse.URL;

                // Add the state ID to the user's session object 
                this.HttpContext.Session.SetString("state", request.Callback.State);

                // Add the global cache with the request status
                status.RequestStateId = request.Callback.State;
                status.RequestStatus = UserMessages.REQUEST_CREATED;
                status.AddHistory(status.RequestStatus, status.CalculateExecutionTime());

                // Send telemetry from this web app to Application Insights.
                AppInsightsHelper.TrackApi(_Telemetry, this.Request, status);

                // Add the status object to the cache
                _Cache.Set(request.Callback.State, status.ToString(), DateTimeOffset.Now.AddMinutes(AppSettings.CACHE_EXPIRES_IN_MINUTES));
            }
            else
            {
                AppInsightsHelper.TrackError(_Telemetry, this.Request, UserMessages.ERROR_API_ERROR, _Response.ResponseBody);
                _Response.ErrorMessage = _Response.ResponseBody;
                _Response.ErrorUserMessage = ResponseError.Parse(_Response.ResponseBody).GetUserMessage();
            }
        }
        catch (Exception ex)
        {
            AppInsightsHelper.TrackError(_Telemetry, this.Request, ex);
            _Response.ErrorMessage = ex.Message;
        }

        return _Response;
    }

}