
using System.Net;
using System.Security.Cryptography;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using WoodgroveDemo.Models;
using Microsoft.Identity.VerifiedID.Issuance;
using Microsoft.Identity.VerifiedID.Manifest;
using Microsoft.Identity.VerifiedID.Presentation;

namespace WoodgroveDemo.Helpers;

public class AppInsightsHelper
{

    public static void TrackApi(TelemetryClient Telemetry, HttpRequest request, Status status)
    {

        string[] parts = request.Path.Value.Split("/");

        if (parts.Length == 4)
        {
            EventTelemetry eventTelemetry = new EventTelemetry($"{parts[2]}_{parts[3]}");
            eventTelemetry.Properties.Add("Scenario", status.Scenario);
            eventTelemetry.Properties.Add("Action", status.Flow);
            eventTelemetry.Properties.Add("Type", "API");
            eventTelemetry.Properties.Add("State", status.RequestStateId);
            eventTelemetry.Properties.Add("RequestStatus", status.RequestStatus);
            eventTelemetry.Properties.Add("ExecutionTime", status.CalculateExecutionTime());
            eventTelemetry.Properties.Add("ExecutionSeconds", status.CalculateExecutionSeconds().ToString());
            Telemetry.TrackEvent(eventTelemetry);
        }
    }
    public static void TrackPage(TelemetryClient Telemetry, HttpRequest request)
    {

        // For a page, check is route value
        if (request.RouteValues["page"] == null)
        {
            Telemetry.TrackPageView("Unknown");
            return;
        }

        // Get the page name in format of /area/page. 
        // The area may not exists for top level pages, such as the index and help.
        string PageRoute = request.RouteValues["page"].ToString();

        // Remove the slash at the beginning
        if (PageRoute.StartsWith("/"))
        {
            PageRoute = PageRoute.Remove(0, 1);
        }

        string[] parts = PageRoute.Split("/");

        PageViewTelemetry pageView = new PageViewTelemetry(PageRoute.Replace("/", "_"));

        // Type of the page
        pageView.Properties.Add("Type", "Page");

        // If the page name is under area, get the area and the page name
        // The area represents the verifiable credential scenario, while the page represents the action type; issue or present
        if (parts.Length > 1)
        {
            pageView.Properties.Add("Scenario", parts[0]);
            pageView.Properties.Add("Action", parts[1]);
        }

        // Get the web address from which the page has been requested
        if (request.Headers.ContainsKey("Referer"))
        {
            // Check the referer of the request
            string referer = request.Headers["Referer"].ToString();
            try
            {
                // Get the host name
                var uri = new System.Uri(referer);
                pageView.Properties.Add("Referral", uri.Host.ToLower());

                // Add the full URL
                pageView.Properties.Add("ReferralURL", referer);
            }
            catch (System.Exception ex)
            {
                pageView.Properties.Add("Referral", "Invalid");

                // Add the full URL
                pageView.Properties.Add("ReferralURL", referer);
            }
        }
        else
        {
            pageView.Properties.Add("Referral", "Unknown");
        }

        // Track the page view 
        Telemetry.TrackPageView(pageView);
    }

    public static void TrackError(TelemetryClient Telemetry, HttpRequest request, Exception ex, string body = null)
    {
        ExceptionTelemetry expTelemetry = new ExceptionTelemetry(ex);

        string[] parts = request.Path.Value.Split("/");

        if (parts.Length == 4)
        {
            // API url: /api/scenario/action
            expTelemetry.Properties.Add("Scenario", parts[2]);
            expTelemetry.Properties.Add("Action", parts[3]);
            expTelemetry.Properties.Add("Type", "API");
        }
        if (parts.Length == 3)
        {
            // Page url: /scenario/action
            expTelemetry.Properties.Add("Scenario", parts[1]);
            expTelemetry.Properties.Add("Action", parts[2]);
            expTelemetry.Properties.Add("Type", "Page");
        }

        if (!string.IsNullOrEmpty(body))
        {
            expTelemetry.Properties.Add("Error", body);
        }

        Telemetry.TrackException(expTelemetry);
    }

    public static void TrackError(TelemetryClient Telemetry, HttpRequest request, string message, string body = null)
    {
        TrackError(Telemetry, request, new Exception(message), body);
    }
}