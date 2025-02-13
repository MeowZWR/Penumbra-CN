using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.Mods.Groups;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab.Groups;

public readonly struct CombiningModGroupEditDrawer(ModGroupEditDrawer editor, CombiningModGroup group) : IModGroupEditDrawer
{
    public void Draw()
    {
        foreach (var (option, optionIdx) in group.OptionData.WithIndex())
        {
            using var id = ImUtf8.PushId(optionIdx);
            editor.DrawOptionPosition(group, option, optionIdx);

            ImUtf8.SameLineInner();
            editor.DrawOptionDefaultMultiBehaviour(group, option, optionIdx);

            ImUtf8.SameLineInner();
            editor.DrawOptionName(option);

            ImUtf8.SameLineInner();
            editor.DrawOptionDescription(option);

            ImUtf8.SameLineInner();
            editor.DrawOptionDelete(option);
        }

        DrawNewOption();
        DrawContainerNames();
    }

    private void DrawNewOption()
    {
        var count = group.OptionData.Count;
        if (count >= IModGroup.MaxCombiningOptions)
            return;

        var name = editor.DrawNewOptionBase(group, count);

        var validName = name.Length > 0;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, validName
                ? "为此组添加一个新选项。"u8
                : "请输入新选项的名称。"u8, default, !validName))
        {
            editor.ModManager.OptionEditor.CombiningEditor.AddOption(group, name);
            editor.NewOptionName = null;
        }
    }

    private unsafe void DrawContainerNames()
    {
        if (ImUtf8.ButtonEx("编辑容器名称"u8,
                "为组合组的数据容器添加可选名称。\n这些名称仅用于在编辑模组时方便识别，一般不会显示给用户。"u8,
                new Vector2(400 * ImUtf8.GlobalScale, 0)))
            ImUtf8.OpenPopup("DataContainerNames"u8);

        var sizeX = group.OptionData.Count * (ImGui.GetStyle().ItemInnerSpacing.X + ImGui.GetFrameHeight()) + 300 * ImUtf8.GlobalScale;
        ImGui.SetNextWindowSize(new Vector2(sizeX, ImGui.GetFrameHeightWithSpacing() * Math.Min(16, group.Data.Count) + 200 * ImUtf8.GlobalScale));
        using var popup = ImUtf8.Popup("DataContainerNames"u8);
        if (!popup)
            return;

        foreach (var option in group.OptionData)
        {
            ImUtf8.RotatedText(option.Name, true);
            ImUtf8.SameLineInner();
        }

        ImGui.NewLine();
        ImGui.Separator();
        using var child = ImUtf8.Child("##Child"u8, ImGui.GetContentRegionAvail());
        ImGuiClip.ClippedDraw(group.Data, DrawRow, ImGui.GetFrameHeightWithSpacing());
    }

    private void DrawRow(CombinedDataContainer container, int index)
    {
        using var id = ImUtf8.PushId(index);
        using (ImRaii.Disabled())
        {
            for (var i = 0; i < group.OptionData.Count; ++i)
            {
                id.Push(i);
                var check = (index & (1 << i)) != 0;
                ImUtf8.Checkbox(""u8, ref check);
                ImUtf8.SameLineInner();
                id.Pop();
            }
        }

        var name = editor.CombiningDisplayIndex == index ? editor.CombiningDisplayName ?? container.Name : container.Name;
        if (ImUtf8.InputText("##Nothing"u8, ref name, "可选显示名称..."u8))
        {
            editor.CombiningDisplayIndex = index;
            editor.CombiningDisplayName  = name;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
            editor.ModManager.OptionEditor.CombiningEditor.SetDisplayName(container, name);

        if (ImGui.IsItemDeactivated())
        {
            editor.CombiningDisplayIndex = -1;
            editor.CombiningDisplayName  = null;
        }
    }
}
