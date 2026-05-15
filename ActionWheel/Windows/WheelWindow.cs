using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using ActionWheel.Services;

namespace ActionWheel.Windows;

public class WheelWindow : Window, IDisposable
{
    private const float OuterRadius    = 120f;
    private const float InnerRadius    = 44f;
    private const float WindowHalfW   = 160f;
    private const float WindowHalfH   = 160f;
    private const float PageBarHeight  = 50f;
    private const float IconSize       = 30f;
    private const int   ArcSegs        = 20;
    public  const int   EmotesPerPage  = 8;
    public  const int   MaxPages       = 10;

    private readonly Plugin plugin;
    private Configuration cfg => plugin.Configuration;
    private List<EmoteInfo?> emotes      = new();
    private Vector2         center;
    private bool            pendingOpen;
    private int             currentPage;
    private bool            _prevNavWasDown;
    private bool            _nextNavWasDown;
    private bool            _lmbWasDown;
    private bool            _rmbWasDown;

    public int  HoveredIndex  { get; private set; } = -1;
    public bool HoveredCenter { get; private set; } = false;

    public WheelWindow(Plugin plugin)
        : base("##ActionWheelRadial",
            ImGuiWindowFlags.NoTitleBar      |
            ImGuiWindowFlags.NoCollapse      |
            ImGuiWindowFlags.NoBackground    |
            ImGuiWindowFlags.NoMove          |
            ImGuiWindowFlags.NoResize        |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoNav           |
            ImGuiWindowFlags.NoFocusOnAppearing)
    {
        Size = new Vector2(WindowHalfW * 2, WindowHalfH * 2 + PageBarHeight);
        SizeCondition = ImGuiCond.Always;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public void OpenAtCursor(List<EmoteInfo?> wheelEmotes)
    {
        emotes        = wheelEmotes;
        pendingOpen   = true;
        int totalPages = Math.Min(MaxPages, Math.Max(1, (wheelEmotes.Count + EmotesPerPage - 1) / EmotesPerPage));
        currentPage   = Math.Min(plugin.ActiveCharConfig.DefaultPage, totalPages - 1);
        HoveredIndex  = -1;
        HoveredCenter = false;
        _prevNavWasDown = false;
        _nextNavWasDown = false;
        _lmbWasDown = (Plugin.GetAsyncKeyState(0x01) & 0x8000) != 0;
        _rmbWasDown = (Plugin.GetAsyncKeyState(0x02) & 0x8000) != 0;
        IsOpen        = true;
    }

    public void ExecuteAndClose()
    {
        EmoteInfo? emote = null;
        var page = GetPageSpan();
        if (HoveredCenter)
        {
            emote = page.Length > 0 ? page[0] : null;
        }
        else if (HoveredIndex >= 0)
        {
            int ringIdx = HoveredIndex + 1;
            if (ringIdx < page.Length)
                emote = page[ringIdx];
        }

        IsOpen = false;
        if (emote != null && !string.IsNullOrWhiteSpace(emote.TextCommand))
        {
            bool showLog = plugin.ActiveCharConfig.ChatLogEmotes?.Contains(emote.RowId) == true;
            Plugin.ExecuteEmote(emote.TextCommand, showLog);
        }
    }

    private ReadOnlySpan<EmoteInfo?> GetPageSpan()
    {
        int start = currentPage * EmotesPerPage;
        if (start >= emotes.Count) return ReadOnlySpan<EmoteInfo?>.Empty;
        int count = Math.Min(EmotesPerPage, emotes.Count - start);
        return CollectionsMarshal.AsSpan(emotes).Slice(start, count);
    }

    public override void Draw()
    {
        if (pendingOpen)
        {
            center      = ImGui.GetMousePos();
            pendingOpen = false;
        }

        ImGui.SetWindowPos(center - new Vector2(WindowHalfW, WindowHalfH));

        var mouse      = ImGui.GetMousePos();
        var charCfg   = plugin.ActiveCharConfig;
        var pageSpan  = GetPageSpan();
        int ringCount = Math.Max(0, pageSpan.Length - 1);
        EmoteInfo? centerEmote = pageSpan.Length > 0 ? pageSpan[0] : null;
        int totalPages = Math.Min(MaxPages, Math.Max(1, (emotes.Count + EmotesPerPage - 1) / EmotesPerPage));

        bool lmbDown = (Plugin.GetAsyncKeyState(0x01) & 0x8000) != 0;
        bool rmbDown = (Plugin.GetAsyncKeyState(0x02) & 0x8000) != 0;
        bool lmbEdge = lmbDown && !_lmbWasDown;
        bool rmbEdge = rmbDown && !_rmbWasDown;
        _lmbWasDown  = lmbDown;
        _rmbWasDown  = rmbDown;

        // no double clicking the nav arrows if mouse buttons are used for navigation
        bool arrowUsedLmb = false;
        if (totalPages > 1 && lmbEdge)
        {
            const float HitR   = 16f;
            float       arrowX = OuterRadius + 26f;
            var         lPos   = new Vector2(center.X - arrowX, center.Y);
            var         rPos   = new Vector2(center.X + arrowX, center.Y);
            if ((mouse - lPos).Length() < HitR)
            { currentPage = (currentPage - 1 + totalPages) % totalPages; arrowUsedLmb = true; }
            else if ((mouse - rPos).Length() < HitR)
            { currentPage = (currentPage + 1) % totalPages; arrowUsedLmb = true; }
        }

        if (totalPages > 1)
        {
            float scroll = ImGui.GetIO().MouseWheel;
            if (scroll > 0)      currentPage = (currentPage - 1 + totalPages) % totalPages;
            else if (scroll < 0) currentPage = (currentPage + 1) % totalPages;
        }

        // LMB/RMB keys share the same edge signal as arrow detection so they
        // can never double-fire on the same physical click.
        if (totalPages > 1)
        {
            if (NavEdge(cfg.PagePrevKey, cfg.PagePrevCtrl, cfg.PagePrevAlt, cfg.PagePrevShift,
                        lmbEdge, rmbEdge, arrowUsedLmb, ref _prevNavWasDown))
                currentPage = (currentPage - 1 + totalPages) % totalPages;
            if (NavEdge(cfg.PageNextKey, cfg.PageNextCtrl, cfg.PageNextAlt, cfg.PageNextShift,
                        lmbEdge, rmbEdge, arrowUsedLmb, ref _nextNavWasDown))
                currentPage = (currentPage + 1) % totalPages;
        }
        else
        {
            TickNavWasDown(cfg.PagePrevKey, ref _prevNavWasDown);
            TickNavWasDown(cfg.PageNextKey, ref _nextNavWasDown);
        }

        var dl    = ImGui.GetWindowDrawList();
        var delta = mouse - center;
        var dist  = delta.Length();

        HoveredIndex  = -1;
        HoveredCenter = false;

        if (dist <= InnerRadius && centerEmote != null)
        {
            HoveredCenter = true;
        }
        else if (dist > InnerRadius && ringCount > 0)
        {
            float angle  = MathF.Atan2(delta.Y, delta.X);
            float step   = MathF.PI * 2f / ringCount;
            float rel    = ((angle + MathF.PI / 2f) % (MathF.PI * 2f) + MathF.PI * 2f) % (MathF.PI * 2f);
            rel          = (rel + step / 2f) % (MathF.PI * 2f);
            int candidate = (int)(rel / step) % ringCount;
            HoveredIndex  = pageSpan[candidate + 1] != null ? candidate : -1;
        }

        dl.AddCircleFilled(center, OuterRadius + 10f, Col(0, 0, 0, 140), 64);

        var pageColor      = charCfg.GetPageColor(currentPage)      ?? cfg.WheelColor;
        var pageHoverColor = charCfg.GetPageHoverColor(currentPage) ?? cfg.WheelHoverColor;
        var pageTextColor  = charCfg.GetPageTextColor(currentPage)  ?? cfg.WheelTextColor;
        var chatLogEmotes  = charCfg.ChatLogEmotes;

        float segStep   = MathF.PI * 2f / Math.Max(1, ringCount);
        float segOffset = -MathF.PI / 2f;

        for (int i = 0; i < ringCount; i++)
        {
            var    ringEmote  = pageSpan[i + 1];
            bool   isEmpty    = ringEmote == null;
            bool   hovered    = !isEmpty && i == HoveredIndex;
            float  a0      = segOffset + i * segStep - segStep / 2f;
            float  a1      = a0 + segStep;
            float  mid     = segOffset + i * segStep;

            var fillCol   = hovered ? ColV(pageHoverColor)
                          : isEmpty ? Col(20, 25, 30, 80)
                          :           ColV(pageColor);
            var hTint     = pageHoverColor;
            var borderCol = hovered ? Col((byte)Math.Min(255, hTint.X * 1.15f * 255), (byte)Math.Min(255, hTint.Y * 1.15f * 255), (byte)Math.Min(255, hTint.Z * 1.15f * 255), 255)
                          : isEmpty ? Col(40, 50, 60, 80)
                          :           Col(80, 110, 140, 200);

            DrawRingSegment(dl, center, InnerRadius, OuterRadius, a0, a1, fillCol);

            dl.AddLine(
                center + new Vector2(MathF.Cos(a0) * InnerRadius, MathF.Sin(a0) * InnerRadius),
                center + new Vector2(MathF.Cos(a0) * OuterRadius, MathF.Sin(a0) * OuterRadius),
                borderCol, 1.5f);

            if (isEmpty) continue;

            // Icon + name
            var    emoteInfo  = ringEmote!;
            float  labelR     = (InnerRadius + OuterRadius) / 2f;
            var    labelPos   = center + new Vector2(MathF.Cos(mid) * labelR, MathF.Sin(mid) * labelR);
            uint   textColor  = hovered ? Col(255, 245, 180, 255) : ColV(pageTextColor);

            var tex = Plugin.GetIcon(emoteInfo.IconId);

            if (tex != null)
            {
                var iconOffset = cfg.ShowWheelText ? new Vector2(IconSize / 2f, IconSize / 2f + 7f) : new Vector2(IconSize / 2f, IconSize / 2f);
                var iconMin = labelPos - iconOffset;
                dl.AddImage(tex.Handle, iconMin, iconMin + new Vector2(IconSize, IconSize));
                if (chatLogEmotes?.Contains(emoteInfo.RowId) == true)
                    dl.AddRect(iconMin, iconMin + new Vector2(IconSize, IconSize), Col(255, 165, 50, 230), 0, 0, 1.5f);

                if (cfg.ShowWheelText)
                {
                    var label    = Truncate(emoteInfo.Name, 10);
                    var textSize = ImGui.CalcTextSize(label);
                    dl.AddText(labelPos + new Vector2(-textSize.X / 2f, IconSize / 2f - 2f), textColor, label);
                }
            }
            else if (cfg.ShowWheelText)
            {
                var label    = Truncate(emoteInfo.Name, 12);
                var textSize = ImGui.CalcTextSize(label);
                dl.AddText(labelPos - textSize / 2f, textColor, label);
            }
        }

        bool cHov  = HoveredCenter;
        uint cFill = cHov ? ColV(pageHoverColor) : Col(15, 20, 25, 230);
        dl.AddCircleFilled(center, InnerRadius,       cFill,                   32);
        dl.AddCircle(      center, InnerRadius,       Col( 80, 110, 140, 200), 32, 1.5f);
        dl.AddCircle(      center, OuterRadius + 10f, Col( 80, 110, 140, 120), 64, 1.5f);

        if (centerEmote != null)
        {
            const float CIconSize = 26f;
            var cTex = Plugin.GetIcon(centerEmote.IconId);
            uint textCol = cHov ? Col(255, 245, 180, 255) : ColV(pageTextColor);
            if (cTex != null)
            {
                var iconOffset = cfg.ShowWheelText ? new Vector2(CIconSize / 2f, CIconSize / 2f + 5f) : new Vector2(CIconSize / 2f, CIconSize / 2f);
                var iconMin  = center - iconOffset;
                dl.AddImage(cTex.Handle, iconMin, iconMin + new Vector2(CIconSize, CIconSize));
                if (chatLogEmotes?.Contains(centerEmote.RowId) == true)
                    dl.AddRect(iconMin, iconMin + new Vector2(CIconSize, CIconSize), Col(255, 165, 50, 230), 0, 0, 1.5f);
                if (cfg.ShowWheelText)
                {
                    var label    = Truncate(centerEmote.Name, 8);
                    var textSize = ImGui.CalcTextSize(label);
                    dl.AddText(center + new Vector2(-textSize.X / 2f, CIconSize / 2f - 4f), textCol, label);
                }
            }
            else if (cfg.ShowWheelText)
            {
                var label    = Truncate(centerEmote.Name, 8);
                var textSize = ImGui.CalcTextSize(label);
                dl.AddText(center - textSize / 2f, textCol, label);
            }
        }

        if (totalPages > 1)
        {
            float barY     = center.Y + WindowHalfH + PageBarHeight / 2f - 6f;
            float arrowX   = OuterRadius + 26f;
            var   leftPos  = new Vector2(center.X - arrowX, center.Y);
            var   rightPos = new Vector2(center.X + arrowX, center.Y);

            uint arrowCol = Col(210, 220, 235, 220);
            var  leftSz   = ImGui.CalcTextSize("<");
            var  rightSz  = ImGui.CalcTextSize(">");
            dl.AddText(leftPos  - leftSz  / 2f, arrowCol, "<");
            dl.AddText(rightPos - rightSz / 2f, arrowCol, ">");

            float dotSpacing = 22f;
            float totalDotW  = (totalPages - 1) * dotSpacing;
            float startX     = center.X - totalDotW / 2f;

            for (int p = 0; p < totalPages; p++)
            {
                float px   = startX + p * dotSpacing;
                bool  cur  = p == currentPage;
                float r    = cur ? 9f : 7f;
                uint  dCol = cur ? Col(255, 210, 50, 255) : Col(120, 150, 180, 200);
                dl.AddCircleFilled(new Vector2(px, barY), r, dCol, 16);

                var  numStr  = $"{p + 1}";
                var  numSize = ImGui.CalcTextSize(numStr);
                uint numCol  = cur ? Col(20, 15, 0, 255) : Col(200, 215, 230, 200);
                dl.AddText(new Vector2(px - numSize.X / 2f, barY - numSize.Y / 2f), numCol, numStr);
            }

            var   curPageName  = charCfg.GetPageName(currentPage);
            float nameFontSz   = ImGui.GetFontSize() * 1.2f;
            var   nameSize     = ImGui.CalcTextSize(curPageName) * 1.2f;
            uint  nameTextCol  = ColV(pageTextColor);
            dl.AddText(ImGui.GetFont(), nameFontSz,
                new Vector2(center.X - nameSize.X / 2f, barY + 12f),
                nameTextCol, curPageName);
        }
    }

    private static bool NavEdge(int key, bool ctrl, bool alt, bool shift,
                                 bool lmbEdge, bool rmbEdge, bool arrowUsedLmb,
                                 ref bool wasDown)
    {
        if (key == 0) { wasDown = false; return false; }
        bool c = (Plugin.GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool a = (Plugin.GetAsyncKeyState(0x12) & 0x8000) != 0;
        bool s = (Plugin.GetAsyncKeyState(0x10) & 0x8000) != 0;
        if (c != ctrl || a != alt || s != shift) { wasDown = false; return false; }

        if (key == 0x01) return lmbEdge && !arrowUsedLmb;  // LMB
        if (key == 0x02) return rmbEdge;                    // RMB (arrows not RMB-clickable)

        bool down = (Plugin.GetAsyncKeyState(key) & 0x8000) != 0;
        bool edge = down && !wasDown;
        wasDown   = down;
        return edge;
    }
    private static void TickNavWasDown(int key, ref bool wasDown)
    {
        if (key == 0x01 || key == 0x02 || key == 0) { wasDown = false; return; }
        wasDown = (Plugin.GetAsyncKeyState(key) & 0x8000) != 0;
    }

    private static void DrawRingSegment(ImDrawListPtr dl, Vector2 c,
        float r0, float r1, float a0, float a1, uint col)
    {
        dl.PathClear();
        for (int j = 0; j <= ArcSegs; j++)
        {
            float a = a0 + (a1 - a0) * j / ArcSegs;
            dl.PathLineTo(new Vector2(c.X + MathF.Cos(a) * r1, c.Y + MathF.Sin(a) * r1));
        }
        for (int j = ArcSegs; j >= 0; j--)
        {
            float a = a0 + (a1 - a0) * j / ArcSegs;
            dl.PathLineTo(new Vector2(c.X + MathF.Cos(a) * r0, c.Y + MathF.Sin(a) * r0));
        }
        dl.PathFillConvex(col);
    }
    private static uint Col(byte r, byte g, byte b, byte a)
        => (uint)((a << 24) | (b << 16) | (g << 8) | r);

    private static uint ColV(System.Numerics.Vector4 v)
        => Col((byte)(v.X * 255), (byte)(v.Y * 255), (byte)(v.Z * 255), (byte)(v.W * 255));

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";

}

