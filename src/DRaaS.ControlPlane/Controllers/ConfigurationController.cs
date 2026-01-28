using DRaaS.Core.Models;
using DRaaS.Core.Providers;
using DRaaS.Core.Services.Monitoring;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.AspNetCore.JsonPatch;

namespace DRaaS.ControlPlane.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    private readonly IDrasiServerConfigurationProvider _drasiServerConfigurationProvider;
    private readonly IStatusUpdateService _statusUpdateService;

    public ConfigurationController(
        IDrasiServerConfigurationProvider drasiServerConfigurationProvider,
        IStatusUpdateService statusUpdateService)
    {
        _drasiServerConfigurationProvider = drasiServerConfigurationProvider;
        _statusUpdateService = statusUpdateService;
    }

    [HttpPatch]
    [Route("instances/{instanceId}")]
    public async Task<IActionResult> PatchConfiguration(
        string instanceId,
        [FromBody] JsonPatchDocument<Configuration> patchDocument)
    {
        if (patchDocument is null)
        {
            return BadRequest("Patch document is required");
        }

        try
        {
            var updatedConfiguration = await _drasiServerConfigurationProvider.ApplyPatchAsync(instanceId, patchDocument);

            // Trigger reconciliation by publishing configuration changed event
            await _statusUpdateService.PublishStatusUpdateAsync(
                instanceId,
                InstanceStatus.ConfigurationChanged,
                "ConfigurationController",
                new Dictionary<string, string>
                {
                    ["ChangeType"] = "PatchApplied",
                    ["Timestamp"] = DateTime.UtcNow.ToString("O")
                });

            return Accepted(updatedConfiguration); // 202 - Change accepted, will be reconciled
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Instance '{instanceId}' not found");
        }
    }

    [HttpGet]
    [Route("instances/{instanceId}")]
    public async Task<IActionResult> GetConfiguration(string instanceId)
    {
        try
        {
            var configuration = await _drasiServerConfigurationProvider.GetConfigurationAsync(instanceId);
            return Ok(configuration);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Instance '{instanceId}' not found");
        }
    }
}
