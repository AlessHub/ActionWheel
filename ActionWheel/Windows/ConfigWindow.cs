using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ActionWheel.Services;

namespace ActionWheel.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;
    private bool capturingKeybind    = false;
    private bool capturingPagePrev   = false;
    private bool capturingPageNext   = false;
    private DateTime? mouseCaptureReadyAt = null;
    private int  _slotDragSrc  = -1;
    private uint _emoteDragId  = 0;
    private string emoteSearch = string.Empty;
    private List<EmoteInfo>? cachedEmotes;
    private List<EmoteInfo>? cachedIndividualMacros;
    private List<EmoteInfo>? cachedSharedMacros;

    private static readonly VirtualKey[] KeysToCheck =
    [
        VirtualKey.F1,  VirtualKey.F2,  VirtualKey.F3,  VirtualKey.F4,
        VirtualKey.F5,  VirtualKey.F6,  VirtualKey.F7,  VirtualKey.F8,
        VirtualKey.F9,  VirtualKey.F10, VirtualKey.F11, VirtualKey.F12,
        VirtualKey.KEY_0, VirtualKey.KEY_1, VirtualKey.KEY_2, VirtualKey.KEY_3,
        VirtualKey.KEY_4, VirtualKey.KEY_5, VirtualKey.KEY_6, VirtualKey.KEY_7,
        VirtualKey.KEY_8, VirtualKey.KEY_9,
        VirtualKey.A, VirtualKey.B, VirtualKey.C, VirtualKey.D, VirtualKey.E,
        VirtualKey.F, VirtualKey.G, VirtualKey.H, VirtualKey.I, VirtualKey.J,
        VirtualKey.K, VirtualKey.L, VirtualKey.M, VirtualKey.N, VirtualKey.O,
        VirtualKey.P, VirtualKey.Q, VirtualKey.R, VirtualKey.S, VirtualKey.T,
        VirtualKey.U, VirtualKey.V, VirtualKey.W, VirtualKey.X, VirtualKey.Y, VirtualKey.Z,
        VirtualKey.NUMPAD0, VirtualKey.NUMPAD1, VirtualKey.NUMPAD2, VirtualKey.NUMPAD3,
        VirtualKey.NUMPAD4, VirtualKey.NUMPAD5, VirtualKey.NUMPAD6, VirtualKey.NUMPAD7,
        VirtualKey.NUMPAD8, VirtualKey.NUMPAD9,
        VirtualKey.SPACE, VirtualKey.RETURN, VirtualKey.TAB,
        VirtualKey.INSERT, VirtualKey.DELETE, VirtualKey.HOME, VirtualKey.END,
        VirtualKey.PRIOR, VirtualKey.NEXT,
        VirtualKey.UP, VirtualKey.DOWN, VirtualKey.LEFT, VirtualKey.RIGHT,
        VirtualKey.MULTIPLY, VirtualKey.ADD, VirtualKey.SUBTRACT, VirtualKey.DIVIDE, VirtualKey.DECIMAL,
        VirtualKey.OEM_1, VirtualKey.OEM_PLUS, VirtualKey.OEM_COMMA, VirtualKey.OEM_MINUS,
        VirtualKey.OEM_PERIOD, VirtualKey.OEM_2, VirtualKey.OEM_3, VirtualKey.OEM_4,
        VirtualKey.OEM_5, VirtualKey.OEM_6, VirtualKey.OEM_7,
    ];

    public ConfigWindow(Plugin plugin)
        : base("Action Wheel###ActionWheelConfig")
    {

        Size = new Vector2(570, 640);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(530, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        configuration = plugin.Configuration;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public void InvalidateEmoteCache()
    {
        cachedEmotes = null;
    }

    public override void PreDraw()
    {
        if (configuration.IsConfigWindowMovable)
            Flags &= ~ImGuiWindowFlags.NoMove;
        else
            Flags |= ImGuiWindowFlags.NoMove;
    }

    public override void Draw()
    {
    

        ImGui.Spacing();
        DrawKeybindSetter(
            "Wheel Keybind",
            ref capturingKeybind,
            configuration.WheelKeybindKey,
            configuration.WheelKeybindCtrl,
            configuration.WheelKeybindAlt,
            configuration.WheelKeybindShift,
            (key, ctrl, alt, shift) =>
            {
                configuration.WheelKeybindKey   = key;
                configuration.WheelKeybindCtrl  = ctrl;
                configuration.WheelKeybindAlt   = alt;
                configuration.WheelKeybindShift = shift;
                configuration.Save();
            },
            () =>
            {
                configuration.WheelKeybindKey   = 0;
                configuration.WheelKeybindCtrl  = false;
                configuration.WheelKeybindAlt   = false;
                configuration.WheelKeybindShift = false;
                configuration.Save();
            }
        );

        ImGui.Spacing();
        DrawKeybindSetter(
            "Page Prev",
            ref capturingPagePrev,
            configuration.PagePrevKey,
            configuration.PagePrevCtrl,
            configuration.PagePrevAlt,
            configuration.PagePrevShift,
            (key, ctrl, alt, shift) =>
            {
                configuration.PagePrevKey   = key;
                configuration.PagePrevCtrl  = ctrl;
                configuration.PagePrevAlt   = alt;
                configuration.PagePrevShift = shift;
                configuration.Save();
            },
            () =>
            {
                configuration.PagePrevKey   = 0;
                configuration.PagePrevCtrl  = false;
                configuration.PagePrevAlt   = false;
                configuration.PagePrevShift = false;
                configuration.Save();
            }
        );

        ImGui.Spacing();
        DrawKeybindSetter(
            "Page Next",
            ref capturingPageNext,
            configuration.PageNextKey,
            configuration.PageNextCtrl,
            configuration.PageNextAlt,
            configuration.PageNextShift,
            (key, ctrl, alt, shift) =>
            {
                configuration.PageNextKey   = key;
                configuration.PageNextCtrl  = ctrl;
                configuration.PageNextAlt   = alt;
                configuration.PageNextShift = shift;
                configuration.Save();
            },
            () =>
            {
                configuration.PageNextKey   = 0;
                configuration.PageNextCtrl  = false;
                configuration.PageNextAlt   = false;
                configuration.PageNextShift = false;
                configuration.Save();
            }
        );

        ImGui.Spacing();
        ImGui.Text("Default Page:");
        ImGui.SameLine();
        ImGui.TextDisabled("(which page opens first)");

        var slots = plugin.ActiveCharConfig.WheelEmoteIds;

        int usedPages   = slots.Count == 0 ? 0 : (slots.Count + WheelWindow.EmotesPerPage - 1) / WheelWindow.EmotesPerPage;
        int activePages = Math.Clamp(Math.Max(plugin.ActiveCharConfig.WheelPageCount, usedPages), 1, WheelWindow.MaxPages);
        int maxSlots    = activePages * WheelWindow.EmotesPerPage;

        for (int p = 0; p < activePages; p++)
        {
            if (p > 0) ImGui.SameLine();
            bool isCur = plugin.ActiveCharConfig.DefaultPage == p;
            if (isCur)
            {
                ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.7f, 0.5f, 0.1f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.6f, 0.15f, 1f));
            }
            if (ImGui.Button($"{plugin.ActiveCharConfig.GetPageName(p)}##defpage{p}", new Vector2(70, 0)) && !isCur)
            {
                plugin.ActiveCharConfig.DefaultPage = p;
                configuration.Save();
            }
            if (isCur) ImGui.PopStyleColor(2);
        }

        // + / - page management buttons
        ImGui.SameLine();
        bool canAddPage = activePages < WheelWindow.MaxPages;
        ImGui.BeginDisabled(!canAddPage);
        if (ImGui.Button("+##addpage", new Vector2(28, 0)))
        {
            plugin.ActiveCharConfig.WheelPageCount = activePages + 1;
            configuration.Save();
        }
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(canAddPage ? "Add a new wheel page" : $"Maximum {WheelWindow.MaxPages} pages reached");

        ImGui.SameLine();
        bool lastPageEmpty = slots.Count <= (activePages - 1) * WheelWindow.EmotesPerPage;
        bool canRemovePage = activePages > 1 && lastPageEmpty;
        ImGui.BeginDisabled(!canRemovePage);
        if (ImGui.Button("-##removepage", new Vector2(28, 0)))
        {
            plugin.ActiveCharConfig.WheelPageCount = activePages - 1;
            if (plugin.ActiveCharConfig.DefaultPage >= plugin.ActiveCharConfig.WheelPageCount)
                plugin.ActiveCharConfig.DefaultPage = plugin.ActiveCharConfig.WheelPageCount - 1;
            configuration.Save();
        }
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(canRemovePage  ? "Remove last wheel page" :
                             activePages <= 1 ? "Need at least one page" :
                                               "Last page has emotes — clear them first");

        ImGui.Spacing();
        ImGui.Text("Wheel Color:");
        ImGui.SameLine();
        var wheelCol = configuration.WheelColor;
        if (ImGui.ColorEdit4("##wheelcolor", ref wheelCol,
            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
        {
            configuration.WheelColor = wheelCol;
            configuration.Save();
        }

        ImGui.SameLine();
        ImGui.Text("Hover Color:");
        ImGui.SameLine();
        var hoverCol = configuration.WheelHoverColor;
        if (ImGui.ColorEdit4("##wheelhovercolor", ref hoverCol,
            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
        {
            configuration.WheelHoverColor = hoverCol;
            configuration.Save();
        }

        ImGui.SameLine();
        ImGui.Text("Text Color:");
        ImGui.SameLine();
        var textCol = configuration.WheelTextColor;
        if (ImGui.ColorEdit4("##wheeltextcolor", ref textCol,
            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
        {
            configuration.WheelTextColor = textCol;
            configuration.Save();
        }

        ImGui.Spacing();
        bool showText = configuration.ShowWheelText;
        if (ImGui.Checkbox("Show emote names on wheel", ref showText))
        {
            configuration.ShowWheelText = showText;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(1f, 0.8f, 0.4f, 1f), "Wheel Pages");
        ImGui.SameLine();
        ImGui.TextDisabled("(click to remove · drag to reorder)");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(1f, 0.65f, 0.2f, 0.9f), "■");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Orange outline on an emote icon means chat log is enabled for that emote.");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.6f, 0.1f, 0.1f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.15f, 0.15f, 1f));
        if (ImGui.SmallButton("Reset all wheels"))
            ImGui.OpenPopup("##resetconfirm");
        ImGui.PopStyleColor(2);

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        if (ImGui.BeginPopupModal("##resetconfirm", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
        {
            ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1f), "Clear all emotes from all wheels?");
            ImGui.Text("This cannot be undone.");
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.6f, 0.1f, 0.1f, 0.9f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.15f, 0.15f, 1f));
            if (ImGui.Button("Clear all", new Vector2(100, 0)))
            {
                plugin.ActiveCharConfig.WheelEmoteIds.Clear();
                plugin.ActiveCharConfig.WheelPageCount = 1;
                plugin.ActiveCharConfig.DefaultPage    = 0;
                configuration.Save();
                ImGui.CloseCurrentPopup();
            }
            ImGui.PopStyleColor(2);
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(100, 0)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        ImGui.Spacing();

        DrawWheelPreviews(slots, maxSlots, activePages, plugin.ActiveCharConfig, configuration.WheelColor, configuration.WheelHoverColor, configuration.WheelTextColor);

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            // Drag ended without landing on any slot → remove the emote (drag-out-to-remove).
            if (_slotDragSrc >= 0 && _slotDragSrc < slots.Count)
            {
                slots[_slotDragSrc] = 0;
                TrimTrailingZeros(slots);
                configuration.Save();
            }
            _slotDragSrc = -1;
            _emoteDragId = 0;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Header,        new Vector4(0.18f, 0.28f, 0.38f, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.25f, 0.38f, 0.52f, 1f));
        bool showEmotes = ImGui.CollapsingHeader("Available Emotes##emotepanel", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.PopStyleColor(2);

        if (showEmotes)
        {

        ImGui.InputText("##emotesearch", ref emoteSearch, 64);
        if (string.IsNullOrEmpty(emoteSearch) && !ImGui.IsItemActive())
        {
            var dl       = ImGui.GetWindowDrawList();
            var pos      = ImGui.GetItemRectMin() + new Vector2(5, (ImGui.GetItemRectSize().Y - ImGui.GetTextLineHeight()) / 2f);
            dl.AddText(pos, ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.5f, 0.5f, 1f)), "Search...");
        }
        if (!string.IsNullOrEmpty(emoteSearch))
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("✕##clearsearch"))
                emoteSearch = string.Empty;
        }

        cachedEmotes ??= plugin.EmoteService.GetOwnedEmotes();

        var preferredOrder = new[] { "General", "Special", "Expressions" };
        var categories = cachedEmotes
            .Select(e => e.Category)
            .Distinct()
            .OrderBy(c => { int i = Array.IndexOf(preferredOrder, c); return i >= 0 ? i : preferredOrder.Length; })
            .ThenBy(c => c)
            .ToList();

        if (ImGui.BeginTabBar("##emotetabs"))
        {
            if (ImGui.BeginTabItem("★ Favorites##favtab"))
            {
                if (ImGui.SmallButton("Sync from game favorites##importgamefavs"))
                {
                    int added = plugin.ImportGameFavorites();
                    if (added > 0) cachedEmotes = plugin.EmoteService.GetOwnedEmotes(); // refresh
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Import emotes you have starred in the in-game Emote List into plugin favorites.\nAlready-favorited emotes are kept.");
                ImGui.SameLine();
                ImGui.TextDisabled($"({plugin.ActiveCharConfig.FavoriteEmotes?.Count ?? 0} favorited)");
                ImGui.Spacing();

                var favEmotes = cachedEmotes
                    .Where(e => plugin.ActiveCharConfig.FavoriteEmotes?.Contains(e.RowId) == true)
                    .Where(e => string.IsNullOrWhiteSpace(emoteSearch) ||
                                e.Name.Contains(emoteSearch, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                DrawEmoteList(favEmotes, slots, maxSlots);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("All##alltab"))
            {
                var allEmotes = cachedEmotes
                    .Where(e => string.IsNullOrWhiteSpace(emoteSearch) ||
                                e.Name.Contains(emoteSearch, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                DrawEmoteList(allEmotes, slots, maxSlots);
                ImGui.EndTabItem();
            }

            foreach (var cat in categories)
            {
                if (ImGui.BeginTabItem($"{cat}##tab_{cat}"))
                {
                    var catEmotes = cachedEmotes
                        .Where(e => e.Category == cat)
                        .Where(e => string.IsNullOrWhiteSpace(emoteSearch) ||
                                    e.Name.Contains(emoteSearch, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    DrawEmoteList(catEmotes, slots, maxSlots);
                    ImGui.EndTabItem();
                }
            }

            if (ImGui.BeginTabItem("Macros##macrostab"))
            {
                ImGui.TextDisabled("Macros are read from your in-game macro slots. /wait lines are not supported.");
                if (ImGui.SmallButton("Refresh##refreshmacros"))
                {
                    cachedIndividualMacros = null;
                    cachedSharedMacros = null;
                }
                ImGui.SameLine();
                ImGui.TextDisabled("Click Refresh after adding or editing macros in-game for changes to appear here.");
                ImGui.Spacing();
                if (ImGui.BeginTabBar("##macrosubtabs"))
                {
                    if (ImGui.BeginTabItem("Individual##indivmacro"))
                    {
                        cachedIndividualMacros ??= Plugin.MacroService.GetAllMacros(0);
                        var filtered = cachedIndividualMacros
                            .Where(m => string.IsNullOrWhiteSpace(emoteSearch) ||
                                        m.Name.Contains(emoteSearch, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        DrawMacroList(filtered, slots, maxSlots);
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Shared##sharedmacro"))
                    {
                        cachedSharedMacros ??= Plugin.MacroService.GetAllMacros(1);
                        var filtered = cachedSharedMacros
                            .Where(m => string.IsNullOrWhiteSpace(emoteSearch) ||
                                        m.Name.Contains(emoteSearch, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        DrawMacroList(filtered, slots, maxSlots);
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        } // end showEmotes
    }

    private void DrawEmoteList(List<EmoteInfo> emotes, List<uint> slots, int maxSlots)
    {
        const float IconSize = 24f;
        bool canAdd = slots.Count(id => id == 0) > 0 || slots.Count < maxSlots;

        using var child = ImRaii.Child("##emotelist", new Vector2(-1, -1), true);
        if (!child.Success) return;

        if (!ImGui.BeginTable("##emotegrid", 2, ImGuiTableFlags.None)) return;
        ImGui.TableSetupColumn("##col0", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##col1", ImGuiTableColumnFlags.WidthStretch);

        foreach (var emote in emotes)
        {
            ImGui.TableNextColumn();

            bool inWheel  = slots.Contains(emote.RowId);
            bool showsLog = plugin.ActiveCharConfig.ChatLogEmotes?.Contains(emote.RowId) == true;
            bool isFav    = plugin.ActiveCharConfig.FavoriteEmotes?.Contains(emote.RowId) == true;

            ImGui.PushID($"fav_{emote.RowId}");
            ImGui.PushStyleColor(ImGuiCol.Button,        isFav ? new Vector4(0.8f,  0.7f,  0.1f,  1f) : new Vector4(0.2f, 0.2f, 0.2f, 0.5f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, isFav ? new Vector4(0.9f,  0.8f,  0.15f, 1f) : new Vector4(0.3f, 0.3f, 0.3f, 0.7f));
            if (ImGui.SmallButton("★"))
            {
                plugin.ActiveCharConfig.FavoriteEmotes ??= new HashSet<uint>();
                if (isFav) plugin.ActiveCharConfig.FavoriteEmotes.Remove(emote.RowId);
                else       plugin.ActiveCharConfig.FavoriteEmotes.Add(emote.RowId);
                configuration.Save();
            }
            ImGui.PopStyleColor(2);
            ImGui.PopID();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(isFav ? "Remove from favorites" : "Add to favorites");
            ImGui.SameLine();

            ImGui.PushID($"mo_{emote.RowId}");
            ImGui.PushStyleColor(ImGuiCol.Button,        showsLog ? new Vector4(0.75f, 0.4f,  0.1f,  1f) : new Vector4(0.2f, 0.2f, 0.2f, 0.5f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, showsLog ? new Vector4(0.9f,  0.5f,  0.15f, 1f) : new Vector4(0.3f, 0.3f, 0.3f, 0.7f));
            if (ImGui.SmallButton("C"))
            {
                plugin.ActiveCharConfig.ChatLogEmotes ??= new HashSet<uint>();
                if (showsLog) plugin.ActiveCharConfig.ChatLogEmotes.Remove(emote.RowId);
                else          plugin.ActiveCharConfig.ChatLogEmotes.Add(emote.RowId);
                configuration.Save();
            }
            ImGui.PopStyleColor(2);
            ImGui.PopID();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(showsLog
                    ? "Chat log ON: emote shows a message in chat\nClick to silence (motion only)"
                    : "Silent (motion only) · Click to enable chat log message");
            ImGui.SameLine();

            var tex = Plugin.GetIcon(emote.IconId);
            if (tex != null)
            {
                ImGui.Image(tex.Handle, new Vector2(IconSize, IconSize));
                ImGui.SameLine();
            }

            if (inWheel)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.9f, 0.4f, 1f));

            bool clicked = ImGui.Selectable($"{emote.Name}##emote{emote.RowId}", inWheel,
                ImGuiSelectableFlags.None, new Vector2(0, IconSize));

            if (inWheel)
                ImGui.PopStyleColor();

            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
            {
                _emoteDragId = emote.RowId;
                ImGui.SetDragDropPayload("WE_EMOTE", new byte[] { 1 }, ImGuiCond.None);
                var dtex = Plugin.GetIcon(emote.IconId);
                if (dtex != null) { ImGui.Image(dtex.Handle, new Vector2(24, 24)); ImGui.SameLine(); }
                ImGui.Text(emote.Name);
                ImGui.EndDragDropSource();
            }

            if (clicked)
            {
                if (inWheel)
                {
                    int idx = slots.IndexOf(emote.RowId);
                    if (idx >= 0) { slots[idx] = 0; TrimTrailingZeros(slots); }
                    configuration.Save();
                }
                else if (canAdd)
                {
                    int freeIdx = slots.IndexOf((uint)0);
                    if (freeIdx >= 0) slots[freeIdx] = emote.RowId;
                    else if (slots.Count < maxSlots) slots.Add(emote.RowId);
                    configuration.Save();
                }
            }

            if (ImGui.IsItemHovered() && !string.IsNullOrWhiteSpace(emote.TextCommand))
                ImGui.SetTooltip(inWheel ? $"Click to remove from wheel\n{emote.TextCommand}"
                                         : canAdd ? $"Click to add to wheel\n{emote.TextCommand}"
                                                  : $"Wheel is full (max {WheelWindow.MaxPages} pages)");
        }

        ImGui.EndTable();
    }

    private void DrawWheelPreviews(List<uint> slots, int maxSlots, int pageCount, CharacterWheelConfig charCfg, Vector4 globalColor, Vector4 globalHoverColor, Vector4 globalTextColor)
    {
        const float OuterR    = 76f;
        const float InnerR    = 27f;
        const float IconSz    = 22f;
        const float CanvasR   = OuterR + 10f;  // 86
        const float CanvasW   = CanvasR * 2f;  // 172
        const float BtnHalf   = 18f;
        const int   ArcSegs   = 20;
        const int   RingCount = WheelWindow.EmotesPerPage - 1; // 7

        float segStep   = MathF.PI * 2f / RingCount;
        float segOffset = -MathF.PI / 2f;
        float labelR    = (InnerR + OuterR) / 2f;

        var  dl    = ImGui.GetWindowDrawList();
        var  mouse = ImGui.GetMousePos();

        // Responsive: fit as many wheels per row as the available content width allows.
        // Each wheel is CanvasW wide with 20px gap between columns (plus 8px left indent).
        float availW = ImGui.GetContentRegionAvail().X;
        int WheelsPerRow = Math.Max(1, (int)((availW + 20f) / (CanvasW + 20f)));

        for (int page = 0; page < pageCount; page++)
        {
            int col = page % WheelsPerRow;
            if (col > 0) ImGui.SameLine(0f, 20f);
            else ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8f);  // keep border away from window edge
            ImGui.PushID(page);
            ImGui.BeginGroup();

            var groupTopPos = ImGui.GetCursorScreenPos();

            const float NameInputW = 120f;
            var pageName = page < charCfg.PageNames.Count ? charCfg.PageNames[page] : string.Empty;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (CanvasW - NameInputW) / 2f);
            ImGui.SetNextItemWidth(NameInputW);
            if (ImGui.InputTextWithHint($"##pagename{page}", $"Page {page + 1}", ref pageName, 32))
            {
                charCfg.SetPageName(page, pageName);
                configuration.Save();
            }

            var canvasPos = ImGui.GetCursorScreenPos();
            var canvasSz  = new Vector2(CanvasW, CanvasW);
            var center    = canvasPos + new Vector2(CanvasR, CanvasR);

            ImGui.Dummy(canvasSz);

            int baseSlot = page * WheelWindow.EmotesPerPage;

            var   delta    = mouse - center;
            float dist     = delta.Length();
            var   mouseRel = mouse - canvasPos;
            bool  inCanvas = mouseRel.X >= 0 && mouseRel.X <= CanvasW
                          && mouseRel.Y >= 0 && mouseRel.Y <= CanvasW;

            int  hovRing = -1;
            bool hovCen  = false;

            if (inCanvas)
            {
                if (dist <= InnerR)
                    hovCen = baseSlot < slots.Count;
                else if (dist > InnerR && dist <= OuterR)
                {
                    float angle = MathF.Atan2(delta.Y, delta.X);
                    float rel   = ((angle + MathF.PI / 2f) % (MathF.PI * 2f) + MathF.PI * 2f) % (MathF.PI * 2f);
                    rel         = (rel + segStep / 2f) % (MathF.PI * 2f);
                    hovRing     = (int)(rel / segStep) % RingCount;
                }
            }

            dl.AddCircleFilled(center, OuterR + 6f, WheelCol(0, 0, 0, 110), 64);

            for (int i = 0; i < RingCount; i++)
            {
                bool  hov  = i == hovRing;
                float a0   = segOffset + i * segStep - segStep / 2f;
                float a1   = a0 + segStep;
                float mid  = segOffset + i * segStep;

                var  pageHovCol = charCfg.GetPageHoverColor(page) ?? globalHoverColor;
                uint fillCol   = hov ? WheelColV(pageHovCol) : WheelColV(charCfg.GetPageColor(page) ?? globalColor);
                uint borderCol = hov
                    ? WheelCol((byte)Math.Min(255, (int)(pageHovCol.X * 1.15f * 255)),
                               (byte)Math.Min(255, (int)(pageHovCol.Y * 1.15f * 255)),
                               (byte)Math.Min(255, (int)(pageHovCol.Z * 1.15f * 255)), 255)
                    : WheelCol(80, 110, 140, 200);

                DrawWheelRingSegment(dl, center, InnerR, OuterR, a0, a1, fillCol, ArcSegs);
                dl.AddLine(
                    center + new Vector2(MathF.Cos(a0) * InnerR, MathF.Sin(a0) * InnerR),
                    center + new Vector2(MathF.Cos(a0) * OuterR, MathF.Sin(a0) * OuterR),
                    borderCol, 1f);

                int        slotIdx = baseSlot + i + 1;
                EmoteInfo? emote   = ResolveSlot(slots, slotIdx);
                var        pos     = center + new Vector2(MathF.Cos(mid) * labelR, MathF.Sin(mid) * labelR);
                uint       txtCol  = hov ? WheelCol(255, 245, 180, 255) : WheelCol(210, 220, 235, 200);
                var        tex     = emote != null ? Plugin.GetIcon(emote.IconId) : null;

                if (tex != null)
                {
                    var iconMin = pos - new Vector2(IconSz / 2f, IconSz / 2f + 4f);
                    dl.AddImage(tex.Handle, iconMin, iconMin + new Vector2(IconSz, IconSz));
                    if (plugin.ActiveCharConfig.ChatLogEmotes?.Contains(emote!.RowId) == true)
                        dl.AddRect(iconMin, iconMin + new Vector2(IconSz, IconSz), WheelCol(255, 165, 50, 230), 0, 0, 1.5f);
                    var lbl = WheelTrunc(emote!.Name, 7);
                    var lsz = ImGui.CalcTextSize(lbl);
                    dl.AddText(pos + new Vector2(-lsz.X / 2f, IconSz / 2f - 2f), txtCol, lbl);
                }
                else if (emote != null)
                {
                    var lbl = WheelTrunc(emote.Name, 7);
                    var lsz = ImGui.CalcTextSize(lbl);
                    dl.AddText(pos - lsz / 2f, txtCol, lbl);
                }
                else
                {
                    var lbl = $"{i + 2}";
                    var lsz = ImGui.CalcTextSize(lbl);
                    dl.AddText(pos - lsz / 2f, WheelCol(80, 100, 120, 130), lbl);
                }
            }

            uint cFill = hovCen ? WheelCol(220, 160, 20, 230) : WheelCol(15, 20, 25, 230);
            dl.AddCircleFilled(center, InnerR, cFill, 32);
            dl.AddCircle(center, InnerR,        WheelCol(80, 110, 140, 200), 32, 1.5f);
            dl.AddCircle(center, OuterR + 6f,   WheelCol(80, 110, 140, 120), 64, 1.5f);

            const float CIconSz    = 14f;
            EmoteInfo? centerEmote = ResolveSlot(slots, baseSlot);

            if (centerEmote != null)
            {
                uint ctxtCol = hovCen ? WheelCol(255, 245, 180, 255) : WheelCol(210, 220, 235, 200);
                var  cTex    = Plugin.GetIcon(centerEmote.IconId);
                if (cTex != null)
                {
                    var iconMin = center - new Vector2(CIconSz / 2f, CIconSz / 2f + 3f);
                    dl.AddImage(cTex.Handle, iconMin, iconMin + new Vector2(CIconSz, CIconSz));
                    if (plugin.ActiveCharConfig.ChatLogEmotes?.Contains(centerEmote.RowId) == true)
                        dl.AddRect(iconMin, iconMin + new Vector2(CIconSz, CIconSz), WheelCol(255, 165, 50, 230), 0, 0, 1.5f);
                    var lbl = WheelTrunc(centerEmote.Name, 5);
                    var lsz = ImGui.CalcTextSize(lbl);
                    dl.AddText(center + new Vector2(-lsz.X / 2f, CIconSz / 2f - 2f), ctxtCol, lbl);
                }
                else
                {
                    var lbl = WheelTrunc(centerEmote.Name, 5);
                    var lsz = ImGui.CalcTextSize(lbl);
                    dl.AddText(center - lsz / 2f, ctxtCol, lbl);
                }
            }
            else
            {
                var lbl = "1";
                var lsz = ImGui.CalcTextSize(lbl);
                dl.AddText(center - lsz / 2f, WheelCol(80, 100, 120, 130), lbl);
            }

            for (int s = 0; s < WheelWindow.EmotesPerPage; s++)
            {
                int     slotIdx   = baseSlot + s;
                Vector2 btnCenter = s == 0
                    ? center
                    : center + new Vector2(
                        MathF.Cos(segOffset + (s - 1) * segStep) * labelR,
                        MathF.Sin(segOffset + (s - 1) * segStep) * labelR);

                ImGui.SetCursorScreenPos(btnCenter - new Vector2(BtnHalf, BtnHalf));
                ImGui.PushID($"seg_{s}");

                EmoteInfo? segEmote = ResolveSlot(slots, slotIdx);
                var        segTex   = segEmote != null ? Plugin.GetIcon(segEmote.IconId) : null;

                bool clicked = ImGui.InvisibleButton("##segbtn", new Vector2(BtnHalf * 2f, BtnHalf * 2f));

                if (ImGui.IsItemHovered())
                {
                    string slotLabel = $"Slot {s + 1}{(s == 0 ? " (center)" : "")}";
                    ImGui.SetTooltip(segEmote != null
                        ? $"{slotLabel}: {segEmote.Name}\nClick to remove · Drag to reorder · Drag outside wheel to remove"
                        : $"{slotLabel}: Empty — drop an emote here");
                }

                if (clicked && slotIdx < slots.Count && slots[slotIdx] != 0)
                {
                    slots[slotIdx] = 0;
                    TrimTrailingZeros(slots);
                    configuration.Save();
                }

                if (segEmote != null && ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
                {
                    _slotDragSrc = slotIdx;
                    ImGui.SetDragDropPayload("WE_SLOT", new byte[] { 1 }, ImGuiCond.None);
                    if (segTex != null) { ImGui.Image(segTex.Handle, new Vector2(32, 32)); ImGui.SameLine(); }
                    ImGui.Text(segEmote.Name);
                    ImGui.EndDragDropSource();
                }

                if (ImGui.BeginDragDropTarget())
                {
                    _ = ImGui.AcceptDragDropPayload("WE_SLOT");
                    _ = ImGui.AcceptDragDropPayload("WE_EMOTE");

                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    {
                        if (_slotDragSrc >= 0 && _slotDragSrc != slotIdx &&
                            _slotDragSrc < slots.Count && slots[_slotDragSrc] != 0)
                        {
                            var emoteId = slots[_slotDragSrc];
                            EnsureSlotCount(slots, slotIdx + 1);
                            slots[_slotDragSrc] = slots[slotIdx]; // 0 if target was empty
                            slots[slotIdx] = emoteId;
                            TrimTrailingZeros(slots);
                            configuration.Save();
                        }
                        else if (_emoteDragId != 0)
                        {
                            uint eid = _emoteDragId;
                            EnsureSlotCount(slots, slotIdx + 1);
                            int existingIdx = slots.IndexOf(eid);
                            if (existingIdx >= 0) slots[existingIdx] = 0; // vacate old position
                            slots[slotIdx] = eid; // overwrite target (old emote there is discarded)
                            TrimTrailingZeros(slots);
                            configuration.Save();
                        }
                        // Always clear drag state when mouse released over any drop target
                        // so that dropping back onto the same slot doesn't trigger removal.
                        _slotDragSrc = -1;
                        _emoteDragId = 0;
                    }

                    ImGui.EndDragDropTarget();
                }

                ImGui.PopID();
            }

            // ── Per-page controls row (centered under the wheel) ───────
            // ── Per-page controls ──────────────────────────────────────
            // Row 1: colour swatches + optional ↺ resets (compact 4-px spacing always fits in CanvasW)
            // Row 2: Clear button (centred, separate line so it never overflows the border)
            const float Compact = 4f;
            var   style_       = ImGui.GetStyle();
            float swatchW_     = ImGui.GetFrameHeight();
            float fpX_         = style_.FramePadding.X;
            float resetBtnW_   = ImGui.CalcTextSize("↺").X + fpX_ * 2f;
            float clearBtnW_   = ImGui.CalcTextSize("Clear").X + fpX_ * 2f;
            bool  hasFillCol_  = charCfg.GetPageColor(page).HasValue;
            bool  hasHovCol_   = charCfg.GetPageHoverColor(page).HasValue;
            bool  hasTextCol_  = charCfg.GetPageTextColor(page).HasValue;

            float rowW1_ = swatchW_
                         + (hasFillCol_  ? Compact + resetBtnW_ : 0f)
                         + Compact + swatchW_
                         + (hasHovCol_   ? Compact + resetBtnW_ : 0f)
                         + Compact + swatchW_
                         + (hasTextCol_  ? Compact + resetBtnW_ : 0f);
            ImGui.SetCursorScreenPos(new Vector2(
                canvasPos.X + (CanvasW - rowW1_) / 2f, canvasPos.Y + CanvasW + 4f));

            // Fill colour swatch
            var fillColVal = charCfg.GetPageColor(page) ?? globalColor;
            if (ImGui.ColorEdit4($"##pagecolor{page}", ref fillColVal,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.NoLabel))
            {
                charCfg.SetPageColor(page, fillColVal);
                configuration.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(hasFillCol_ ? "Fill color — click to edit" : "Set custom fill color for this page");
            if (hasFillCol_)
            {
                ImGui.SameLine(0f, Compact);
                ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.25f, 0.2f, 0.05f, 0.8f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.45f, 0.35f, 0.1f, 1f));
                if (ImGui.SmallButton($"↺##resetpc{page}")) { charCfg.SetPageColor(page, null); configuration.Save(); }
                ImGui.PopStyleColor(2);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset fill to global color");
            }
            ImGui.SameLine(0f, Compact);

            var hovColVal = charCfg.GetPageHoverColor(page) ?? globalHoverColor;
            if (ImGui.ColorEdit4($"##pagehovercolor{page}", ref hovColVal,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.NoLabel))
            {
                charCfg.SetPageHoverColor(page, hovColVal);
                configuration.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(hasHovCol_ ? "Hover color — click to edit" : "Set custom hover color for this page");
            if (hasHovCol_)
            {
                ImGui.SameLine(0f, Compact);
                ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.25f, 0.2f, 0.05f, 0.8f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.45f, 0.35f, 0.1f, 1f));
                if (ImGui.SmallButton($"↺##resethc{page}")) { charCfg.SetPageHoverColor(page, null); configuration.Save(); }
                ImGui.PopStyleColor(2);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset hover to global color");
            }
            ImGui.SameLine(0f, Compact);

            // Text colour swatch
            var txtColVal = charCfg.GetPageTextColor(page) ?? globalTextColor;
            if (ImGui.ColorEdit4($"##pagetextcolor{page}", ref txtColVal,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.NoLabel))
            {
                charCfg.SetPageTextColor(page, txtColVal);
                configuration.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(hasTextCol_ ? "Text color — click to edit" : "Set custom text color for this page");
            if (hasTextCol_)
            {
                ImGui.SameLine(0f, Compact);
                ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.25f, 0.2f, 0.05f, 0.8f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.45f, 0.35f, 0.1f, 1f));
                if (ImGui.SmallButton($"↺##resettc{page}")) { charCfg.SetPageTextColor(page, null); configuration.Save(); }
                ImGui.PopStyleColor(2);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset text to global color");
            }

            // Row 2: Clear button (centred, on its own line)
            float row2Y_ = canvasPos.Y + CanvasW + 4f + swatchW_ + Compact;
            ImGui.SetCursorScreenPos(new Vector2(canvasPos.X + (CanvasW - clearBtnW_) / 2f, row2Y_));
            ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.5f, 0.1f, 0.1f, 0.8f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.15f, 0.15f, 1f));
            if (ImGui.SmallButton($"Clear##clearpage{page}"))
                ImGui.OpenPopup($"##clearpageconfirm{page}");
            ImGui.PopStyleColor(2);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Clear all emotes from this page");

            // Per-page clear confirmation popup
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            if (ImGui.BeginPopupModal($"##clearpageconfirm{page}",
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
            {
                ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1f),
                    $"Clear all emotes from \"{charCfg.GetPageName(page)}\"?");
                ImGui.Text("This cannot be undone.");
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.6f, 0.1f, 0.1f, 0.9f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.15f, 0.15f, 1f));
                if (ImGui.Button("Clear", new Vector2(100, 0)))
                {
                    int pageBase = page * WheelWindow.EmotesPerPage;
                    for (int s = 0; s < WheelWindow.EmotesPerPage && pageBase + s < slots.Count; s++)
                        slots[pageBase + s] = 0;
                    TrimTrailingZeros(slots);
                    configuration.Save();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.PopStyleColor(2);
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(100, 0)))
                    ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }

            float borderBot_ = row2Y_ + swatchW_ + 6f;
            dl.AddRect(
                groupTopPos - new Vector2(6f, 3f),
                new Vector2(groupTopPos.X + CanvasW + 6f, borderBot_),
                WheelCol(70, 95, 120, 90), 6f, 0, 1f);

            ImGui.SetCursorScreenPos(new Vector2(canvasPos.X, borderBot_ - 2f));

            ImGui.EndGroup();
            ImGui.PopID();

            if (col == WheelsPerRow - 1 && page < pageCount - 1)
                ImGui.Dummy(new Vector2(0f, 10f));
        }
    }

    private EmoteInfo? ResolveSlot(List<uint> slots, int idx)
    {
        if (idx < 0 || idx >= slots.Count || slots[idx] == 0) return null;
        var id = slots[idx];
        if (Services.MacroService.IsMacroId(id))
        {
            var (set, slot) = Services.MacroService.DecodeId(id);
            return Plugin.MacroService.GetMacroInfo(set, slot);
        }
        return plugin.EmoteService.GetById(id);
    }

    private static void TrimTrailingZeros(List<uint> slots)
    {
        while (slots.Count > 0 && slots[^1] == 0)
            slots.RemoveAt(slots.Count - 1);
    }

    private void DrawMacroList(List<EmoteInfo> macros, List<uint> slots, int maxSlots)
    {
        const float IconSize = 24f;
        bool canAdd = slots.Count(id => id == 0) > 0 || slots.Count < maxSlots;

        using var child = ImRaii.Child("##macrolist", new Vector2(-1, -1), true);
        if (!child.Success) return;

        if (macros.Count == 0)
        {
            ImGui.TextDisabled("No macros found in this set.");
            return;
        }

        if (!ImGui.BeginTable("##macrogrid", 2, ImGuiTableFlags.None)) return;
        ImGui.TableSetupColumn("##mcol0", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##mcol1", ImGuiTableColumnFlags.WidthStretch);

        foreach (var macro in macros)
        {
            ImGui.TableNextColumn();

            bool inWheel = slots.Contains(macro.RowId);

            var tex = Plugin.GetIcon(macro.IconId);
            if (tex != null)
            {
                ImGui.Image(tex.Handle, new Vector2(IconSize, IconSize));
                ImGui.SameLine();
            }

            if (inWheel)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.9f, 0.4f, 1f));

            bool clicked = ImGui.Selectable($"{macro.Name}##macro{macro.RowId}", inWheel,
                ImGuiSelectableFlags.None, new Vector2(0, IconSize));

            if (inWheel) ImGui.PopStyleColor();

            var (set, slot) = Services.MacroService.DecodeId(macro.RowId);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(inWheel
                    ? $"Click to remove from wheel\n{macro.Category} Macro {slot + 1}"
                    : canAdd
                        ? $"Click to add to wheel\n{macro.Category} Macro {slot + 1}"
                        : $"Wheel is full (max {WheelWindow.MaxPages} pages)");

            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
            {
                _emoteDragId = macro.RowId;
                ImGui.SetDragDropPayload("WE_EMOTE", new byte[] { 1 }, ImGuiCond.None);
                if (tex != null) { ImGui.Image(tex.Handle, new Vector2(24, 24)); ImGui.SameLine(); }
                ImGui.Text(macro.Name);
                ImGui.EndDragDropSource();
            }

            if (clicked)
            {
                if (inWheel)
                {
                    int idx = slots.IndexOf(macro.RowId);
                    if (idx >= 0) { slots[idx] = 0; TrimTrailingZeros(slots); }
                    configuration.Save();
                }
                else if (canAdd)
                {
                    int freeIdx = slots.IndexOf((uint)0);
                    if (freeIdx >= 0) slots[freeIdx] = macro.RowId;
                    else if (slots.Count < maxSlots) slots.Add(macro.RowId);
                    configuration.Save();
                }
            }
        }

        ImGui.EndTable();
    }

    private static void EnsureSlotCount(List<uint> slots, int minCount)
    {
        while (slots.Count < minCount)
            slots.Add(0);
    }

    private static void DrawWheelRingSegment(ImDrawListPtr dl, Vector2 c,
        float r0, float r1, float a0, float a1, uint col, int segs)
    {
        dl.PathClear();
        for (int j = 0; j <= segs; j++)
        {
            float a = a0 + (a1 - a0) * j / segs;
            dl.PathLineTo(new Vector2(c.X + MathF.Cos(a) * r1, c.Y + MathF.Sin(a) * r1));
        }
        for (int j = segs; j >= 0; j--)
        {
            float a = a0 + (a1 - a0) * j / segs;
            dl.PathLineTo(new Vector2(c.X + MathF.Cos(a) * r0, c.Y + MathF.Sin(a) * r0));
        }
        dl.PathFillConvex(col);
    }

    private static uint WheelColV(Vector4 c)
        => WheelCol((byte)(c.X * 255), (byte)(c.Y * 255), (byte)(c.Z * 255), (byte)(c.W * 255));

    private static uint WheelCol(byte r, byte g, byte b, byte a)
        => (uint)((a << 24) | (b << 16) | (g << 8) | r);

    private static string WheelTrunc(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";

    private void DrawKeybindSetter(
        string label,
        ref bool capturing,
        int currentKey,
        bool currentCtrl,
        bool currentAlt,
        bool currentShift,
        Action<int, bool, bool, bool> onSet,
        Action onClear)
    {
        ImGui.Text($"{label}:");
        ImGui.SameLine();

        string buttonText;
        if (capturing)
        {
            bool waitPhase = mouseCaptureReadyAt.HasValue && DateTime.UtcNow < mouseCaptureReadyAt.Value;
            buttonText = waitPhase ? ">>> Wait... <<<" : ">>> Press any key <<<";
        }
        else
        {
            buttonText = FormatKeybind(currentKey, currentCtrl, currentAlt, currentShift);
        }

        ImGui.PushStyleColor(ImGuiCol.Button,        capturing ? new Vector4(0.4f, 0.7f, 0.4f, 1f) : new Vector4(0.2f, 0.2f, 0.2f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, capturing ? new Vector4(0.5f, 0.8f, 0.5f, 1f) : new Vector4(0.3f, 0.3f, 0.3f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  capturing ? new Vector4(0.3f, 0.6f, 0.3f, 1f) : new Vector4(0.15f, 0.15f, 0.15f, 1f));

        if (ImGui.Button($"{buttonText}###{label}_btn", new Vector2(260, 0)))
        {
            capturing = !capturing;
            mouseCaptureReadyAt = capturing ? DateTime.UtcNow.AddMilliseconds(300) : null;
        }

        ImGui.PopStyleColor(3);

        if (!capturing && currentKey != 0)
        {
            ImGui.SameLine();
            if (ImGui.Button($"Clear###{label}_clear"))
                onClear();
        }

        if (!capturing) return;

        var keyState = Plugin.KeyState;
        if (keyState == null) return;

        if (keyState[VirtualKey.ESCAPE])
        {
            capturing = false;
            mouseCaptureReadyAt = null;
            return;
        }

        bool ctrl  = (Plugin.GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool alt   = (Plugin.GetAsyncKeyState(0x12) & 0x8000) != 0;
        bool shift = (Plugin.GetAsyncKeyState(0x10) & 0x8000) != 0;

        // Side mouse buttons (XB1/XB2/Middle) - wait for arm delay so the click that opened capture is not recorded
        if (mouseCaptureReadyAt.HasValue && DateTime.UtcNow >= mouseCaptureReadyAt.Value)
        {
            bool lb  = (Plugin.GetAsyncKeyState((int)VirtualKey.LBUTTON)  & 0x8000) != 0;
            bool rb  = (Plugin.GetAsyncKeyState((int)VirtualKey.RBUTTON)  & 0x8000) != 0;
            bool mb  = (Plugin.GetAsyncKeyState((int)VirtualKey.MBUTTON)  & 0x8000) != 0;
            bool xb1 = (Plugin.GetAsyncKeyState((int)VirtualKey.XBUTTON1) & 0x8000) != 0;
            bool xb2 = (Plugin.GetAsyncKeyState((int)VirtualKey.XBUTTON2) & 0x8000) != 0;
            if (lb || rb || mb || xb1 || xb2)
            {
                var mouseVk = lb ? VirtualKey.LBUTTON : rb ? VirtualKey.RBUTTON
                            : mb ? VirtualKey.MBUTTON : xb1 ? VirtualKey.XBUTTON1 : VirtualKey.XBUTTON2;
                onSet((int)mouseVk, ctrl, alt, shift);
                capturing = false;
                mouseCaptureReadyAt = null;
                return;
            }
        }

        // Keyboard
        foreach (var vk in KeysToCheck)
        {
            if ((Plugin.GetAsyncKeyState((int)vk) & 0x8001) != 0x8001) continue;
            onSet((int)vk, ctrl, alt, shift);
            capturing = false;
            mouseCaptureReadyAt = null;
            break;
        }
    }

    private static string FormatKeybind(int key, bool ctrl, bool alt, bool shift)
    {
        if (key == 0) return "Not set";
        var sb = new System.Text.StringBuilder();
        if (ctrl)  sb.Append("Ctrl+");
        if (alt)   sb.Append("Alt+");
        if (shift) sb.Append("Shift+");
        sb.Append(((VirtualKey)key) switch
        {
            VirtualKey.LBUTTON  => "Left Mouse",
            VirtualKey.RBUTTON  => "Right Mouse",
            VirtualKey.MBUTTON  => "Middle Mouse",
            VirtualKey.XBUTTON1 => "Mouse Button 4",
            VirtualKey.XBUTTON2 => "Mouse Button 5",
            var k               => k.ToString()
        });
        return sb.ToString();
    }
}
