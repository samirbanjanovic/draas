using DRaaS.Core.Models;
using DRaaS.Core.Providers;
using DRaaS.Core.Services.Instance;
using DRaaS.Core.Messaging;
using DRaaS.Core.Messaging.Commands;
using DRaaS.Core.Messaging.Responses;
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
    private readonly IMessageBus _messageBus;

    public ServerController(
        IDrasiInstanceService instanceService,
        IDrasiServerConfigurationProvider configurationProvider,
        IMessageBus messageBus)
    {
        _instanceService = instanceService;
        _configurationProvider = configurationProvider;
        _messageBus = messageBus;
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
        try
        {
            var instance = await _instanceService.GetInstanceAsync(instanceId);
            if (instance == null)
            {
                return NotFound($"Instance '{instanceId}' not found");
            }

            // Route to appropriate platform worker via message bus to cleanup running instance
            if (instance.PlatformType == PlatformType.Process || 
                instance.PlatformType == PlatformType.Docker ||
                instance.PlatformType == PlatformType.AKS)
            {
                var command = new DeleteInstanceCommand
                {
                    InstanceId = instanceId
                };

                var response = await _messageBus.RequestAsync<DeleteInstanceCommand, DeleteInstanceResponse>(
                    Channels.GetInstanceCommandChannel(instance.PlatformType),
                    command,
                    timeout: TimeSpan.FromSeconds(30));

                if (response?.Success != true)
                {
                    return StatusCode(500, new { error = response?.ErrorMessage ?? "Failed to cleanup running instance" });
                }
            }

            // Delete configuration
            await _configurationProvider.DeleteConfigurationAsync(instanceId);

            // Delete instance metadata
            var deleted = await _instanceService.DeleteInstanceAsync(instanceId);
            if (!deleted)
            {
                return NotFound($"Instance '{instanceId}' not found");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to delete instance: {ex.Message}");
        }
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

            // Route to appropriate platform worker via message bus
            if (instance.PlatformType == PlatformType.Process || 
                instance.PlatformType == PlatformType.Docker ||
                instance.PlatformType == PlatformType.AKS)
            {
                var command = new StartInstanceCommand
                {
                    InstanceId = instanceId,
                    Configuration = config
                };

                var response = await _messageBus.RequestAsync<StartInstanceCommand, StartInstanceResponse>(
                    Channels.GetInstanceCommandChannel(instance.PlatformType),
                    command,
                    timeout: TimeSpan.FromSeconds(30));

                if (response?.Success == true)
                {
                    await _instanceService.UpdateInstanceStatusAsync(instanceId, InstanceStatus.Running);
                    return Ok(new { message = $"Instance '{instanceId}' started successfully", runtimeInfo = response.RuntimeInfo });
                }
                else
                {
                    return StatusCode(500, new { error = response?.ErrorMessage ?? "Operation timed out" });
                }
            }
            else
            {
                return BadRequest($"Unsupported platform type '{instance.PlatformType}'");
            }
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

            // Route to appropriate platform worker via message bus
            if (instance.PlatformType == PlatformType.Process || 
                instance.PlatformType == PlatformType.Docker ||
                instance.PlatformType == PlatformType.AKS)
            {
                var command = new StopInstanceCommand
                {
                    InstanceId = instanceId
                };

                var response = await _messageBus.RequestAsync<StopInstanceCommand, StopInstanceResponse>(
                    Channels.GetInstanceCommandChannel(instance.PlatformType),
                    command,
                    timeout: TimeSpan.FromSeconds(30));

                if (response?.Success == true)
                {
                    await _instanceService.UpdateInstanceStatusAsync(instanceId, InstanceStatus.Stopped);
                    return Ok(new { message = $"Instance '{instanceId}' stopped successfully", runtimeInfo = response.RuntimeInfo });
                }
                else
                {
                    return StatusCode(500, new { error = response?.ErrorMessage ?? "Operation timed out" });
                }
            }
            else
            {
                return BadRequest($"Unsupported platform type '{instance.PlatformType}'");
            }
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

            // Route to appropriate platform worker via message bus
            if (instance.PlatformType == PlatformType.Process || 
                instance.PlatformType == PlatformType.Docker ||
                instance.PlatformType == PlatformType.AKS)
            {
                var channel = Channels.GetInstanceCommandChannel(instance.PlatformType);

                // Stop the instance first
                var stopCommand = new StopInstanceCommand
                {
                    InstanceId = instanceId
                };

                var stopResponse = await _messageBus.RequestAsync<StopInstanceCommand, StopInstanceResponse>(
                    channel,
                    stopCommand,
                    timeout: TimeSpan.FromSeconds(30));

                if (stopResponse?.Success != true)
                {
                    return StatusCode(500, new { error = stopResponse?.ErrorMessage ?? "Failed to stop instance" });
                }

                // Small delay for clean shutdown
                await Task.Delay(TimeSpan.FromSeconds(2));

                // Start the instance
                var startCommand = new StartInstanceCommand
                {
                    InstanceId = instanceId,
                    Configuration = config
                };

                var startResponse = await _messageBus.RequestAsync<StartInstanceCommand, StartInstanceResponse>(
                    channel,
                    startCommand,
                    timeout: TimeSpan.FromSeconds(30));

                if (startResponse?.Success == true)
                {
                    await _instanceService.UpdateInstanceStatusAsync(instanceId, InstanceStatus.Running);
                    return Ok(new { message = $"Instance '{instanceId}' restarted successfully", runtimeInfo = startResponse.RuntimeInfo });
                }
                else
                {
                    return StatusCode(500, new { error = startResponse?.ErrorMessage ?? "Failed to start instance" });
                }
            }
            else
            {
                return BadRequest($"Unsupported platform type '{instance.PlatformType}'");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to restart instance: {ex.Message}");
        }
    }
}
