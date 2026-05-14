using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ActionWheel;

[Serializable]
public class CharacterWheelConfig
{
    public int DefaultPage { get; set; } = 0;
    public int WheelPageCount { get; set; } = 1;
    public List<uint>     WheelEmoteIds  { get; set; } = new();
    public HashSet<uint>  ChatLogEmotes  { get; set; } = new();
    public HashSet<uint>  FavoriteEmotes { get; set; } = new();

    public List<string>   PageNames  { get; set; } = new();

    public List<Vector4?> PageColors { get; set; } = new();

    public List<Vector4?> PageHoverColors { get; set; } = new();

    public string GetPageName(int page)
        => page >= 0 && page < PageNames.Count && !string.IsNullOrWhiteSpace(PageNames[page])
           ? PageNames[page]
           : $"Page {page + 1}";

    public Vector4? GetPageColor(int page)
        => page >= 0 && page < PageColors.Count ? PageColors[page] : null;

    public void SetPageName(int page, string name)
    {
        while (PageNames.Count <= page) PageNames.Add(string.Empty);
        PageNames[page] = name;
    }

    public void SetPageColor(int page, Vector4? color)
    {
        while (PageColors.Count <= page) PageColors.Add(null);
        PageColors[page] = color;
    }

    public Vector4? GetPageHoverColor(int page)
        => page >= 0 && page < PageHoverColors.Count ? PageHoverColors[page] : null;

    public void SetPageHoverColor(int page, Vector4? color)
    {
        while (PageHoverColors.Count <= page) PageHoverColors.Add(null);
        PageHoverColors[page] = color;
    }

    public List<Vector4?> PageTextColors { get; set; } = new();

    public Vector4? GetPageTextColor(int page)
        => page >= 0 && page < PageTextColors.Count ? PageTextColors[page] : null;

    public void SetPageTextColor(int page, Vector4? color)
    {
        while (PageTextColors.Count <= page) PageTextColors.Add(null);
        PageTextColors[page] = color;
    }
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;

    public int WheelKeybindKey { get; set; } = 0xC0; // OEM_3 (backtick/tilde)
    public bool WheelKeybindCtrl { get; set; } = false;
    public bool WheelKeybindAlt { get; set; } = false;
    public bool WheelKeybindShift { get; set; } = false;

    public int  PagePrevKey   { get; set; } = 0x02; // VK_RBUTTON
    public bool PagePrevCtrl  { get; set; } = false;
    public bool PagePrevAlt   { get; set; } = false;
    public bool PagePrevShift { get; set; } = false;

    public int  PageNextKey   { get; set; } = 0x01; // VK_LBUTTON
    public bool PageNextCtrl  { get; set; } = false;
    public bool PageNextAlt   { get; set; } = false;
    public bool PageNextShift { get; set; } = false;

    public Vector4 WheelColor      { get; set; } = new Vector4(30f / 255f, 55f / 255f, 80f / 255f, 200f / 255f);

    public Vector4 WheelHoverColor { get; set; } = new Vector4(220f / 255f, 160f / 255f, 20f / 255f, 210f / 255f);

    public Vector4 WheelTextColor { get; set; } = new Vector4(210f / 255f, 220f / 255f, 235f / 255f, 200f / 255f);

    public bool ShowWheelText { get; set; } = true;

    public Dictionary<string, CharacterWheelConfig> CharacterConfigs { get; set; } = new();

    public int           DefaultPage    { get; set; } = 0;
    public int           WheelPageCount { get; set; } = 1;
    public List<uint>    WheelEmoteIds  { get; set; } = new();
    public HashSet<uint> ChatLogEmotes  { get; set; } = new();
    public HashSet<uint> FavoriteEmotes { get; set; } = new();

    public CharacterWheelConfig GetCharacterConfig(string key)
    {
        if (!CharacterConfigs.TryGetValue(key, out var cfg))
            CharacterConfigs[key] = cfg = new CharacterWheelConfig();
        return cfg;
    }

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
