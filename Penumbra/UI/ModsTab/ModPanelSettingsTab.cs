using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using OtterGui.Services;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.UI.Classes;
using Penumbra.Collections.Manager;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.Mods.Settings;
using Penumbra.UI.ModsTab.Groups;

namespace Penumbra.UI.ModsTab;

public class ModPanelSettingsTab(
    CollectionManager collectionManager,
    ModManager modManager,
    ModFileSystemSelector selector,
    TutorialService tutorial,
    CommunicatorService communicator,
    ModGroupDrawer modGroupDrawer)
    : ITab, IUiService
{
    private bool          _inherited;
    private ModSettings   _settings   = null!;
    private ModCollection _collection = null!;
    private int?          _currentPriority;

    public ReadOnlySpan<byte> Label
        => "模组设置"u8;

    public void DrawHeader()
        => tutorial.OpenTutorial(BasicTutorialSteps.ModOptions);

    public void Reset()
        => _currentPriority = null;

    public void DrawContent()
    {
        using var child = ImRaii.Child("##settings");
        if (!child)
            return;

        _settings   = selector.SelectedSettings;
        _collection = selector.SelectedSettingCollection;
        _inherited  = _collection != collectionManager.Active.Current;
        DrawInheritedWarning();
        UiHelpers.DefaultLineSpace();
        communicator.PreSettingsPanelDraw.Invoke(selector.Selected!.Identifier);
        DrawEnabledInput();
        tutorial.OpenTutorial(BasicTutorialSteps.EnablingMods);
        ImGui.SameLine();
        DrawPriorityInput();
        tutorial.OpenTutorial(BasicTutorialSteps.Priority);
        DrawRemoveSettings();

        communicator.PostEnabledDraw.Invoke(selector.Selected!.Identifier);

        modGroupDrawer.Draw(selector.Selected!, _settings);
        UiHelpers.DefaultLineSpace();
        communicator.PostSettingsPanelDraw.Invoke(selector.Selected!.Identifier);
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
        if (ImGui.Button($"此模组设置继承自合集{_collection.Name}。", width))
            collectionManager.Editor.SetModInheritance(collectionManager.Active.Current, selector.Selected!, false);

        ImGuiUtil.HoverTooltip( "你可以点击这个按钮将当前设置独立到此合集。\n"
          + "你也可以在下面随意修改设置，修改后此模组的设置也会独立到此合集。" );
    }

    /// <summary> Draw a checkbox for the enabled status of the mod. </summary>
    private void DrawEnabledInput()
    {
        var enabled = _settings.Enabled;
        if (!ImGui.Checkbox("启用", ref enabled))
            return;

        modManager.SetKnown(selector.Selected!);
        collectionManager.Editor.SetModState(collectionManager.Active.Current, selector.Selected!, enabled);
    }

    /// <summary>
    /// Draw a priority input.
    /// Priority is changed on deactivation of the input box.
    /// </summary>
    private void DrawPriorityInput()
    {
        using var group    = ImRaii.Group();
        var       priority = _currentPriority ?? _settings.Priority.Value;
        ImGui.SetNextItemWidth(50 * UiHelpers.Scale);
        if (ImGui.InputInt("##Priority", ref priority, 0, 0))
            _currentPriority = priority;

        if (ImGui.IsItemDeactivatedAfterEdit() && _currentPriority.HasValue)
        {
            if (_currentPriority != _settings.Priority.Value)
                collectionManager.Editor.SetModPriority(collectionManager.Active.Current, selector.Selected!,
                    new ModPriority(_currentPriority.Value));

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
        if (_inherited || _settings == ModSettings.Empty)
            return;

        var scroll = ImGui.GetScrollMaxY() > 0 ? ImGui.GetStyle().ScrollbarSize : 0;
        ImGui.SameLine(ImGui.GetWindowWidth() - ImGui.CalcTextSize(text).X - ImGui.GetStyle().FramePadding.X * 2 - scroll);
        if (ImGui.Button(text))
            collectionManager.Editor.SetModInheritance(collectionManager.Active.Current, selector.Selected!, true);

        ImGuiUtil.HoverTooltip("在此合集中移除当前模组的设置，以便它可以从其他合集继承设置。\n"
          + "在继承的合集中如果没有设置这个模组，此模组将被禁用。");
    }
}
