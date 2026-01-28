using DRaaS.Core.Models;
using DRaaS.Core.Services.Monitoring;
using Microsoft.AspNetCore.Mvc;

namespace DRaaS.ControlPlane.Controllers;

/// <summary>
/// API endpoint for external daemons (Docker/AKS monitors) to push instance status updates.
/// This endpoint receives status changes from distributed platform monitors running outside the control plane.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly IStatusUpdateService _statusUpdateService;

    public StatusController(IStatusUpdateService statusUpdateService)
    {
        _statusUpdateService = statusUpdateService;
    }

    /// <summary>
    /// Receives a status update from an external daemon.
    /// Docker/AKS daemons POST to this endpoint when they detect status changes.
    /// </summary>
    /// <param name="request">The status update request</param>
    /// <returns>Accepted if the update was processed</returns>
    [HttpPost("updates")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceiveStatusUpdate([FromBody] StatusUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InstanceId) || string.IsNullOrWhiteSpace(request.Source))
        {
            return BadRequest("InstanceId and Source are required");
        }

        try
        {
            await _statusUpdateService.PublishStatusUpdateAsync(
                request.InstanceId,
                request.Status,
                request.Source,
                request.Metadata);

            return Accepted(); // 202 - Update received and will be processed
        }
        catch (Exception ex)
        {
            // TODO: Log error
            return BadRequest($"Failed to process status update: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the last known status for an instance.
    /// Useful for daemon health checks and synchronization.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <returns>The last known runtime information</returns>
    [HttpGet("{instanceId}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInstanceStatus(string instanceId)
    {
        var status = await _statusUpdateService.GetLastKnownStatusAsync(instanceId);
        if (status == null)
        {
            return NotFound($"Instance '{instanceId}' not found");
        }

        return Ok(status);
    }

    /// <summary>
    /// Gets recent status changes for reconciliation polling.
    /// Returns status changes that occurred since the specified timestamp.
    /// </summary>
    /// <param name="since">ISO 8601 timestamp to filter changes (default: last 5 minutes)</param>
    /// <param name="statusFilter">Optional status filter (e.g., "ConfigurationChanged")</param>
    /// <returns>List of status changes</returns>
    [HttpGet("recent-changes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentStatusChanges(
        [FromQuery] DateTime? since = null,
        [FromQuery] InstanceStatus? statusFilter = null)
    {
        // Default to last 5 minutes if not specified
        var sinceTimestamp = since ?? DateTime.UtcNow.AddMinutes(-5);

        var recentChanges = await _statusUpdateService.GetRecentChangesAsync(
            sinceTimestamp,
            statusFilter);

        return Ok(recentChanges);
    }
}

/// <summary>
/// Request model for status updates from external daemons.
/// </summary>
public record StatusUpdateRequest
{
    /// <summary>
    /// The instance identifier being updated.
    /// </summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>
    /// The new status of the instance.
    /// </summary>
    public InstanceStatus Status { get; init; }

    /// <summary>
    /// The source of the update (e.g., "DockerDaemon", "AKSDaemon").
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Optional metadata about the status change.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}
