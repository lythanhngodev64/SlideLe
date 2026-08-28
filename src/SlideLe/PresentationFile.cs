namespace SlideLe;

internal sealed record PresentationFile(
    string Name,
    string RelativePath,
    long? SizeInBytes,
    string DownloadUrl)
{
    public string DisplaySize => SizeInBytes is long size ? FormatSize(size) : "—";

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes:N0} {units[unitIndex]}"
            : $"{value:N1} {units[unitIndex]}";
    }
}

