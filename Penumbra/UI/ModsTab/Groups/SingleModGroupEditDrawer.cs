using ImSharp;
using Luna;
using Penumbra.Mods.Groups;

namespace Penumbra.UI.ModsTab.Groups;

public readonly struct SingleModGroupEditDrawer(ModGroupEditDrawer editor, SingleModGroup group) : IModGroupEditDrawer
{
    public void Draw()
    {
        foreach (var (optionIdx, option) in group.OptionData.Index())
        {
            using var id = Im.Id.Push(optionIdx);
            editor.DrawOptionPosition(group, option, optionIdx);

            Im.Line.SameInner();
            editor.DrawOptionDefaultSingleBehaviour(group, option, optionIdx);

            Im.Line.SameInner();
            editor.DrawOptionName(option);

            Im.Line.SameInner();
            editor.DrawOptionButtons(option);

            Im.Line.SameInner();
            editor.DrawOptionDelete(option);

            Im.Line.SameInner();
            Im.Dummy(new Vector2(editor.PriorityWidth, 0));
        }

        DrawNewOption();
        DrawConvertButton();
    }

    private void DrawConvertButton()
    {
        var convertible = group.Options.Count <= IModGroup.MaxMultiOptions;
        var g           = group;
        var e           = editor.ModManager.OptionEditor.SingleEditor;
        if (ImEx.Button("转换为多选项组"u8, editor.AvailableWidth, !convertible))
            editor.ActionQueue.Enqueue(() => e.ChangeToMulti(g));
        if (!convertible)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled,
                "由于选项数量超过了最大限制，无法转换为多选项组。"u8);
    }

    private void DrawNewOption()
    {
        var count = group.Options.Count;
        if (count >= int.MaxValue)
            return;

        var name = editor.DrawNewOptionBase(group, count);

        var validName = name.Length > 0;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, validName
                ? "向此组添加一个新选项"u8
                : "请输入新选项的名称"u8, !validName))
        {
            editor.ModManager.OptionEditor.SingleEditor.AddOption(group, name);
            editor.NewOptionName = null;
        }
    }
}
