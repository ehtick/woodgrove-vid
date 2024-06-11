using System.Net;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using WoodgroveDemo.Helpers;
using System.Net.Http.Headers;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Identity.VerifiedID.Manifest;
using Microsoft.Identity.VerifiedID.Issuance;
using WoodgroveDemo.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.ApplicationInsights;

namespace WoodgroveDemo.Pages.SelfIssued;
public class IssueModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private IMemoryCache _cache;
    private TelemetryClient _telemetry;

    // UI elements
    public AppSettings _AppSettings { get; set; }

    public IssueModel(TelemetryClient telemetry, IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _telemetry = telemetry;

        // Load the settings of this demo
        _AppSettings = new AppSettings(configuration, "SelfIssued", false);
    }

    public void OnGet()
    {
        // Send telemetry from this web app to Application Insights.
        AppInsightsHelper.TrackPage(_telemetry, this.Request);

        // Get the credential manifest and deserialize
        _AppSettings.ManifestContent = RequestHelper.GetCredentialManifest(_AppSettings.ManifestUrl, _httpClientFactory, _cache, _AppSettings.UseCache);
        Manifest manifest = Manifest.Parse(_AppSettings.ManifestContent);

        _AppSettings.CardDetails = manifest.Display;
        _AppSettings.ManifestContent = manifest.ToHtml();
    }
}

