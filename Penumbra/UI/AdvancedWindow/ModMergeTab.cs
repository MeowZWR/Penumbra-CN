using ImSharp;
using Luna;
using Penumbra.Mods.Editor;
using Penumbra.Mods.SubMods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public sealed class ModMergeTab(ModMerger modMerger, ModComboWithoutCurrent combo) : IConstructedService
{
    public readonly ModMerger ModMerger   = modMerger;
    private         string    _newModName = string.Empty;

    public void Draw()
    {
        if (ModMerger.MergeFromMod is null)
            return;

        using var tab = Im.TabBar.BeginItem("合并模组"u8);
        if (!tab)
            return;

        using var id = Im.Id.Push(ModMerger.MergeFromMod!.Identifier);
        Im.Dummy(Vector2.Zero);
        var size = 550 * Im.Style.GlobalScale;
        DrawMergeInto(size);
        Im.Line.Same();
        DrawMergeIntoDesc();

        Im.Dummy(Vector2.Zero);
        Im.Separator();
        Im.Dummy(Vector2.Zero);

        DrawSplitOff(size);
        Im.Line.Same();
        DrawSplitOffDesc();

        DrawError();
        DrawWarnings();
    }

    private void DrawMergeInto(float size)
    {
        using var bigGroup     = Im.Group();
        var       minComboSize = 300 * Im.Style.GlobalScale;
        var       textSize     = Im.Font.CalculateSize($"合并 {ModMerger.MergeFromMod!.Name} 到 ").X;

        Im.Cursor.FrameAlign();

        using (Im.Group())
        {
            Im.Text("合并"u8);
            Im.Line.NoSpacing();
            if (size - textSize < minComboSize)
            {
                Im.Text("选择的模组"u8, ColorId.FolderLine.Value());
                Im.Tooltip.OnHover(ModMerger.MergeFromMod!.Name);
            }
            else
            {
                Im.Text(ModMerger.MergeFromMod!.Name, ColorId.FolderLine.Value());
            }

            Im.Line.NoSpacing();
            Im.Text("到"u8);
        }

        Im.Line.Same();
        DrawCombo(size - Im.Item.Size.X - Im.Style.ItemSpacing.X);

        using (Im.Group())
        {
            using var disabled    = Im.Disabled(ModMerger.MergeFromMod.HasOptions);
            var       buttonWidth = (size - Im.Style.ItemSpacing.X) / 2;
            var       group       = ModMerger.MergeToMod?.Groups.FirstOrDefault(g => g.Name == ModMerger.OptionGroupName);
            var color = group is not null || ModMerger.OptionGroupName.Length is 0 && ModMerger.OptionName.Length is 0
                ? Colors.PressEnterWarningBg
                : LunaStyle.DiscordColor;
            using var style = ImStyleBorder.Frame.Push(color);
            Im.Item.SetNextWidth(buttonWidth);
            Im.Input.Text("##optionGroupInput"u8, ref ModMerger.OptionGroupName, "目标选项组"u8);
            Im.Tooltip.OnHover(
                "这是合并到目标模组中现有的或新建的选项组名称。将选项组和选项名称都留空则会将其合并到default option中。\n"u8
              + "红色边框表示现有的选项组，蓝色边框表示新的选项组。"u8);
            Im.Line.Same();


            color = color == LunaStyle.DiscordColor
                ? LunaStyle.DiscordColor
                : group is null || group.Options.Any(o => o.Name == ModMerger.OptionName)
                    ? Colors.PressEnterWarningBg
                    : LunaStyle.DiscordColor;
            style.Push(ImGuiColor.Border, color);
            Im.Item.SetNextWidth(buttonWidth);
            Im.Input.Text("##optionInput"u8, ref ModMerger.OptionName, "目标选项名称"u8);
            Im.Tooltip.OnHover(
                "这是合并到目标模组中现有的或新建的选项名称。将选项组和选项名称都留空则会将其合并到default option中。\n"u8
              + "红色边框表示现有的选项，蓝色边框表示新的选项。"u8);
        }

        if (ModMerger.MergeFromMod.HasOptions)
            Im.Tooltip.OnHover("如果被合并模组没有真正的选项（默认选项或者只有一个单选项都不算），你必须为其在目标模组中分配一个选项。"u8,
                HoveredFlags.AllowWhenDisabled);

        if (ImEx.Button("合并"u8, new Vector2(size, 0),
                ModMerger.CanMerge ? StringU8.Empty : "请选择一个不同于当前模组的目标模组。"u8, !ModMerger.CanMerge))
            ModMerger.Merge();
    }

    private void DrawMergeIntoDesc()
    {
        Im.TextWrapped(ModMerger.MergeFromMod!.HasOptions
            ? "当前选择的模组有选项。\n\n这意味着，所有这些选项都将合并到目标中，如果合并选项时由于重定向已经存在于现有选项中，则所有更改将会被撤销并中断。"u8
            : "当前选择的模组没有真正的选项（默认选项或者只有一个单选项都不算）。\n\n这意味着，你可以选择一个现有的选项或创建新的选项，将其所有更改合并到目标模组中。合并到现有选项失败时，所有更改将会被撤销。"u8);
    }

    private void DrawCombo(float width)
    {
        if (combo.Draw("##ModSelection"u8, ModMerger.MergeToMod?.Name ?? "选择目标模组...", StringU8.Empty, width,
                out var cacheMod))
            ModMerger.MergeToMod = cacheMod.Item;
    }

    private void DrawSplitOff(float size)
    {
        using var group = Im.Group();
        Im.Item.SetNextWidth(size);
        Im.Input.Text("##newModInput"u8, ref _newModName, "新模组名称..."u8);
        Im.Tooltip.OnHover("为新创建的模组命名一个名称，不需要具备唯一性。"u8);
        var tt = _newModName.Length is 0
            ? "请先输入新建模组的名称。"u8
            : ModMerger.SelectedOptions.Count is 0
                ? "请至少选择一个选项进行拆分。"u8
                : StringU8.Empty;
        if (ImEx.Button(
                $"拆分 {ModMerger.SelectedOptions.Count} 个选项###SplitOff",
                new Vector2(size, 0), tt, tt.Length > 0))
            ModMerger.SplitIntoMod(_newModName);

        Im.Dummy(Vector2.One);
        var buttonSize = new Vector2((size - 2 * Im.Style.ItemSpacing.X) / 3, 0);
        if (Im.Button("全选"u8, buttonSize))
            ModMerger.SelectedOptions.UnionWith(ModMerger.MergeFromMod!.AllDataContainers);
        Im.Line.Same();
        if (Im.Button("取消全选"u8, buttonSize))
            ModMerger.SelectedOptions.Clear();
        Im.Line.Same();
        if (Im.Button("反选"u8, buttonSize))
            ModMerger.SelectedOptions.SymmetricExceptWith(ModMerger.MergeFromMod!.AllDataContainers);
        DrawOptionTable(size);
    }

    private static void DrawSplitOffDesc()
    {
        Im.TextWrapped("在这里，你可以创建当前所选模组的副本或部分副本。\n\n"u8
          + "选择你想要复制的选项，输入新模组名称并点击拆分按钮。\n\n"u8
          + "你可以右键点击选项组来选择或取消选择该组中的所有选项，也可以使用表格上方的三个按钮进行快速操作。\n\n"u8
          + "只有选中的文件才会被复制到新的模组中，选项和选项组名称将会在新模组中保留，如果未选择'默认选项'，则新模组的'默认选项'将留空。"u8);
    }

    private void DrawOptionTable(float size)
    {
        var options = ModMerger.MergeFromMod!.AllDataContainers.ToList();
        var height = ModMerger.Warnings.Count is 0 && ModMerger.Error is null
            ? Im.ContentRegion.Available.Y - 3 * Im.Style.FrameHeightWithSpacing
            : 8 * Im.Style.FrameHeightWithSpacing;
        height = Math.Min(height, (options.Count + 1) * Im.Style.FrameHeightWithSpacing);
        var tableSize = new Vector2(size, height);
        using var table = Im.Table.Begin("##options"u8, 6,
            TableFlags.RowBackground
          | TableFlags.SizingFixedFit
          | TableFlags.ScrollY
          | TableFlags.BordersOuterVertical
          | TableFlags.BordersOuterHorizontal,
            tableSize);
        if (!table)
            return;

        table.SetupColumn("##Selected"u8,   TableColumnFlags.WidthFixed, Im.Style.FrameHeight);
        table.SetupColumn("选项"u8,       TableColumnFlags.WidthStretch);
        table.SetupColumn("选项组"u8, TableColumnFlags.WidthFixed, 120 * Im.Style.GlobalScale);
        table.SetupColumn("#文件数"u8,       TableColumnFlags.WidthFixed, 50 * Im.Style.GlobalScale);
        table.SetupColumn("#替换数"u8,       TableColumnFlags.WidthFixed, 50 * Im.Style.GlobalScale);
        table.SetupColumn("#元数据数"u8,      TableColumnFlags.WidthFixed, 50 * Im.Style.GlobalScale);
        table.HeaderRow();
        foreach (var (idx, option) in options.Index())
        {
            using var id       = Im.Id.Push(idx);
            var       selected = ModMerger.SelectedOptions.Contains(option);

            table.NextColumn();
            if (Im.Checkbox("##check"u8, ref selected))
                Handle(option, selected);

            if (option.Group is not { } group)
            {
                table.DrawColumn(option.GetFullName());
                table.NextColumn();
            }
            else
            {
                table.DrawColumn(option.GetName());
                table.NextColumn();
                Im.Selectable(group.Name);
                using var popup = Im.Popup.BeginContextItem("##groupContext"u8);
                if (popup)
                {
                    if (Im.Menu.Item("全选"u8))
                        // ReSharper disable once PossibleMultipleEnumeration
                        foreach (var opt in group.DataContainers)
                            Handle(opt, true);

                    if (Im.Menu.Item("取消全选"u8))
                        // ReSharper disable once PossibleMultipleEnumeration
                        foreach (var opt in group.DataContainers)
                            Handle(opt, false);
                }
            }

            table.NextColumn();
            ImEx.TextRightAligned($"{option.Files.Count}", 3 * Im.Style.GlobalScale);
            table.NextColumn();
            ImEx.TextRightAligned($"{option.FileSwaps.Count}", 3 * Im.Style.GlobalScale);
            table.NextColumn();
            ImEx.TextRightAligned($"{option.Manipulations.Count}", 3 * Im.Style.GlobalScale);
            continue;

            void Handle(IModDataContainer option2, bool selected2)
            {
                if (selected2)
                    ModMerger.SelectedOptions.Add(option2);
                else
                    ModMerger.SelectedOptions.Remove(option2);
            }
        }
    }

    private void DrawWarnings()
    {
        if (ModMerger.Warnings.Count is 0)
            return;

        Im.Separator();
        Im.Dummy(Vector2.One);
        using var color = ImGuiColor.Text.Push(Colors.TutorialBorder);
        foreach (var warning in ModMerger.Warnings.SkipLast(1))
        {
            Im.TextWrapped(warning);
            Im.Separator();
        }

        Im.TextWrapped(ModMerger.Warnings[^1]);
    }

    private void DrawError()
    {
        if (ModMerger.Error == null)
            return;

        Im.Separator();
        Im.Dummy(Vector2.One);
        using var color = ImGuiColor.Text.Push(Colors.RegexWarningBorder);
        Im.TextWrapped($"{ModMerger.Error}");
    }
}
