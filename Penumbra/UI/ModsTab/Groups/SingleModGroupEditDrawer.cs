using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.Mods.Groups;

namespace Penumbra.UI.ModsTab.Groups;

public readonly struct SingleModGroupEditDrawer(ModGroupEditDrawer editor, SingleModGroup group) : IModGroupEditDrawer
{
    public void Draw()
    {
        foreach (var (option, optionIdx) in group.OptionData.WithIndex())
        {
            using var id = ImRaii.PushId(optionIdx);
            editor.DrawOptionPosition(group, option, optionIdx);

            ImUtf8.SameLineInner();
            editor.DrawOptionDefaultSingleBehaviour(group, option, optionIdx);

            ImUtf8.SameLineInner();
            editor.DrawOptionName(option);

            ImUtf8.SameLineInner();
            editor.DrawOptionDescription(option);

            ImUtf8.SameLineInner();
            editor.DrawOptionDelete(option);

            ImUtf8.SameLineInner();
            ImGui.Dummy(new Vector2(editor.PriorityWidth, 0));
        }

        DrawNewOption();
        DrawConvertButton();
    }

    private void DrawConvertButton()
    {
        var convertible = group.Options.Count <= IModGroup.MaxMultiOptions;
        var g = group;
        var e = editor.ModManager.OptionEditor.SingleEditor;
        if (ImUtf8.ButtonEx("转换为多选项组", editor.AvailableWidth, !convertible))
            editor.ActionQueue.Enqueue(() => e.ChangeToMulti(g));
        if (!convertible)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled,
                "由于超过了选项的最大数量限制，无法转换为多选项组。"u8);
    }

    private void DrawNewOption()
    {
        var count = group.Options.Count;
        if (count >= int.MaxValue)
            return;

        var name = editor.DrawNewOptionBase(group, count);

        var validName = name.Length > 0;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, validName
                ? "向此组添加一个新选项。"u8
                : "请输入新选项的名称。"u8, default, !validName))
        {
            editor.ModManager.OptionEditor.SingleEditor.AddOption(group, name);
            editor.NewOptionName = null;
        }
    }
}
