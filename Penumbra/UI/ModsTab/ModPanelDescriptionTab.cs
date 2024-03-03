using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using OtterGui.Widgets;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ModsTab;

public class ModPanelDescriptionTab : ITab
{
    private readonly ModFileSystemSelector _selector;
    private readonly TutorialService       _tutorial;
    private readonly ModManager            _modManager;
    private readonly TagButtons            _localTags = new();
    private readonly TagButtons            _modTags   = new();

    public ModPanelDescriptionTab(ModFileSystemSelector selector, TutorialService tutorial, ModManager modManager)
    {
        _selector   = selector;
        _tutorial   = tutorial;
        _modManager = modManager;
    }

    public ReadOnlySpan<byte> Label
        => "模组描述"u8;

    public void DrawContent()
    {
        using var child = ImRaii.Child("##description");
        if (!child)
            return;

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));
        var tagIdx = _localTags.Draw( "本地标签：",
            "个人设置的自定义标签，不会导出到模组。\n"
          + "如果模组已经有与本地标签相同的标签，此本地标签会被忽略。", _selector.Selected!.LocalTags,
            out var editedTag);
        _tutorial.OpenTutorial(BasicTutorialSteps.Tags);
        if (tagIdx >= 0)
            _modManager.DataEditor.ChangeLocalTag(_selector.Selected!, tagIdx, editedTag);

        if (_selector.Selected!.ModTags.Count > 0)
            _modTags.Draw( "模组标签：", "由模组作者创建的标签，随模组数据保存，通过编辑选项卡来修改。",
                _selector.Selected!.ModTags, out var _, false,
                ImGui.CalcTextSize("Local ").X - ImGui.CalcTextSize("Mod ").X);

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));
        ImGui.Separator();

        ImGuiUtil.TextWrapped(_selector.Selected!.Description);
    }
}
