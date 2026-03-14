using ImSharp;
using Lumina.Data.Files;
using Luna;
using OtterTex;
using Penumbra.UI.Classes;

namespace Penumbra.Import.Textures;

public static class TextureDrawer
{
    public static void Draw(Texture texture, Vector2 size)
    {
        if (texture.TextureWrap != null)
        {
            size = texture.TextureWrap.Size.Contain(size);

            Im.Image.Draw(texture.TextureWrap.Id, size);
            DrawData(texture);
        }
        else if (texture.LoadError != null)
        {
            const string link = "https://aka.ms/vcredist";
            Im.Text("无法加载文件："u8);

            if (texture.LoadError is DllNotFoundException)
            {
                Im.Text("无法找到纹理处理依赖。请安装最新的 Microsoft VC Redistributable。"u8,
                    Colors.RegexWarningBorder);
                if (Im.Button("Microsoft VC Redistributables"u8))
                    Dalamud.Utility.Util.OpenLink(link);
                Im.Tooltip.OnHover($"在浏览器中打开 {link}。");
            }

            Im.Text($"{texture.LoadError}", Colors.RegexWarningBorder);
        }
    }

    public static void PathInputBox(TextureManager textures, Texture current, ref string? tmpPath, ReadOnlySpan<byte> label,
        ReadOnlySpan<byte> hint, ReadOnlySpan<byte> tooltip,
        string? startPath, FileDialogService fileDialog, string defaultModImportPath)
    {
        tmpPath ??= current.Path;
        using var spacing = ImStyleDouble.ItemSpacing.PushX(Im.Style.GlobalScale * 3);
        Im.Item.SetNextWidth(-3 * Im.Style.FrameHeight - 9 * Im.Style.GlobalScale);
        if (ImEx.InputOnDeactivation.Text(label, tmpPath, out tmpPath, hint))
            current.Load(textures, tmpPath);

        Im.Tooltip.OnHover(tooltip);
        Im.Line.Same();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon))
        {
            if (defaultModImportPath.Length > 0)
                startPath = defaultModImportPath;

            void UpdatePath(bool success, List<string> paths)
            {
                if (success && paths.Count > 0)
                    current.Load(textures, paths[0]);
            }

            fileDialog.OpenFilePicker("打开图像...", "Textures{.png,.dds,.tex,.atex,.tga}", UpdatePath, 1, startPath, false);
        }

        Im.Line.Same();
        if (ImEx.Icon.Button(LunaStyle.RefreshIcon, "重新加载当前选中路径。"u8))
            current.Reload(textures);

        Im.Line.Same();
        if (ImEx.Icon.Button(LunaStyle.ToClipboardIcon, "Copy the currently selected path."u8))
            Im.Clipboard.Set(current.Path);
    }

    private static void DrawData(Texture texture)
    {
        using var table = Im.Table.Begin("##data"u8, 2, TableFlags.SizingFixedFit);
        table.DrawColumn("宽"u8);
        table.DrawColumn($"{texture.TextureWrap!.Width}");
        table.DrawColumn("高"u8);
        table.DrawColumn($"{texture.TextureWrap!.Height}");
        table.DrawColumn("文件类型"u8);
        table.DrawColumn($"{texture.Type}");
        table.DrawColumn("位图大小"u8);
        table.DrawColumn($"{FormattingFunctions.HumanReadableSize(texture.RgbaPixels.Length)} ({texture.RgbaPixels.Length} Bytes)");
        if (texture.TryGetRgbaSolidColor(out var color))
        {
            table.DrawColumn("Solid Color"u8);
            table.DrawColumn($"{color}");
        }

        switch (texture.BaseImage.Image)
        {
            case ScratchImage s:
                table.DrawColumn("格式"u8);
                table.DrawColumn($"{s.Meta.Format}");
                table.DrawColumn("Mip 级别"u8);
                table.DrawColumn($"{s.Meta.MipLevels}");
                table.DrawColumn("数据大小"u8);
                table.DrawColumn($"{FormattingFunctions.HumanReadableSize(s.Pixels.Length)} ({s.Pixels.Length} Bytes)");
                table.DrawColumn("图像数量"u8);
                table.DrawColumn($"{s.Images.Length}");
                break;
            case TexFile t:
                table.DrawColumn("格式"u8);
                table.DrawColumn($"{t.Header.Format}");
                table.DrawColumn("Mip 级别"u8);
                table.DrawColumn($"{t.Header.MipCount}");
                table.DrawColumn("数据大小"u8);
                table.DrawColumn($"{FormattingFunctions.HumanReadableSize(t.ImageData.Length)} ({t.ImageData.Length} Bytes)");
                break;
        }
    }
}
