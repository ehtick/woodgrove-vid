using System.Net;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.VerifiedID.AdminApi;
using WoodgroveDemo.Helpers;
using WoodgroveDemo.Models;

namespace WoodgroveDemo.Controllers;

[ApiController]
[Route("[controller]")]
public class RevokeController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    public AppSettings _AppSettings { get; set; }
    private readonly ILogger<CallbackController> _logger;
    private IMemoryCache _cache;
    private TelemetryClient _Telemetry;


    public RevokeController(ILogger<CallbackController> logger, IConfiguration configuration, IMemoryCache cache, IHttpClientFactory httpClientFactory, TelemetryClient telemetry)
    {
        _logger = logger;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _Telemetry = telemetry;

        // Load the settings of this demo
        _AppSettings = new AppSettings(configuration);
    }

    [AllowAnonymous]
    [HttpGet("/api/revoke")]
    public async Task<string> GetAsync()
    {
        Status newStatus = new Status();
        try
        {
            // Get the current user's state ID from the user's session
            string state = this.HttpContext.Session.GetString("state");

            // If the state object was not found, return an error message
            if (string.IsNullOrEmpty(state))
            {
                return "Session object not foud.";
            }

            // Try to read the request status object from the global cache using the state ID key
            Status status = null;

            if (_cache.TryGetValue(state, out string requestState))
            {
                status = Status.Parse(requestState);
            }
            else
            {
                return "Cache object not found.";
            }

            if (string.IsNullOrEmpty(status.IndexedClaimValue))
            {
                return "Indexed claim value not found.";
            }

            // The VC Request API is an authenticated API. 
            // To call the API, first aquire an access token which will be sent as bearer to the VC Request API
            var accessToken = await MsalAccessTokenHandler.GetAccessToken(_AppSettings, new string[] { _AppSettings.RevokeCredentialsDemo.Scope });
            if (accessToken.Item1 == String.Empty)
            {
                return String.Format("Failed to acquire access token: {0} : {1}", accessToken.error, accessToken.error_description);
            }

            // Prepare the HTTP request with the Bearer access token and the request body
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.token);

            // Calculat the search value
            string HashedSearchClaimValue = string.Empty;
            using (var sha256 = SHA256.Create())
            {
                var input = _AppSettings.RevokeCredentialsDemo.Contract + status.IndexedClaimValue;
                byte[] inputasbytes = Encoding.UTF8.GetBytes(input);
                HashedSearchClaimValue = Convert.ToBase64String(sha256.ComputeHash(inputasbytes));
                HashedSearchClaimValue = System.Net.WebUtility.UrlEncode(HashedSearchClaimValue);
            }

            string url = $"{_AppSettings.RevokeCredentialsDemo.Endpoint}?filter=indexclaimhash%20eq%20{HashedSearchClaimValue}";
            HttpResponseMessage response = await client.GetAsync(url);

            // Read the response content
            string responseString = await response.Content.ReadAsStringAsync();

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return responseString;
            }

            AdminApiResponse apiResponse = AdminApiResponse.Parse(responseString);

            if (apiResponse.value == null || apiResponse.value.Count == 0)
            {
                return "Value object not foud";
            }

            // Check if the credential already invalidated
            if (apiResponse.value[0].status != "valid")
            {
                return $"The credential status is '{apiResponse.value[0].status}', no need to revoke.";
            }

            ///POST v1.0/verifiableCredentials/authorities/:authorityId/contracts/:contractId/credentials/:credentialid/revoke
            url = $"{_AppSettings.RevokeCredentialsDemo.Endpoint}/{apiResponse.value[0].id}/revoke";

            response = await client.PostAsync(url, null);

            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                // Read the response content
                responseString = await response.Content.ReadAsStringAsync();
                return responseString;
            }

            // Send telemetry from this web app to Application Insights.
            newStatus.RequestStatus = "Revocation completed";
            newStatus.Flow = "Revocation";
            newStatus.AddHistory(status.RequestStatus, status.CalculateExecutionTime());
            AppInsightsHelper.TrackApi(_Telemetry, this.Request, newStatus);

            // Add the revoked card indexed claim to the cache
            // We use this method to avoid a case where user can uses the card before Entra ID manages to complete the revolution
            _cache.Set(status.IndexedClaimValue, "revoked", DateTimeOffset.Now.AddMinutes(AppSettings.CACHE_EXPIRES_IN_MINUTES));

            return "OK";
        }
        catch (Exception ex)
        {
            AppInsightsHelper.TrackError(_Telemetry, this.Request, ex);
            return "Error: " + ex.Message;
        }
    }
}