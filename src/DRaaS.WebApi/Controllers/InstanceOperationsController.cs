using DRaaS.CoreLib.Services;
using DRaaS.WebApi.Models.Mappers;
using DRaaS.WebApi.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace DRaaS.WebApi.Controllers;

/// <summary>
/// Controller for instance lifecycle operations.
/// Handles start, stop, and restart commands.
/// </summary>
[ApiController]
[Route("api/instances/{instanceId}/operations")]
public class InstanceOperationsController : ControllerBase
{
    private readonly IInstanceOrchestrationService _orchestrationService;
    private readonly ILogger<InstanceOperationsController> _logger;

    public InstanceOperationsController(
        IInstanceOrchestrationService orchestrationService,
        ILogger<InstanceOperationsController> logger)
    {
        _orchestrationService = orchestrationService;
        _logger = logger;
    }

    /// <summary>
    /// Starts an instance.
    /// Instance must be deployed and in a stopped/created state.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(InstanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InstanceResponseDto>> StartInstance(
        string instanceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var instance = await _orchestrationService.StartInstanceAsync(instanceId, cancellationToken);
            return Ok(instance.ToResponseDto());
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot start instance {InstanceId}: {Message}", instanceId, ex.Message);
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse { Error = $"Instance {instanceId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start instance {InstanceId}", instanceId);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Stops a running instance.
    /// Instance must be deployed and in a running/starting state.
    /// </summary>
    [HttpPost("stop")]
    [ProducesResponseType(typeof(InstanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InstanceResponseDto>> StopInstance(
        string instanceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var instance = await _orchestrationService.StopInstanceAsync(instanceId, cancellationToken);
            return Ok(instance.ToResponseDto());
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot stop instance {InstanceId}: {Message}", instanceId, ex.Message);
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse { Error = $"Instance {instanceId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop instance {InstanceId}", instanceId);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Restarts an instance (stop then start).
    /// Instance must be deployed and in a running state.
    /// </summary>
    [HttpPost("restart")]
    [ProducesResponseType(typeof(InstanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InstanceResponseDto>> RestartInstance(
        string instanceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var instance = await _orchestrationService.RestartInstanceAsync(instanceId, cancellationToken);
            return Ok(instance.ToResponseDto());
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot restart instance {InstanceId}: {Message}", instanceId, ex.Message);
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse { Error = $"Instance {instanceId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart instance {InstanceId}", instanceId);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }
}
