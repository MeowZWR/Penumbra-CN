﻿using Dalamud.Game.ClientState.Objects;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.UI.Classes;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Housing;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Interop.Services;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.UI.ModsTab;
using ModFileSystemSelector = Penumbra.UI.ModsTab.ModFileSystemSelector;
using Penumbra.Collections.Manager;

namespace Penumbra.UI.Tabs;

public class ModsTab : ITab
{
    private readonly ModFileSystemSelector  _selector;
    private readonly ModPanel               _panel;
    private readonly TutorialService        _tutorial;
    private readonly ModManager             _modManager;
    private readonly ActiveCollections      _activeCollections;
    private readonly RedrawService          _redrawService;
    private readonly Configuration          _config;
    private readonly IClientState           _clientState;
    private readonly CollectionSelectHeader _collectionHeader;
    private readonly ITargetManager         _targets;
    private readonly IObjectTable           _objectTable;

    public ModsTab(ModManager modManager, CollectionManager collectionManager, ModFileSystemSelector selector, ModPanel panel,
        TutorialService tutorial, RedrawService redrawService, Configuration config, IClientState clientState,
        CollectionSelectHeader collectionHeader, ITargetManager targets, IObjectTable objectTable)
    {
        _modManager        = modManager;
        _activeCollections = collectionManager.Active;
        _selector          = selector;
        _panel             = panel;
        _tutorial          = tutorial;
        _redrawService     = redrawService;
        _config            = config;
        _clientState       = clientState;
        _collectionHeader  = collectionHeader;
        _targets           = targets;
        _objectTable       = objectTable;
    }

    public bool IsVisible
        => _modManager.Valid;

    public ReadOnlySpan<byte> Label
        => "模组列表"u8;

    public void DrawHeader()
        => _tutorial.OpenTutorial(BasicTutorialSteps.Mods);

    public Mod SelectMod
    {
        set => _selector.SelectByValue(value);
    }

    public void DrawContent()
    {
        try
        {
            _selector.Draw(GetModSelectorSize(_config));
            ImGui.SameLine();
            using var group = ImRaii.Group();
            _collectionHeader.Draw(false);

            using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

            using (var child = ImRaii.Child("##ModsTabMod", new Vector2(-1, _config.HideRedrawBar ? 0 : -ImGui.GetFrameHeight()),
                       true, ImGuiWindowFlags.HorizontalScrollbar))
            {
                style.Pop();
                if (child)
                    _panel.Draw();

                style.Push(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            }

            style.Push(ImGuiStyleVar.FrameRounding, 0);
            DrawRedrawLine();
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Exception thrown during ModPanel Render:\n{e}");
            Penumbra.Log.Error($"{_modManager.Count} Mods\n"
              + $"{_activeCollections.Current.AnonymizedName} Current Collection\n"
              + $"{_activeCollections.Current.Settings.Count} Settings\n"
              + $"{_selector.SortMode.Name} Sort Mode\n"
              + $"{_selector.SelectedLeaf?.Name ?? "NULL"} Selected Leaf\n"
              + $"{_selector.Selected?.Name ?? "NULL"} Selected Mod\n"
              + $"{string.Join(", ", _activeCollections.Current.DirectlyInheritsFrom.Select(c => c.AnonymizedName))} Inheritances\n"
              + $"{_selector.SelectedSettingCollection.AnonymizedName} Collection\n");
        }
    }

    /// <summary> Get the correct size for the mod selector based on current config. </summary>
    public static float GetModSelectorSize(Configuration config)
    {
        var absoluteSize = Math.Clamp(config.ModSelectorAbsoluteSize, Configuration.Constants.MinAbsoluteSize,
            Math.Min(Configuration.Constants.MaxAbsoluteSize, ImGui.GetContentRegionAvail().X - 100));
        var relativeSize = config.ScaleModSelector
            ? Math.Clamp(config.ModSelectorScaledSize, Configuration.Constants.MinScaledSize, Configuration.Constants.MaxScaledSize)
            : 0;
        return !config.ScaleModSelector
            ? absoluteSize
            : Math.Max(absoluteSize, relativeSize * ImGui.GetContentRegionAvail().X / 100);
    }

    private void DrawRedrawLine()
    {
        if (_config.HideRedrawBar)
        {
            _tutorial.SkipTutorial(BasicTutorialSteps.Redrawing);
            return;
        }

        var frameHeight = new Vector2(0, ImGui.GetFrameHeight());
        var frameColor  = ImGui.GetColorU32(ImGuiCol.FrameBg);
        using (var _ = ImRaii.Group())
        {
            using (var font = ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGuiUtil.DrawTextButton(FontAwesomeIcon.InfoCircle.ToIconString(), frameHeight, frameColor);
                ImGui.SameLine();
            }

            ImGuiUtil.DrawTextButton( "重绘:        ", frameHeight, frameColor);
        }

        var hovered = ImGui.IsItemHovered();
        _tutorial.OpenTutorial(BasicTutorialSteps.Redrawing);
        if (hovered)
            ImGui.SetTooltip($"/penumbra redraw'命令支持的修饰符为：\n{TutorialService.SupportedRedrawModifiers}");

        using var id       = ImRaii.PushId("Redraw");
        using var disabled = ImRaii.Disabled(_clientState.LocalPlayer == null);
        ImGui.SameLine();
        var buttonWidth = frameHeight with { X = ImGui.GetContentRegionAvail().X / 5 };
        var tt = _objectTable.GetObjectAddress(0) == nint.Zero
            ? "\n仅当您已登录并且您的角色可用时才能使用。"
            : string.Empty;
        DrawButton(buttonWidth, "全部", string.Empty, tt);
        ImGui.SameLine();
        DrawButton(buttonWidth, "自己", "self", tt);
        ImGui.SameLine();

        tt = _targets.Target == null && _targets.GPoseTarget == null
            ? "\n仅当您有目标时才能使用。"
            : string.Empty;
        DrawButton(buttonWidth, "目标", "target", tt);
        ImGui.SameLine();

        tt = _targets.FocusTarget == null
            ? "\n仅当您有焦点目标时才能使用。"
            : string.Empty;
        DrawButton(buttonWidth, "焦点", "focus", tt);
        ImGui.SameLine();

        tt = !IsIndoors()
            ? "\n目前只能用于室内家具。"
            : string.Empty;
        DrawButton(frameHeight with { X = ImGui.GetContentRegionAvail().X - 1 }, "家具", "furniture", tt);
        return;

        void DrawButton(Vector2 size, string label, string lower, string additionalTooltip)
        {
            using (_ = ImRaii.Disabled(additionalTooltip.Length > 0))
            {
                if (ImGui.Button(label, size))
                {
                    if (lower.Length > 0)
                        _redrawService.RedrawObject(lower, RedrawType.Redraw);
                    else
                        _redrawService.RedrawAll(RedrawType.Redraw);
                }
            }

            ImGuiUtil.HoverTooltip(lower.Length > 0
                ? $"执行命令 '/penumbra redraw {lower}'.{additionalTooltip}"
                : $"执行命令 '/penumbra redraw'.{additionalTooltip}", ImGuiHoveredFlags.AllowWhenDisabled);
        }
    }

    private static unsafe bool IsIndoors()
        => HousingManager.Instance()->IsInside();
}
