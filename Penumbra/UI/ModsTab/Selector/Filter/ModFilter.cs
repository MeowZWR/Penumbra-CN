using ImSharp;
using Luna;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class ModFilter : TokenizedFilter<ModFilterTokenType, ModFileSystemCache.ModData, ModFilterToken>,
    IFileSystemFilter<ModFileSystemCache.ModData>
{
    private          ModTypeFilter     _stateFilter;
    private readonly ModManager        _modManager;
    private readonly ActiveCollections _collections;

    public ModFilter(ModManager modManager, ActiveCollections collections, Configuration config)
    {
        _modManager  = modManager;
        _collections = collections;
        if (config.RememberModFilters)
        {
            _stateFilter = config.Filters.ModTypeFilter;
            Set(config.Filters.ModFilter);
        }

        FilterChanged += () =>
        {
            config.Filters.ModFilter     = Text;
            config.Filters.ModTypeFilter = StateFilter;
        };
    }

    public ModTypeFilter StateFilter
        => _stateFilter;

    protected override void DrawTooltip()
    {
        if (!Im.Item.Hovered())
            return;

        using var tt             = Im.Tooltip.Begin();
        var       highlightColor = ColorId.NewMod.Value().ToVector();
        Im.Text("根据输入的文本筛选模组，按空格分词，在模组完整路径或名称中查找包含这些文本的模组。"u8);
        ImEx.TextMultiColored("输入 "u8).Then("c:[string]"u8, highlightColor).Then(" 可按修改了指定物品的模组进行筛选。"u8).End();
        ImEx.TextMultiColored("输入 "u8).Then("t:[string]"u8, highlightColor).Then(" 可按已设置的指定标签筛选模组。"u8).End();
        ImEx.TextMultiColored("Enter "u8).Then("n:[string]"u8, highlightColor)
            .Then(" 只按模组名称进行筛选（不考虑路径）。"u8).End();
        ImEx.TextMultiColored("输入 "u8).Then("a:[string]"u8, highlightColor).Then(" 可按指定作者筛选模组。"u8).End();
        ImEx.TextMultiColored("输入 "u8).Then("s:[string]"u8, highlightColor).Then(
                $" 可按所更改物品的类别筛选模组（使用 1-{ChangedItemFlagExtensions.NumCategories + 1} 或类别名称的一部分）。")
            .End();
        ImEx.TextMultiColored("输入 "u8).Then("f:[string]"u8, highlightColor)
            .Then(
                " 可在模组名称、路径、描述、标签、更改的物品，以及分组或选项的名称和描述中搜索包含该文本的模组。"u8)
            .End();
        Im.Line.New();
        ImEx.TextMultiColored("使用 "u8).Then("None"u8, highlightColor).Then(" 作为占位值，只会匹配为空的列表或名称。"u8)
            .End();
        Im.Text("默认情况下，模组需要分别满足所有给定的条件。"u8);
        ImEx.TextMultiColored("在搜索词前加上 "u8).Then("'-'"u8, highlightColor)
            .Then(" 只会匹配不符合该条件的模组。"u8).End();
        ImEx.TextMultiColored("在搜索词前加上 "u8).Then("'?'"u8, highlightColor)
            .Then(" 用于“或”条件，即模组只要匹配任意一个以 '?' 开头的条件即可。"u8).End();
        ImEx.TextMultiColored("将包含空格的文本包在 "u8).Then("\"[string with space]\""u8, highlightColor)
            .Then(" 中，以匹配这段完整文本。"u8).End();
        Im.Line.New();
        Im.Text("示例：'t:Tag1 t:\"Tag2\" -t:Tag3 -a:None s:Body -c:Hempen ?c:Camise ?n:Top' 将匹配满足以下条件的任意模组："u8);
        Im.BulletText("包含标签 'tag1' 和 'tag2'；"u8);
        Im.BulletText("不包含标签 'tag3'；"u8);
        Im.BulletText("已设置任意作者（对 None 取反等同于“任意”）；"u8);
        Im.BulletText("修改了“Body”类别中的任意一个物品；"u8);
        Im.BulletText("并且要么有名称中包含 'camise' 的变更物品，要么模组名称中包含 'top'。"u8);
    }

    public override bool DrawFilter(ReadOnlySpan<byte> label, Vector2 availableRegion)
    {
        var ret = base.DrawFilter(label, availableRegion with { X = availableRegion.X - Im.Style.FrameHeight });
        Im.Line.NoSpacing();
        ret |= DrawFilterCombo();
        return ret;
    }

    private bool DrawFilterCombo()
    {
        var       everything = _stateFilter is not ModTypeFilterExtensions.UnfilteredStateMods;
        using var color      = ImGuiColor.Button.Push(Colors.FilterActive, everything);
        using var combo = Im.Combo.Begin("##combo"u8, StringU8.Empty,
            ComboFlags.NoPreview | ComboFlags.HeightLargest | ComboFlags.PopupAlignLeft);

        if (Im.Item.MiddleClicked())
        {
            // Ensure that a right-click clears the text filter if it is currently being edited.
            Im.Id.ClearActive();
            Clear();
        }

        Im.Tooltip.OnHover("按模组激活的状态进行筛选。\n中键点击清除所有筛选, 包括文本筛选."u8);

        var changes = false;
        if (combo)
        {
            using var style = ImStyleDouble.ItemSpacing.PushY(3 * Im.Style.GlobalScale);
            changes |= Im.Checkbox("全部"u8, ref _stateFilter, ModTypeFilterExtensions.UnfilteredStateMods);
            Im.Dummy(new Vector2(0, 5 * Im.Style.GlobalScale));
            foreach (var (onFlag, offFlag, name) in ModTypeFilterExtensions.TriStatePairs)
                changes |= ImEx.TriStateCheckbox(name, ref _stateFilter, onFlag, offFlag);

            foreach (var group in ModTypeFilterExtensions.Groups)
            {
                Im.Separator();
                foreach (var (flag, name) in group)
                    changes |= Im.Checkbox(name, ref _stateFilter, flag);
            }
        }

        if (changes)
            InvokeEvent();

        return changes;
    }

    public override bool Clear()
    {
        var changes = _stateFilter is not ModTypeFilterExtensions.UnfilteredStateMods;
        _stateFilter = ModTypeFilterExtensions.UnfilteredStateMods;
        Im.Id.ClearActive();
        if (!SetInternal(string.Empty) && !changes)
            return false;

        InvokeEvent();
        return true;
    }

    protected override bool Matches(in ModFilterToken token, in ModFileSystemCache.ModData cacheItem)
        => token.Type switch
        {
            ModFilterTokenType.Default => cacheItem.Node.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase)
             || cacheItem.Node.Value.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
            ModFilterTokenType.ChangedItem => cacheItem.Node.Value.LowerChangedItemsString.Contains(token.Needle, StringComparison.Ordinal),
            ModFilterTokenType.Tag         => cacheItem.Node.Value.AllTagsLower.Contains(token.Needle, StringComparison.Ordinal),
            ModFilterTokenType.Name        => cacheItem.Node.Value.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
            ModFilterTokenType.Author      => cacheItem.Node.Value.Author.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
            ModFilterTokenType.Category    => CheckCategory(token.IconFlagFilter, cacheItem),
            ModFilterTokenType.FullContext => CheckFullContext(token.Needle, cacheItem),
            _                              => true,
        };

    private static bool CheckCategory(ChangedItemIconFlag flag, ModFileSystemCache.ModData cacheItem)
        => cacheItem.Node.Value.ChangedItems.Any(p => (p.Value.Icon.ToFlag() & flag) is not 0);

    protected override bool MatchesNone(ModFilterTokenType type, bool negated, in ModFileSystemCache.ModData cacheItem)
        => type switch
        {
            ModFilterTokenType.Author when negated      => cacheItem.Node.Value.Author.Length > 0,
            ModFilterTokenType.Author                   => cacheItem.Node.Value.Author.Length is 0,
            ModFilterTokenType.ChangedItem when negated => cacheItem.Node.Value.LowerChangedItemsString.Length > 0,
            ModFilterTokenType.ChangedItem              => cacheItem.Node.Value.LowerChangedItemsString.Length is 0,
            ModFilterTokenType.Tag when negated         => cacheItem.Node.Value.AllTagsLower.Length > 0,
            ModFilterTokenType.Tag                      => cacheItem.Node.Value.AllTagsLower.Length is 0,
            ModFilterTokenType.Category when negated    => cacheItem.Node.Value.ChangedItems.Count > 0,
            ModFilterTokenType.Category                 => cacheItem.Node.Value.ChangedItems.Count is 0,
            _                                           => true,
        };

    public override bool WouldBeVisible(in ModFileSystemCache.ModData cacheItem, int globalIndex)
    {
        if (!base.WouldBeVisible(in cacheItem, globalIndex))
            return false;

        if (_stateFilter is ModTypeFilterExtensions.UnfilteredStateMods)
            return true;

        return CheckStateFilters(cacheItem.Node.Value);
    }

    private bool CheckStateFilters(Mod mod)
    {
        var (settings, collection) = _collections.Current.GetActualSettings(mod.Index);
        var isNew = _modManager.IsNew(mod);
        // Handle mod details.
        if (CheckFlags(mod.TotalFileCount,     ModTypeFilter.HasNoFiles,             ModTypeFilter.HasFiles)
         || CheckFlags(mod.TotalSwapCount,     ModTypeFilter.HasNoFileSwaps,         ModTypeFilter.HasFileSwaps)
         || CheckFlags(mod.TotalManipulations, ModTypeFilter.HasNoMetaManipulations, ModTypeFilter.HasMetaManipulations)
         || CheckFlags(mod.HasOptions ? 1 : 0, ModTypeFilter.HasNoConfig,            ModTypeFilter.HasConfig)
         || CheckFlags(isNew ? 1 : 0,          ModTypeFilter.NotNew,                 ModTypeFilter.IsNew))
            return false;

        // Handle Favoritism
        if (!_stateFilter.HasFlag(ModTypeFilter.Favorite) && mod.Favorite
         || !_stateFilter.HasFlag(ModTypeFilter.NotFavorite) && !mod.Favorite)
            return false;

        // Handle Temporary
        if (!_stateFilter.HasFlag(ModTypeFilter.Temporary) || !_stateFilter.HasFlag(ModTypeFilter.NotTemporary))
        {
            if (settings is null && _stateFilter.HasFlag(ModTypeFilter.Temporary))
                return false;

            if (settings is not null && settings.IsTemporary() != _stateFilter.HasFlag(ModTypeFilter.Temporary))
                return false;
        }

        // Handle Inheritance
        if (collection == _collections.Current)
        {
            if (!_stateFilter.HasFlag(ModTypeFilter.Uninherited))
                return false;
        }
        else
        {
            if (!_stateFilter.HasFlag(ModTypeFilter.Inherited))
                return false;
        }

        // Handle settings.
        if (settings is null)
        {
            if (!_stateFilter.HasFlag(ModTypeFilter.Undefined)
             || !_stateFilter.HasFlag(ModTypeFilter.Disabled)
             || !_stateFilter.HasFlag(ModTypeFilter.NoConflict))
                return false;
        }
        else if (!settings.Enabled)
        {
            if (!_stateFilter.HasFlag(ModTypeFilter.Disabled)
             || !_stateFilter.HasFlag(ModTypeFilter.NoConflict))
                return false;
        }
        else
        {
            if (!_stateFilter.HasFlag(ModTypeFilter.Enabled))
                return false;

            // Conflicts can only be relevant if the mod is enabled.
            var conflicts = _collections.Current.Conflicts(mod);
            if (conflicts.Count > 0)
            {
                if (conflicts.Any(c => !c.Solved))
                {
                    if (!_stateFilter.HasFlag(ModTypeFilter.UnsolvedConflict))
                        return false;
                }
                else
                {
                    if (!_stateFilter.HasFlag(ModTypeFilter.SolvedConflict))
                        return false;
                }
            }
            else if (!_stateFilter.HasFlag(ModTypeFilter.NoConflict))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Check the state filter for a specific pair of has/has-not flags.
    /// Uses count == 0 to check for has-not and count != 0 for has.
    /// Returns true if it should be filtered and false if not. 
    /// </summary>
    private bool CheckFlags(int count, ModTypeFilter hasNoFlag, ModTypeFilter hasFlag)
        => count switch
        {
            0 when _stateFilter.HasFlag(hasNoFlag) => false,
            0                                      => true,
            _ when _stateFilter.HasFlag(hasFlag)   => false,
            _                                      => true,
        };

    public bool WouldBeVisible(in FileSystemFolderCache folder)
    {
        if (_stateFilter is not ModTypeFilterExtensions.UnfilteredStateMods)
            return false;

        switch (State)
        {
            case FilterState.NoFilters: return true;
            case FilterState.NoMatches: return false;
        }

        foreach (var token in Forced)
        {
            if (token.Type switch
                {
                    ModFilterTokenType.Name        => !folder.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    ModFilterTokenType.Default     => !folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    ModFilterTokenType.FullContext => !folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    _                              => true,
                })
                return false;
        }

        foreach (var token in Negated)
        {
            if (token.Type switch
                {
                    ModFilterTokenType.Name        => folder.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    ModFilterTokenType.Default     => folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    ModFilterTokenType.FullContext => folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    _                              => false,
                })
                return false;
        }

        foreach (var token in General)
        {
            if (token.Type switch
                {
                    ModFilterTokenType.Name        => folder.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    ModFilterTokenType.Default     => folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    ModFilterTokenType.FullContext => !folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    _                              => false,
                })
                return true;
        }

        return General.Count is 0;
    }

    private static bool CheckFullContext(string needle, in ModFileSystemCache.ModData cacheItem)
    {
        if (needle.Length is 0)
            return true;

        if (cacheItem.Node.FullPath.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return true;

        var mod = cacheItem.Node.Value;
        if (mod.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return true;

        if (mod.Description.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return true;

        if (mod.AllTagsLower.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return true;

        if (mod.ChangedItems.Keys.Any(k => k.Contains(needle, StringComparison.OrdinalIgnoreCase)))
            return true;

        foreach (var group in mod.Groups)
        {
            if (group.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
            if (group.Description.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var option in group.Options)
            {
                if (option.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (option.Description.Contains(needle, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    public override bool IsEmpty
        => base.IsEmpty && _stateFilter is ModTypeFilterExtensions.UnfilteredStateMods;
}
