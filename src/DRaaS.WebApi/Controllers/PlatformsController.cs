using DRaaS.CoreLib.Services;
using DRaaS.WebApi.Models.Mappers;
using DRaaS.WebApi.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace DRaaS.WebApi.Controllers;

/// <summary>
/// Controller for platform discovery and information.
/// Read-only operations for querying available platforms.
/// </summary>
[ApiController]
[Route("api/platforms")]
public class PlatformsController : ControllerBase
{
    private readonly IInstanceOrchestrationService _orchestrationService;
    private readonly ILogger<PlatformsController> _logger;

    public PlatformsController(
        IInstanceOrchestrationService orchestrationService,
        ILogger<PlatformsController> logger)
    {
        _orchestrationService = orchestrationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all available platforms.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PlatformDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<PlatformDetailsDto>>> GetAvailablePlatforms(
        CancellationToken cancellationToken)
    {
        try
        {
            var platforms = await _orchestrationService.GetAvailablePlatformsAsync(cancellationToken);
            return Ok(platforms.Select(p => p.ToResponseDto()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available platforms");
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Gets the default platform.
    /// </summary>
    [HttpGet("default")]
    [ProducesResponseType(typeof(PlatformDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PlatformDetailsDto>> GetDefaultPlatform(
        CancellationToken cancellationToken)
    {
        try
        {
            var platform = await _orchestrationService.GetDefaultPlatformAsync(cancellationToken);
            return Ok(platform.ToResponseDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get default platform");
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Gets a specific platform by type.
    /// </summary>
    [HttpGet("{platformType}")]
    [ProducesResponseType(typeof(PlatformDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PlatformDetailsDto>> GetPlatform(
        string platformType,
        CancellationToken cancellationToken)
    {
        try
        {
            var platforms = await _orchestrationService.GetAvailablePlatformsAsync(cancellationToken);
            var platform = platforms.FirstOrDefault(p => 
                p.PlatformType.Equals(platformType, StringComparison.OrdinalIgnoreCase));

            if (platform == null)
            {
                return NotFound(new ErrorResponse { Error = $"Platform '{platformType}' not found" });
            }

            return Ok(platform.ToResponseDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get platform {PlatformType}", platformType);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }
}
