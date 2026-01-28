using DRaaS.Core.Models;
using DRaaS.Core.Providers;
using DRaaS.Core.Services.Instance;
using DRaaS.Core.Services.Orchestration;
using DRaaS.Core.Services.Factory;
using DRaaS.ControlPlane.DTOs;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;

namespace DRaaS.ControlPlane.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServerController : ControllerBase
{
    private readonly IDrasiInstanceService _instanceService;
    private readonly IDrasiServerConfigurationProvider _configurationProvider;

    public ServerController(
        IDrasiInstanceService instanceService,
        IDrasiServerConfigurationProvider configurationProvider)
    {
        _instanceService = instanceService;
        _configurationProvider = configurationProvider;
    }

    // Instance Management
    [HttpPost]
    [Route("instances")]
    public async Task<IActionResult> CreateInstance([FromBody] CreateInstanceRequest request)
    {
        // Create instance metadata
        var instance = await _instanceService.CreateInstanceAsync(request.Name, request.Description ?? "");

        // Initialize configuration for the instance
        await _configurationProvider.InitializeConfigurationAsync(instance.Id, request.ServerConfiguration);

        return CreatedAtAction(nameof(GetInstance), new { instanceId = instance.Id }, instance);
    }

    [HttpGet]
    [Route("instances")]
    public async Task<IActionResult> GetAllInstances()
    {
        var instances = await _instanceService.GetAllInstancesAsync();
        return Ok(instances);
    }

    [HttpGet]
    [Route("instances/{instanceId}")]
    public async Task<IActionResult> GetInstance(string instanceId)
    {
        var instance = await _instanceService.GetInstanceAsync(instanceId);
        if (instance == null)
        {
            return NotFound($"Instance '{instanceId}' not found");
        }
        return Ok(instance);
    }

    [HttpDelete]
    [Route("instances/{instanceId}")]
    public async Task<IActionResult> DeleteInstance(string instanceId)
    {
        // Delete configuration first
        await _configurationProvider.DeleteConfigurationAsync(instanceId);

        // Then delete instance
        var deleted = await _instanceService.DeleteInstanceAsync(instanceId);
        if (!deleted)
        {
            return NotFound($"Instance '{instanceId}' not found");
        }
        return NoContent();
    }

    [HttpPut]
    [Route("instances/{instanceId}/status")]
    public async Task<IActionResult> UpdateInstanceStatus(string instanceId, [FromBody] InstanceStatus status)
    {
        try
        {
            var instance = await _instanceService.UpdateInstanceStatusAsync(instanceId, status);
            return Ok(instance);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Instance '{instanceId}' not found");
        }
    }

    // Server Configuration Management
    [HttpGet]
    [Route("instances/{instanceId}/configuration/server")]
    public async Task<IActionResult> GetServerConfiguration(string instanceId)
    {
        try
        {
            var serverConfig = await _configurationProvider.GetServerConfigurationAsync(instanceId);
            return Ok(serverConfig);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Instance '{instanceId}' not found");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch]
    [Route("instances/{instanceId}/configuration/server")]
    public async Task<IActionResult> PatchServerConfiguration(
        string instanceId, 
        [FromBody] JsonPatchDocument<ServerConfiguration> patchDocument)
    {
        if (patchDocument == null)
        {
            return BadRequest("Patch document is required");
        }

        var updatedServerConfig = await _configurationProvider.ApplyServerPatchAsync(instanceId, patchDocument);
        return Ok(updatedServerConfig);
    }

    [HttpPut]
    [Route("instances/{instanceId}/configuration/server/host")]
    public async Task<IActionResult> UpdateHost(string instanceId, [FromBody] string host)
    {
        var serverConfig = await _configurationProvider.UpdateHostAsync(instanceId, host);
        return Ok(serverConfig);
    }

    [HttpPut]
    [Route("instances/{instanceId}/configuration/server/port")]
    public async Task<IActionResult> UpdatePort(string instanceId, [FromBody] int port)
    {
        var serverConfig = await _configurationProvider.UpdatePortAsync(instanceId, port);
        return Ok(serverConfig);
    }

    [HttpPut]
    [Route("instances/{instanceId}/configuration/server/loglevel")]
    public async Task<IActionResult> UpdateLogLevel(string instanceId, [FromBody] string logLevel)
    {
        var serverConfig = await _configurationProvider.UpdateLogLevelAsync(instanceId, logLevel);
        return Ok(serverConfig);
    }
}
