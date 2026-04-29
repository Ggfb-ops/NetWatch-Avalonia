namespace NetWatch.Models;

public class SystemLogFile
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public string Category { get; init; } = "";
    public long SizeBytes { get; init; }
    public string SizeDisplay => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024} KB",
        < 1024 * 1024 * 1024 => $"{SizeBytes / (1024 * 1024)} MB",
        _ => $"{SizeBytes / (1024 * 1024 * 1024)} GB"
    };
    public bool IsReadable { get; init; }
}
