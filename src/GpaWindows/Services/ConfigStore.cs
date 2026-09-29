using System.Text.Json;
using GpaWindows.Models;

namespace GpaWindows.Services;

public static class ConfigStore
{
    public static string BaseDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GPAWindows");

    public static string ConfigPath { get; } = Path.Combine(BaseDirectory, "policy.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static PolicyConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return new PolicyConfig();

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<PolicyConfig>(json, JsonOptions) ?? new PolicyConfig();
        }
        catch
        {
            return new PolicyConfig();
        }
    }

    public static void Save(PolicyConfig config)
    {
        Directory.CreateDirectory(BaseDirectory);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var temp = ConfigPath + ".tmp";

        File.WriteAllText(temp, json);
        File.Move(temp, ConfigPath, true);
    }
}
