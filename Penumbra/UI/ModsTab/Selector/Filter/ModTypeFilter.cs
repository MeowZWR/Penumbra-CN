using ImSharp;

namespace Penumbra.UI.ModsTab.Selector;

[Flags]
public enum ModTypeFilter
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
    Temporary              = 1 << 19,
    NotTemporary           = 1 << 20,
    Undefined              = 1 << 21,
};

public static class ModTypeFilterExtensions
{
    public const ModTypeFilter UnfilteredStateMods = (ModTypeFilter)((1 << 22) - 1);

    public static readonly IReadOnlyList<(ModTypeFilter On, ModTypeFilter Off, StringU8 Name)> TriStatePairs =
    [
        (ModTypeFilter.Enabled, ModTypeFilter.Disabled, new StringU8("已启用"u8)),
        (ModTypeFilter.IsNew, ModTypeFilter.NotNew, new StringU8("新导入"u8)),
        (ModTypeFilter.Favorite, ModTypeFilter.NotFavorite, new StringU8("收藏"u8)),
        (ModTypeFilter.HasConfig, ModTypeFilter.HasNoConfig, new StringU8("有选项"u8)),
        (ModTypeFilter.HasFiles, ModTypeFilter.HasNoFiles, new StringU8("有重定向"u8)),
        (ModTypeFilter.HasMetaManipulations, ModTypeFilter.HasNoMetaManipulations, new StringU8("有元数据操作"u8)),
        (ModTypeFilter.HasFileSwaps, ModTypeFilter.HasNoFileSwaps, new StringU8("有文件替换"u8)),
        (ModTypeFilter.Temporary, ModTypeFilter.NotTemporary, new StringU8("临时"u8)),
    ];

    public static readonly IReadOnlyList<IReadOnlyList<(ModTypeFilter Filter, StringU8 Name)>> Groups =
    [
        [
            (ModTypeFilter.NoConflict, new StringU8("没有冲突"u8)),
            (ModTypeFilter.SolvedConflict, new StringU8("已解决冲突"u8)),
            (ModTypeFilter.UnsolvedConflict, new StringU8("未解决冲突"u8)),
        ],
        [
            (ModTypeFilter.Undefined, new StringU8("未配置"u8)),
            (ModTypeFilter.Inherited, new StringU8("继承配置"u8)),
            (ModTypeFilter.Uninherited, new StringU8("未继承配置"u8)),
        ],
    ];
}
