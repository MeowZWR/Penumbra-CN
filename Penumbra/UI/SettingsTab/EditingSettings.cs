using ImSharp;
using Luna;

namespace Penumbra.UI;

public sealed class EditingSettings(EditingConfig config) : IUiService
{
    public void Draw()
    {
        DrawGeneralEditing();
        DrawMaterialEditing();
    }

    private void DrawGeneralEditing()
    {
        using var tree = Im.Tree.Node("常规"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("高级编辑：在编辑窗口中自动固定模组"u8,
                "决定打开新的高级编辑窗口时的默认固定行为。\n\n已固定：编辑窗口将始终锁定在打开或固定时所选的模组上，不会随主界面切换。\n未固定：当你在主窗口切换所选 Mod 时，编辑窗口将同步跟随切换（除非新选中的模组已经有一个已固定的编辑窗口）。"u8,
                config.DefaultEditWindowModPinned))
            config.DefaultEditWindowModPinned ^= true;
        LunaStyle.DrawSeparator();
    }


    private void DrawMaterialEditing()
    {
        using var tree = Im.Tree.Node("材质"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("高级编辑：编辑原始Tile UV变换"u8,
                "编辑Tile UV变换的原始矩阵组件，而不是将它们分解为缩放、旋转和剪切。"u8,
                config.EditRawTileTransforms))
            config.EditRawTileTransforms ^= true;

        if (SettingsTab.Checkbox("高级编辑：悬停选择按钮时始终高亮颜色组"u8,
                "使整个颜色组选择按钮都能触发游戏内高亮反馈，而不仅限于悬停在拾取图标上。开启此项后，无需按住 Ctrl 键即可生效。"u8,
                config.WholePairSelectorAlwaysHighlights))
            config.WholePairSelectorAlwaysHighlights ^= true;

        if (SettingsTab.Checkbox("高级编辑：解锁更多染料通道"u8,
                "虽然原版游戏限制了两个染料通道，但当前材质文件格式支持四个。\n此选项将允许在材质编辑器中使用这四个染料通道。\n请注意，此选项有限制：目前，这四个通道只能在材质编辑器中使用。"u8,
                config.AllDyeChannels))
            config.AllDyeChannels ^= true;
    }
}
