namespace Penumbra.UI.ModsTab;

public enum RenameField
{
    None,
    RenameSearchPath,
    RenameData,
    BothSearchPathPrio,
    BothDataPrio,
}

public static class RenameFieldExtensions
{
    public static (string Name, string Desc) GetData(this RenameField value)
        => value switch
        {
            RenameField.None             => ("无", "在模组的上下文菜单中不显示重命名字段。"),
            RenameField.RenameSearchPath => ("搜索路径", "在模组的上下文菜单中仅显示搜索路径 / 移动字段。"),
            RenameField.RenameData       => ("模组名称", "在模组的上下文菜单中仅显示模组名称字段。"),
            RenameField.BothSearchPathPrio => ("两者（焦点在搜索路径）",
                "在模组的上下文菜单中显示两个重命名字段，但将键盘光标放在搜索路径字段上。"),
            RenameField.BothDataPrio => ("两者（焦点在设计名称）",
                "在模组的上下文菜单中显示两个重命名字段，但将键盘光标放在设计名称字段上。"),
            _ => (string.Empty, string.Empty),
        };
}
