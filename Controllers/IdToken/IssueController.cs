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
using Microsoft.Identity.VerifiedID.Issuance;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Identity.VerifiedID;

namespace WoodgroveDemo.Controllers.IdToken;

[ApiController]
[Route("[controller]")]
public class IssueController : ControllerBase
{
    protected readonly IConfiguration _Configuration;
    protected TelemetryClient _Telemetry;
    protected IMemoryCache _Cache;
    protected AppSettings _AppSettings { get; set; }
    protected readonly IHttpClientFactory _HttpClientFactory;
    public ResponseToClient _Response { get; set; } = new ResponseToClient();

    public IssueController(TelemetryClient telemetry, IConfiguration configuration, IMemoryCache cache, IHttpClientFactory httpClientFactory)
    {
        _Configuration = configuration;
        _Cache = cache;
        _Telemetry = telemetry;
        _HttpClientFactory = httpClientFactory;

        // Load the settings of this demo
        _AppSettings = new AppSettings(configuration, "IdToken", false);
    }

    [AllowAnonymous]
    [HttpPost("/api/IdToken/Issue")]
    public async Task<ResponseToClient> Post()
    {
        // Clear session
        this.HttpContext.Session.Clear();

        // Initiate the status object
        Status status = new Status("IdToken", "Issue");

        try
        {
            // Create an issuance request object
            IssuanceRequest request = RequestHelper.CreateIssuanceRequest(_AppSettings, this.Request, false);

            // Serialize the request object to JSON string format
            _Response.RequestPayload = request.ToString();

            // Prepare the HTTP request with the Bearer access token and the request body
            var client = _HttpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await MsalAccessTokenHandler.AcquireToken(_AppSettings, _Cache));

            // Call the Microsoft Entra ID request endpoint
            HttpResponseMessage response = await client.PostAsync(
                _AppSettings.RequestUrl,
                new StringContent(_Response.RequestPayload, Encoding.UTF8, "application/json"));

            // Serialize the request object to HTML format
            _Response.RequestPayload = request.ToHtml();

            // Read the response content
            _Response.ResponseBody = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Created)
            {
                IssuanceResponse issuanceResponse = IssuanceResponse.Parse(_Response.ResponseBody);
                _Response.ResponseBody = issuanceResponse.ToHtml();
                _Response.QrCodeUrl = issuanceResponse.Url;

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
