using System.Text.Json;

namespace SlideLe;

internal static class AppSettingsStore
{
    private const string SettingsFileName = "settings.json";

    public static string LoadSourceUrl(string fallbackUrl)
    {
        try
        {
            string settingsPath = GetSettingsPath();
            if (!File.Exists(settingsPath))
            {
                return fallbackUrl;
            }

            using FileStream stream = File.OpenRead(settingsPath);
            SavedSettings? settings = JsonSerializer.Deserialize<SavedSettings>(stream);
            string sourceUrl = settings?.SourceUrl?.Trim() ?? string.Empty;
            GitHubFolderAddress.Parse(sourceUrl);
            return sourceUrl;
        }
        catch (IOException)
        {
            return fallbackUrl;
        }
        catch (UnauthorizedAccessException)
        {
            return fallbackUrl;
        }
        catch (JsonException)
        {
            return fallbackUrl;
        }
        catch (ArgumentException)
        {
            return fallbackUrl;
        }
    }

    public static void SaveSourceUrl(string sourceUrl)
    {
        string settingsDirectory = Application.UserAppDataPath;
        Directory.CreateDirectory(settingsDirectory);

        string settingsPath = GetSettingsPath();
        string temporaryPath = Path.Combine(settingsDirectory,
            $"{SettingsFileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new SavedSettings(sourceUrl)));
            File.Move(temporaryPath, settingsPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string GetSettingsPath() =>
        Path.Combine(Application.UserAppDataPath, SettingsFileName);

    private sealed record SavedSettings(string SourceUrl);
}
