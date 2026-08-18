using Dalamud.Interface;
using ImSharp;
using Luna;
using Penumbra.UI.ModsTab;
using Penumbra.UI.ModsTab.ModPreview;
using Penumbra.UI.ModsTab.Selector;

namespace Penumbra.UI;

public sealed class UiSettings(UiConfig config, IUiBuilder uiBuilder) : IUiService
{
    public void Draw()
    {
        DrawWindowSettings();
        DrawDisplaySettings();
        DrawPreviewImagePanelSettings();
        DrawModSelectorSettings();
        DrawOptionGroupSettings();
        DrawFilterSettings();
    }

    private void DrawWindowSettings()
    {
        using var tree = Im.Tree.Node("Config Window"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("Open Config Window at Game Start"u8,
                "Whether the Penumbra main window should be open or closed after launching the game."u8,
                config.OpenWindowAtStart))
            config.OpenWindowAtStart ^= true;

        if (SettingsTab.Checkbox("Hide Config Window when UI is Hidden"u8,
                "Hide the Penumbra main window when you manually hide the in-game user interface."u8, config.HideUiWhenUiHidden))
        {
            uiBuilder.DisableUserUiHide =  config.HideUiWhenUiHidden;
            config.HideUiWhenUiHidden   ^= true;
        }

        if (SettingsTab.Checkbox("Hide Config Window when in Cutscenes"u8,
                "Hide the Penumbra main window when you are currently watching a cutscene."u8, config.HideUiInCutscenes))
        {
            uiBuilder.DisableCutsceneUiHide =  config.HideUiInCutscenes;
            config.HideUiInCutscenes        ^= true;
        }

        if (SettingsTab.Checkbox("Hide Config Window when in GPose"u8,
                "Hide the Penumbra main window when you are currently in GPose mode."u8, config.HideUiInGPose))
        {
            uiBuilder.DisableGposeUiHide =  config.HideUiInGPose;
            config.HideUiInGPose         ^= true;
        }
        LunaStyle.DrawSeparator();
    }

    private void DrawFilterSettings()
    {
        using var tree = Im.Tree.Node("Filters"u8);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("Remember Mod Filters Across Sessions"u8,
                "Whether filters in the Mods tab should remember their input and start with their respective lists filtered identically to the last session."u8,
                config.RememberModFilters))
            config.RememberModFilters ^= true;
        if (SettingsTab.Checkbox("Remember Collection Filters Across Sessions"u8,
                "Whether filters in the Collections tab should remember their input and start with their respective lists filtered identically to the last session."u8,
                config.RememberCollectionFilters))
            config.RememberCollectionFilters ^= true;
        if (SettingsTab.Checkbox("Remember Changed Items Filters Across Sessions"u8,
                "Whether filters in the Changed Items tab should remember their input and start with their respective lists filtered identically to the last session."u8,
                config.RememberChangedItemFilters))
            config.RememberChangedItemFilters ^= true;
        if (SettingsTab.Checkbox("Remember Effective Changes Filters Across Sessions"u8,
                "Whether filters in the Effective Changes tab should remember their input and start with their respective lists filtered identically to the last session."u8,
                config.RememberEffectiveChangesFilters))
            config.RememberEffectiveChangesFilters ^= true;
        if (SettingsTab.Checkbox("Remember On-Screen Filters Across Sessions"u8,
                "Whether filters in the On-Screen tab should remember their input and start with their respective lists filtered identically to the last session."u8,
                config.RememberOnScreenFilters))
            config.RememberOnScreenFilters ^= true;
        if (SettingsTab.Checkbox("Remember Resource Manager Filters Across Sessions"u8,
                "Whether filters in the Resource Manager tab should remember their input and start with their respective lists filtered identically to the last session."u8,
                config.RememberResourceManagerFilters))
            config.RememberResourceManagerFilters ^= true;
        LunaStyle.DrawSeparator();
    }

    private void DrawDisplaySettings()
    {
        using var tree = Im.Tree.Node("General Display"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("Hide Redraw Bar in Mod Panel"u8, "Hides the lower redraw buttons in the mod panel in your Mods tab."u8,
                config.HideRedrawBar))
            config.HideRedrawBar ^= true;
        if (SettingsTab.Checkbox("Hide Changed Item Filters"u8,
                "Hides the category filter line in the Changed Items tab and the Changed Items mod panel."u8,
                config.HideChangedItemFilters))
            config.HideChangedItemFilters ^= true;

        ChangedItemModeExtensions.DrawCombo("##ChangedItemMode"u8, config.ChangedItemDisplay, UiHelpers.InputTextWidth.X, v =>
        {
            config.ChangedItemDisplay = v;
            config.Save();
        });
        LunaStyle.DrawAlignedHelpMarkerLabel("Mod Changed Item Display"u8,
            "Configure how to display the changed items of a single mod in the mods info panel."u8);
        if (SettingsTab.Checkbox("Omit Machinist Offhands in Changed Items"u8,
                "Omits all Aetherotransformers (machinist offhands) in the changed items tabs because any change on them changes all of them at the moment.\n\n"u8
              + "Changing this triggers a rediscovery of your mods so all changed items can be updated."u8,
                config.HideMachinistOffhandFromChangedItems))
            config.HideMachinistOffhandFromChangedItems ^= true;

        if (SettingsTab.Checkbox("Hide Priority Numbers in Mod Selector"u8,
                "Hides the bracketed non-zero priority numbers displayed in the mod selector when there is enough space for them."u8,
                config.HidePrioritiesInSelector))
            config.HidePrioritiesInSelector ^= true;
        LunaStyle.DrawSeparator();
    }

    private void DrawPreviewImagePanelSettings()
    {
        using var tree = Im.Tree.Node("模组预览面板"u8);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("显示预览面板"u8, "在模组设置面板中显示预览图片面板。"u8, config.ShowModPreviewPanel))
            config.ShowModPreviewPanel ^= true;

        if (!config.ShowModPreviewPanel)
        {
            LunaStyle.DrawSeparator();
            return;
        }

        if (SettingsTab.Checkbox("保存预览面板显示状态"u8, "保存预览面板的展开或隐藏状态。"u8, config.SavePreviewPanelState))
            config.SavePreviewPanelState ^= true;

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##previewRatio"u8, config.PreviewPanelRatio, out var newRatio, "%.2f"u8, 0.1f, 0.5f, 0.01f,
                SliderFlags.AlwaysClamp))
            config.PreviewPanelRatio = newRatio;
        LunaStyle.DrawAlignedHelpMarkerLabel("预览面板比例"u8, "预览面板占总宽度的比例。"u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##previewMinWidth"u8, config.PreviewPanelMinWidth, out var newMinWidth, "%.0f"u8, 100f, 800f, 1f,
                SliderFlags.AlwaysClamp))
            config.PreviewPanelMinWidth = newMinWidth;
        LunaStyle.DrawAlignedHelpMarkerLabel("预览面板最小宽度"u8, "预览面板的最小宽度限制。"u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##previewMaxWidth"u8, config.PreviewPanelMaxWidth, out var newMaxWidth, "%.0f"u8, 200f, 1000f, 1f,
                SliderFlags.AlwaysClamp))
            config.PreviewPanelMaxWidth = newMaxWidth;
        LunaStyle.DrawAlignedHelpMarkerLabel("预览面板最大宽度"u8, "预览面板的最大宽度限制。"u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##previewImageMinWidth"u8, config.PreviewImageMinWidth, out var newImageMinWidth, "%.0f"u8, 100f,
                800f, 1f, SliderFlags.AlwaysClamp))
            config.PreviewImageMinWidth = newImageMinWidth;
        LunaStyle.DrawAlignedHelpMarkerLabel("预览图片最小宽度"u8, "预览图片的最小显示宽度，影响图片的排列方式。"u8);

        var maxMemoryMB = (int)(config.PreviewPanelMaxMemory / (1024L * 1024L));
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##previewMaxMemory"u8, maxMemoryMB, out maxMemoryMB, 128, 2048, 1f))
            config.PreviewPanelMaxMemory = maxMemoryMB * 1024L * 1024L;
        LunaStyle.DrawAlignedHelpMarkerLabel("图片缓存最大使用内存 (MB)"u8, "预览面板缓存图片可使用的最大内存量。"u8);

        var currentMemoryUsage = ModPreviewImagePanel.GetCurrentMemoryUsage();
        if (currentMemoryUsage > 0)
        {
            Im.Line.Same();
            Im.TextDisabled($"(当前使用: {currentMemoryUsage / (1024 * 1024):F1} MB)");
        }

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##previewSpacing"u8, config.PreviewPanelImageSpacing, out var newSpacing, "%.1f"u8, 0f, 10f, 0.1f,
                SliderFlags.AlwaysClamp))
            config.PreviewPanelImageSpacing = newSpacing;
        LunaStyle.DrawAlignedHelpMarkerLabel("图片间距"u8, "预览面板中图片之间的垂直间距。"u8);
        LunaStyle.DrawSeparator();
    }

    private void DrawModSelectorSettings()
    {
        using var tree = Im.Tree.Node("Mod Selector Display"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        DrawFolderSortType();
        DrawRenameSettings();
        if (SettingsTab.Checkbox("Open Folders by Default"u8, "Whether to start with all folders collapsed or expanded in the mod selector."u8,
                config.OpenFoldersByDefault))
            config.OpenFoldersByDefault ^= true;
        LunaStyle.DrawSeparator();
    }

    /// <summary> Different supported sort modes as a combo. </summary>
    private void DrawFolderSortType()
    {
        if (SortModeCombo.DrawCombo(ISortMode.Valid.Values, "##sortMode"u8, config.SortMode, out var newSortMode, false,
                UiHelpers.InputTextWidth.X))
            config.SortMode = newSortMode!;

        LunaStyle.DrawAlignedHelpMarkerLabel("Sort Mode"u8, "Choose the sort mode for the mod selector in the mods tab."u8);
    }

    private void DrawRenameSettings()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        using (var combo = Im.Combo.Begin("##renameSettings"u8, config.ShowRename.ToNameU8()))
        {
            if (combo)
                foreach (var value in RenameField.Values)
                {
                    if (Im.Selectable(value.ToNameU8(), config.ShowRename == value))
                        config.ShowRename = value;

                    Im.Tooltip.OnHover(value.Tooltip());
                }
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Rename Fields in Mod Context Menu"u8,
            "Select which of the two renaming input fields are visible when opening the right-click context menu of a mod in the mod selector."u8);
    }

    private void DrawOptionGroupSettings()
    {
        using var tree = Im.Tree.Node("Mod Configuration Display"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("Draw Tabs for Option Pages"u8,
                "When this is on, pages set for options in a mod's metadata are drawn as a tab bar. When it is off, pages are drawn successively on the same page using sections of collapsing headers."u8,
                config.DisplayPages))
            config.DisplayPages ^= true;

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##groupLine"u8, config.ModSettingLineScale,
                out var newLine, "%.2f"u8, 0, 4, 0.005f, SliderFlags.AlwaysClamp))
            config.ModSettingLineScale = newLine;
        LunaStyle.DrawAlignedHelpMarkerLabel("Group Settings Line Factor"u8,
            "The thickness of the tree line connecting group settings."u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##groupBorder"u8, config.ModSettingBorderScale,
                out var newBorder, "%.2f"u8, 1, 4, 0.005f, SliderFlags.AlwaysClamp))
            config.ModSettingBorderScale = newBorder;
        LunaStyle.DrawAlignedHelpMarkerLabel("Group Settings Border Factor"u8,
            "The thickness of the border around UI elements connected by the tree line in group settings."u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##vertSpace"u8, config.ModSettingItemSpacingFactor,
                out var newFactor, "%.2f"u8, 0, 10, 0.01f, SliderFlags.AlwaysClamp))
            config.ModSettingItemSpacingFactor = newFactor;
        LunaStyle.DrawAlignedHelpMarkerLabel("Vertical Spacing between Option Groups Factor"u8,
            "An additional factor applied to your regular ImGui style's item spacing in the vertical direction between the nodes in your mod settings tab.\n\n"u8
          + "A value of 1 means that the normal item spacing is used."u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##groupAlign"u8, config.ModSettingLabelAlignment,
                out var newAlignment, "%.2f"u8, 0, 1, 0.0005f, SliderFlags.AlwaysClamp))
            config.ModSettingLabelAlignment = newAlignment;
        LunaStyle.DrawAlignedHelpMarkerLabel("Group Label Text Alignment"u8,
            "The alignment of the text in group labels. A value of 0 means the text is left-aligned, and a value of 1 means it is right-aligned. "u8
          + "The caret is always left-aligned, and the tooltip icon is always right-aligned."u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##comboAlign"u8, config.ModSettingComboAlignment,
                out var newCombo, "%.2f"u8, 0, 1, 0.0005f, SliderFlags.AlwaysClamp))
            config.ModSettingComboAlignment = newCombo;
        LunaStyle.DrawAlignedHelpMarkerLabel("Setting Combo Preview Text Alignment"u8,
            "The alignment of the preview text in single select combos. A value of 0 means the text is left-aligned, and a value of 1 means it is right-aligned. "u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##groupHomo"u8, config.ModSettingMaximumExtendLabelWidth,
                out var newExtend, "%.0f"u8, -1))
            config.ModSettingMaximumExtendLabelWidth = newExtend;
        LunaStyle.DrawAlignedHelpMarkerLabel("Maximum Group Label Homogenization"u8,
            "The maximum width in unscaled pixels that group labels are extended in the settings screen. "u8
          + "Labels are sized according to the largest group label available, up to this value. "u8
          + "If a group label requires more space than this, it is an outlier and other labels are not extended to its width."u8);

        DrawSingleSelectRadioMax();
    }

    /// <summary> Draw a selection for the maximum number of single select options displayed as a radio toggle. </summary>
    private void DrawSingleSelectRadioMax()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##SingleSelectRadioMax"u8, config.SingleGroupRadioMax, out var newValue, 1, null, 0.01f,
                SliderFlags.AlwaysClamp))
        {
            config.SingleGroupRadioMax = newValue;
            config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Upper Limit for Single-Selection Group Radio Buttons"u8,
            "All Single-Selection Groups with more options than specified here will be displayed as Combo-Boxes at the top.\n"u8
          + "All other Single-Selection Groups will be displayed as a set of Radio-Buttons."u8);
    }
}
