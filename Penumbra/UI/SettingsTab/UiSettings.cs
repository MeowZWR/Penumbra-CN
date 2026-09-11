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
        using var tree = Im.Tree.Node("设置窗口"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("游戏启动时自动开启设置窗口"u8,
                "游戏启动后，Penumbra主窗口应该打开还是关闭。"u8,
                config.OpenWindowAtStart))
            config.OpenWindowAtStart ^= true;

        if (SettingsTab.Checkbox("隐藏游戏UI时，隐藏设置窗口"u8,
                "手动隐藏游戏UI时，隐藏Penumbra的主窗口。"u8, config.HideUiWhenUiHidden))
        {
            uiBuilder.DisableUserUiHide =  config.HideUiWhenUiHidden;
            config.HideUiWhenUiHidden   ^= true;
        }

        if (SettingsTab.Checkbox("进入过场动画时，隐藏设置窗口"u8,
                "在观看过场动画时，隐藏Penumbra的主窗口。"u8, config.HideUiInCutscenes))
        {
            uiBuilder.DisableCutsceneUiHide =  config.HideUiInCutscenes;
            config.HideUiInCutscenes        ^= true;
        }

        if (SettingsTab.Checkbox("进入集体动作(GPose)模式时，隐藏设置窗口"u8,
                "进入集体动作模式时，隐藏Penumbra主窗口。"u8, config.HideUiInGPose))
        {
            uiBuilder.DisableGposeUiHide =  config.HideUiInGPose;
            config.HideUiInGPose         ^= true;
        }

        LunaStyle.DrawSeparator();
    }

    private void DrawFilterSettings()
    {
        using var tree = Im.Tree.Node("筛选"u8);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("跨会话保留模组筛选"u8,
                "是否让“模组列表”选项卡中的过滤器记住输入内容，并在下次启动时保持与上次相同的过滤列表。"u8,
                config.RememberModFilters))
            config.RememberModFilters ^= true;
        if (SettingsTab.Checkbox("跨会话保留合集筛选"u8,
                "是否让“合集设置”选项卡中的过滤器记住输入内容，并在下次启动时保持与上次相同的过滤列表。"u8,
                config.RememberCollectionFilters))
            config.RememberCollectionFilters ^= true;
        if (SettingsTab.Checkbox("跨会话保留更改项目筛选"u8,
                "是否让“更改项目”选项卡中的过滤器记住输入内容，并在下次启动时保持与上次相同的过滤列表。"u8,
                config.RememberChangedItemFilters))
            config.RememberChangedItemFilters ^= true;
        if (SettingsTab.Checkbox("跨会话保留有效更改筛选"u8,
                "是否让“有效更改”选项卡中的过滤器记住输入内容，并在下次启动时保持与上次相同的过滤列表。"u8,
                config.RememberEffectiveChangesFilters))
            config.RememberEffectiveChangesFilters ^= true;
        if (SettingsTab.Checkbox("跨会话保留屏幕角色筛选"u8,
                "是否让“屏幕角色”选项卡中的过滤器记住输入内容，并在下次启动时保持与上次相同的过滤列表。"u8,
                config.RememberOnScreenFilters))
            config.RememberOnScreenFilters ^= true;
        if (SettingsTab.Checkbox("跨会话保留资源管理器筛选"u8,
                "是否让“资源管理器”选项卡中的过滤器记住输入内容，并在下次启动时保持与上次相同的过滤列表。"u8,
                config.RememberResourceManagerFilters))
            config.RememberResourceManagerFilters ^= true;
        LunaStyle.DrawSeparator();
    }

    private void DrawDisplaySettings()
    {
        using var tree = Im.Tree.Node("常规显示"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("在模组界面中隐藏预设栏"u8,
                "隐藏模组选项卡下模组界面顶部用于预设应用和设置导入/导出的一行按钮。"u8,
                config.HidePresetBar))
            config.HidePresetBar ^= true;

        if (SettingsTab.Checkbox("在模组界面中隐藏重绘栏"u8, "隐藏模组选项卡下模组界面底部的重绘栏。"u8,
                config.HideRedrawBar))
            config.HideRedrawBar ^= true;
        if (SettingsTab.Checkbox("隐藏更改项目筛选栏"u8,
                "隐藏在更改项目（包括模组面板里的更改项目）选项卡中的一行筛选栏。"u8,
                config.HideChangedItemFilters))
            config.HideChangedItemFilters ^= true;

        ChangedItemModeExtensions.DrawCombo("##ChangedItemMode"u8, config.ChangedItemDisplay, UiHelpers.InputTextWidth.X, v =>
        {
            config.ChangedItemDisplay = v;
            config.Save();
        });
        LunaStyle.DrawAlignedHelpMarkerLabel("模组更改项目显示模式"u8,
            "配置如何在模组信息面板中显示单个模组的更改项目。"u8);
        if (SettingsTab.Checkbox("在更改项目中忽略机工副手"u8,
                "在更改项目标签中忽略所有以太转换器（机工副手），因为对它们的任何更改都会同时更改所有这些项目。\n\n"u8
              + "更改此选项会重新扫描您的模组，以便更新所有已更改的项目。"u8,
                config.HideMachinistOffhandFromChangedItems))
            config.HideMachinistOffhandFromChangedItems ^= true;

        if (SettingsTab.Checkbox("隐藏模组选择器优先级数字标识"u8,
                "如果模组选择器里的模组优先级不是0，而且有足够的空间显示，则在模组名称后添加优先级数字标识。勾选此选项后隐藏这个标识。"u8,
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
        using var tree = Im.Tree.Node("模组选择器显示参数"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        DrawFolderSortType();
        DrawRenameSettings();
        if (SettingsTab.Checkbox("默认展开折叠组"u8, "打开模组选择器时，默认展开全部折叠组，否则最小化全部折叠组。"u8,
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

        LunaStyle.DrawAlignedHelpMarkerLabel("模组排序方式"u8, "选择模组选项卡中模组选择器的默认排序方式。"u8);
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

        LunaStyle.DrawAlignedHelpMarkerLabel("模组上下文菜单中的重命名字段"u8,
            "选择在模组选择器中打开模组右键上下文菜单时可见的两个重命名输入字段中的哪一个。"u8);
    }

    private void DrawOptionGroupSettings()
    {
        using var tree = Im.Tree.Node("模组设置显示参数"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("选项页使用标签栏显示"u8,
                "启用后，模组元数据中为选项设置的页面将以标签栏显示。禁用后，页面将以可折叠标题分段的形式依次显示在同一页上。"u8,
                config.DisplayPages))
            config.DisplayPages ^= true;

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##groupLine"u8, config.ModSettingLineScale,
                out var newLine, "%.2f"u8, 0, 4, 0.005f, SliderFlags.AlwaysClamp))
            config.ModSettingLineScale = newLine;
        LunaStyle.DrawAlignedHelpMarkerLabel("选项组设置连线粗细系数"u8,
            "连接选项组设置的树状线粗细。"u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##groupBorder"u8, config.ModSettingBorderScale,
                out var newBorder, "%.2f"u8, 1, 4, 0.005f, SliderFlags.AlwaysClamp))
            config.ModSettingBorderScale = newBorder;
        LunaStyle.DrawAlignedHelpMarkerLabel("选项组设置边框粗细系数"u8,
            "选项组设置中，由树状线连接的界面元素的边框粗细。"u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##vertSpace"u8, config.ModSettingItemSpacingFactor,
                out var newFactor, "%.2f"u8, 0, 10, 0.01f, SliderFlags.AlwaysClamp))
            config.ModSettingItemSpacingFactor = newFactor;
        LunaStyle.DrawAlignedHelpMarkerLabel("选项组之间垂直间距系数"u8,
            "应用于模组设置选项卡中各节点之间垂直方向项目间距的额外系数。\n\n"u8
          + "值为 1 表示使用正常的项目间距。"u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##groupAlign"u8, config.ModSettingLabelAlignment,
                out var newAlignment, "%.2f"u8, 0, 1, 0.0005f, SliderFlags.AlwaysClamp))
            config.ModSettingLabelAlignment = newAlignment;
        LunaStyle.DrawAlignedHelpMarkerLabel("选项组标签文本对齐"u8,
            "选项组标签中文本的对齐方式。值为 0 表示左对齐，值为 1 表示右对齐。"u8
          + "折叠箭头始终左对齐，提示图标始终右对齐。"u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##comboAlign"u8, config.ModSettingComboAlignment,
                out var newCombo, "%.2f"u8, 0, 1, 0.0005f, SliderFlags.AlwaysClamp))
            config.ModSettingComboAlignment = newCombo;
        LunaStyle.DrawAlignedHelpMarkerLabel("设置下拉预览文本对齐"u8,
            "单选项下拉菜单中预览文本的对齐方式。值为 0 表示左对齐，值为 1 表示右对齐。"u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##groupWidth"u8, config.ModSettingMaximumLabelWidth,
                out var newLabelWidth, "%.0f"u8, 50, 2000, 1f, SliderFlags.AlwaysClamp))
            config.ModSettingMaximumLabelWidth = newLabelWidth;
        LunaStyle.DrawAlignedHelpMarkerLabel("Maximum Group Label Width"u8,
            "The maximum width in unscaled pixels that group label are allowed to use."u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##groupHomo"u8, config.ModSettingMaximumExtendLabelWidth,
                out var newExtend, "%.0f"u8, -1, 2000, 1f, SliderFlags.AlwaysClamp))
            config.ModSettingMaximumExtendLabelWidth = newExtend;
        LunaStyle.DrawAlignedHelpMarkerLabel("选项组标签最大齐宽"u8,
            "设置界面中选项组标签扩展的最大未缩放像素宽度。"u8
          + "标签宽度会按最大的组标签对齐，但不超过此值。"u8
          + "如果某个组标签所需空间超过此值，则视为异常值，其他标签不会扩展到该宽度。"u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##comboWidth"u8, config.ModSettingMaximumComboWidth,
                out var newComboWidth, "%.0f"u8, 50, 2000, 1f, SliderFlags.AlwaysClamp))
            config.ModSettingMaximumComboWidth = newComboWidth;
        LunaStyle.DrawAlignedHelpMarkerLabel("Maximum Option Combo Preview Width"u8,
            "The maximum width in unscaled pixels that option previews are allowed to use."u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##comboHomo"u8, config.ModSettingMaximumExtendComboWidth, out var newComboHomo, "%.0f"u8, -1, 2000, 1f, SliderFlags.AlwaysClamp))
            config.ModSettingMaximumExtendComboWidth = newComboHomo;
        LunaStyle.DrawAlignedHelpMarkerLabel("选项下拉预览最大齐宽"u8,
            "设置界面中单选项组下拉预览扩展的最大未缩放像素宽度。"u8
          + "下拉预览宽度会按所有下拉菜单中最长的选项名称对齐，但不超过此值。"u8
          + "如果某个下拉菜单的选项名称所需空间超过此值，则视为异常值，其他下拉菜单不会扩展到该宽度。"u8);

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##comboMin"u8, config.ModSettingMinimumComboWidth, out var newComboMin, "%.0f"u8, 50, 500, 1f, SliderFlags.AlwaysClamp))
            config.ModSettingMinimumComboWidth = newComboMin;
        LunaStyle.DrawAlignedHelpMarkerLabel("选项下拉预览最小宽度"u8,
            "单选项组下拉预览使用的最小宽度，与选项名称长度无关。"u8);

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

        LunaStyle.DrawAlignedHelpMarkerLabel("单选项组单选项显示上限"u8,
            "如果单选项组的选项数量等于或多于此处设定的值，将收起变更为下拉菜单。\n"u8
          + "少于此值的单选项组仍会展开显示。"u8);
    }
}
