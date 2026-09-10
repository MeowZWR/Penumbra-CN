using ImSharp;
using Luna;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ManagementTab;

public sealed class ReservedFilesTab(ModManager mods, TextureManager textures, UiNavigator navigator, ReservedFiles service, ManagementLog<ReservedFiles> log)
    : ITab<ManagementTabType>
{
    private readonly ReservedFilesTable _table = new(mods, textures, navigator, service, log);

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
            "保留文件在游戏中被广泛使用，并具有非常特定的语义，因此操纵它们通常会导致意想不到的副作用。如果允许重定向这些文件，轻则导致图形错误，重则导致游戏崩溃或无限挂起。\n\n保留文件并不多，即使没有修复，它们也会被阻止应用，因此如果您不确定如何修复模组，您不需要过多担心此警告。\n\n保留文件包括："u8);
        using (Im.Group())
        {
            foreach (var name in ReservedFiles.Files.Values)
                Im.BulletText(name.Span);
        }

        Im.Line.Same();
        using (Im.Group())
        {
            foreach (var id in ReservedFiles.Files.Keys)
                Im.Text(Description(id));
        }

        return;

        static ReadOnlySpan<byte> Description(uint hash)
        {
            return hash switch
            {
                0x90E4EE2F => "应为最小尺寸的纯白纹理。"u8,
                0x84815A1A => "必须是完全不透明的纯白方块。"u8,
                0x749091FB => "必须是完全不透明的纯黑方块。"u8,
                0x5CB9681A => "用作默认 ID 映射，必须是完全不透明的 #780000 纯色方块。"u8,
                0x2A583051 => "必须是完全不透明的纯红方块。"u8,
                0x7E78D000 => "必须是完全不透明的纯红方块。"u8,
                0xBDC0BFD3 => "必须是完全不透明的纯绿方块。"u8,
                0xC410E850 => "必须是完全不透明的纯蓝方块。"u8,
                0xD5CFA221 => "用作默认法线贴图，必须是完全不透明的 #7E7FFF 纯色方块。"u8,
                0xBE48CA67 => "用作默认皮肤蒙版，必须是完全不透明的 #A5749A 纯色方块。"u8,
                _          => StringU8.Empty,
            };
        }
    }
}
