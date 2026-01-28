using DRaaS.Core.Models;
using DRaaS.Core.Services.Monitoring;
using Microsoft.AspNetCore.JsonPatch;
using System.Net.Http.Json;
using System.Text.Json;

namespace DRaaS.Reconciliation;

/// <summary>
/// HTTP client implementation for calling ControlPlane APIs.
/// All instance and configuration operations are performed through the ControlPlane REST API,
/// ensuring centralized management, RBAC enforcement, and audit trails.
/// </summary>
public class ReconciliationApiClient : IReconciliationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReconciliationApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ReconciliationApiClient(
        HttpClient httpClient,
        ILogger<ReconciliationApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<DrasiInstance?> GetInstanceAsync(string instanceId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/servers/instances/{instanceId}");
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DrasiInstance>(_jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get instance {InstanceId} from ControlPlane", instanceId);
            throw;
        }
    }

    public async Task<IEnumerable<DrasiInstance>> GetAllInstancesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/servers/instances");
            response.EnsureSuccessStatusCode();
            
            var instances = await response.Content.ReadFromJsonAsync<IEnumerable<DrasiInstance>>(_jsonOptions);
            return instances ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all instances from ControlPlane");
            throw;
        }
    }

    public async Task<bool> StartInstanceAsync(string instanceId, Configuration? configuration = null)
    {
        try
        {
            HttpResponseMessage response;
            
            if (configuration != null)
            {
                // POST with configuration in body
                response = await _httpClient.PostAsJsonAsync(
                    $"/api/servers/instances/{instanceId}/start",
                    configuration,
                    _jsonOptions);
            }
            else
            {
                // POST without body
                response = await _httpClient.PostAsync(
                    $"/api/servers/instances/{instanceId}/start",
                    null);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to start instance {InstanceId}. Status: {StatusCode}",
                    instanceId,
                    response.StatusCode);
                return false;
            }

            _logger.LogInformation("Successfully started instance {InstanceId}", instanceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting instance {InstanceId}", instanceId);
            return false;
        }
    }

    public async Task<bool> StopInstanceAsync(string instanceId)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/servers/instances/{instanceId}/stop",
                null);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to stop instance {InstanceId}. Status: {StatusCode}",
                    instanceId,
                    response.StatusCode);
                return false;
            }

            _logger.LogInformation("Successfully stopped instance {InstanceId}", instanceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping instance {InstanceId}", instanceId);
            return false;
        }
    }

    public async Task<bool> RestartInstanceAsync(string instanceId)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/servers/instances/{instanceId}/restart",
                null);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to restart instance {InstanceId}. Status: {StatusCode}",
                    instanceId,
                    response.StatusCode);
                return false;
            }

            _logger.LogInformation("Successfully restarted instance {InstanceId}", instanceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting instance {InstanceId}", instanceId);
            return false;
        }
    }

    public async Task<Configuration?> GetConfigurationAsync(string instanceId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/configuration/instances/{instanceId}");
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Configuration>(_jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration for instance {InstanceId}", instanceId);
            throw;
        }
    }

    public async Task<bool> UpdateConfigurationAsync(
        string instanceId,
        JsonPatchDocument<Configuration> patch)
    {
        try
        {
            var response = await _httpClient.PatchAsJsonAsync(
                $"/api/configuration/instances/{instanceId}",
                patch,
                _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to update configuration for instance {InstanceId}. Status: {StatusCode}",
                    instanceId,
                    response.StatusCode);
                return false;
            }

            _logger.LogInformation("Successfully updated configuration for instance {InstanceId}", instanceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration for instance {InstanceId}", instanceId);
            return false;
        }
    }

    public async Task<IEnumerable<StatusChangeRecord>> GetRecentStatusChangesAsync(
        DateTime since,
        InstanceStatus? statusFilter = null)
    {
        try
        {
            var url = $"/api/status/recent-changes?since={since:O}";
            if (statusFilter.HasValue)
            {
                url += $"&statusFilter={statusFilter.Value}";
            }

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var changes = await response.Content.ReadFromJsonAsync<IEnumerable<StatusChangeRecord>>(_jsonOptions);
            return changes ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent status changes from ControlPlane");
            return [];
        }
    }
}
