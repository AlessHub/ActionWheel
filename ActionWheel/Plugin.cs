using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Keys;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using ActionWheel.Services;
using ActionWheel.Windows;

namespace ActionWheel;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;

    private const string CommandName = "/actionwheel";

    public Configuration Configuration { get; init; }
    public EmoteService EmoteService { get; init; }
    public static MacroService MacroService { get; private set; } = null!;

    /// <summary>Returns "CharacterName@WorldId" for the current player, or empty when not logged in.</summary>
    private string GetCurrentCharacterKey()
    {
        var player = ObjectTable.LocalPlayer;
        if (player == null) return string.Empty;
        return $"{player.Name.TextValue}@{player.HomeWorld.RowId}";
    }

    /// <summary>Wheel configuration for the currently logged-in character (blank default when not in-game).</summary>
    public CharacterWheelConfig ActiveCharConfig
    {
        get
        {
            var key = GetCurrentCharacterKey();
            return Configuration.GetCharacterConfig(string.IsNullOrEmpty(key) ? "__offline__" : key);
        }
    }

    public readonly WindowSystem WindowSystem = new("ActionWheel");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private WheelWindow WheelWindow { get; init; }

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private static bool IsGameWindowFocused()
    {
        var fg = GetForegroundWindow();
        GetWindowThreadProcessId(fg, out var fgPid);
        return fgPid == (uint)Environment.ProcessId;
    }

    internal static IDalamudTextureWrap? GetIcon(uint iconId)
    {
        if (iconId == 0) return null;
        try { return TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault(); }
        catch { return null; }
    }

    private static uint _savedEmoteTextType = 0;
    private static int  _emoteTextRestoreCountdown = 0;

    internal static void ExecuteEmote(string textCommand, bool showLog)
    {
        if (textCommand.StartsWith("__macro:"))
        {
            var parts = textCommand.Split(':');
            if (parts.Length == 3 &&
                uint.TryParse(parts[1], out var macSet) &&
                uint.TryParse(parts[2], out var macSlot))
            {
                foreach (var line in MacroService.GetMacroLines(macSet, macSlot))
                    ExecuteCommand(line);
            }
            return;
        }

        if (!showLog)
        {
            ExecuteCommand($"{textCommand} motion");
            return;
        }

        if (_emoteTextRestoreCountdown == 0)
            GameConfig.UiConfig.TryGet("EmoteTextType", out _savedEmoteTextType);

        GameConfig.UiConfig.Set("EmoteTextType", (uint)1);
        ExecuteCommand(textCommand);
        _emoteTextRestoreCountdown = 120; // restore after ~2s or bad thing happen
    }

    internal static unsafe void ExecuteCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        if (!command.StartsWith('/') || command.Length > 500)
        {
            Log.Warning($"ExecuteCommand: rejected disallowed command (length={command.Length}).");
            return;
        }
        try
        {
            var uiModule = UIModule.Instance();
            if (uiModule == null) return;
            var shell = uiModule->GetRaptureShellModule();
            if (shell == null) return;
            Utf8String str = default;
            str.SetString(command);
            shell->ExecuteCommandInner(&str, uiModule);
            str.Dtor();
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to execute command: {command}");
        }
    }

    private List<EmoteInfo?> GetWheelEmotes()
    {
        var charCfg = ActiveCharConfig;
        var result = new List<EmoteInfo?>();
        foreach (var id in charCfg.WheelEmoteIds)
        {
            if (id == 0)
                result.Add(null);
            else if (MacroService.IsMacroId(id))
            {
                var (set, slot) = MacroService.DecodeId(id);
                result.Add(MacroService.GetMacroInfo(set, slot));
            }
            else
                result.Add(EmoteService.GetById(id));
        }
        int minCount = charCfg.WheelPageCount * WheelWindow.EmotesPerPage;
        while (result.Count < minCount)
            result.Add(null);
        return result;
    }

    private void OnCharacterLoaded(string key)
    {
        if (!Configuration.CharacterConfigs.ContainsKey(key))
        {
            if (Configuration.WheelEmoteIds.Count > 0 ||
                Configuration.ChatLogEmotes.Count  > 0 ||
                Configuration.FavoriteEmotes.Count > 0)
            {
                Configuration.CharacterConfigs[key] = new CharacterWheelConfig
                {
                    DefaultPage    = Configuration.DefaultPage,
                    WheelPageCount = Configuration.WheelPageCount,
                    WheelEmoteIds  = new List<uint>(Configuration.WheelEmoteIds),
                    ChatLogEmotes  = new HashSet<uint>(Configuration.ChatLogEmotes),
                    FavoriteEmotes = new HashSet<uint>(Configuration.FavoriteEmotes),
                };
                Configuration.WheelEmoteIds.Clear();
                Configuration.ChatLogEmotes.Clear();
                Configuration.FavoriteEmotes.Clear();
                Configuration.WheelPageCount = 1;
                Configuration.DefaultPage    = 0;
                Configuration.Save();
                Log.Information($"WheelEmote: migrated legacy config to character '{key}'.");
            }
        }
        ConfigWindow.InvalidateEmoteCache();
    }

    private bool _keybindWasPressed = false;
    private string _lastCharacterKey = string.Empty;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        EmoteService = new EmoteService(DataManager, Log);
        MacroService = new MacroService(Log);

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        WheelWindow = new WheelWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(WheelWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Action Wheel window."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        Framework.Update += OnFrameworkUpdate;

        Log.Information($"WheelEmote loaded.");
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();
        WheelWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var currentKey = GetCurrentCharacterKey();
        if (currentKey != _lastCharacterKey)
        {
            if (!string.IsNullOrEmpty(currentKey))
                OnCharacterLoaded(currentKey);
            _lastCharacterKey = currentKey;
        }
        if (_emoteTextRestoreCountdown > 0)
        {
            if (--_emoteTextRestoreCountdown == 0)
                GameConfig.UiConfig.Set("EmoteTextType", _savedEmoteTextType);
        }

        var cfg = Configuration;
        if (cfg.WheelKeybindKey == 0 || !IsGameWindowFocused())
        {
            _keybindWasPressed = false;
            return;
        }

        bool ctrl  = (GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool alt   = (GetAsyncKeyState(0x12) & 0x8000) != 0;
        bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
        bool keyDown = (GetAsyncKeyState(cfg.WheelKeybindKey) & 0x8000) != 0;

        var isPressed = keyDown
            && ctrl  == cfg.WheelKeybindCtrl
            && alt   == cfg.WheelKeybindAlt
            && shift == cfg.WheelKeybindShift;

        if (isPressed && !_keybindWasPressed)
        {
            var wheelEmotes = GetWheelEmotes();
            if (wheelEmotes.Any(e => e != null))
                WheelWindow.OpenAtCursor(wheelEmotes);
        }
        else if (!isPressed && _keybindWasPressed && WheelWindow.IsOpen)
        {
            WheelWindow.ExecuteAndClose();
        }
        _keybindWasPressed = isPressed;
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
