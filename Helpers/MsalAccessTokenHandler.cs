using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;
using WoodgroveDemo.Models;
using System.Drawing;
using Microsoft.Extensions.Caching.Memory;

namespace WoodgroveDemo.Helpers
{
    public class MsalAccessTokenHandler
    {
        private static X509Certificate2 ReadCertificate(string certificateThumbprint)
        {
            if (string.IsNullOrWhiteSpace(certificateThumbprint))
            {
                throw new ArgumentException("certificateThumbprint should not be empty. Please set the certificateThumbprint setting in the appsettings.json", "certificateThumbprint");
            }
            CertificateDescription certificateDescription = CertificateDescription.FromStoreWithThumbprint(
                 certificateThumbprint,
                 StoreLocation.CurrentUser,
                 StoreName.My);

            DefaultCertificateLoader defaultCertificateLoader = new DefaultCertificateLoader();
            defaultCertificateLoader.LoadIfNeeded(certificateDescription);

            if (certificateDescription.Certificate == null)
            {
                throw new Exception("Cannot find the certificate.");
            }

            return certificateDescription.Certificate;
        }

        public static async Task<string> AcquireToken(AppSettings settings, IMemoryCache cache)
        {
            // Aquire an access token which will be sent as bearer to the request API
            // Try to read the manifest from the cache using its URL key
            //string returnValue = string.Empty;

            // if (!cache.TryGetValue("AppAccessToken", out returnValue))
            // {
            var accessToken = await MsalAccessTokenHandler.GetAccessToken(settings);
            if (accessToken.Item1 == String.Empty)
            {
                throw new Exception(String.Format("Failed to acquire access token: {0} : {1}", accessToken.error, accessToken.error_description));
            }

            //cache.Set("AppAccessToken", accessToken.Item1, DateTimeOffset.Now.AddMinutes(50));
            return accessToken.Item1;
            // }
            // else
            // {
            //     Console.WriteLine("Successfully read the access token from the cache.");
            // }
            //return returnValue;
        }

        public static async Task<(string token, string error, string error_description)> GetAccessToken(AppSettings settings, string[] scopes = null)
        {
            // You can run this sample using ClientSecret or Certificate. The code will differ only when instantiating the IConfidentialClientApplication
            string authority = $"{settings.EntraID.Authority}{settings.EntraID.TenantId}";

            // Since we are using application permissions this will be a confidential client application
            IConfidentialClientApplication app;
            if (!string.IsNullOrWhiteSpace(settings.EntraID.ClientSecret))
            {
                app = ConfidentialClientApplicationBuilder.Create(settings.EntraID.ClientId)
                    .WithClientSecret(settings.EntraID.ClientSecret)
                    .WithAuthority(new Uri(authority))
                    .Build();
            }
            else
            {
                X509Certificate2 certificate = ReadCertificate(settings.EntraID.CertificateThumbprint);
                app = ConfidentialClientApplicationBuilder.Create(settings.EntraID.ClientId)
                    .WithCertificate(certificate)
                    .WithAuthority(new Uri(authority))
                    .Build();
            }

            //configure in memory cache for the access tokens. The tokens are typically valid for 60 seconds,
            //so no need to create new ones for every web request
            app.AddDistributedTokenCache(services =>
            {
                services.AddDistributedMemoryCache();
                services.AddLogging(configure => configure.AddConsole())
                .Configure<LoggerFilterOptions>(options => options.MinLevel = Microsoft.Extensions.Logging.LogLevel.Debug);
            });

            // With client credentials flows the scopes is ALWAYS of the shape "resource/.default", as the 
            // application permissions need to be set statically (in the portal or by PowerShell), and then granted by
            // a tenant administrator. 
            if (scopes == null)
            {
                scopes = new string[] { settings.EntraID.Scope };
            }

            AuthenticationResult result = null;
            try
            {
                result = await app.AcquireTokenForClient(scopes)
                    .ExecuteAsync();
            }
            catch (MsalServiceException ex) when (ex.Message.Contains("AADSTS70011"))
            {
                // Invalid scope. The scope has to be of the form "https://resourceurl/.default"
                // Mitigation: change the scope to be as expected
                return (string.Empty, "500", "Scope provided is not supported");
                //return BadRequest(new { error = "500", error_description = "Scope provided is not supported" });
            }
            catch (MsalServiceException ex)
            {
                // general error getting an access token
                return (String.Empty, "500", "Something went wrong getting an access token for the client API:" + ex.Message);
                //return BadRequest(new { error = "500", error_description = "Something went wrong getting an access token for the client API:" + ex.Message });
            }

            return (result.AccessToken, String.Empty, String.Empty);
        }

    }
}
