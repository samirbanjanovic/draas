using DRaaS.Core.Models;
using DRaaS.Core.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.AspNetCore.JsonPatch;

namespace DRaaS.ControlPlane.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    private readonly IDrasiServerConfigurationProvider _drasiServerConfigurationProvider;

    public ConfigurationController(IDrasiServerConfigurationProvider drasiServerConfigurationProvider)
    {
        _drasiServerConfigurationProvider = drasiServerConfigurationProvider;
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
            return Ok(updatedConfiguration);
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
    