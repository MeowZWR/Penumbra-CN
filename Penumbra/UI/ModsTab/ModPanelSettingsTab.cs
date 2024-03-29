﻿using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.UI.Classes;
using Dalamud.Interface.Components;
using Penumbra.Collections.Manager;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Subclasses;
using Penumbra.Services;

namespace Penumbra.UI.ModsTab;

public class ModPanelSettingsTab : ITab
{
    private readonly Configuration         _config;
    private readonly CommunicatorService   _communicator;
    private readonly CollectionManager     _collectionManager;
    private readonly ModFileSystemSelector _selector;
    private readonly TutorialService       _tutorial;
    private readonly ModManager            _modManager;

    private bool          _inherited;
    private ModSettings   _settings   = null!;
    private ModCollection _collection = null!;
    private bool          _empty;
    private int?          _currentPriority = null;

    public ModPanelSettingsTab(CollectionManager collectionManager, ModManager modManager, ModFileSystemSelector selector,
        TutorialService tutorial, CommunicatorService communicator, Configuration config)
    {
        _collectionManager = collectionManager;
        _communicator      = communicator;
        _modManager        = modManager;
        _selector          = selector;
        _tutorial          = tutorial;
        _config            = config;
    }

    public ReadOnlySpan<byte> Label
        => "模组设置"u8;

    public void DrawHeader()
        => _tutorial.OpenTutorial(BasicTutorialSteps.ModOptions);

    public void Reset()
        => _currentPriority = null;

    public void DrawContent()
    {
        using var child = ImRaii.Child("##settings");
        if (!child)
            return;

        _settings   = _selector.SelectedSettings;
        _collection = _selector.SelectedSettingCollection;
        _inherited  = _collection != _collectionManager.Active.Current;
        _empty      = _settings == ModSettings.Empty;

        DrawInheritedWarning();
        UiHelpers.DefaultLineSpace();
        _communicator.PreSettingsPanelDraw.Invoke(_selector.Selected!.Identifier);
        DrawEnabledInput();
        _tutorial.OpenTutorial(BasicTutorialSteps.EnablingMods);
        ImGui.SameLine();
        DrawPriorityInput();
        _tutorial.OpenTutorial(BasicTutorialSteps.Priority);
        DrawRemoveSettings();

        _communicator.PostEnabledDraw.Invoke(_selector.Selected!.Identifier);

        if (_selector.Selected!.Groups.Count > 0)
        {
            var useDummy = true;
            foreach (var (group, idx) in _selector.Selected!.Groups.WithIndex()
                         .Where(g => g.Value.Type == GroupType.Single && g.Value.Count > _config.SingleGroupRadioMax))
            {
                ImGuiUtil.Dummy(UiHelpers.DefaultSpace, useDummy);
                useDummy = false;
                DrawSingleGroupCombo(group, idx);
            }

            useDummy = true;
            foreach (var (group, idx) in _selector.Selected!.Groups.WithIndex().Where(g => g.Value.IsOption))
            {
                ImGuiUtil.Dummy(UiHelpers.DefaultSpace, useDummy);
                useDummy = false;
                switch (group.Type)
                {
                    case GroupType.Multi:
                        DrawMultiGroup(group, idx);
                        break;
                    case GroupType.Single when group.Count <= _config.SingleGroupRadioMax:
                        DrawSingleGroupRadio(group, idx);
                        break;
                }
            }
        }

        UiHelpers.DefaultLineSpace();
        _communicator.PostSettingsPanelDraw.Invoke(_selector.Selected!.Identifier);
    }

    /// <summary> Draw a big red bar if the current setting is inherited. </summary>
    private void DrawInheritedWarning()
    {
        if (!_inherited)
            return;

        //using var color = ImRaii.PushColor(ImGuiCol.Button, Colors.PressEnterWarningBg);
        var InheritanceBorderColor = ImGui.GetColorU32( ImGuiCol.Border );//不喜欢红色，常见现象没必要这么刺眼。
        using var color = ImRaii.PushColor( ImGuiCol.Border, InheritanceBorderColor );
        var       width = new Vector2(ImGui.GetContentRegionAvail().X, 0);
        if (ImGui.Button($"此模组设置继承自'{_collection.Name}'合集。", width))
            _collectionManager.Editor.SetModInheritance(_collectionManager.Active.Current, _selector.Selected!, false);

        ImGuiUtil.HoverTooltip( "你可以点击这个按钮将当前设置独立到此合集。\n"
          + "你也可以在下面随意修改设置，修改后此模组的设置也会独立到此合集。" );
    }

    /// <summary> Draw a checkbox for the enabled status of the mod. </summary>
    private void DrawEnabledInput()
    {
        var enabled = _settings.Enabled;
        if (!ImGui.Checkbox("启用", ref enabled))
            return;

        _modManager.SetKnown(_selector.Selected!);
        _collectionManager.Editor.SetModState(_collectionManager.Active.Current, _selector.Selected!, enabled);
    }

    /// <summary>
    /// Draw a priority input.
    /// Priority is changed on deactivation of the input box.
    /// </summary>
    private void DrawPriorityInput()
    {
        using var group    = ImRaii.Group();
        var       priority = _currentPriority ?? _settings.Priority;
        ImGui.SetNextItemWidth(50 * UiHelpers.Scale);
        if (ImGui.InputInt("##Priority", ref priority, 0, 0))
            _currentPriority = priority;

        if (ImGui.IsItemDeactivatedAfterEdit() && _currentPriority.HasValue)
        {
            if (_currentPriority != _settings.Priority)
                _collectionManager.Editor.SetModPriority(_collectionManager.Active.Current, _selector.Selected!, _currentPriority.Value);

            _currentPriority = null;
        }

        ImGuiUtil.LabeledHelpMarker( "优先级", "优先级更高的模组文件将优先使用。\n"
          + "如果要用模组A覆盖模组B，则模组A的优先级应高于模组B。" );
    }

    /// <summary>
    /// Draw a button to remove the current settings and inherit them instead
    /// on the top-right corner of the window/tab.
    /// </summary>
    private void DrawRemoveSettings()
    {
        const string text = "继承设置";
        if (_inherited || _empty)
            return;

        var scroll = ImGui.GetScrollMaxY() > 0 ? ImGui.GetStyle().ScrollbarSize : 0;
        ImGui.SameLine(ImGui.GetWindowWidth() - ImGui.CalcTextSize(text).X - ImGui.GetStyle().FramePadding.X * 2 - scroll);
        if (ImGui.Button(text))
            _collectionManager.Editor.SetModInheritance(_collectionManager.Active.Current, _selector.Selected!, true);

        ImGuiUtil.HoverTooltip( "在此合集中移除当前模组的设置，以便它可以从其他合集继承设置。\n"
          + "在继承的合集中如果没有设置这个模组，此模组将被禁用。" );
    }

    /// <summary>
    /// Draw a single group selector as a combo box.
    /// If a description is provided, add a help marker besides it.
    /// </summary>
    private void DrawSingleGroupCombo(IModGroup group, int groupIdx)
    {
        using var id             = ImRaii.PushId(groupIdx);
        var       selectedOption = _empty ? (int)group.DefaultSettings : (int)_settings.Settings[groupIdx];
        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X * 3 / 4);
        using (var combo = ImRaii.Combo(string.Empty, group[selectedOption].Name))
        {
            if (combo)
                for (var idx2 = 0; idx2 < group.Count; ++idx2)
                {
                    id.Push(idx2);
                    var option = group[idx2];
                    if (ImGui.Selectable(option.Name, idx2 == selectedOption))
                        _collectionManager.Editor.SetModSetting(_collectionManager.Active.Current, _selector.Selected!, groupIdx, (uint)idx2);

                    if (option.Description.Length > 0)
                        ImGuiUtil.SelectableHelpMarker(option.Description);

                    id.Pop();
                }
        }

        ImGui.SameLine();
        if (group.Description.Length > 0)
            ImGuiUtil.LabeledHelpMarker(group.Name, group.Description);
        else
            ImGui.TextUnformatted(group.Name);
    }

    // Draw a single group selector as a set of radio buttons.
    // If a description is provided, add a help marker besides it.
    private void DrawSingleGroupRadio(IModGroup group, int groupIdx)
    {
        using var id             = ImRaii.PushId(groupIdx);
        var       selectedOption = _empty ? (int)group.DefaultSettings : (int)_settings.Settings[groupIdx];
        var       minWidth       = Widget.BeginFramedGroup(group.Name, description:group.Description);

        DrawCollapseHandling(group, minWidth, DrawOptions);

        Widget.EndFramedGroup();
        return;

        void DrawOptions()
        {
            for (var idx = 0; idx < group.Count; ++idx)
            {
                using var i      = ImRaii.PushId(idx);
                var       option = group[idx];
                if (ImGui.RadioButton(option.Name, selectedOption == idx))
                    _collectionManager.Editor.SetModSetting(_collectionManager.Active.Current, _selector.Selected!, groupIdx, (uint)idx);

                if (option.Description.Length <= 0)
                    continue;

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(option.Description);
            }
        }
    }


    private void DrawCollapseHandling(IModGroup group, float minWidth, Action draw)
    {
        if (group.Count <= _config.OptionGroupCollapsibleMin)
        {
            draw();
        }
        else
        {
            var collapseId     = ImGui.GetID("Collapse");
            var shown          = ImGui.GetStateStorage().GetBool(collapseId, true);
            var buttonTextShow = $"显示 {group.Count} 个选项";
            var buttonTextHide = $"隐藏 {group.Count} 个选项";
            var buttonWidth = Math.Max(ImGui.CalcTextSize(buttonTextShow).X, ImGui.CalcTextSize(buttonTextHide).X)
              + 2 * ImGui.GetStyle().FramePadding.X;
            minWidth = Math.Max(buttonWidth, minWidth);
            if (shown)
            {
                var pos = ImGui.GetCursorPos();
                ImGui.Dummy(UiHelpers.IconButtonSize);
                using (var _ = ImRaii.Group())
                {
                    draw();
                }


                var width  = Math.Max(ImGui.GetItemRectSize().X, minWidth);
                var endPos = ImGui.GetCursorPos();
                ImGui.SetCursorPos(pos);
                if (ImGui.Button(buttonTextHide, new Vector2(width, 0)))
                    ImGui.GetStateStorage().SetBool(collapseId, !shown);

                ImGui.SetCursorPos(endPos);
            }
            else
            {
                var optionWidth = group.Max(o => ImGui.CalcTextSize(o.Name).X)
                  + ImGui.GetStyle().ItemInnerSpacing.X
                  + ImGui.GetFrameHeight()
                  + ImGui.GetStyle().FramePadding.X;
                var width = Math.Max(optionWidth, minWidth);
                if (ImGui.Button(buttonTextShow, new Vector2(width, 0)))
                    ImGui.GetStateStorage().SetBool(collapseId, !shown);
            }
        }
    }

    /// <summary>
    /// Draw a multi group selector as a bordered set of checkboxes.
    /// If a description is provided, add a help marker in the title.
    /// </summary>
    private void DrawMultiGroup(IModGroup group, int groupIdx)
    {
        using var id       = ImRaii.PushId(groupIdx);
        var       flags    = _empty ? group.DefaultSettings : _settings.Settings[groupIdx];
        var       minWidth = Widget.BeginFramedGroup(group.Name, description: group.Description);

        void DrawOptions()
        {
            for (var idx = 0; idx < group.Count; ++idx)
            {
                using var i       = ImRaii.PushId(idx);
                var       option  = group[idx];
                var       flag    = 1u << idx;
                var       setting = (flags & flag) != 0;

                if (ImGui.Checkbox(option.Name, ref setting))
                {
                    flags = setting ? flags | flag : flags & ~flag;
                    _collectionManager.Editor.SetModSetting(_collectionManager.Active.Current, _selector.Selected!, groupIdx, flags);
                }

                if (option.Description.Length > 0)
                {
                    ImGui.SameLine();
                    ImGuiComponents.HelpMarker(option.Description);
                }
            }
        }

        DrawCollapseHandling(group, minWidth, DrawOptions);

        Widget.EndFramedGroup();
        var label = $"##multi{groupIdx}";
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup($"##multi{groupIdx}");

        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1);
        using var popup = ImRaii.Popup(label);
        if (!popup)
            return;

        ImGui.TextUnformatted(group.Name);
        ImGui.Separator();
        if (ImGui.Selectable("启用全部"))
        {
            flags = group.Count == 32 ? uint.MaxValue : (1u << group.Count) - 1u;
            _collectionManager.Editor.SetModSetting(_collectionManager.Active.Current, _selector.Selected!, groupIdx, flags);
        }

        if (ImGui.Selectable("禁用全部"))
            _collectionManager.Editor.SetModSetting(_collectionManager.Active.Current, _selector.Selected!, groupIdx, 0);
    }
}
