using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace ActionWheel.Services;

public class MacroService
{
    private readonly IPluginLog log;

    public const uint MacroBase = 0x40000000u;

    public MacroService(IPluginLog log)
    {
        this.log = log;
    }

    public static bool IsMacroId(uint id) => id >= MacroBase && id < MacroBase + 0x400u;

    public static uint EncodeId(uint set, uint slot) => MacroBase | ((set & 1u) << 8) | (slot & 0xFFu);

    public static (uint set, uint slot) DecodeId(uint id)
    {
        var v = id - MacroBase;
        return ((v >> 8) & 1u, v & 0xFFu);
    }

    public unsafe EmoteInfo? GetMacroInfo(uint set, uint slot)
    {
        try
        {
            var module = RaptureMacroModule.Instance();
            if (module == null) return null;
            var macro = module->GetMacro(set, slot);
            if (macro == null || !macro->IsNotEmpty()) return null;
            var name = macro->Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) name = $"Macro {slot + 1}";
            var cmd = $"__macro:{set}:{slot}";
            var cat = set == 0 ? "Individual" : "Shared";
            return new EmoteInfo(EncodeId(set, slot), name, 0, macro->IconId, cmd, cat);
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to read macro set={set} slot={slot}");
            return null;
        }
    }

    public List<EmoteInfo> GetAllMacros(uint set)
    {
        var result = new List<EmoteInfo>();
        for (uint slot = 0; slot < 100; slot++)
        {
            var info = GetMacroInfo(set, slot);
            if (info != null) result.Add(info);
        }
        return result;
    }

    public unsafe string[] GetMacroLines(uint set, uint slot)
    {
        try
        {
            var module = RaptureMacroModule.Instance();
            if (module == null) return Array.Empty<string>();
            var macro = module->GetMacro(set, slot);
            if (macro == null) return Array.Empty<string>();
            var lines = new List<string>();
            for (int i = 0; i < 15; i++)
            {
                var line = macro->Lines[i].ToString();
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }
            return lines.ToArray();
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to read macro lines set={set} slot={slot}");
            return Array.Empty<string>();
        }
    }
}
