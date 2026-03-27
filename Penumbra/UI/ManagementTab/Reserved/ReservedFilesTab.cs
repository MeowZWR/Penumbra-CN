using System.Collections.Frozen;
using ImSharp;
using Luna;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.Mods.Manager;
using Penumbra.String;

namespace Penumbra.UI.ManagementTab;

public sealed class ReservedFilesTab(ModManager mods, TextureManager textures, UiNavigator navigator, Configuration config)
    : ITab<ManagementTabType>
{
    private readonly ReservedFilesTable _table = new(mods, textures, navigator, config);

    public static readonly FrozenDictionary<uint, CiByteString> ReservedFiles = (((uint, CiByteString)[])
    [
        (0x90E4EE2F, new CiByteString("common/graphics/texture/dummy.tex"u8,    MetaDataComputation.All)),
        (0x84815A1A, new CiByteString("chara/common/texture/white.tex"u8,       MetaDataComputation.All)),
        (0x749091FB, new CiByteString("chara/common/texture/black.tex"u8,       MetaDataComputation.All)),
        (0x5CB9681A, new CiByteString("chara/common/texture/id_16.tex"u8,       MetaDataComputation.All)),
        (0x2A583051, new CiByteString("chara/common/texture/common_id.tex"u8,   MetaDataComputation.All)),
        (0x7E78D000, new CiByteString("chara/common/texture/red.tex"u8,         MetaDataComputation.All)),
        (0xBDC0BFD3, new CiByteString("chara/common/texture/green.tex"u8,       MetaDataComputation.All)),
        (0xC410E850, new CiByteString("chara/common/texture/blue.tex"u8,        MetaDataComputation.All)),
        (0xD5CFA221, new CiByteString("chara/common/texture/null_normal.tex"u8, MetaDataComputation.All)),
        (0xBE48CA67, new CiByteString("chara/common/texture/skin_mask.tex"u8,   MetaDataComputation.All)),
    ]).ToFrozenDictionary(p => p.Item1, p =>
    {
        Debug.Assert((uint)p.Item2.Crc32 == p.Item1,
            $"Invalid hash computation in reserved files for {p.Item2} ({p.Item1:X} vs {p.Item2.Crc32:X}).");
        return p.Item2;
    });

    public void PostTabButton()
    {
        if (Im.Item.Hovered())
            DrawTooltip();
    }

    public ReadOnlySpan<byte> Label
        => "保留文件"u8;

    public void DrawContent()
    {
        var hovered = LunaStyle.DrawAlignedHelpMarker();
        Im.Line.SameInner();
        ImEx.TextFrameAligned("什么是保留文件？"u8);
        if (hovered || Im.Item.Hovered())
            DrawTooltip();

        _table.Draw();
    }

    public ManagementTabType Identifier
        => ManagementTabType.ReservedFiles;

    private static void DrawTooltip()
    {
        Im.Window.SetNextSize(ImEx.ScaledVectorX(800));
        using var tt = Im.Tooltip.Begin();
        Im.TextWrapped(
            "保留文件在游戏中被广泛使用，并具有非常特定的语义，因此操纵它们通常会导致意想不到的副作用。允许重定向这些文件会导致图形错误（最好的情况），导致游戏崩溃或无限挂起（最坏的情况）。\n\n保留文件并不多，即使没有修复，它们也会被阻止应用，因此如果您不确定如何修复模组，您不需要过多担心此警告。\n\n保留文件包括："u8);
        using (Im.Group())
        {
            foreach (var name in ReservedFiles.Values)
                Im.BulletText(name.Span);
        }

        Im.Line.Same();
        using (Im.Group())
        {
            foreach (var id in ReservedFiles.Keys)
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
                0x2A583051 => "必须是全不透明的纯红方块。"u8,
                0x7E78D000 => "必须是全不透明的纯红方块。"u8,
                0xBDC0BFD3 => "必须是全不透明的纯绿方块。"u8,
                0xC410E850 => "必须是全不透明的纯蓝方块。"u8,
                0xD5CFA221 => "用作默认法线贴图，必须是全不透明的 #7E7FFF 纯色方块。"u8,
                0xBE48CA67 => "用作默认皮肤蒙版，必须是全不透明的 #A5749A 纯色方块。"u8,
                _          => StringU8.Empty,
            };
        }
    }
}
