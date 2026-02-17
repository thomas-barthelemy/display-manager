using System;
using System.Linq;

namespace DisplayManager;

class Program
{
    static int Main(string[] args)
    {
        // Handle --list flag for diagnostics (works without config)
        if (args.Length == 1 && args[0].Equals("--list", StringComparison.OrdinalIgnoreCase))
        {
            DisplayConfig? listConfig = null;
            string configPath = ConfigLoader.GetDefaultConfigPath();
            if (System.IO.File.Exists(configPath))
            {
                try { listConfig = ConfigLoader.Load(configPath); }
                catch { /* Ignore config errors for --list */ }
            }

            var configurator = listConfig != null
                ? new DisplayConfigurator(listConfig)
                : new DisplayConfigurator();
            configurator.ListMonitors();
            return 0;
        }

        // Load configuration
        DisplayConfig config;
        string cfgPath = ConfigLoader.GetDefaultConfigPath();
        try
        {
            config = ConfigLoader.Load(cfgPath);
        }
        catch (System.IO.FileNotFoundException)
        {
            Console.Error.WriteLine($"Error: Config file not found at: {cfgPath}");
            Console.Error.WriteLine($"Copy config.example.json to config.json and edit it for your setup.");
            Console.Error.WriteLine($"Run 'DisplayManager.exe --list' to discover your monitor details.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading config: {ex.Message}");
            return 1;
        }

        // Validate arguments
        if (args.Length != 1)
        {
            PrintUsage(config);
            return 1;
        }

        // Resolve mode name: accept either a name or a 1-based index
        string? modeName = ResolveModeName(args[0], config);
        if (modeName == null)
        {
            Console.Error.WriteLine($"Unknown mode: '{args[0]}'");
            Console.Error.WriteLine();
            PrintUsage(config);
            return 1;
        }

        try
        {
            var configurator = new DisplayConfigurator(config);
            bool success = configurator.ApplyMode(modeName);
            return success ? 0 : 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 3;
        }
    }

    /// <summary>
    /// Resolve user input to a mode name. Accepts:
    /// - Exact mode name (case-insensitive)
    /// - 1-based numeric index
    /// </summary>
    static string? ResolveModeName(string input, DisplayConfig config)
    {
        // Try exact name match (case-insensitive)
        var match = config.Modes.Keys
            .FirstOrDefault(k => k.Equals(input, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            return match;

        // Try numeric index (1-based)
        if (int.TryParse(input, out int index) && index >= 1 && index <= config.Modes.Count)
            return config.Modes.Keys.ElementAt(index - 1);

        return null;
    }

    static void PrintUsage(DisplayConfig config)
    {
        Console.WriteLine("DisplayManager — Monitor Configuration Tool");
        Console.WriteLine();
        Console.WriteLine("Usage: DisplayManager.exe <mode>");
        Console.WriteLine();
        Console.WriteLine("Available modes:");

        int i = 1;
        foreach (var (name, mode) in config.Modes)
        {
            var enabledDisplays = mode.Displays.Where(d => d.Enabled).ToList();
            var primary = enabledDisplays.FirstOrDefault(d => d.Primary);
            string primaryName = primary != null && config.Monitors.TryGetValue(primary.Monitor, out var pm)
                ? pm.Name : "?";

            var summary = string.Join(", ",
                mode.Displays.Select(d =>
                {
                    var mon = config.Monitors.TryGetValue(d.Monitor, out var m) ? m.Name : d.Monitor;
                    if (!d.Enabled) return $"{mon} (off)";
                    var label = d.Primary ? "primary" : "extend";
                    return $"{mon} ({label}, {d.Width}x{d.Height}@{d.RefreshRate}Hz)";
                }));

            Console.WriteLine($"  {i}. {name,-12}  {summary}");
            i++;
        }

        Console.WriteLine();
        Console.WriteLine("Diagnostics:");
        Console.WriteLine("  --list      Show all detected monitors and their device paths");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid arguments or config error");
        Console.WriteLine("  2  Failed to apply configuration");
        Console.WriteLine("  3  Unhandled error");
    }
}
