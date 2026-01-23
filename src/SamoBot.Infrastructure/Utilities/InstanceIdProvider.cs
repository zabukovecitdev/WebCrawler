namespace SamoBot.Infrastructure.Utilities;

/// <summary>
/// Provides a unique instance identifier for the current process/instance
/// </summary>
public class InstanceIdProvider
{
    public InstanceIdProvider()
    {
        // Generate a unique instance ID: machine name + process ID + startup timestamp
        var machineName = Environment.MachineName;
        var processId = Environment.ProcessId;
        var startupTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        InstanceId = $"{machineName}:{processId}:{startupTime}";
    }

    /// <summary>
    /// Gets the unique instance identifier for this process
    /// </summary>
    public string InstanceId { get; }
}
