
using WoodgroveDemo.Helpers;
using Microsoft.Identity.VerifiedID;
using Microsoft.Identity.VerifiedID.Manifest;

namespace WoodgroveDemo.Models;
public class AppSettings
{
    public const int CACHE_EXPIRES_IN_MINUTES = 10;
    
    public AppSettings(IConfiguration configuration)
    {
        RequestUrl = configuration.GetSection("VerifiedID:ApiEndpoint").Value!;
        UseCache = configuration.GetValue("VerifiedID:UseCache", true);

        // Read the Microsoft Entra ID settings from the app settings 
        EntraID.Authority = configuration["VerifiedID:Authority"]!;
        EntraID.TenantId = configuration["VerifiedID:TenantId"]!;
        EntraID.ClientId = configuration["VerifiedID:ClientId"]!;
        EntraID.ClientSecret = configuration["VerifiedID:ClientSecret"]!;
        EntraID.CertificateThumbprint = configuration["VerifiedID:certificateThumbprint"]!;
        EntraID.Scope = configuration["VerifiedID:scope"]!;
        EntraID.DidAuthority = configuration["VerifiedID:DidAuthority"]!;

        // Read the user experience settings from the app settings 
        UX.ClientName = configuration["VerifiedID:client_name"]!;
        UX.Purpose = configuration.GetValue("VerifiedID:purpose", "")!;
        UX.IncludeQRCode = configuration.GetValue("VerifiedID:includeQRCode", false);
        UX.IssuancePinCodeLength = configuration.GetValue("VerifiedID:IssuancePinCodeLength", 0)!;
        UX.IncludeReceipt = configuration.GetValue("VerifiedID:includeReceipt", false)!;
        UX.AllowRevoked = configuration.GetValue("VerifiedID:allowRevoked", false);
        UX.ValidateLinkedDomain = configuration.GetValue("VerifiedID:validateLinkedDomain", true);

        // Read the callback endpint settings from the app settings
        Api.ApiKey = configuration["VerifiedID:ApiKey"]!;

        // Revoke credentials demo settings from the app settings
        RevokeCredentialsDemo.Endpoint = configuration["VerifiedID:RevokeCredentialsDemo:Endpoint"]!;
        RevokeCredentialsDemo.Scope = configuration["VerifiedID:RevokeCredentialsDemo:Scope"]!;
        RevokeCredentialsDemo.Contract = configuration["VerifiedID:RevokeCredentialsDemo:Contract"]!;
    }

    public AppSettings(IConfiguration configuration, string definitionPath, bool isPresentation) : this(configuration)
    {
        Presentation = isPresentation;
        DefinitionPath = definitionPath;
        ManifestUrl = configuration.GetSection($"VerifiedID:Manifest:{DefinitionPath}").Value!;
        CredentialType = configuration.GetSection($"VerifiedID:CredentialType:{DefinitionPath}").Value!;

        if (isPresentation)
        {
            RequestUrl = configuration.GetSection("VerifiedID:ApiEndpoint").Value! + Endpoints.CreatePresentationRequest;
            Flow = "Presentation";
        }
        else
        {
            RequestUrl = configuration.GetSection("VerifiedID:ApiEndpoint").Value! + Endpoints.CreateIssuanceRequest;
            Flow = "Issuance";
        }
    }
    public EntraIDSettings EntraID { get; set; } = new EntraIDSettings();
    public UxSettings UX { get; set; } = new UxSettings();
    public ApiSettings Api { get; set; } = new ApiSettings();
    public RevokeCredentialsDemo RevokeCredentialsDemo { get; set; } = new RevokeCredentialsDemo();
    public bool UseCache { get; set; } = true;
    public string RequestUrl { get; set; } = string.Empty;
    public string ManifestUrl { get; set; } = string.Empty;
    public string ManifestContent { get; set; } = string.Empty;
    public string CredentialType { get; set; } = string.Empty;
    public Display CardDetails { get; set; }
    public string DefinitionPath { get; set; } = string.Empty;
    public bool Presentation { get; set; } = false;
    public string Flow { get; set; } = string.Empty;
}

public class EntraIDSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CertificateThumbprint { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string DidAuthority { get; set; } = string.Empty;
}

public class UxSettings
{
    public string ClientName { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public bool IncludeQRCode { get; set; } = false;
    public bool IncludeReceipt { get; set; } = false;
    public int IssuancePinCodeLength { get; set; } = 0;
    public bool AllowRevoked { get; set; } = false;
    public bool ValidateLinkedDomain { get; set; } = true;
}

public class RevokeCredentialsDemo
{
    public string Endpoint { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Contract { get; set; } = string.Empty;
}

public class ApiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string URL(HttpRequest httpRequest)
    {
        return $"{RequestHelper.GetRequestHostName(httpRequest)}/api/callback";
    }
}