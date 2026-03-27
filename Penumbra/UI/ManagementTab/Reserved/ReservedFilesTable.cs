using ImSharp;
using ImSharp.Table;
using Luna;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class ReservedFilesTable(ModManager mods, TextureManager textures, UiNavigator navigator, Configuration config)
    : TableBase<ReservedFileCacheObject, ScannerTabCache<ReservedFileCacheObject, ReservedFileRedirection>>(new StringU8("##fft"u8),
        new ActionColumn(mods, config),
        new GamePathColumn<ReservedFileCacheObject, ReservedFileRedirection> { Label = new StringU8("Game Path"u8) },
        new StateColumn { Label                                                        = new StringU8("State"u8) },
        new TargetColumn<ReservedFileCacheObject, ReservedFileRedirection> { Label   = new StringU8("Target File"u8) },
        new ModColumn(navigator) { Label                                               = new StringU8("Mod"u8) },
        new ContainerColumn(navigator) { Label                                         = new StringU8("Option"u8) })
{
    private const bool DryRun = false;

    /// <remarks> Implemented in the cache due to use of scanner. </remarks>>
    public override IEnumerable<ReservedFileCacheObject> GetItems()
        => [];

    protected override void PreDraw(in ScannerTabCache<ReservedFileCacheObject, ReservedFileRedirection> cache)
    {
        cache.DrawScanButtons();

        var active = config.DeleteModModifier.IsActive();
        if (ImEx.Button("移除所有简单重定向"u8, default, !active))
            RemoveRedundant(cache);

        if (Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
        {
            using var tt = Im.Tooltip.Begin();
            Im.Text("执行此操作将："u8);
            Im.BulletText("移除所有列出的文件替换(File Swaps)，因为这些配置不合理。"u8);
            Im.BulletText("移除所有状态为“损坏(Broken)”的重定向，因为它们无法被读取且无效。"u8);
            Im.BulletText("移除所有状态为“缺失 (Missing)”的重定向，因为目标文件不存在且无效。"u8);
            Im.BulletText(
                "移除所有状态为“一致 (Equal)”的重定向，因为目标文件与游戏原始文件相同，没有重定向意义。"u8);
            Im.BulletText("删除所有在上述操作后不再有关联重定向的目标文件。"u8);
            Im.Text("\n【此操作无法撤销，请谨慎操作】"u8, Colors.RegexWarningBorder);

            if (!active)
                Im.Text($"\n请在点击时按住 {config.DeleteModModifier} 键。");
        }
    }

    protected override ScannerTabCache<ReservedFileCacheObject, ReservedFileRedirection> CreateCache()
        => new Cache(mods, textures, this);

    private sealed class Cache(ModManager mods, TextureManager textures, ReservedFilesTable parent)
        : ScannerTabCache<ReservedFileCacheObject, ReservedFileRedirection>(parent, new ReservedFileScanner(mods, textures))
    {
        protected override ReservedFileCacheObject Convert(ReservedFileRedirection obj)
            => new(obj);
    }

    private sealed class ActionColumn : BasicColumn<ReservedFileCacheObject>
    {
        private readonly ModManager    _mods;
        private readonly Configuration _config;
        private          int           _deleteIndex = -1;

        public ActionColumn(ModManager mods, Configuration config)
        {
            _mods   =  mods;
            _config =  config;
            Flags   |= TableColumnFlags.NoSort | TableColumnFlags.NoResize;
        }

        public override void PostDraw(in TableCache<ReservedFileCacheObject> cache)
        {
            if (_deleteIndex is -1)
                return;

            cache.DeleteSingleItem(_deleteIndex);
            _deleteIndex = -1;
        }

        public override void DrawColumn(in ReservedFileCacheObject item, int globalIndex)
        {
            var disabled = !_config.DeleteModModifier.IsActive();
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon,
                    item.ScannedObject.FileSwap
                        ? "移除此文件替换(File Swap)。"u8
                        : "移除此重定向并删除目标文件，如果它是模组中最后一个重定向。"u8,
                    disabled))
            {
                if (item.ScannedObject.Container.TryGetTarget(out var container))
                {
                    if (item.ScannedObject.FileSwap)
                    {
                        var swaps = container.FileSwaps.ToDictionary();
                        if (swaps.Remove(item.ScannedObject.GamePath, out _))
                        {
                            if (!DryRun)
                                _mods.OptionEditor.SetFileSwaps(container, swaps);
                            Penumbra.Log.Debug(
                                $"[ReservedFiles] Removed reserved file swap {item.ScannedObject.GamePath} -> {item.ScannedObject.FilePath} in {container.Mod.Name} - {container.GetFullName()}.");
                        }
                    }
                    else
                    {
                        var redirections = container.Files.ToDictionary();
                        if (redirections.Remove(item.ScannedObject.GamePath, out var file))
                        {
                            if (!DryRun)
                                _mods.OptionEditor.SetFiles(container, redirections);
                            Penumbra.Log.Debug(
                                $"[ReservedFiles] Removed reserved file redirection {item.ScannedObject.GamePath} -> {item.ScannedObject.FilePath} in {container.Mod.Name} - {container.GetFullName()}.");
                            if (file.Exists && ((Mod)container.Mod).AllDataContainers.All(c => !c.Files.ContainsValue(file)))
                                DeleteFile(file);
                        }
                    }
                }

                _deleteIndex = globalIndex;
            }

            if (disabled)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"\n按住 {_config.DeleteModModifier} 键以删除。");
        }

        public override float ComputeWidth(IEnumerable<ReservedFileCacheObject> _)
            => Im.Style.FrameHeight;
    }

    private sealed class ModColumn(UiNavigator navigator) : ModColumn<ReservedFileCacheObject>(navigator)
    {
        protected override Mod? GetMod(in ReservedFileCacheObject item, int globalIndex)
            => item.ScannedObject.Container.TryGetTarget(out var c) ? c.Mod as Mod : null;

        protected override StringPair GetModName(in ReservedFileCacheObject item, int globalIndex)
            => item.Mod;

        private string _lastMod = string.Empty;

        protected override bool MatchesLastItem(in ReservedFileCacheObject item)
        {
            var ret = _lastMod == item.Mod.Utf16;
            _lastMod = item.Mod.Utf16;
            return ret;
        }

        public override void PostDraw(in TableCache<ReservedFileCacheObject> cache)
        {
            _lastMod = string.Empty;
        }
    }

    private sealed class ContainerColumn(UiNavigator navigator) : ModColumn<ReservedFileCacheObject>(navigator)
    {
        protected override Mod? GetMod(in ReservedFileCacheObject item, int globalIndex)
            => item.ScannedObject.Container.TryGetTarget(out var c) ? c.Mod as Mod : null;

        protected override StringPair GetModName(in ReservedFileCacheObject item, int globalIndex)
            => item.Container;

        private string _lastMod       = string.Empty;
        private string _lastContainer = string.Empty;

        protected override bool MatchesLastItem(in ReservedFileCacheObject item)
        {
            var ret = _lastMod == item.Mod.Utf16 && _lastContainer == item.Container.Utf16;
            _lastMod       = item.Mod.Utf16;
            _lastContainer = item.Container.Utf16;
            return ret;
        }

        public override void PostDraw(in TableCache<ReservedFileCacheObject> cache)
        {
            _lastMod       = string.Empty;
            _lastContainer = string.Empty;
        }
    }

    private sealed class StateColumn : TextColumn<ReservedFileCacheObject>
    {
        protected override string ComparisonText(in ReservedFileCacheObject item, int globalIndex)
            => item.State;

        protected override StringU8 DisplayText(in ReservedFileCacheObject item, int globalIndex)
            => item.State;

        protected override void DrawTooltip(in ReservedFileCacheObject item, int globalIndex)
        {
            using var tt = Im.Tooltip.Begin();
            Im.Text(item.ScannedObject.FileSwap
                ? "对此类文件，不存在有效或有意义的文件替换(File Swap)。"u8
                : item.ScannedObject.Broken
                    ? "扫描器无法读取或解析该文件，此重定向无效，建议移除。"u8
                    : item.ScannedObject.Missing
                        ? "重定向指向的文件不存在，请直接移除此重定向。"u8
                        : item.ScannedObject.ConceptuallyEqual
                            ? "重定向目标与游戏原始文件一致，可直接移除此重定向，无不良影响。"u8
                            : "此文件与游戏原始文件在语义上不一致，模组可能需要由作者修复。\n\n该重定向本就不会生效，您可以移除此重定向以消除警告，但模组可能无法按预期工作。"u8);
        }

        public override void DrawColumn(in ReservedFileCacheObject item, int globalIndex)
        {
            base.DrawColumn(in item, globalIndex);
            if (item.State.Utf16 is not "Different")
                return;

            Im.Line.SameInner();
            ImEx.Icon.Draw(LunaStyle.WarningIcon, Rgba32.Yellow);
            if (Im.Item.Hovered())
                DrawTooltip(item, globalIndex);
        }

        public override float ComputeWidth(IEnumerable<ReservedFileCacheObject> _)
            => ReservedFileCacheObject.Different.Utf8.CalculateSize().X + Im.Style.FrameHeightWithSpacing;
    }

    private void RemoveRedundant(ScannerTabCache<ReservedFileCacheObject, ReservedFileRedirection> cache)
    {
        var files   = new SetDictionary<Mod, FullPath>();
        var indices = new List<int>(cache.AllItems.Count);
        foreach (var group in cache.AllItems.Select((r, i) => (r.ScannedObject, i))
                     .GroupBy(r => r.ScannedObject.Container.TryGetTarget(out var container) ? container : null).ToList())
        {
            if (group.Key is not { } container)
                continue;

            var swaps        = container.FileSwaps.ToDictionary();
            var redirections = container.Files.ToDictionary();

            foreach (var (redirection, index) in group)
            {
                if (redirection.FileSwap)
                {
                    swaps.Remove(redirection.GamePath);
                    Penumbra.Log.Debug(
                        $"[ReservedFiles] Removed reserved file swap {redirection.GamePath} -> {redirection.FilePath} in {container.Mod.Name} - {container.GetFullName()}.");
                    indices.Add(index);
                }
                else if (redirection.ConceptuallyEqual || redirection.Missing || redirection.Broken)
                {
                    redirections.Remove(redirection.GamePath, out var file);
                    files.TryAdd((Mod)container.Mod, file);
                    Penumbra.Log.Debug(
                        $"[ReservedFiles] Removed reserved file redirection {redirection.GamePath} -> {redirection.FilePath} in {container.Mod.Name} - {container.GetFullName()} because the target file was {(redirection.Broken ? "broken." : redirection.Missing ? "missing." : "conceptually equal.")}");
                    indices.Add(index);
                }
            }

            if (swaps.Count < container.FileSwaps.Count)
            {
                Penumbra.Log.Information(
                    $"[ReservedFiles] Removed {container.FileSwaps.Count - swaps.Count} reserved file swaps in {container.Mod.Name} - {container.GetFullName()}.");
                if (!DryRun)
                    mods.OptionEditor.SetFileSwaps(container, swaps);
            }

            if (redirections.Count < container.Files.Count)
            {
                Penumbra.Log.Information(
                    $"[ReservedFiles] Removed {container.Files.Count - redirections.Count} reserved file redirections in {container.Mod.Name} - {container.GetFullName()}.");
                if (!DryRun)
                    mods.OptionEditor.SetFiles(container, redirections);
            }
        }

        indices.Sort();
        foreach (var idx in indices.AsEnumerable().Reverse())
            cache.DeleteSingleItem(idx);

        foreach (var (mod, modFiles) in files.Grouped)
        {
            var collectedUsage = mod.AllDataContainers.SelectMany(m => m.Files.Values).ToHashSet();
            foreach (var file in modFiles)
            {
                if (!file.Exists)
                    continue;

                if (!collectedUsage.Contains(file))
                    DeleteFile(file);
            }
        }
    }

    private static void DeleteFile(in FullPath file)
    {
        try
        {
            if (!DryRun)
                File.Delete(file.FullName);
            Penumbra.Log.Information(
                $"[ReservedFiles] Deleted now unused file {file.FullName} after removing it from reserved file redirections.");
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error(
                $"[ReservedFiles] Unable to delete reserved file {file.FullName} removed from all redirections:\n{ex}");
        }
    }
}
