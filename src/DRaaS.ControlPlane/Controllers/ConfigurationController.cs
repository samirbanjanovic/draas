using DRaaS.ControlPlane.Models;
using DRaaS.ControlPlane.Providers;
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
    [Route("")]
    public async Task<IActionResult> PatchConfiguration([FromBody] JsonPatchDocument<Configuration> patchDocument)
    {
        if (patchDocument is null)
        {
            return BadRequest("Patch document is required");
        }

        var updatedConfiguration = await _drasiServerConfigurationProvider.ApplyPatchAsync(patchDocument);
        return Ok(updatedConfiguration);
    }

    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetConfiguration()
    {
        var configuration = await _drasiServerConfigurationProvider.GetConfigurationAsync();
        return Ok(configuration);
    }
}
    