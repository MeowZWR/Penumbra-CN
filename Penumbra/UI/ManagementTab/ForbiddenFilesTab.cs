using System.Collections.Frozen;
using ImSharp;
using Luna;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class ForbiddenFilesTab(ModManager mods, TextureManager textures, UiNavigator navigator, Configuration config)
    : ITab<ManagementTabType>
{
    public static readonly FrozenDictionary<uint, CiByteString> ForbiddenFiles = (((uint, CiByteString)[])
    [
        (0x90E4EE2F, new CiByteString("common/graphics/texture/dummy.tex"u8,    MetaDataComputation.All)),
        (0x84815A1A, new CiByteString("chara/common/texture/white.tex"u8,       MetaDataComputation.All)),
        (0x749091FB, new CiByteString("chara/common/texture/black.tex"u8,       MetaDataComputation.All)),
        (0x5CB9681A, new CiByteString("chara/common/texture/id_16.tex"u8,       MetaDataComputation.All)),
        (0x7E78D000, new CiByteString("chara/common/texture/red.tex"u8,         MetaDataComputation.All)),
        (0xBDC0BFD3, new CiByteString("chara/common/texture/green.tex"u8,       MetaDataComputation.All)),
        (0xC410E850, new CiByteString("chara/common/texture/blue.tex"u8,        MetaDataComputation.All)),
        (0xD5CFA221, new CiByteString("chara/common/texture/null_normal.tex"u8, MetaDataComputation.All)),
        (0xBE48CA67, new CiByteString("chara/common/texture/skin_mask.tex"u8,   MetaDataComputation.All)),
    ]).ToFrozenDictionary(p => p.Item1, p =>
    {
        Debug.Assert((uint)p.Item2.Crc32 == p.Item1,
            $"Invalid hash computation in forbidden files for {p.Item2} ({p.Item1:X} vs {p.Item2.Crc32:X}).");
        return p.Item2;
    });

    public void PostTabButton()
    {
        if (Im.Item.Hovered())
            DrawTooltip();
    }

    private static void DrawTooltip()
    {
        Im.Window.SetNextSize(ImEx.ScaledVectorX(800));
        using var tt = Im.Tooltip.Begin();
        Im.TextWrapped(
            "被禁止的文件在游戏中被广泛使用，并具有非常特定的语义，因此操纵它们通常会导致意想不到的副作用。允许重定向这些文件会导致图形错误（最好的情况），导致游戏崩溃或无限挂起（最坏的情况）。\n\n被禁止的文件并不多，即使没有修复，它们也会被阻止应用，因此如果您不确定如何修复模组，您不需要过多担心此警告。\n\n被禁止的文件包括："u8);
        using (Im.Group())
        {
            foreach (var name in ForbiddenFiles.Values)
                Im.BulletText(name.Span);
        }

        Im.Line.Same();
        using (Im.Group())
        {
            foreach (var id in ForbiddenFiles.Keys)
                Im.Text(Description(id));
        }

        return;

        static ReadOnlySpan<byte> Description(uint hash)
        {
            return hash switch
            {
                0x90E4EE2F => "应为最小尺寸的纯白纹理。"u8,
                0x84815A1A => "必须是全不透明的纯白方块。"u8,
                0x749091FB => "必须是全不透明的纯黑方块。"u8,
                0x5CB9681A => "用作默认 ID 映射，必须是全不透明的 #780000 纯色方块。"u8,
                0x7E78D000 => "必须是全不透明的纯红方块。"u8,
                0xBDC0BFD3 => "必须是全不透明的纯绿方块。"u8,
                0xC410E850 => "必须是全不透明的纯蓝方块。"u8,
                0xD5CFA221 => "用作默认法线贴图，必须是全不透明的 #7E7FFF 纯色方块。"u8,
                0xBE48CA67 => "用作默认皮肤蒙版，必须是全不透明的 #A5749A 纯色方块。"u8,
                _          => StringU8.Empty,
            };
        }
    }


    public ReadOnlySpan<byte> Label
        => "被禁止的文件"u8;

    private sealed class Cache(ModManager mods, TextureManager textures) : BasicCache
    {
        public sealed class ForbiddenFileRedirection(
            Utf8GamePath path,
            FullPath redirection,
            IModDataContainer container,
            bool swap,
            bool missing,
            bool conceptuallyEqual)
            : BaseScannedRedirection(path, redirection, container, swap)
        {
            public readonly bool Missing           = missing;
            public readonly bool ConceptuallyEqual = conceptuallyEqual;
        }

        public sealed class ForbiddenFileScanner(ModManager mods, TextureManager textures) : RedirectionScanner<ForbiddenFileRedirection>(mods)
        {
            public FrozenDictionary<uint, Rgba32>? OriginalFiles;

            protected override ForbiddenFileRedirection Create(Utf8GamePath path, FullPath redirection, IModDataContainer container, bool swap)
            {
                if (swap)
                    return new ForbiddenFileRedirection(path, redirection, container, true, false, false);

                try
                {
                    if (!File.Exists(redirection.FullName))
                        return new ForbiddenFileRedirection(path, redirection, container, false, false, false);

                    var data             = textures.LoadTex(redirection.FullName);
                    var redirectionColor = data.IsSolidColor();
                    return new ForbiddenFileRedirection(path, redirection, container, false, false,
                        ContextuallyEqual((uint)path.Path.Crc32, redirectionColor));
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Warning($"Could not read forbidden file redirection file {redirection}:\n{ex}");
                    return new ForbiddenFileRedirection(path, redirection, container, false, false, false);
                }
            }

            private bool ContextuallyEqual(uint crc32, ColorParameter parameter)
            {
                if (parameter.IsDefault)
                    return false;

                var origColor = OriginalFiles![crc32];
                switch (crc32)
                {
                    case 0x90E4EE2F: // dummy.tex  
                    case 0x84815A1A: // white.tex  
                    case 0x7E78D000: // red.tex    
                    case 0xBDC0BFD3: // green.tex  
                    case 0x749091FB: // black.tex  
                    case 0xC410E850: // blue.tex   
                    case 0xBE48CA67: // skin_mask.tex - This can possibly be relaxed more?
                        return parameter.Color!.Value == origColor;
                    case 0x5CB9681A: // id_16.tex  
                        return parameter.Color!.Value.Color is >= 0xFF00006Fu and <= 0xFF00007Fu;
                    case 0xD5CFA221: // null_normal.tex
                        // red and green in ranges, blue and alpha full.
                        return (origColor.Color & 0xFF00u) is >= 0x7E00u and <= 0x8100u
                         && (origColor.Color & 0xFFFF00FFu) is >= 0xFFFF007Eu and <= 0xFFFF0081u;
                }

                return false;
            }

            protected override bool DoCreateRedirection(Utf8GamePath path, FullPath redirection, IModDataContainer container, bool swap)
                => ForbiddenFiles.ContainsKey((uint)path.Path.Crc32);
        }


        public readonly ForbiddenFileScanner Redirections = new(mods, textures);

        public override void Update()
        {
            Redirections.OriginalFiles ??= ForbiddenFiles.ToFrozenDictionary(kvp => kvp.Key, kvp =>
            {
                var file = textures.LoadTex(kvp.Value.ToString());
                var data = file.IsSolidColor();
                return data.IsDefault
                    ? throw new Exception($"Forbidden file {kvp.Value} could not be loaded from game files.")
                    : data.Color!.Value;
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Redirections.Dispose();
        }
    }

    public void DrawContent()
    {
        var hovered = LunaStyle.DrawAlignedHelpMarker();
        Im.Line.SameInner();
        ImEx.TextFrameAligned("什么是被禁止的文件？"u8);
        if (hovered || Im.Item.Hovered())
            DrawTooltip();

        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache(mods, textures));
        ManagementTab.DrawScanButtons(cache.Redirections);
        var active = config.DeleteModModifier.IsActive();

        if (ImEx.Button("删除所有冗余重定向"u8, default, !active))
            ;

        using var table = Im.Table.Begin("t"u8, 6,
            TableFlags.RowBackground | TableFlags.SizingFixedFit | TableFlags.BordersOuter | TableFlags.ScrollX | TableFlags.ScrollY,
            Im.ContentRegion.Available);
        if (!table)
            return;

        var       data = cache.Redirections.GetCurrentList();
        using var clip = new Im.ListClipper(data.Count, Im.Style.TextHeightWithSpacing);
        using var id   = new Im.IdDisposable();
        foreach (var (idx, redirection) in clip.Iterate(data).Index())
        {
            id.Push(idx);
            table.DrawColumn($"{redirection.GamePath}");
            table.DrawColumn(redirection.Redirection.FullName);
            if (redirection.Container.TryGetTarget(out var container))
            {
                table.NextColumn();
                if (Im.Selectable(container.Mod.Name))
                    navigator.MoveTo(container.Mod as Mod);

                table.DrawColumn(container.GetFullName());
            }
            else
            {
                table.DrawColumn("模组缺失"u8);
                table.NextColumn();
            }

            table.DrawColumn(redirection.FileSwap ? "S"u8 : "R"u8);
            table.DrawColumn(redirection.ConceptuallyEqual ? "EQUAL"u8 : "DIFF"u8);
            id.Pop();
        }
    }

    public ManagementTabType Identifier
        => ManagementTabType.ForbiddenFiles;
}
