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

namespace WoodgroveDemo.Controllers.IdTokenHint;

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
        _AppSettings = new AppSettings(configuration, "IdTokenHint", false);
    }

    [AllowAnonymous]
    [HttpPost("/api/IdTokenHint/Issue")]
    public async Task<ResponseToClient> Post([FromBody] RequestJson json)
    {
        // Clear session
        this.HttpContext.Session.Clear();

        // Initiate the status object
        Status status = new Status("IdTokenHint", "Issue");

        try
        {
            // Create an issuance request object
            IssuanceRequest request = RequestHelper.CreateIssuanceRequest(_AppSettings, this.Request, true);

            // Generate a card number
            Random r = new Random();
            long cardId = r.NextInt64(1234567890, 9976654334);

            // Use the values entered by the user
            request.Claims = new Dictionary<string, string>
                    {
                        { "id", cardId.ToString() },
                        { "given_name", json.FirstName.Trim() },
                        { "family_name", json.LastName.Trim() }
                    };

            // Add the photo
            if (!string.IsNullOrEmpty(json.Photo))
            {
                request.Claims.Add("photo", json.Photo.Trim());
            }

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
                _Response.PinCode = request.PIN.Value;

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

public class RequestJson
{
    public string FirstName { set; get; }
    public string LastName { set; get; }
    public string? Photo { set; get; }
}
