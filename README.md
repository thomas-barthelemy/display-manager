# DisplayManager

A Windows command-line tool for switching between monitor configurations. Define your display modes once in a config file, then switch between them with a single command.

Built on the Windows [CCD (Connecting and Configuring Displays)](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/ccd-apis) API. Monitors are identified by serial number via device path, so configurations survive display index changes after reboots or cable swaps.

## Features

- **Named modes** — Switch configurations by name (e.g., `gaming`, `work`, `focus`)
- **Any number of monitors** — Not locked to a specific count; scales to your setup
- **Fault tolerance** — Retries failed operations and restores last known good config on failure
- **Stable identification** — Uses monitor serial numbers, not display indices
- **Explicit positioning** — You define each monitor's desktop position per mode

## Requirements

- Windows 10/11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Quick Start

### 1. Build

```sh
dotnet build -c Release
```

### 2. Discover your monitors

```sh
DisplayManager.exe --list
```

This prints each connected monitor with its **UID** and **serial number** — you'll need these for the config file.

### 3. Create your config

Copy `config.example.json` to `config.json` (next to the executable) and fill in your monitor details:

```json
{
  "monitors": {
    "main": {
      "serial": "YOUR_SERIAL",
      "uid": "5&xxxxxxxx&0&UIDxxxx",
      "name": "My Primary Monitor"
    }
  },
  "modes": {
    "default": {
      "displays": [
        {
          "monitor": "main",
          "enabled": true,
          "primary": true,
          "width": 2560,
          "height": 1440,
          "refreshRate": 144,
          "position": { "x": 0, "y": 0 }
        }
      ]
    }
  }
}
```

### 4. Apply a mode

```sh
# By name
DisplayManager.exe gaming

# By index (1-based)
DisplayManager.exe 1
```

## Config Format

The config file has two sections:

### `monitors`

Maps an alias to a physical monitor. Use `--list` to find the `serial` and `uid` for each connected display.

| Field    | Description                                                     |
|----------|-----------------------------------------------------------------|
| `serial` | Monitor serial number (from EDID / WMI)                        |
| `uid`    | Device path UID segment (e.g., `5&1c94b30a&0&UID4353`)         |
| `name`   | Friendly name for logging                                       |

### `modes`

Each mode defines which monitors to enable/disable and their display settings.

| Field         | Required       | Description                            |
|---------------|----------------|----------------------------------------|
| `monitor`     | Always         | Alias from the `monitors` section      |
| `enabled`     | Always         | `true` to enable, `false` to disable   |
| `primary`     | When enabled   | Exactly one enabled display must be primary |
| `width`       | When enabled   | Horizontal resolution                  |
| `height`      | When enabled   | Vertical resolution                    |
| `refreshRate` | When enabled   | Refresh rate in Hz                     |
| `position`    | When enabled   | Desktop position `{ "x": 0, "y": 0 }` |

Disabled displays only need `monitor` and `enabled: false`.

## CLI Reference

```
DisplayManager.exe <mode>      Apply a display mode by name or index
DisplayManager.exe --list      Show all detected monitors
```

### Exit Codes

| Code | Meaning                          |
|------|----------------------------------|
| 0    | Success                          |
| 1    | Invalid arguments or config error|
| 2    | Failed to apply configuration    |
| 3    | Unhandled error                  |

## How It Works

1. Queries all display paths (active and inactive) via the CCD API
2. Matches physical monitors to config entries using the UID → serial mapping
3. Builds a minimal set of active paths with unique source IDs (for extend, not clone)
4. Validates and applies the new configuration
5. Verifies the result by re-querying active paths
6. On failure, retries up to 3 times, then restores the last known good config

## License

[MIT](LICENSE)
