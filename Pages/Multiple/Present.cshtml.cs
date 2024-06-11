using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using WoodgroveDemo.Helpers;
using WoodgroveDemo.Models;
using Microsoft.Identity.VerifiedID.Manifest;
using Microsoft.Identity.VerifiedID.Presentation;

namespace WoodgroveDemo.Pages.Multiple
{
    public class PresentModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private IMemoryCache _cache;
        private TelemetryClient _telemetry;

        // UI elements
        public AppSettings _AppSettings { get; set; }

        public PresentModel(TelemetryClient telemetry, IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _telemetry = telemetry;

            // Load the settings of this demo
            _AppSettings = new AppSettings(configuration,  "Multiple", true);
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
}
