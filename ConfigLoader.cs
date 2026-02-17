using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DisplayManager;

// ── Config model ──────────────────────────────────────────────────

public class DisplayConfig
{
    [JsonPropertyName("monitors")]
    public Dictionary<string, MonitorDefinition> Monitors { get; set; } = new();

    [JsonPropertyName("modes")]
    public Dictionary<string, ModeConfig> Modes { get; set; } = new();
}

public class MonitorDefinition
{
    [JsonPropertyName("serial")]
    public string Serial { get; set; } = "";

    [JsonPropertyName("uid")]
    public string Uid { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class ModeConfig
{
    [JsonPropertyName("displays")]
    public List<DisplayModeEntry> Displays { get; set; } = new();
}

public class DisplayModeEntry
{
    [JsonPropertyName("monitor")]
    public string Monitor { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }

    [JsonPropertyName("width")]
    public uint Width { get; set; }

    [JsonPropertyName("height")]
    public uint Height { get; set; }

    [JsonPropertyName("refreshRate")]
    public uint RefreshRate { get; set; }

    [JsonPropertyName("position")]
    public PositionConfig? Position { get; set; }
}

public class PositionConfig
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}

// ── Loader ────────────────────────────────────────────────────────

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Load and validate the configuration from a JSON file.
    /// </summary>
    public static DisplayConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file not found: {path}");

        string json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<DisplayConfig>(json, s_options)
                     ?? throw new InvalidOperationException("Config file deserialized to null.");

        Validate(config);
        return config;
    }

    /// <summary>
    /// Find the default config path next to the executable.
    /// </summary>
    public static string GetDefaultConfigPath()
    {
        string exeDir = AppContext.BaseDirectory;
        return Path.Combine(exeDir, "config.json");
    }

    // ── Validation ────────────────────────────────────────────────

    private static void Validate(DisplayConfig config)
    {
        if (config.Monitors.Count == 0)
            throw new InvalidOperationException("Config must define at least one monitor.");

        if (config.Modes.Count == 0)
            throw new InvalidOperationException("Config must define at least one mode.");

        foreach (var (modeName, mode) in config.Modes)
        {
            // Check monitor references
            foreach (var display in mode.Displays)
            {
                if (!config.Monitors.ContainsKey(display.Monitor))
                    throw new InvalidOperationException(
                        $"Mode '{modeName}' references unknown monitor '{display.Monitor}'. " +
                        $"Defined monitors: {string.Join(", ", config.Monitors.Keys)}");
            }

            // Check exactly one primary among enabled displays
            var enabledDisplays = mode.Displays.Where(d => d.Enabled).ToList();
            if (enabledDisplays.Count == 0)
                throw new InvalidOperationException(
                    $"Mode '{modeName}' has no enabled displays. At least one display must be enabled.");

            int primaryCount = enabledDisplays.Count(d => d.Primary);
            if (primaryCount != 1)
                throw new InvalidOperationException(
                    $"Mode '{modeName}' has {primaryCount} primary display(s). Exactly one enabled display must be set as primary.");

            // Check enabled displays have resolution set
            foreach (var display in enabledDisplays)
            {
                if (display.Width == 0 || display.Height == 0)
                    throw new InvalidOperationException(
                        $"Mode '{modeName}', monitor '{display.Monitor}': enabled displays must specify width and height.");

                if (display.RefreshRate == 0)
                    throw new InvalidOperationException(
                        $"Mode '{modeName}', monitor '{display.Monitor}': enabled displays must specify refreshRate.");
            }
        }
    }

    // ── Helpers for building runtime data ─────────────────────────

    /// <summary>
    /// Build the UID → serial lookup map from the config's monitor definitions.
    /// </summary>
    public static Dictionary<string, string> BuildUidToSerialMap(DisplayConfig config)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, monitor) in config.Monitors)
        {
            if (!string.IsNullOrWhiteSpace(monitor.Uid) && !string.IsNullOrWhiteSpace(monitor.Serial))
                map[monitor.Uid] = monitor.Serial;
        }
        return map;
    }

    /// <summary>
    /// Build a serial → friendly name lookup from the config's monitor definitions.
    /// </summary>
    public static Dictionary<string, string> BuildSerialToNameMap(DisplayConfig config)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, monitor) in config.Monitors)
        {
            if (!string.IsNullOrWhiteSpace(monitor.Serial))
                map[monitor.Serial] = monitor.Name;
        }
        return map;
    }

    /// <summary>
    /// Resolve a mode config into a list of DisplaySpec records using the monitor definitions.
    /// </summary>
    public static List<DisplaySpec> ResolveMode(DisplayConfig config, string modeName)
    {
        if (!config.Modes.TryGetValue(modeName, out var mode))
            throw new ArgumentException($"Unknown mode: '{modeName}'. Available modes: {string.Join(", ", config.Modes.Keys)}");

        var specs = new List<DisplaySpec>();
        foreach (var entry in mode.Displays)
        {
            var monitor = config.Monitors[entry.Monitor];
            specs.Add(new DisplaySpec(
                Serial: monitor.Serial,
                FriendlyName: monitor.Name,
                Enabled: entry.Enabled,
                IsPrimary: entry.Primary,
                Width: entry.Width,
                Height: entry.Height,
                RefreshRate: entry.RefreshRate,
                PositionX: entry.Position?.X ?? 0,
                PositionY: entry.Position?.Y ?? 0
            ));
        }
        return specs;
    }
}
