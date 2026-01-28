using System.Collections.Concurrent;

namespace DRaaS.Core.Services.ResourceAllocation;

/// <summary>
/// Thread-safe port allocator that manages port assignment across platform managers.
/// </summary>
public class PortAllocator : IPortAllocator
{
    private readonly ConcurrentDictionary<int, bool> _allocatedPorts = new();
    private int _nextPort = 8080;
    private readonly object _portLock = new();

    public int AllocatePort()
    {
        lock (_portLock)
        {
            // Find next available port
            while (_allocatedPorts.ContainsKey(_nextPort))
            {
                _nextPort++;
                if (_nextPort > 9000) // Arbitrary upper limit
                {
                    _nextPort = 8080; // Wrap around
                }
            }

            var allocatedPort = _nextPort;
            _allocatedPorts.TryAdd(allocatedPort, true);
            _nextPort++;

            return allocatedPort;
        }
    }

    public void ReleasePort(int port)
    {
        _allocatedPorts.TryRemove(port, out _);
    }

    public bool IsPortAllocated(int port)
    {
        return _allocatedPorts.ContainsKey(port);
    }
}
