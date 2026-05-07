using System.Collections.Frozen;
using Luna;
using Penumbra.Mods;

namespace Penumbra.UI.ModsTab.Selector;

public readonly struct ImportDate : ISortMode
{
    public static readonly ImportDate Instance = new();

    public ReadOnlySpan<byte> Name
        => "导入日期（旧到新）"u8;

    public ReadOnlySpan<byte> Description
        => "在每个折叠组中，按字典顺序排序所有子折叠组，然后按导入日期排序所有数据节点。"u8;

    public IEnumerable<IFileSystemNode> GetChildren(IFileSystemFolder f)
        => ISortMode.GetFolderLike(f)
            .Concat(ISortMode.GetLeaveLike(f).OrderBy(l => l switch
            {
                IFileSystemData<Mod> m => m.Value.ImportDate,
                IFileSystemSeparator s => s.CreationDate,
                _                      => 0L,
            }));
}

public readonly struct InverseImportDate : ISortMode
{
    public static readonly InverseImportDate Instance = new();

    public ReadOnlySpan<byte> Name
        => "导入日期（新到旧）"u8;

    public ReadOnlySpan<byte> Description
        => "在每个折叠组中，按字典顺序排序所有子折叠组，然后按反导入日期排序所有数据节点。"u8;

    public IEnumerable<IFileSystemNode> GetChildren(IFileSystemFolder f)
        => ISortMode.GetFolderLike(f)
            .Concat(ISortMode.GetLeaveLike(f).OrderByDescending(l => l switch
            {
                IFileSystemData<Mod> m => m.Value.ImportDate,
                IFileSystemSeparator s => s.CreationDate,
                _                      => 0L,
            }));
}

public static class SortModeExtensions
{
    private static readonly FrozenDictionary<string, ISortMode> ValidSortModes = new Dictionary<string, ISortMode>
    {
        [nameof(ISortMode.FoldersFirst)]           = ISortMode.FoldersFirst,
        [nameof(ISortMode.Lexicographical)]        = ISortMode.Lexicographical,
        [nameof(ImportDate)]                       = ISortMode.ImportDate,
        [nameof(InverseImportDate)]                = ISortMode.InverseImportDate,
        [nameof(ISortMode.InverseFoldersFirst)]    = ISortMode.InverseFoldersFirst,
        [nameof(ISortMode.InverseLexicographical)] = ISortMode.InverseLexicographical,
        [nameof(ISortMode.FoldersLast)]            = ISortMode.FoldersLast,
        [nameof(ISortMode.InverseFoldersLast)]     = ISortMode.InverseFoldersLast,
        [nameof(ISortMode.InternalOrder)]          = ISortMode.InternalOrder,
        [nameof(ISortMode.InverseInternalOrder)]   = ISortMode.InverseInternalOrder,
    }.ToFrozenDictionary();

    extension(ISortMode)
    {
        public static ISortMode ImportDate
            => ImportDate.Instance;

        public static ISortMode InverseImportDate
            => InverseImportDate.Instance;

        public static IReadOnlyDictionary<string, ISortMode> Valid
            => ValidSortModes;
    }
}
