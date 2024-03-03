namespace Penumbra.UI.ModsTab;

[Flags]
public enum ModFilter
{
    Enabled                = 1 << 0,
    Disabled               = 1 << 1,
    Favorite               = 1 << 2,
    NotFavorite            = 1 << 3,
    NoConflict             = 1 << 4,
    SolvedConflict         = 1 << 5,
    UnsolvedConflict       = 1 << 6,
    HasNoMetaManipulations = 1 << 7,
    HasMetaManipulations   = 1 << 8,
    HasNoFileSwaps         = 1 << 9,
    HasFileSwaps           = 1 << 10,
    HasConfig              = 1 << 11,
    HasNoConfig            = 1 << 12,
    HasNoFiles             = 1 << 13,
    HasFiles               = 1 << 14,
    IsNew                  = 1 << 15,
    NotNew                 = 1 << 16,
    Inherited              = 1 << 17,
    Uninherited            = 1 << 18,
    Undefined              = 1 << 19,
};

public static class ModFilterExtensions
{
    public const ModFilter UnfilteredStateMods = (ModFilter)((1 << 20) - 1);

    public static string ToName(this ModFilter filter)
        => filter switch
        {
            ModFilter.Enabled                => "已启用",
            ModFilter.Disabled               => "已禁用",
            ModFilter.Favorite               => "已收藏",
            ModFilter.NotFavorite            => "未收藏",
            ModFilter.NoConflict             => "无冲突",
            ModFilter.SolvedConflict         => "冲突已解决",
            ModFilter.UnsolvedConflict       => "冲突未解决",
            ModFilter.HasNoMetaManipulations => "无元素据操作",
            ModFilter.HasMetaManipulations   => "有元素据操作",
            ModFilter.HasNoFileSwaps         => "无文件转换",
            ModFilter.HasFileSwaps           => "有文件转换",
            ModFilter.HasNoConfig            => "无设置选项",
            ModFilter.HasConfig              => "有设置选项",
            ModFilter.HasNoFiles             => "无文件",
            ModFilter.HasFiles               => "有文件",
            ModFilter.IsNew                  => "最近导入",
            ModFilter.NotNew                 => "不是最近导入",
            ModFilter.Inherited              => "继承配置",
            ModFilter.Uninherited            => "自己的配置",
            ModFilter.Undefined              => "未被配置",
            _                                => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
        };
}
