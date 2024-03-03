using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Subclasses;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public class ModMergeTab(ModMerger modMerger)
{
    private readonly ModCombo _modCombo   = new(() => modMerger.ModsWithoutCurrent.ToList());
    private          string   _newModName = string.Empty;

    public void Draw()
    {
        if (modMerger.MergeFromMod == null)
            return;

        using var tab = ImRaii.TabItem("合并模组");
        if (!tab)
            return;

        ImGui.Dummy(Vector2.One);
        var size = 550 * ImGuiHelpers.GlobalScale;
        DrawMergeInto(size);
        ImGui.SameLine();
        DrawMergeIntoDesc();

        ImGui.Dummy(Vector2.One);
        ImGui.Separator();
        ImGui.Dummy(Vector2.One);

        DrawSplitOff(size);
        ImGui.SameLine();
        DrawSplitOffDesc();


        DrawError();
        DrawWarnings();
    }

    private void DrawMergeInto(float size)
    {
        using var bigGroup = ImRaii.Group();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"合并模组 {modMerger.MergeFromMod!.Name} 到 ");
        ImGui.SameLine();
        DrawCombo(size - ImGui.GetItemRectSize().X - ImGui.GetStyle().ItemSpacing.X);

        var width = ImGui.GetItemRectSize();
        using (var g = ImRaii.Group())
        {
            using var disabled    = ImRaii.Disabled(modMerger.MergeFromMod.HasOptions);
            var       buttonWidth = (size - ImGui.GetStyle().ItemSpacing.X) / 2;
            using var style       = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1);
            var       group       = modMerger.MergeToMod?.Groups.FirstOrDefault(g => g.Name == modMerger.OptionGroupName);
            var color = group != null || modMerger.OptionGroupName.Length == 0 && modMerger.OptionName.Length == 0
                ? Colors.PressEnterWarningBg
                : Colors.DiscordColor;
            using var c = ImRaii.PushColor(ImGuiCol.Border, color);
            ImGui.SetNextItemWidth(buttonWidth);
            ImGui.InputTextWithHint("##optionGroupInput", "目标选项组", ref modMerger.OptionGroupName, 64);
            ImGuiUtil.HoverTooltip(
                "这是合并到目标模组中现有的或新建的选项组名称。将选项组和选项名称都留空则会将其合并到default option中。\n"
              + "红色边框表示现有的选项组，蓝色边框表示新的选项组。");
            ImGui.SameLine();


            color = color == Colors.DiscordColor
                ? Colors.DiscordColor
                : group == null || group.Any(o => o.Name == modMerger.OptionName)
                    ? Colors.PressEnterWarningBg
                    : Colors.DiscordColor;
            c.Push(ImGuiCol.Border, color);
            ImGui.SetNextItemWidth(buttonWidth);
            ImGui.InputTextWithHint("##optionInput", "目标选项名称", ref modMerger.OptionName, 64);
            ImGuiUtil.HoverTooltip(
                "这是合并到目标模组中现有的或新建的选项名称。将选项组和选项名称都留空则会将其合并到default option中。\n"
              + "红色边框表示现有的选项组，蓝色边框表示新的选项组。");
        }

        if (modMerger.MergeFromMod.HasOptions)
            ImGuiUtil.HoverTooltip( "如果被合并模组没有真正的选项（默认选项或者只有一个单选项都不算），你必须为其在目标模组中分配一个选项。",
                ImGuiHoveredFlags.AllowWhenDisabled);

        if (ImGuiUtil.DrawDisabledButton("Merge", new Vector2(size, 0),
                modMerger.CanMerge ? string.Empty : "Please select a target mod different from the current mod.", !modMerger.CanMerge))
            modMerger.Merge();
    }

    private void DrawMergeIntoDesc()
    {
        ImGuiUtil.TextWrapped(modMerger.MergeFromMod!.HasOptions
            ? "当前被合并的模组拥有选项。\n\n这意味着，所有这些选项都将合并到目标中，如果目标模组中已存在相同选项且修改了相同的重定向路径，则会中断合并进程撤销所有更改。"
            : "当前被合并的模组没有真正的选项（默认选项或者只有一个单选项都不算）。\n\n这意味着，你可以选择一个现有的选项或创建新的选项，将其所有更改合并到目标模组中。合并到现有选项失败时，所有更改将会被撤销。" );
    }

    private void DrawCombo(float width)
    {
        _modCombo.Draw("##ModSelection", _modCombo.CurrentSelection?.Name.Text ?? "Select the target Mod...", string.Empty, width,
            ImGui.GetTextLineHeight());
        modMerger.MergeToMod = _modCombo.CurrentSelection;
    }

    private void DrawSplitOff(float size)
    {
        using var group = ImRaii.Group();
        ImGui.SetNextItemWidth(size);
        ImGui.InputTextWithHint("##newModInput", "新模组名称...", ref _newModName, 64);
        ImGuiUtil.HoverTooltip("为新创建的模组命名一个名称，不需要具备唯一性。");
        var tt = _newModName.Length == 0
            ? "请先输入新建模组的名称。"
            : modMerger.SelectedOptions.Count == 0
                ? "请至少选择一个选项进行拆分。"
                : string.Empty;
        var buttonText =
            $"拆分 {modMerger.SelectedOptions.Count} 个选项{(modMerger.SelectedOptions.Count > 1 ? "s" : string.Empty)}###SplitOff";
        if (ImGuiUtil.DrawDisabledButton(buttonText, new Vector2(size, 0), tt, tt.Length > 0))
            modMerger.SplitIntoMod(_newModName);

        ImGui.Dummy(Vector2.One);
        var buttonSize = new Vector2((size - 2 * ImGui.GetStyle().ItemSpacing.X) / 3, 0);
        if (ImGui.Button("全选", buttonSize))
            modMerger.SelectedOptions.UnionWith(modMerger.MergeFromMod!.AllSubMods);
        ImGui.SameLine();
        if (ImGui.Button("取消全选", buttonSize))
            modMerger.SelectedOptions.Clear();
        ImGui.SameLine();
        if (ImGui.Button("反选", buttonSize))
            modMerger.SelectedOptions.SymmetricExceptWith(modMerger.MergeFromMod!.AllSubMods);
        DrawOptionTable(size);
    }

    private void DrawSplitOffDesc()
    {
        ImGuiUtil.TextWrapped("在这里，你可以选择创建当前所选模组的副本或部分副本。\n\n"
          + "选择你需要复制的选项，输入新模组名称并点击拆分按钮。\n\n"
          + "右键点击选项组可以全选或取消全选此组里的选项，也可以使用表格上方的三个按钮进行快速操作。\n\n"
          + "只有选择的文件才会被复制到新的模组中，选项和选项组名称将会在新模组中保留，如果未选择'默认选项'，则新模组的'默认选项'将留空。");
    }

    private void DrawOptionTable(float size)
    {
        var options = modMerger.MergeFromMod!.AllSubMods.ToList();
        var height = modMerger.Warnings.Count == 0 && modMerger.Error == null
            ? ImGui.GetContentRegionAvail().Y - 3 * ImGui.GetFrameHeightWithSpacing()
            : 8 * ImGui.GetFrameHeightWithSpacing();
        height = Math.Min(height, (options.Count + 1) * ImGui.GetFrameHeightWithSpacing());
        var tableSize = new Vector2(size, height);
        using var table = ImRaii.Table("##options", 6,
            ImGuiTableFlags.RowBg
          | ImGuiTableFlags.SizingFixedFit
          | ImGuiTableFlags.ScrollY
          | ImGuiTableFlags.BordersOuterV
          | ImGuiTableFlags.BordersOuterH,
            tableSize);
        if (!table)
            return;

        ImGui.TableSetupColumn("##Selected",   ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("选项",       ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("选项组", ImGuiTableColumnFlags.WidthFixed, 120 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("文件数",       ImGuiTableColumnFlags.WidthFixed, 50 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("替换数",       ImGuiTableColumnFlags.WidthFixed, 50 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("元数据数",      ImGuiTableColumnFlags.WidthFixed, 50 * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();
        foreach (var (option, idx) in options.WithIndex())
        {
            using var id       = ImRaii.PushId(idx);
            var       selected = modMerger.SelectedOptions.Contains(option);

            ImGui.TableNextColumn();
            if (ImGui.Checkbox("##check", ref selected))
                Handle(option, selected);

            if (option.IsDefault)
            {
                ImGuiUtil.DrawTableColumn(option.FullName);
                ImGui.TableNextColumn();
            }
            else
            {
                ImGuiUtil.DrawTableColumn(option.Name);
                var group = option.ParentMod.Groups[option.GroupIdx];
                ImGui.TableNextColumn();
                ImGui.Selectable(group.Name, false);
                if (ImGui.BeginPopupContextItem("##groupContext"))
                {
                    if (ImGui.MenuItem("全选"))
                        foreach (var opt in group)
                            Handle((SubMod)opt, true);

                    if (ImGui.MenuItem("取消全选"))
                        foreach (var opt in group)
                            Handle((SubMod)opt, false);
                    ImGui.EndPopup();
                }
            }

            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(option.FileData.Count.ToString(), 3 * ImGuiHelpers.GlobalScale);
            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(option.FileSwapData.Count.ToString(), 3 * ImGuiHelpers.GlobalScale);
            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(option.Manipulations.Count.ToString(), 3 * ImGuiHelpers.GlobalScale);
            continue;

            void Handle(SubMod option2, bool selected2)
            {
                if (selected2)
                    modMerger.SelectedOptions.Add(option2);
                else
                    modMerger.SelectedOptions.Remove(option2);
            }
        }
    }

    private void DrawWarnings()
    {
        if (modMerger.Warnings.Count == 0)
            return;

        ImGui.Separator();
        ImGui.Dummy(Vector2.One);
        using var color = ImRaii.PushColor(ImGuiCol.Text, Colors.TutorialBorder);
        foreach (var warning in modMerger.Warnings.SkipLast(1))
        {
            ImGuiUtil.TextWrapped(warning);
            ImGui.Separator();
        }

        ImGuiUtil.TextWrapped(modMerger.Warnings[^1]);
    }

    private void DrawError()
    {
        if (modMerger.Error == null)
            return;

        ImGui.Separator();
        ImGui.Dummy(Vector2.One);
        using var color = ImRaii.PushColor(ImGuiCol.Text, Colors.RegexWarningBorder);
        ImGuiUtil.TextWrapped(modMerger.Error.ToString());
    }
}
