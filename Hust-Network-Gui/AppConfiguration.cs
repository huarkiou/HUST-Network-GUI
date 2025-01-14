using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HustNetworkGui;

internal struct Configuration
{
    public Configuration()
    {
    }

    public int ErrorTimeout { get; set; } = 10000;
    public int SuccessTimeout { get; set; } = 60000;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

[JsonSerializable(typeof(Configuration))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class ConfigurationContext : JsonSerializerContext;

internal class AppConfiguration
{
    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");

    public Configuration Config = new();

    private AppConfiguration()
    {
        if (!File.Exists(ConfigPath)) return;
        Config = JsonSerializer.Deserialize(
            File.ReadAllText(ConfigPath), ConfigurationContext.Default.Configuration);
    }

    ~AppConfiguration()
    {
        Save();
    }

    public static AppConfiguration Instance { get; } = new();

    public void Save()
    {
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(
            Config, typeof(Configuration), ConfigurationContext.Default));
    }
}