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
    private readonly IInstanceManagerFactory _managerFactory;

    public ServerController(
        IDrasiInstanceService instanceService,
        IDrasiServerConfigurationProvider configurationProvider,
        IInstanceManagerFactory managerFactory)
    {
        _instanceService = instanceService;
        _configurationProvider = configurationProvider;
        _managerFactory = managerFactory;
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

    // Instance Lifecycle Operations
    [HttpPost]
    [Route("instances/{instanceId}/start")]
    public async Task<IActionResult> StartInstance(
        string instanceId,
        [FromBody] Configuration? configuration = null)
    {
        try
        {
            var instance = await _instanceService.GetInstanceAsync(instanceId);
            if (instance == null)
            {
                return NotFound($"Instance '{instanceId}' not found");
            }

            // Get configuration (use provided or fetch stored)
            var config = configuration ?? await _configurationProvider.GetConfigurationAsync(instanceId);
            if (config == null)
            {
                return BadRequest("No configuration available for instance");
            }

            // Get the appropriate manager for the platform
            var manager = _managerFactory.GetManager(instance.PlatformType);
            if (manager == null)
            {
                return BadRequest($"No manager found for platform '{instance.PlatformType}'");
            }

            // Start the instance
            await manager.StartInstanceAsync(instanceId, config);

            // Update status
            await _instanceService.UpdateInstanceStatusAsync(instanceId, InstanceStatus.Running);

            return Ok($"Instance '{instanceId}' started successfully");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to start instance: {ex.Message}");
        }
    }

    [HttpPost]
    [Route("instances/{instanceId}/stop")]
    public async Task<IActionResult> StopInstance(string instanceId)
    {
        try
        {
            var instance = await _instanceService.GetInstanceAsync(instanceId);
            if (instance == null)
            {
                return NotFound($"Instance '{instanceId}' not found");
            }

            // Get the appropriate manager for the platform
            var manager = _managerFactory.GetManager(instance.PlatformType);
            if (manager == null)
            {
                return BadRequest($"No manager found for platform '{instance.PlatformType}'");
            }

            // Stop the instance
            await manager.StopInstanceAsync(instanceId);

            // Update status
            await _instanceService.UpdateInstanceStatusAsync(instanceId, InstanceStatus.Stopped);

            return Ok($"Instance '{instanceId}' stopped successfully");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to stop instance: {ex.Message}");
        }
    }

    [HttpPost]
    [Route("instances/{instanceId}/restart")]
    public async Task<IActionResult> RestartInstance(string instanceId)
    {
        try
        {
            var instance = await _instanceService.GetInstanceAsync(instanceId);
            if (instance == null)
            {
                return NotFound($"Instance '{instanceId}' not found");
            }

            // Get configuration
            var config = await _configurationProvider.GetConfigurationAsync(instanceId);
            if (config == null)
            {
                return BadRequest("No configuration available for instance");
            }

            // Get the appropriate manager for the platform
            var manager = _managerFactory.GetManager(instance.PlatformType);
            if (manager == null)
            {
                return BadRequest($"No manager found for platform '{instance.PlatformType}'");
            }

            // Stop the instance
            await manager.StopInstanceAsync(instanceId);

            // Small delay for clean shutdown
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Start the instance
            await manager.StartInstanceAsync(instanceId, config);

            // Update status
            await _instanceService.UpdateInstanceStatusAsync(instanceId, InstanceStatus.Running);

            return Ok($"Instance '{instanceId}' restarted successfully");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to restart instance: {ex.Message}");
        }
    }
}
