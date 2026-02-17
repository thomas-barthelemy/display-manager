using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace DisplayManager;

/// <summary>
/// Describes a desired display state for a single monitor.
/// </summary>
public record DisplaySpec(
    string Serial,
    string FriendlyName,
    bool Enabled,
    bool IsPrimary,
    uint Width,
    uint Height,
    uint RefreshRate,
    int PositionX = 0,
    int PositionY = 0
);

/// <summary>
/// Core engine that identifies monitors by serial number (from EDID via device path)
/// and applies display configurations using the Windows CCD API.
/// </summary>
public class DisplayConfigurator
{
    private readonly DisplayConfig _config;
    private readonly Dictionary<string, string> _uidToSerial;

    private const int MaxRetries = 3;
    private static readonly int[] RetryDelaysMs = [500, 1000, 2000];

    public DisplayConfigurator(DisplayConfig config)
    {
        _config = config;
        _uidToSerial = ConfigLoader.BuildUidToSerialMap(config);
    }

    /// <summary>
    /// Constructor for diagnostic use (--list) when no config is available.
    /// </summary>
    public DisplayConfigurator()
    {
        _config = new DisplayConfig();
        _uidToSerial = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    // ── Public API ─────────────────────────────────────────────────

    /// <summary>
    /// Apply a named display mode from the loaded configuration.
    /// </summary>
    public bool ApplyMode(string modeName)
    {
        var specs = ConfigLoader.ResolveMode(_config, modeName);

        Log($"╔══════════════════════════════════════════════════════╗");
        Log($"║  Display Manager — Applying Mode: {modeName,-19}║");
        Log($"╚══════════════════════════════════════════════════════╝");
        Log("");

        foreach (var s in specs)
        {
            var state = s.Enabled ? (s.IsPrimary ? "PRIMARY" : "EXTEND ") : "DISABLE";
            var res = s.Enabled ? $"{s.Width}x{s.Height} @ {s.RefreshRate}Hz" : "n/a";
            Log($"  [{state}] {s.FriendlyName} (Serial: {s.Serial})  {res}");
        }
        Log("");

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            Log($"  Attempt {attempt}/{MaxRetries}...");

            bool ok = TryApply(specs);
            if (ok)
            {
                Log("");
                Log($"  ✓ Mode '{modeName}' applied successfully.");
                return true;
            }

            if (attempt < MaxRetries)
            {
                int delay = RetryDelaysMs[attempt - 1];
                Log($"  ✗ Attempt failed. Retrying in {delay}ms...");
                System.Threading.Thread.Sleep(delay);
            }
        }

        Log("");
        Log($"  ✗ FAILED after {MaxRetries} attempts. Attempting to restore last known good config...");
        RestoreLastKnownGood();
        return false;
    }

    /// <summary>
    /// Returns the list of available mode names from the configuration.
    /// </summary>
    public IEnumerable<string> GetAvailableModes() => _config.Modes.Keys;

    // ── Core apply logic ───────────────────────────────────────────

    private bool TryApply(List<DisplaySpec> specs)
    {
        // Step 1: Query ALL paths (active + inactive) so we can find disabled monitors too
        if (!QueryAllPaths(out var allPaths, out var allModes))
            return false;

        Log($"    Queried {allPaths.Length} paths, {allModes.Length} modes");

        // Step 2: Collect ALL candidate path indices per serial
        var serialToCandidates = MapSerialsToCandidatePaths(allPaths);

        foreach (var spec in specs)
        {
            if (!serialToCandidates.ContainsKey(spec.Serial))
            {
                Log($"    ⚠ Could not find monitor with serial '{spec.Serial}' ({spec.FriendlyName})");
                Log($"      Available serials: {string.Join(", ", serialToCandidates.Keys)}");
                return false;
            }
        }

        // Step 3: For enabled displays, pick paths with UNIQUE source IDs to avoid cloning.
        var enabledSpecs = specs.Where(s => s.Enabled).ToList();

        var selectedPaths = SelectPathsWithUniqueSources(enabledSpecs, serialToCandidates, allPaths);
        if (selectedPaths == null)
        {
            Log($"    ✗ Could not find paths with unique source IDs for all enabled displays");
            return false;
        }

        // Log the selected source assignments
        foreach (var (serial, pathIdx) in selectedPaths)
        {
            var p = allPaths[pathIdx];
            var spec = enabledSpecs.First(s => s.Serial == serial);
            Log($"    {spec.FriendlyName}: target={p.targetInfo.id} → source={p.sourceInfo.id}");
        }

        // Step 4: Build MINIMAL path and mode arrays — only the paths we want active.
        var newPaths = new List<DISPLAYCONFIG_PATH_INFO>();
        var newModes = new List<DISPLAYCONFIG_MODE_INFO>();

        foreach (var spec in enabledSpecs)
        {
            int origIdx = selectedPaths[spec.Serial];
            var path = allPaths[origIdx];
            var position = new POINTL { x = spec.PositionX, y = spec.PositionY };

            // Mark path as active
            path.flags = NativeApi.DISPLAYCONFIG_PATH_ACTIVE;

            // Create source mode entry and point path to it
            uint sourceModeIdx = (uint)newModes.Count;
            var sourceMode = new DISPLAYCONFIG_MODE_INFO
            {
                infoType = DISPLAYCONFIG_MODE_INFO_TYPE.SOURCE,
                id = path.sourceInfo.id,
                adapterId = path.sourceInfo.adapterId,
            };
            sourceMode.info.sourceMode = new DISPLAYCONFIG_SOURCE_MODE
            {
                width = spec.Width,
                height = spec.Height,
                pixelFormat = DISPLAYCONFIG_PIXELFORMAT.PIXELFORMAT_32BPP,
                position = position,
            };
            newModes.Add(sourceMode);
            path.sourceInfo.modeInfoIdx = sourceModeIdx;

            // Create target mode entry and point path to it
            uint targetModeIdx = (uint)newModes.Count;
            var targetMode = new DISPLAYCONFIG_MODE_INFO
            {
                infoType = DISPLAYCONFIG_MODE_INFO_TYPE.TARGET,
                id = path.targetInfo.id,
                adapterId = path.targetInfo.adapterId,
            };
            targetMode.info.targetMode = new DISPLAYCONFIG_TARGET_MODE
            {
                targetVideoSignalInfo = new DISPLAYCONFIG_VIDEO_SIGNAL_INFO
                {
                    activeSize = new DISPLAYCONFIG_2DREGION { cx = spec.Width, cy = spec.Height },
                    totalSize = new DISPLAYCONFIG_2DREGION { cx = spec.Width, cy = spec.Height },
                    vSyncFreq = new DISPLAYCONFIG_RATIONAL { Numerator = spec.RefreshRate, Denominator = 1 },
                    hSyncFreq = new DISPLAYCONFIG_RATIONAL { Numerator = 0, Denominator = 0 },
                    pixelRate = 0,
                    scanLineOrdering = DISPLAYCONFIG_SCANLINE_ORDERING.PROGRESSIVE,
                }
            };
            newModes.Add(targetMode);
            path.targetInfo.modeInfoIdx = targetModeIdx;

            // Set path target properties
            path.targetInfo.refreshRate = new DISPLAYCONFIG_RATIONAL
            {
                Numerator = spec.RefreshRate,
                Denominator = 1
            };
            path.targetInfo.scanLineOrdering = DISPLAYCONFIG_SCANLINE_ORDERING.PROGRESSIVE;
            path.targetInfo.rotation = DISPLAYCONFIG_ROTATION.IDENTITY;
            path.targetInfo.scaling = DISPLAYCONFIG_SCALING.IDENTITY;

            newPaths.Add(path);
            Log($"    Enabled path for {spec.FriendlyName} at position ({position.x}, {position.y})");
        }

        var pathArray = newPaths.ToArray();
        var modeArray = newModes.ToArray();

        // Step 5: Apply the configuration
        var flagsApply = SetDisplayConfigFlags.SDC_APPLY
                       | SetDisplayConfigFlags.SDC_USE_SUPPLIED_DISPLAY_CONFIG
                       | SetDisplayConfigFlags.SDC_ALLOW_CHANGES
                       | SetDisplayConfigFlags.SDC_SAVE_TO_DATABASE;

        // First validate
        var validateFlags = SetDisplayConfigFlags.SDC_VALIDATE
                          | SetDisplayConfigFlags.SDC_USE_SUPPLIED_DISPLAY_CONFIG
                          | SetDisplayConfigFlags.SDC_ALLOW_CHANGES;

        int valResult = NativeApi.SetDisplayConfig(
            (uint)pathArray.Length, pathArray,
            (uint)modeArray.Length, modeArray,
            validateFlags);

        if (valResult != 0)
        {
            Log($"    ⚠ Validation failed (error 0x{valResult:X8}). Trying apply with ALLOW_CHANGES...");
        }
        else
        {
            Log($"    Validation passed");
        }

        // Apply
        int result = NativeApi.SetDisplayConfig(
            (uint)pathArray.Length, pathArray,
            (uint)modeArray.Length, modeArray,
            flagsApply);

        if (result != 0)
        {
            Log($"    ✗ SetDisplayConfig failed with error 0x{result:X8}");
            return false;
        }

        Log($"    ✓ SetDisplayConfig succeeded");

        // Step 6: Verify — re-query active paths and check the right monitors are on
        System.Threading.Thread.Sleep(500); // Give the system a moment to settle
        return VerifyConfiguration(specs);
    }

    // ── Path selection with unique sources ──────────────────────────

    /// <summary>
    /// From the QDC_ALL_PATHS results, select one path per enabled display such that
    /// each path uses a different source ID. Displays sharing a source = cloned/mirrored.
    /// Different source IDs = extended.
    /// </summary>
    private static Dictionary<string, int>? SelectPathsWithUniqueSources(
        List<DisplaySpec> enabledSpecs,
        Dictionary<string, List<int>> serialToCandidates,
        DISPLAYCONFIG_PATH_INFO[] paths)
    {
        // Build: serial → list of (pathIndex, sourceId)
        var candidates = new Dictionary<string, List<(int PathIdx, uint SourceId)>>();
        foreach (var spec in enabledSpecs)
        {
            var list = new List<(int, uint)>();
            foreach (int idx in serialToCandidates[spec.Serial])
            {
                list.Add((idx, paths[idx].sourceInfo.id));
            }
            candidates[spec.Serial] = list;
        }

        // Greedy assignment: for each display, pick a path whose source ID
        // hasn't been used by a previously assigned display.
        var usedSources = new HashSet<uint>();
        var result = new Dictionary<string, int>();

        foreach (var spec in enabledSpecs)
        {
            var options = candidates[spec.Serial];

            if (options.Count == 0)
                return null; // No candidate paths at all for this display

            // Prefer a path with an unused source ID
            (int PathIdx, uint SourceId)? pick = null;
            foreach (var option in options)
            {
                if (!usedSources.Contains(option.SourceId))
                {
                    pick = option;
                    break;
                }
            }

            // All sources are taken — fall back to first available
            // (ALLOW_CHANGES flag may remap sources for us)
            pick ??= options[0];

            result[spec.Serial] = pick.Value.PathIdx;
            usedSources.Add(pick.Value.SourceId);
        }

        return result;
    }

    // ── Verification ───────────────────────────────────────────────

    private bool VerifyConfiguration(List<DisplaySpec> specs)
    {
        if (!QueryActivePaths(out var activePaths, out _))
        {
            Log($"    ⚠ Post-apply verification query failed");
            return false; // Can't verify, treat as failure
        }

        var activeSerials = new HashSet<string>();
        for (int i = 0; i < activePaths.Length; i++)
        {
            string? serial = GetMonitorSerial(activePaths[i]);
            if (serial != null)
                activeSerials.Add(serial);
        }

        bool allGood = true;
        foreach (var spec in specs)
        {
            bool isActive = activeSerials.Contains(spec.Serial);
            if (spec.Enabled && !isActive)
            {
                Log($"    ⚠ Verify: {spec.FriendlyName} should be ENABLED but is not active");
                allGood = false;
            }
            else if (!spec.Enabled && isActive)
            {
                Log($"    ⚠ Verify: {spec.FriendlyName} should be DISABLED but is still active");
                allGood = false;
            }
            else
            {
                Log($"    ✓ Verify: {spec.FriendlyName} is correctly {(spec.Enabled ? "enabled" : "disabled")}");
            }
        }

        return allGood;
    }

    // ── Restore ────────────────────────────────────────────────────

    private void RestoreLastKnownGood()
    {
        // Use SDC_USE_DATABASE_CURRENT which restores the last persisted config
        int result = NativeApi.SetDisplayConfig(
            0, null!,
            0, null!,
            SetDisplayConfigFlags.SDC_APPLY | SetDisplayConfigFlags.SDC_USE_DATABASE_CURRENT);

        if (result == 0)
            Log($"  ✓ Restored last known good configuration");
        else
            Log($"  ✗ Failed to restore last known good config (error 0x{result:X8})");
    }

    // ── Path querying helpers ──────────────────────────────────────

    private static bool QueryAllPaths(out DISPLAYCONFIG_PATH_INFO[] paths, out DISPLAYCONFIG_MODE_INFO[] modes)
    {
        return QueryPaths(QueryDisplayConfigFlags.QDC_ALL_PATHS, out paths, out modes);
    }

    private static bool QueryActivePaths(out DISPLAYCONFIG_PATH_INFO[] paths, out DISPLAYCONFIG_MODE_INFO[] modes)
    {
        return QueryPaths(QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, out paths, out modes);
    }

    private static bool QueryPaths(QueryDisplayConfigFlags flags, out DISPLAYCONFIG_PATH_INFO[] paths, out DISPLAYCONFIG_MODE_INFO[] modes)
    {
        paths = [];
        modes = [];

        int sizeResult = NativeApi.GetDisplayConfigBufferSizes(flags, out uint pathCount, out uint modeCount);
        if (sizeResult != 0)
        {
            Log($"    ✗ GetDisplayConfigBufferSizes failed (0x{sizeResult:X8})");
            return false;
        }

        paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

        // IMPORTANT: currentTopologyId MUST be NULL (IntPtr.Zero) for both
        // QDC_ALL_PATHS and QDC_ONLY_ACTIVE_PATHS — MSDN says passing a
        // non-null pointer causes ERROR_INVALID_PARAMETER (0x57).
        int queryResult = NativeApi.QueryDisplayConfig(
            flags,
            ref pathCount, paths,
            ref modeCount, modes,
            IntPtr.Zero);

        if (queryResult != 0)
        {
            Log($"    ✗ QueryDisplayConfig failed (0x{queryResult:X8})");
            return false;
        }

        // Trim arrays to actual count
        Array.Resize(ref paths, (int)pathCount);
        Array.Resize(ref modes, (int)modeCount);
        return true;
    }

    // ── Monitor identification ─────────────────────────────────────

    /// <summary>
    /// Collect ALL candidate path indices per serial number.
    /// QDC_ALL_PATHS returns multiple paths per target (one per possible source).
    /// We need all of them so we can pick paths with unique source IDs.
    /// </summary>
    private Dictionary<string, List<int>> MapSerialsToCandidatePaths(DISPLAYCONFIG_PATH_INFO[] paths)
    {
        var result = new Dictionary<string, List<int>>();

        for (int i = 0; i < paths.Length; i++)
        {
            string? serial = GetMonitorSerial(paths[i]);
            if (serial == null) continue;

            if (!result.TryGetValue(serial, out var list))
            {
                list = new List<int>();
                result[serial] = list;
            }
            list.Add(i);
        }

        // Sort candidates: active paths first (they have valid mode data)
        foreach (var (_, list) in result)
        {
            list.Sort((a, b) =>
            {
                bool aActive = (paths[a].flags & NativeApi.DISPLAYCONFIG_PATH_ACTIVE) != 0;
                bool bActive = (paths[b].flags & NativeApi.DISPLAYCONFIG_PATH_ACTIVE) != 0;
                return bActive.CompareTo(aActive); // true (active) sorts first
            });
        }

        return result;
    }

    /// <summary>
    /// Extract the serial number from the monitor device path in the target name info.
    /// </summary>
    private string? GetMonitorSerial(DISPLAYCONFIG_PATH_INFO path)
    {
        var deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
        deviceName.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_TARGET_NAME;
        deviceName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
        deviceName.header.adapterId = path.targetInfo.adapterId;
        deviceName.header.id = path.targetInfo.id;

        int result = NativeApi.DisplayConfigGetDeviceInfo(ref deviceName);
        if (result != 0)
            return null;

        string monitorPath = deviceName.monitorDevicePath ?? "";
        return LookupSerialFromDevicePath(monitorPath);
    }

    // ── Serial lookup via UID in device path ───────────────────────

    private string? LookupSerialFromDevicePath(string devicePath)
    {
        if (string.IsNullOrEmpty(devicePath))
            return null;

        // CCD path: \\?\DISPLAY#DELA19C#5&1c94b30a&0&UID4353#{guid}
        // Split on # → parts[2] = UID segment
        string[] parts = devicePath.Split('#');
        if (parts.Length >= 3)
        {
            string uid = parts[2];
            if (_uidToSerial.TryGetValue(uid, out string? serial))
                return serial;
        }

        return null;
    }

    // ── Diagnostic: list all detected monitors ─────────────────────

    public void ListMonitors()
    {
        Log("Detected monitors:");
        Log("");

        if (!QueryAllPaths(out var paths, out var modes))
        {
            Log("  ✗ Failed to query display config");
            return;
        }

        var seen = new HashSet<string>();
        for (int i = 0; i < paths.Length; i++)
        {
            var devName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
            devName.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_TARGET_NAME;
            devName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
            devName.header.adapterId = paths[i].targetInfo.adapterId;
            devName.header.id = paths[i].targetInfo.id;

            int result = NativeApi.DisplayConfigGetDeviceInfo(ref devName);
            if (result != 0) continue;

            string path = devName.monitorDevicePath ?? "(unknown)";
            if (seen.Contains(path)) continue;
            seen.Add(path);

            // Extract UID from device path for config file
            string uid = "(unknown)";
            string[] pathParts = path.Split('#');
            if (pathParts.Length >= 3)
                uid = pathParts[2];

            string serial = LookupSerialFromDevicePath(path) ?? "(not in config)";
            bool isActive = (paths[i].flags & NativeApi.DISPLAYCONFIG_PATH_ACTIVE) != 0;
            string name = devName.monitorFriendlyDeviceName ?? "(unknown)";

            Log($"  {name}");
            Log($"    UID     : {uid}");
            Log($"    Serial  : {serial}");
            Log($"    Path    : {path}");
            Log($"    Active  : {isActive}");
            Log($"    Source  : {paths[i].sourceInfo.id}");
            Log($"    Target  : adapter({paths[i].targetInfo.adapterId.LowPart}) id({paths[i].targetInfo.id})");
            Log("");
        }

        Log("  TIP: Use the UID and serial values above in your config.json.");
        Log("       See config.example.json for the expected format.");
    }

    // ── Logging ────────────────────────────────────────────────────

    private static void Log(string message)
    {
        Console.WriteLine(message);
    }
}
