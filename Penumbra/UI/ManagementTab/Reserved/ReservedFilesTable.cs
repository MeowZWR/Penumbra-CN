using ImSharp;
using ImSharp.Table;
using Luna;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class ReservedFilesTable(
    ModManager mods,
    TextureManager textures,
    UiNavigator navigator,
    Configuration config,
    ReservedFiles reservedFiles,
    ManagementLog<ReservedFiles> log)
    : TableBase<ReservedFileCacheObject, ScannerTabCache<ReservedFileCacheObject, ReservedFileRedirection>>(new StringU8("##fft"u8),
        new ActionColumn(reservedFiles, config),
        new GamePathColumn<ReservedFileCacheObject, ReservedFileRedirection> { Label = new StringU8("游戏路径"u8) },
        new StateColumn { Label                                                      = new StringU8("状态"u8) },
        new TargetColumn<ReservedFileCacheObject, ReservedFileRedirection> { Label   = new StringU8("目标文件"u8) },
        new ModColumn(navigator) { Label                                             = new StringU8("模组"u8) },
        new ContainerColumn(navigator) { Label                                       = new StringU8("选项"u8) })
{
    /// <remarks> Implemented in the cache due to use of scanner. </remarks>>
    public override IEnumerable<ReservedFileCacheObject> GetItems()
        => [];

    protected override void PreDraw(in ScannerTabCache<ReservedFileCacheObject, ReservedFileRedirection> cache)
    {
        cache.DrawScanButtons();

        var active = config.IncognitoModifier.IsActive();
        if (ImEx.Button("移除所有简单重定向"u8, default, !active))
            reservedFiles.RemoveRedundant(cache, false);

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
            Im.Text("\n此操作不可撤销。"u8, Colors.RegexWarningBorder);

            if (!active)
                Im.Text($"\n点击时按住 {config.DeleteModModifier} 键。");
        }

        active = config.DeleteModModifier.IsActive();
        Im.Line.Same();
        using (ImGuiColor.Text.Push(Colors.RegexWarningBorder))
        {
            if (ImEx.Button("全部移除"u8, default, !active))
                reservedFiles.RemoveRedundant(cache, true);
        }

        if (Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
        {
            using var tt = Im.Tooltip.Begin();
            Im.Text("执行此操作将"u8);
            Im.BulletText("移除所有列出的文件替换(File Swaps)，因为这些配置不合理。"u8);
            Im.BulletText(
                "同时移除所有标记为“不同（Different）”的重定向。带有这些重定向的模组可能已经无法正常工作，但移除重定向本身不会改变这一现状。"u8);
            Im.Text("\n此操作不可撤销。"u8, Colors.RegexWarningBorder);
            Im.Text(
                "\n执行此操作后，再次扫描时，此标签页应不再显示任何重定向且所有警告都会消失。但除非您事先记录，否则您将无法得知哪些模组此前受到了影响或现在可能已损坏。"u8,
                Colors.RegexWarningBorder);

            if (!active)
                Im.Text($"\n点击时按住 {config.DeleteModModifier} 键。");
        }
    }

    protected override ScannerTabCache<ReservedFileCacheObject, ReservedFileRedirection> CreateCache()
        => new Cache(mods, textures, log, this);

    private sealed class Cache(ModManager mods, TextureManager textures, ManagementLog<ReservedFiles> log, ReservedFilesTable parent)
        : ScannerTabCache<ReservedFileCacheObject, ReservedFileRedirection>(parent, new ReservedFileScanner(mods, textures, log))
    {
        protected override ReservedFileCacheObject Convert(ReservedFileRedirection obj)
            => new(obj);
    }

    private sealed class ActionColumn : BasicColumn<ReservedFileCacheObject>
    {
        private readonly ReservedFiles _service;
        private readonly Configuration       _config;
        private          int                 _deleteIndex = -1;

        public ActionColumn(ReservedFiles service, Configuration config)
        {
            _service =  service;
            _config  =  config;
            Flags    |= TableColumnFlags.NoSort | TableColumnFlags.NoResize;
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
                _service.DeleteItem(item.ScannedObject);
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
    }

    private sealed class ContainerColumn(UiNavigator navigator) : ModColumn<ReservedFileCacheObject>(navigator)
    {
        protected override Mod? GetMod(in ReservedFileCacheObject item, int globalIndex)
            => item.ScannedObject.Container.TryGetTarget(out var c) ? c.Mod as Mod : null;

        protected override StringPair GetModName(in ReservedFileCacheObject item, int globalIndex)
            => item.Container;

        private string _lastContainer = string.Empty;

        protected override bool MatchesLastItem(in ReservedFileCacheObject item)
        {
            var ret = base.MatchesLastItem(item) && _lastContainer == item.Container.Utf16;
            _lastContainer = item.Container.Utf16;
            return ret;
        }

        public override void PostDraw(in TableCache<ReservedFileCacheObject> cache)
        {
            base.PostDraw(cache);
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
}
