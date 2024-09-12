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

    public static IReadOnlyList<(ModFilter On, ModFilter Off, string Name)> TriStatePairs =
    [
        (ModFilter.Enabled, ModFilter.Disabled, "已启用"),
        (ModFilter.IsNew, ModFilter.NotNew, "新导入"),
        (ModFilter.Favorite, ModFilter.NotFavorite, "已收藏"),
        (ModFilter.HasConfig, ModFilter.HasNoConfig, "有选项"),
        (ModFilter.HasFiles, ModFilter.HasNoFiles, "有文件"),
        (ModFilter.HasMetaManipulations, ModFilter.HasNoMetaManipulations, "有元数据操作"),
        (ModFilter.HasFileSwaps, ModFilter.HasNoFileSwaps, "有文件替换"),
    ];

    public static IReadOnlyList<IReadOnlyList<(ModFilter Filter, string Name)>> Groups =
    [
        [
            (ModFilter.NoConflict, "无冲突"),
            (ModFilter.SolvedConflict, "冲突已解决"),
            (ModFilter.UnsolvedConflict, "冲突未解决"),
        ],
        [
            (ModFilter.Undefined, "未被配置"),
            (ModFilter.Inherited, "继承配置"),
            (ModFilter.Uninherited, "自己的配置"),
        ],
    ];
}
