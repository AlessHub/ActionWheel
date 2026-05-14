using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace ActionWheel.Services;

public record EmoteInfo(uint RowId, string Name, uint UnlockLink, uint IconId, string TextCommand, string Category);

public class EmoteService
{
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    private List<EmoteInfo>? emoteCache;
    private Dictionary<uint, EmoteInfo>? _byIdCache;

    public EmoteService(IDataManager dataManager, IPluginLog log)
    {
        this.dataManager = dataManager;
        this.log = log;
    }

    public List<EmoteInfo> GetAllEmotes()
    {
        if (emoteCache != null) return emoteCache;

        emoteCache = new List<EmoteInfo>();
        try
        {
            var sheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Emote>();
            if (sheet == null)
            {
                log.Warning("Emote sheet was null");
                return emoteCache;
            }

            foreach (var row in sheet)
            {
                var name = row.Name.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;
                uint iconId = 0;
                try { iconId = (uint)row.Icon; } catch (Exception ex) { log.Warning(ex, $"Failed to read icon for emote {row.RowId}"); }
                string textCmd = string.Empty;
                try { textCmd = row.TextCommand.ValueNullable?.Command.ToString() ?? string.Empty; } catch (Exception ex) { log.Warning(ex, $"Failed to read text command for emote {row.RowId}"); }
                string category = "General";
                try { category = row.EmoteCategory.ValueNullable?.Name.ToString() ?? "General"; } catch (Exception ex) { log.Warning(ex, $"Failed to read category for emote {row.RowId}"); }
                if (string.IsNullOrWhiteSpace(category)) category = "General";
                emoteCache.Add(new EmoteInfo(row.RowId, name, (uint)row.UnlockLink, iconId, textCmd, category));
            }

            emoteCache = emoteCache.OrderBy(e => e.Name).ToList();
            _byIdCache = new Dictionary<uint, EmoteInfo>(emoteCache.Count);
            foreach (var e in emoteCache) _byIdCache[e.RowId] = e;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load Emote sheet");
        }

        return emoteCache;
    }

    public unsafe List<EmoteInfo> GetOwnedEmotes()
    {
        var all = GetAllEmotes();
        var result = new List<EmoteInfo>(all.Count);

        try
        {
            var uiState = UIState.Instance();
            if (uiState == null) return all; 

            foreach (var emote in all)
            {
                if (emote.UnlockLink == 0 || uiState->IsUnlockLinkUnlocked(emote.UnlockLink))
                    result.Add(emote);
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to check emote ownership");
            return all;
        }

        return result;
    }

    public void InvalidateCache() { emoteCache = null; _byIdCache = null; }

    public EmoteInfo? GetById(uint id)
    {
        if (_byIdCache == null) GetAllEmotes();
        return _byIdCache?.TryGetValue(id, out var info) == true ? info : null;
    }
}
