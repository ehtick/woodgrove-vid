using System.Net;
using System.Reflection.Metadata;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.VerifiedID;
using WoodgroveDemo.Helpers;
using WoodgroveDemo.Models;

namespace WoodgroveDemo.Controllers;

[ApiController]
[Route("[controller]")]
public class StatusController : ControllerBase
{

    private readonly IConfiguration _configuration;

    private readonly ILogger<CallbackController> _logger;
    private IMemoryCache _cache;
    private TelemetryClient _telemetry;


    public StatusController(ILogger<CallbackController> logger, IConfiguration configuration, IMemoryCache cache, TelemetryClient telemetry)
    {
        _logger = logger;
        _configuration = configuration;
        _cache = cache;
        _telemetry = telemetry;
    }

    [AllowAnonymous]
    [HttpGet("/api/status")]
    public Status Get()
    {
        // Get the current user's state ID from the user's session
        string state = this.HttpContext.Session.GetString("state");

        // If the state object was not found, return an error message
        if (string.IsNullOrEmpty(state))
        {
            return new Status
            {
                RequestStateId = "",
                RequestStatus = "error",
                Message = UserMessages.ERROR_STATE_ID_NOT_FOUND
            };
        }

        // Try to read the request status object from the global cache using the state ID key
        if (_cache.TryGetValue(state, out string requestState))
        {
            try
            {
                Status status = Status.Parse(requestState);
                status.RequestStateId = state;

                // Process the status of the request
                status = this.HandleStatus(status);
                return status;
            }
            catch (Exception ex)
            {
                AppInsightsHelper.TrackError(_telemetry, this.Request, ex);
                return new Status
                {
                    RequestStateId = "",
                    RequestStatus = "error",
                    Message = UserMessages.ERROR_STATE_ID_CANNOT_DESERIALIZE + ex.Message
                };
            }
        }
        else
        {
            // If the request status object was not found in globle cach, return an error message
            return new Status
            {
                RequestStateId = state,
                RequestStatus = "error",
                Message = UserMessages.ERROR_STATE_OBJECT_NOT_FOUND 
            };
        }
    }

    private Status HandleStatus(Status status)
    {
        switch (status.RequestStatus)
        {
            case UserFlowStatusCodes.REQUEST_CREATED:
                status.Message = UserMessages.REQUEST_CREATED;
                break;
            case UserFlowStatusCodes.REQUEST_RETRIEVED:
                status.Message = UserMessages.REQUEST_RETRIEVED;
                break;
            case UserFlowStatusCodes.ISSUANCE_ERROR:
                status.Message = UserMessages.ISSUANCE_ERROR;
                break;
            case UserFlowStatusCodes.ISSUANCE_SUCCESSFUL:
                status.Message = UserMessages.ISSUANCE_SUCCESSFUL;
                break;
            case UserFlowStatusCodes.PRESENTATION_ERROR:
                status.Message = UserMessages.ISSUANCE_ERROR;
                break;
            case UserFlowStatusCodes.PRESENTATION_VERIFIED:
                status.Message = UserMessages.PRESENTATION_VERIFIED;
                break;
            default:
                status.RequestStatus = UserFlowStatusCodes.INVALID_REQUEST_STATUS;

                // TBD add the request status
                status.Message = UserMessages.INVALID_REQUEST_STATUS + " Received status: " + status.RequestStatus;
                break;
        }

        return status;
    }
}