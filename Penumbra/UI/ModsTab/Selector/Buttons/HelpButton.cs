using ImSharp;
using Luna;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Selector;

/// <summary> The button to open the help popup. </summary>
public sealed class HelpButton(ModFileSystemDrawer drawer) : BaseIconButton<AwesomeIcon>
{
    /// <inheritdoc/>
    public override AwesomeIcon Icon
        => LunaStyle.InfoIcon;

    /// <inheritdoc/>
    public override bool HasTooltip
        => true;

    /// <inheritdoc/>
    public override void DrawTooltip()
        => Im.Text("查看详细帮助。"u8);

    /// <inheritdoc/>
    public override void OnClick()
        => Im.Popup.Open("ExtendedHelp"u8);

    /// <inheritdoc/>
    protected override void PostDraw()
    {
        drawer.Tutorial.OpenTutorial(BasicTutorialSteps.AdvancedHelp);
        ImEx.HelpPopup("ExtendedHelp"u8, new Vector2(1000 * Im.Style.GlobalScale, 38.5f * Im.Style.TextHeightWithSpacing), PopupContent);
    }

    private void PopupContent()
    {
        Im.Line.New();
        Im.Text("模组管理"u8);
        Im.BulletText("本行按钮可用于新建空模组或导入现有模组。"u8);
        using var indent = Im.Indent();
        Im.BulletText("支持导入的格式：.ttmp、.ttmp2、.pmp、.pcp。"u8);
        Im.BulletText(
            "也可以导入 .zip、.7z 或 .rar 压缩包，但前提是其中已包含符合 Penumbra 规范、带有完整元数据的模组。"u8);
        indent.Unindent();
        Im.BulletText("你也可以创建空模组文件夹或删除模组。"u8);
        Im.BulletText(
            "如需进一步编辑模组，先选中模组，然后在右侧面板使用“编辑模组”标签页，或打开“高级编辑”弹窗。"u8);
        Im.Line.New();
        Im.Text("模组列表"u8);
        Im.BulletText("选择一个模组以查看详细信息或调整设置。"u8);
        Im.BulletText("模组名称的颜色取决于你的配色设置以及它在当前集合中的状态："u8);
        indent.Indent();
        Im.BulletText("在当前集合中已启用。"u8,                   ColorId.EnabledMod.Value());
        Im.BulletText("在当前集合中已禁用。"u8,                  ColorId.DisabledMod.Value());
        Im.BulletText("因从其他集合继承而被启用。"u8,            ColorId.InheritedMod.Value());
        Im.BulletText("因从其他集合继承而被禁用。"u8,            ColorId.InheritedDisabledMod.Value());
        Im.BulletText("在所有继承的集合中均为未配置状态。"u8,    ColorId.UndefinedMod.Value());
        Im.BulletText("已启用且与另一个已启用模组存在冲突，但优先级不同（即冲突已被解决）。"u8,
            ColorId.HandledConflictMod.Value());
        Im.BulletText("已启用且与另一个已启用模组在同一优先级上冲突。"u8, ColorId.ConflictingMod.Value());
        Im.BulletText("展开的模组文件夹。"u8,                                  ColorId.FolderExpanded.Value());
        Im.BulletText("折叠的模组文件夹。"u8,                                  ColorId.FolderCollapsed.Value());
        indent.Unindent();
        Im.BulletText("中键点击模组：若当前启用则将其禁用，若当前禁用则将其启用。"u8);
        indent.Indent();
        Im.BulletText(
            $"在中键点击时按住 {drawer.Config.DeleteModModifier.ForcedModifier(new DoubleModifier(ModifierHotkey.Control, ModifierHotkey.Shift))} 可改为继承上级设置，并丢弃当前集合中的配置。");
        indent.Unindent();
        Im.BulletText("右键点击模组可设置自定义排序键，默认为模组名称（必要时会附加编号）。"u8);
        indent.Indent();
        Im.BulletText("与模组名称不同的排序键不会单独显示，仅用于排序逻辑。"u8);
        Im.BulletText(
            "如果排序键中包含正斜杠（'/'），其前缀会被自动解释为文件夹层级。"u8);
        indent.Unindent();
        Im.BulletText(
            "你可以将模组或子文件夹拖放到已有文件夹中；拖到某个模组上与拖到其父文件夹效果相同。"u8);
        indent.Indent();
        Im.BulletText(
            "按住 Ctrl 点选可多选多个模组或文件夹，然后一次性拖动它们。"u8);
        Im.BulletText(
            "当一个文件夹被选中时，其中已选中的模组在拖动时会被忽略，它们会保持在该文件夹内，而不是直接移动到目标位置。"u8);
        indent.Unindent();
        Im.BulletText("右键点击文件夹会打开上下文菜单。"u8);
        Im.BulletText("右键点击空白区域可以一次性展开或折叠所有文件夹。"u8);
        Im.BulletText("顶部的“筛选模组...”输入框可根据名称或路径中包含的文本筛选模组列表。"u8);
        indent.Indent();
        Im.BulletText("输入 n:[字符串] 只按模组名称筛选，不考虑路径。"u8);
        Im.BulletText("输入 c:[字符串] 按更改的物品内容进行筛选。"u8);
        Im.BulletText("输入 a:[字符串] 按模组作者进行筛选。"u8);
        indent.Unindent();
        Im.BulletText("使用输入框旁的下拉菜单，可以按更细的条件筛选模组。"u8);
    }
}
