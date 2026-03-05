using ImSharp;
using Luna;
using OtterTex;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;
using TextureType = Penumbra.Import.Textures.TextureType;

namespace Penumbra.UI.FileEditing.Textures;

public partial class CombiningTextureEditor
{
    private static readonly (StringU8, StringU8)[] SaveAsStrings =
    {
        (new StringU8("保持现状 (As Is)"u8),
            new StringU8("尽量按当前纹理的原始格式保存，不进行额外的转换或压缩。"u8)),
        (new StringU8("RGBA (无压缩)"u8),
            new StringU8(
                "将当前纹理保存为未压缩的 BGRA 位图。\n虽然占用空间最大，但在技术上能提供最佳画质。"u8)),
        (new StringU8("BC1 (不透明 RGB 简单压缩)"u8),
            new StringU8(
                "使用 BC1/DXT1 算法压缩纹理。\n提供 8:1 的压缩比，速度快且画质尚可，但仅支持 RGB 通道，不支持透明度 (Alpha)。\n\n常用于漫反射贴图 (Diffuse) 和装备纹理以节省空间。"u8)),
        (new StringU8("BC3 (带有 Alpha 的简单压缩)"u8),
            new StringU8(
                "使用 BC3/DXT5 算法压缩纹理。\n提供 4:1 的压缩比，速度快且画质尚可，完全支持 RGBA 通道。\n\n通用格式，适用于绝大多数纹理。"u8)),
        (new StringU8("BC4 (不透明灰度简单压缩)"u8),
            new StringU8(
                "使用 BC4 算法压缩纹理。\n提供 8:1 的压缩比，画质几乎无损，但仅支持灰度图，不支持透明度。\n\n常用于面部彩绘 (Face paints) 和旧版印记 (Legacy marks)。"u8)),
        (new StringU8("BC5 (不透明 RG 简单压缩)"u8),
            new StringU8(
                "使用 BC5 算法压缩纹理。\n提供 4:1 的压缩比，画质几乎无损，但仅支持 RG 通道，不支持 B 或透明度。\n\n推荐用于索引贴图 (Index maps)，不推荐用于法线贴图。"u8)),
        (new StringU8("BC7 (高质量通用压缩)"u8),
            new StringU8(
                "使用 BC7 算法压缩纹理。\n提供 4:1 的压缩比，画质极高（几乎无损），但转换耗时可能较长。\n\n现代通用格式，适用于绝大多数纹理。"u8)),
    };

    private bool _overlayCollapsed = true;

    bool IFileEditor.DrawToolbar(bool disabled)
        => false;

    public bool DrawPanel(bool disabled)
    {
        try
        {
            _dragDropManager.CreateImGuiSource("TextureDragDrop",
                m => m.Extensions.Any(e => ValidTextureExtensions.Contains(e.ToLowerInvariant())), m =>
                {
                    if (!GetFirstTexture(m.Files, out var file))
                        return false;

                    Im.Text($"拖拽纹理进行编辑: {Path.GetFileName(file)}");
                    return true;
                });
            var childWidth = GetChildWidth();
            var imageSize  = new Vector2(childWidth.X - Im.Style.FramePadding.X * 2);
            DrawInputChild("输入纹理"u8, _left, childWidth, imageSize);
            Im.Line.Same();
            DrawOutputChild(childWidth, imageSize);
            if (!_overlayCollapsed)
            {
                Im.Line.Same();
                DrawInputChild("叠加纹理"u8, _right, childWidth, imageSize);
            }

            Im.Line.Same();
            DrawOverlayCollapseButton();
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Unknown Error while drawing textures:\n{e}");
        }

        return false;
    }

    private Vector2 GetChildWidth()
    {
        var windowWidth = Im.Window.MaximumContentRegion.X - Im.Window.MinimumContentRegion.X - Im.Style.TextHeight;
        if (_overlayCollapsed)
        {
            var width = windowWidth - Im.Style.FramePadding.X * 3;
            return new Vector2(width / 2, -1);
        }

        return new Vector2((windowWidth - Im.Style.FramePadding.X * 5) / 3, -1);
    }

    private void DrawInputChild(ReadOnlySpan<byte> label, Texture tex, Vector2 size, Vector2 imageSize)
    {
        using (var child = Im.Child.Begin(label, size, true))
        {
            if (!child)
                return;

            using var id = Im.Id.Push(label);
            ImEx.TextFramed(label, Im.ContentRegion.Available with { Y = 0 }, ImGuiColor.FrameBackground.Get());
            Im.Line.New();

            using (Im.Disabled(!_center.SaveTask.IsCompleted))
            {
                if (tex != _left || _inModEditWindow)
                {
                    TextureDrawer.PathInputBox(_textures, tex, ref tex.TmpPath, "##input"u8, "导入图像..."u8,
                        "既可以导入游戏内部路径，也可以导入您自己的本地文件。"u8, _context?.Mod?.ModPath.FullName, _fileDialog,
                        _config.DefaultModImportPath);
                    if (_textureSelectCombo is not null
                     && _textureSelectCombo.Draw("##combo"u8,
                            "请选择此模组文件夹内包含的纹理，或选择它们在游戏文件中所替换的原始纹理。"u8, tex.Path,
                            _context?.Mod?.ModPath.FullName.Length + 1 ?? 0, out var newPath)
                     && newPath != tex.Path)
                        tex.Load(_textures, newPath);
                }

                if (tex == _left)
                    _center.DrawMatrixInputLeft(size.X);
                else
                    _center.DrawMatrixInputRight(size.X);
            }

            Im.Line.New();
            using var child2 = Im.Child.Begin("image"u8);
            if (child2)
                TextureDrawer.Draw(tex, imageSize);
        }

        if (_dragDropManager.CreateImGuiTarget("TextureDragDrop", out var files, out _) && GetFirstTexture(files, out var file))
            tex.Load(_textures, file);
    }


    private void SaveAsCombo()
    {
        var (text, desc) = SaveAsStrings[_currentSaveAs];
        Im.Item.SetNextWidth(-Im.Style.FrameHeight - Im.Style.ItemSpacing.X);
        using var combo = Im.Combo.Begin("##format"u8, text);
        Im.Tooltip.OnHover(desc);
        if (!combo)
            return;

        foreach (var (idx, (newText, newDesc)) in SaveAsStrings.Index())
        {
            if (Im.Selectable(newText, idx == _currentSaveAs))
                _currentSaveAs = idx;

            LunaStyle.DrawRightAlignedHelpMarker(newDesc);
        }
    }

    private void RedrawOnSaveBox()
    {
        var redraw = _config.Ephemeral.ForceRedrawOnFileChange;
        if (Im.Checkbox("保存时重绘"u8, ref redraw))
        {
            _config.Ephemeral.ForceRedrawOnFileChange = redraw;
            _config.Ephemeral.Save();
        }

        Im.Tooltip.OnHover("每当您在此保存文件时，强制重新绘制您的玩家角色。"u8);
    }

    private void MipMapInput()
    {
        Im.Checkbox("##mipMaps"u8, ref _addMipMaps);
        Im.Tooltip.OnHover("为文件添加适当数量的 MipMaps。"u8);
    }

    private bool _forceTextureStartPath = true;

    private void DrawOutputChild(Vector2 size, Vector2 imageSize)
    {
        using var child = Im.Child.Begin("Output"u8, size, true);
        if (!child)
            return;

        if (_center.IsLoaded)
        {
            RedrawOnSaveBox();
            Im.Line.Same();
            SaveAsCombo();
            Im.Line.Same();
            MipMapInput();

            var canSaveInPlace = Path.IsPathRooted(_left.Path)
             && _left.Type is TextureType.Tex or TextureType.Dds or TextureType.Png
             && _writable;
            var isActive    = _config.DeleteModModifier.IsActive();
            var buttonSize2 = new Vector2((Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X) / 2,     0);
            var buttonSize3 = new Vector2((Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X * 2) / 3, 0);

            if (_inModEditWindow)
            {
                if (ImEx.Button("覆盖原文件保存"u8, buttonSize2,
                        isActive
                            ? "将纹理保存并覆盖原文件。此操作不可撤销。"u8
                            : $"将纹理保存并覆盖原文件。此操作不可撤销。按住 {_config.DeleteModModifier} 键以保存。",
                        !isActive || !canSaveInPlace || _center.IsLeftCopy && _currentSaveAs is (int)CombinedTexture.TextureSaveType.AsIs))
                    SaveRequested?.Invoke();

                Im.Line.Same();
                if (Im.Button("保存为 TEX"u8, buttonSize2))
                    OpenSaveAsDialog(".tex");
            }

            if (Im.Button("导出为 TGA"u8, buttonSize3))
                OpenSaveAsDialog(".tga");
            Im.Line.Same();
            if (Im.Button("导出为 PNG"u8, buttonSize3))
                OpenSaveAsDialog(".png");
            Im.Line.Same();
            if (Im.Button("导出为 DDS"u8, buttonSize3))
                OpenSaveAsDialog(".dds");
            Im.Line.New();

            var canConvertInPlace = canSaveInPlace && _left.Type is TextureType.Tex or TextureType.Dds && _center.IsLeftCopy;

            if (ImEx.Button("转换为 BC7"u8, buttonSize3,
                    "将此纹理直接转换为 BC7 格式。此操作不可撤销。"u8,
                    !canConvertInPlace || _left.Format is DXGIFormat.BC7Typeless or DXGIFormat.BC7UNorm or DXGIFormat.BC7UNormSRGB))
            {
                _nextSaveAs     = CombinedTexture.TextureSaveType.BC7;
                _nextAddMipMaps = _left.MipMaps > 1;
                SaveRequested?.Invoke();
            }

            Im.Line.Same();
            if (ImEx.Button("转换为 BC3"u8, buttonSize3,
                    "将此纹理直接转换为 BC3 格式。此操作不可撤销。"u8,
                    !canConvertInPlace || _left.Format is DXGIFormat.BC3Typeless or DXGIFormat.BC3UNorm or DXGIFormat.BC3UNormSRGB))
            {
                _nextSaveAs     = CombinedTexture.TextureSaveType.BC3;
                _nextAddMipMaps = _left.MipMaps > 1;
                SaveRequested?.Invoke();
            }

            Im.Line.Same();
            if (ImEx.Button("转换为 RGBA"u8, buttonSize3,
                    "将此纹理直接转换为 RGBA 格式。此操作不可撤销。"u8,
                    !canConvertInPlace
                 || _left.Format is DXGIFormat.B8G8R8A8UNorm or DXGIFormat.B8G8R8A8Typeless or DXGIFormat.B8G8R8A8UNormSRGB))
            {
                _nextSaveAs     = CombinedTexture.TextureSaveType.Bitmap;
                _nextAddMipMaps = _left.MipMaps > 1;
                SaveRequested?.Invoke();
            }
        }

        switch (_center.SaveTask.Status)
        {
            case TaskStatus.WaitingForActivation:
            case TaskStatus.WaitingToRun:
            case TaskStatus.Running:
                ImEx.TextFramed("计算中..."u8, Im.ContentRegion.Available with { Y = 0 }, Colors.PressEnterWarningBg);
                break;
            case TaskStatus.Canceled:
            case TaskStatus.Faulted:
            {
                Im.Text("无法保存文件："u8);
                using var color = ImGuiColor.Text.Push(new Vector4(1, 0, 0, 1));
                Im.TextWrapped(_center.SaveTask.Exception?.ToString() ?? "未知错误");
                break;
            }
            default: Im.Dummy(new Vector2(1, Im.Style.FrameHeight)); break;
        }

        Im.Line.New();

        using var child2 = Im.Child.Begin("image"u8);
        if (child2)
            _center.Draw(_textures, imageSize);
    }

    private void OpenSaveAsDialog(string defaultExtension)
    {
        var fileName = Path.GetFileNameWithoutExtension(_left.Path.Length > 0 ? _left.Path : _right.Path);
        _fileDialog.OpenSavePicker("保存纹理为 TEX, DDS, PNG 或 TGA...", "Textures{.png,.dds,.tex,.tga},.tex,.dds,.png,.tga", fileName,
            defaultExtension,
            (a, b) =>
            {
                if (a)
                {
                    _center.SaveAs(null, _textures, b, (CombinedTexture.TextureSaveType)_currentSaveAs, _addMipMaps);
                    AddPostSaveTask(b);
                }
            }, _context?.Mod?.ModPath.FullName, _forceTextureStartPath);
        _forceTextureStartPath = false;
    }

    private void AddPostSaveTask(string path)
    {
        _center.SaveTask.ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully)
                return;

            if (_context?.Mod is not null
             || _left.Path.Equals(path, StringComparison.OrdinalIgnoreCase)
             || _right.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                _framework.RunOnFrameworkThread(() =>
                {
                    InvokeChange(path);
                    if (_left.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                        _left.Reload(_textures);
                    if (_right.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                        _right.Reload(_textures);
                });
        }, TaskScheduler.Default);
    }

    private void InvokeChange(string path)
    {
        if (!_modManager.TryIdentifyPath(path, out var mod, out var relPathStr) || !Utf8RelPath.FromString(relPathStr, out var relPath))
            return;

        _communicator.ModFileChanged.Invoke(new ModFileChanged.Arguments(mod, relPath,
            _context?.TryFindFileRegistry(ResourceType.Tex, mod, relPath)));
    }

    private void DrawOverlayCollapseButton()
    {
        var (label, tooltip) = _overlayCollapsed
            ? RefTuple.Create(">"u8,
                "显示一个第三面板，您可以在其中导入额外的纹理作为主纹理的叠加层。"u8)
            : RefTuple.Create("<"u8, "隐藏叠加纹理面板并清除当前加载的叠加纹理（如果存在）。"u8);
        if (Im.Button(label, Im.ContentRegion.Available with { X = Im.Style.TextHeight }))
            _overlayCollapsed = !_overlayCollapsed;

        Im.Tooltip.OnHover(tooltip);
    }

    private static bool GetFirstTexture(IEnumerable<string> files, [NotNullWhen(true)] out string? file)
    {
        file = files.FirstOrDefault(f => ValidTextureExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        return file != null;
    }

    private static readonly string[] ValidTextureExtensions =
    [
        ".png",
        ".dds",
        ".tex",
        ".tga",
    ];
}
