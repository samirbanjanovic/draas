namespace DRaaS.Core.Services.ResourceAllocation;

/// <summary>
/// Provides centralized port allocation to avoid conflicts across platform managers.
/// </summary>
public interface IPortAllocator
{
    /// <summary>
    /// Allocates the next available port from the managed pool.
    /// </summary>
    /// <returns>An available port number.</returns>
    int AllocatePort();

    /// <summary>
    /// Releases a previously allocated port back to the pool.
    /// </summary>
    /// <param name="port">The port number to release.</param>
    void ReleasePort(int port);

    /// <summary>
    /// Checks if a specific port is currently allocated.
    /// </summary>
    /// <param name="port">The port number to check.</param>
    /// <returns>True if the port is allocated, false otherwise.</returns>
    bool IsPortAllocated(int port);
}
