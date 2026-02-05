using DRaaS.CoreLib.Models;
using DRaaS.CoreLib.Services;
using DRaaS.WebApi.Models.Mappers;
using DRaaS.WebApi.Models.Requests;
using DRaaS.WebApi.Models.Responses;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;

namespace DRaaS.WebApi.Controllers;

/// <summary>
/// Controller for managing Drasi instances.
/// Handles CRUD operations with DTO abstraction.
/// </summary>
[ApiController]
[Route("api/instances")]
public class InstancesController : ControllerBase
{
    private readonly IInstanceOrchestrationService _orchestrationService;
    private readonly ILogger<InstancesController> _logger;

    public InstancesController(
        IInstanceOrchestrationService orchestrationService,
        ILogger<InstancesController> logger)
    {
        _orchestrationService = orchestrationService;
        _logger = logger;
    }

    /// <summary>
    /// Creates and deploys a new instance.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(InstanceResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InstanceResponseDto>> CreateInstance(
        [FromBody] CreateInstanceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var domainConfig = request.Configuration.ToDomainModel();

            var instance = await _orchestrationService.DeployInstanceAsync(
                request.Name,
                request.Description,
                request.Owners,
                domainConfig,
                request.PlatformType,
                request.Metadata,
                cancellationToken);

            var responseDto = instance.ToResponseDto();

            return CreatedAtAction(
                nameof(GetInstance),
                new { instanceId = instance.InstanceId },
                responseDto);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for instance creation");
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot create instance: {Message}", ex.Message);
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create instance {Name}", request.Name);
            return StatusCode(500, new ErrorResponse 
            { 
                Error = "Internal server error", 
                Detail = ex.Message 
            });
        }
    }

    /// <summary>
    /// Gets a single instance by ID.
    /// </summary>
    [HttpGet("{instanceId}")]
    [ProducesResponseType(typeof(InstanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InstanceResponseDto>> GetInstance(
        string instanceId,
        [FromQuery] bool refresh = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var instance = await _orchestrationService.GetInstanceAsync(
                instanceId,
                refresh,
                cancellationToken);

            return Ok(instance.ToResponseDto());
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse { Error = $"Instance {instanceId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get instance {InstanceId}", instanceId);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Lists all instances with optional filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<InstanceResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<InstanceResponseDto>>> ListInstances(
        [FromQuery] string? owner = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IEnumerable<DrasiInstance> instances;

            if (!string.IsNullOrWhiteSpace(owner))
            {
                instances = await _orchestrationService.GetInstancesByOwnerAsync(owner, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DomainStatus>(status, true, out var domainStatus))
            {
                instances = await _orchestrationService.GetInstancesByStatusAsync(domainStatus, cancellationToken);
            }
            else
            {
                instances = await _orchestrationService.GetAllInstancesAsync(cancellationToken);
            }

            return Ok(instances.Select(i => i.ToResponseDto()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list instances");
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Updates an instance using JSON Patch (RFC 6902).
    /// Supports ID-based addressing for arrays (e.g., /configuration/sources/my-source-id).
    /// </summary>
    [HttpPatch("{instanceId}")]
    [Consumes("application/json-patch+json")]
    [ProducesResponseType(typeof(InstanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InstanceResponseDto>> PatchInstance(
        string instanceId,
        [FromBody] JsonPatchDocument<InstancePatchDto> patchDoc,
        CancellationToken cancellationToken)
    {
        if (patchDoc == null)
        {
            return BadRequest(new ErrorResponse { Error = "Invalid patch document" });
        }

        try
        {
            // 1. Get current instance
            var instance = await _orchestrationService.GetInstanceAsync(
                instanceId,
                refreshState: false,
                cancellationToken);

            // 2. Transform to patch DTO (arrays ? dictionaries for ID-based addressing)
            var patchDto = instance.ToPatchDto();

            // 3. Apply JSON Patch operations to DTO
            patchDoc.ApplyTo(patchDto, ModelState);

            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "Invalid patch operations",
                    ValidationErrors = ModelState.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                    )
                });
            }

            // 4. Validate patch results
            var validationErrors = ValidatePatchDto(patchDto);
            if (validationErrors.Count > 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "Patch validation failed",
                    ValidationErrors = validationErrors
                });
            }

            // 5. Determine what changed and update via orchestration service
            bool configChanged = patchDto.Configuration != null;
            bool metadataChanged = patchDto.Metadata != null;

            if (configChanged)
            {
                var newConfig = patchDto.Configuration!.ToDomainModel();
                instance = await _orchestrationService.UpdateInstanceConfigurationAsync(
                    instanceId,
                    newConfig,
                    patchDto.Metadata,
                    cancellationToken);
            }
            else if (metadataChanged)
            {
                instance = await _orchestrationService.UpdateInstanceMetadataAsync(
                    instanceId,
                    patchDto.Metadata!,
                    cancellationToken);
            }

            return Ok(instance.ToResponseDto());
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse { Error = $"Instance {instanceId} not found" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot patch instance {InstanceId}: {Message}", instanceId, ex.Message);
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to patch instance {InstanceId}", instanceId);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Deletes an instance and its infrastructure.
    /// </summary>
    [HttpDelete("{instanceId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteInstance(
        string instanceId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _orchestrationService.DeleteInstanceAsync(instanceId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse { Error = $"Instance {instanceId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete instance {InstanceId}", instanceId);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Validates the patch DTO after JSON Patch operations are applied.
    /// </summary>
    private static Dictionary<string, string[]> ValidatePatchDto(InstancePatchDto dto)
    {
        var errors = new Dictionary<string, string[]>();

        if (dto.Configuration == null) return errors;

        // Validate Sources: dictionary key must match Source.Id
        if (dto.Configuration.Sources != null)
        {
            var mismatchedIds = dto.Configuration.Sources
                .Where(kvp => kvp.Key != kvp.Value.Id)
                .Select(kvp => $"Key '{kvp.Key}' does not match Id '{kvp.Value.Id}'")
                .ToArray();

            if (mismatchedIds.Length > 0)
            {
                errors["Configuration.Sources"] = mismatchedIds;
            }
        }

        // Validate Queries: dictionary key must match Query.Id
        if (dto.Configuration.Queries != null)
        {
            var mismatchedIds = dto.Configuration.Queries
                .Where(kvp => kvp.Key != kvp.Value.Id)
                .Select(kvp => $"Key '{kvp.Key}' does not match Id '{kvp.Value.Id}'")
                .ToArray();

            if (mismatchedIds.Length > 0)
            {
                errors["Configuration.Queries"] = mismatchedIds;
            }

            // Validate query source references
            var sourceIds = dto.Configuration.Sources?.Keys.ToHashSet() ?? new HashSet<string>();
            foreach (var query in dto.Configuration.Queries.Values)
            {
                var invalidRefs = query.Sources?
                    .Where(s => !sourceIds.Contains(s.SourceId))
                    .Select(s => s.SourceId)
                    .ToArray();

                if (invalidRefs?.Length > 0)
                {
                    errors[$"Configuration.Queries[{query.Id}].Sources"] = 
                        new[] { $"References invalid source IDs: {string.Join(", ", invalidRefs)}" };
                }
            }
        }

        // Validate Reactions: dictionary key must match Reaction.Id
        if (dto.Configuration.Reactions != null)
        {
            var mismatchedIds = dto.Configuration.Reactions
                .Where(kvp => kvp.Key != kvp.Value.Id)
                .Select(kvp => $"Key '{kvp.Key}' does not match Id '{kvp.Value.Id}'")
                .ToArray();

            if (mismatchedIds.Length > 0)
            {
                errors["Configuration.Reactions"] = mismatchedIds;
            }

            // Validate reaction query references
            var queryIds = dto.Configuration.Queries?.Keys.ToHashSet() ?? new HashSet<string>();
            foreach (var reaction in dto.Configuration.Reactions.Values)
            {
                var invalidRefs = reaction.Queries?
                    .Where(q => !queryIds.Contains(q))
                    .ToArray();

                if (invalidRefs?.Length > 0)
                {
                    errors[$"Configuration.Reactions[{reaction.Id}].Queries"] = 
                        new[] { $"References invalid query IDs: {string.Join(", ", invalidRefs)}" };
                }
            }
        }

        return errors;
    }
}
