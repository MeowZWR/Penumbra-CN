using ImSharp;
using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public class ModPanelDescriptionTab(
    ModSelection selection,
    TutorialService tutorial,
    ModManager modManager,
    PredefinedTagManager predefinedTagsConfig)
    : ITab<ModPanelTab>
{
    public ReadOnlySpan<byte> Label
        => "模组描述"u8;

    public ModPanelTab Identifier
        => ModPanelTab.Description;

    public void DrawContent()
    {
        using var id    = Im.Id.Push(selection.ModName);
        using var child = Im.Child.Begin("##description"u8);
        if (!child)
            return;

        Im.ScaledDummy(2, 2);
        Im.ScaledDummy(2, 2);
        var (predefinedTagsEnabled, predefinedTagButtonOffset) = predefinedTagsConfig.Enabled
            ? (true, Im.Style.FrameHeight + Im.Style.WindowPadding.X + (Im.Scroll.MaximumY > 0 ? Im.Style.ScrollbarSize : 0))
            : (false, 0);
        var tagIdx = TagButtons.Draw("本地标签："u8,
            "个人设置的自定义标签，不会导出到模组数据。\n"u8
          + "如果模组已经包含与本地标签相同的标签，此本地标签会被忽略。"u8, selection.Mod!.LocalTags,
            out var editedTag, rightEndOffset: predefinedTagButtonOffset);
        tutorial.OpenTutorial(BasicTutorialSteps.Tags);
        if (tagIdx >= 0)
            modManager.DataEditor.ChangeLocalTag(selection.Mod!, tagIdx, editedTag);

        if (predefinedTagsEnabled)
            predefinedTagsConfig.DrawAddFromSharedTagsAndUpdateTags(selection.Mod, true);

        if (selection.Mod!.ModTags.Count > 0)
            TagButtons.Draw("模组标签："u8, "由模组作者创建的标签，随模组数据保存，通过编辑选项卡来修改。"u8,
                selection.Mod!.ModTags, out _, false, Im.Font.CalculateSize("本地 "u8).X - Im.Font.CalculateSize("模组 "u8).X);

        Im.ScaledDummy(2, 2);
        Im.Separator();

        Im.TextWrapped(selection.Mod!.Description);
    }
}
