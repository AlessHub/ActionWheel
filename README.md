# Action Wheel

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for Final Fantasy XIV that adds a customisable radial action wheel.

## Features

- **Radial wheel UI** — hold your keybind to open the wheel, hover a segment and release to execute the emote
- **Multiple pages** — organise actions across pages with optional custom names
- **Per-slot assignment** — assign any unlocked emote or macro to any wheel slot
- **Chat log option** — toggle whether each emote is sent to the chat log
- **Per-page colour theming** — override wheel colour, hover colour, and text colour per page
- **Modifier key support** — optionally require a modifier (Ctrl / Alt / Shift) alongside the main keybind


## Usage

1. Open the config window via `/actionwheel` or the Dalamud plugin list
2. Set a **Toggle Key** (the key you hold to open the wheel)
3. Add emote pages and assign emotes to slots
4. In game, hold the toggle key — the wheel appears. Hover a segment and release to use that emote

## Building

```
dotnet build "ActionWheel.sln"
```

Requires the [Dalamud.NET.Sdk](https://github.com/goatcorp/Dalamud.NET.Sdk) NuGet package (restored automatically).

## License

[GNU Affero General Public License v3.0 or later](LICENSE)
