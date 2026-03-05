using Luna.Generators;

namespace Penumbra.UI.ModsTab;

[NamedEnum(Utf16: false)]
[TooltipEnum]
public enum RenameField
{
    [Name("无")]
    [Tooltip("在模组的上下文菜单中不显示重命名字段。")]
    None,

    [Name("搜索路径")]
    [Tooltip("在模组的上下文菜单中仅显示搜索路径 / 移动字段。")]
    RenameSearchPath,

    [Name("模组名称")]
    [Tooltip("在模组的上下文菜单中仅显示模组名称字段。")]
    RenameData,

    [Name("两者（焦点在搜索路径）")]
    [Tooltip("在模组的上下文菜单中显示两个重命名字段，但将键盘光标放在搜索路径字段上。")]
    BothSearchPathPrio,

    [Name("两者（焦点在设计名称）")]
    [Tooltip("在模组的上下文菜单中显示两个重命名字段，但将键盘光标放在设计名称字段上。")]
    BothDataPrio,
}
