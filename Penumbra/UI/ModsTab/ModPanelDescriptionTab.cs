using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using OtterGui.Services;
using OtterGui.Widgets;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ModsTab;

public class ModPanelDescriptionTab(
    ModFileSystemSelector selector,
    TutorialService tutorial,
    ModManager modManager,
    PredefinedTagManager predefinedTagsConfig)
    : ITab, IUiService
{
    private readonly TagButtons _localTags = new();
    private readonly TagButtons _modTags   = new();

    public ReadOnlySpan<byte> Label
        => "模组描述"u8;

    public void DrawContent()
    {
        using var child = ImRaii.Child("##description");
        if (!child)
            return;

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));
        var (predefinedTagsEnabled, predefinedTagButtonOffset) = predefinedTagsConfig.Count > 0
            ? (true, ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.X + (ImGui.GetScrollMaxY() > 0 ? ImGui.GetStyle().ScrollbarSize : 0))
            : (false, 0);
        var tagIdx = _localTags.Draw("本地标签：",
            "个人设置的自定义标签，不会导出到模组。\n"
          + "如果模组已经有与本地标签相同的标签，此本地标签会被忽略。", selector.Selected!.LocalTags,
            out var editedTag, rightEndOffset: predefinedTagButtonOffset);
        tutorial.OpenTutorial(BasicTutorialSteps.Tags);
        if (tagIdx >= 0)
            modManager.DataEditor.ChangeLocalTag(selector.Selected!, tagIdx, editedTag);

        if (predefinedTagsEnabled)
            predefinedTagsConfig.DrawAddFromSharedTagsAndUpdateTags(selector.Selected!.LocalTags, selector.Selected!.ModTags, true,
                selector.Selected!);

        if (selector.Selected!.ModTags.Count > 0)
            _modTags.Draw("模组标签：", "由模组作者创建的标签，随模组数据保存，通过编辑选项卡来修改。",
                selector.Selected!.ModTags, out _, false,
                ImGui.CalcTextSize("Local ").X - ImGui.CalcTextSize("Mod ").X);

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));
        ImGui.Separator();

        ImGuiUtil.TextWrapped(selector.Selected!.Description);
    }
}
