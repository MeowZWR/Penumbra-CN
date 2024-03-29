﻿using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterTex;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly TextureManager _textures;

    private readonly Texture                       _left  = new();
    private readonly Texture                       _right = new();
    private readonly CombinedTexture               _center;
    private readonly TextureDrawer.PathSelectCombo _textureSelectCombo;

    private bool _overlayCollapsed = true;
    private bool _addMipMaps       = true;
    private int  _currentSaveAs;

    private static readonly (string, string)[] SaveAsStrings =
    {
        ("保持原样", "尽可能地将当前纹理保存为其自身格式，不作额外的转换或压缩。"),
        ("RGBA (未压缩)",
            "将当前纹理保存为未压缩的BGRA位图。文件体积最大，但在技术上能提供最好的质量。"),
        ("BC3 (简单压缩)",
            "将当前纹理保存为BC3/DXT5压缩格式。提供4:1的压缩比，速度快质量也可接受。"),
        ("BC7 (复杂压缩)",
            "将当前纹理保存为BC7压缩格式。提供4:1的压缩比，并且具有与未压缩格式几乎相同的质量，但要花一些时间。"),
    };

    private void DrawInputChild(string label, Texture tex, Vector2 size, Vector2 imageSize)
    {
        using (var child = ImRaii.Child(label, size, true))
        {
            if (!child)
                return;

            using var id = ImRaii.PushId(label);
            ImGuiUtil.DrawTextButton(label, new Vector2(-1, 0), ImGui.GetColorU32(ImGuiCol.FrameBg));
            ImGui.NewLine();

            using (var disabled = ImRaii.Disabled(!_center.SaveTask.IsCompleted))
            {
                TextureDrawer.PathInputBox(_textures, tex, ref tex.TmpPath, "##input", "导入图像...",
                    "可以导入游戏路径以及你自己的文件。", Mod!.ModPath.FullName, _fileDialog, _config.DefaultModImportPath);
                if (_textureSelectCombo.Draw("##combo",
                        "选择在你驱动器上的模组包含的纹理，或游戏文件中替换的纹理。", tex.Path,
                        Mod.ModPath.FullName.Length + 1, out var newPath)
                 && newPath != tex.Path)
                    tex.Load(_textures, newPath);

                if (tex == _left)
                    _center.DrawMatrixInputLeft(size.X);
                else
                    _center.DrawMatrixInputRight(size.X);
            }

            ImGui.NewLine();
            using var child2 = ImRaii.Child("图像");
            if (child2)
                TextureDrawer.Draw(tex, imageSize);
        }

        if (_dragDropManager.CreateImGuiTarget("TextureDragDrop", out var files, out _) && GetFirstTexture(files, out var file))
            tex.Load(_textures, file);
    }

    private void SaveAsCombo()
    {
        var (text, desc) = SaveAsStrings[_currentSaveAs];
        ImGui.SetNextItemWidth(-ImGui.GetFrameHeight() - ImGui.GetStyle().ItemSpacing.X);
        using var combo = ImRaii.Combo("##format", text);
        ImGuiUtil.HoverTooltip(desc);
        if (!combo)
            return;

        foreach (var ((newText, newDesc), idx) in SaveAsStrings.WithIndex())
        {
            if (ImGui.Selectable(newText, idx == _currentSaveAs))
                _currentSaveAs = idx;

            ImGuiUtil.SelectableHelpMarker(newDesc);
        }
    }

    private void RedrawOnSaveBox()
    {
        var redraw = _config.Ephemeral.ForceRedrawOnFileChange;
        if (ImGui.Checkbox("Redraw on Save", ref redraw))
        {
            _config.Ephemeral.ForceRedrawOnFileChange = redraw;
            _config.Ephemeral.Save();
        }

        ImGuiUtil.HoverTooltip("Force a redraw of your player character whenever you save a file here.");
    }

    private void MipMapInput()
    {
        ImGui.Checkbox("##mipMaps", ref _addMipMaps);
        ImGuiUtil.HoverTooltip(
            "将适当数量的MipMaps添加到文件。" );
    }

    private bool _forceTextureStartPath = true;

    private void DrawOutputChild(Vector2 size, Vector2 imageSize)
    {
        using var child = ImRaii.Child("输出", size, true);
        if (!child)
            return;

        if (_center.IsLoaded)
        {
            RedrawOnSaveBox();
            ImGui.SameLine();
            SaveAsCombo();
            ImGui.SameLine();
            MipMapInput();

            var canSaveInPlace = Path.IsPathRooted(_left.Path) && _left.Type is TextureType.Tex or TextureType.Dds or TextureType.Png;
            var isActive       = _config.DeleteModModifier.IsActive();
            var tt = isActive
                ? "在原路径下保存直接覆盖原文件，此操作无法恢复。"
                : $"在原路径下保存直接覆盖原文件，此操作无法恢复。按住{_config.DeleteModModifier}进行保存。";

            var buttonSize2 = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2, 0);
            if (ImGuiUtil.DrawDisabledButton("覆盖保存", buttonSize2,
                    tt, !isActive || !canSaveInPlace || _center.IsLeftCopy && _currentSaveAs == (int)CombinedTexture.TextureSaveType.AsIs))
            {
                _center.SaveAs(_left.Type, _textures, _left.Path, (CombinedTexture.TextureSaveType)_currentSaveAs, _addMipMaps);
                InvokeChange(Mod, _left.Path);
                AddReloadTask(_left.Path, false);
            }

            ImGui.SameLine();
            if (ImGui.Button("保存为TEX格式", buttonSize2))
                OpenSaveAsDialog(".tex");

            if (ImGui.Button("导出为PNG格式", buttonSize2))
                OpenSaveAsDialog(".png");
            ImGui.SameLine();
            if (ImGui.Button("导出为DDS格式", buttonSize2))
                OpenSaveAsDialog(".dds");

            ImGui.NewLine();

            var canConvertInPlace = canSaveInPlace && _left.Type is TextureType.Tex && _center.IsLeftCopy;

            var buttonSize3 = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X * 2) / 3, 0);
            if (ImGuiUtil.DrawDisabledButton("转换为BC7", buttonSize3,
                    "将此纹理转换为BC7压缩格式并覆盖原文件，此操作无法恢复。",
                    !canConvertInPlace || _left.Format is DXGIFormat.BC7Typeless or DXGIFormat.BC7UNorm or DXGIFormat.BC7UNormSRGB))
            {
                _center.SaveAsTex(_textures, _left.Path, CombinedTexture.TextureSaveType.BC7, _left.MipMaps > 1);
                InvokeChange(Mod, _left.Path);
                AddReloadTask(_left.Path, false);
            }

            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton("转换为BC3", buttonSize3,
                    "将此纹理转换为BC3压缩格式并覆盖原文件，此操作无法恢复。",
                    !canConvertInPlace || _left.Format is DXGIFormat.BC3Typeless or DXGIFormat.BC3UNorm or DXGIFormat.BC3UNormSRGB))
            {
                _center.SaveAsTex(_textures, _left.Path, CombinedTexture.TextureSaveType.BC3, _left.MipMaps > 1);
                InvokeChange(Mod, _left.Path);
                AddReloadTask(_left.Path, false);
            }

            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton("转换为RGBA", buttonSize3,
                    "将此纹理转换为RGBA压缩格式并覆盖原文件，此操作无法恢复。",
                    !canConvertInPlace
                 || _left.Format is DXGIFormat.B8G8R8A8UNorm or DXGIFormat.B8G8R8A8Typeless or DXGIFormat.B8G8R8A8UNormSRGB))
            {
                _center.SaveAsTex(_textures, _left.Path, CombinedTexture.TextureSaveType.Bitmap, _left.MipMaps > 1);
                InvokeChange(Mod, _left.Path);
                AddReloadTask(_left.Path, false);
            }
        }

        switch (_center.SaveTask.Status)
        {
            case TaskStatus.WaitingForActivation:
            case TaskStatus.WaitingToRun:
            case TaskStatus.Running:
                ImGuiUtil.DrawTextButton("Computing...", -Vector2.UnitX, Colors.PressEnterWarningBg);

                break;
            case TaskStatus.Canceled:
            case TaskStatus.Faulted:
            {
                ImGui.TextUnformatted("Could not save file:");
                using var color = ImRaii.PushColor(ImGuiCol.Text, 0xFF0000FF);
                ImGuiUtil.TextWrapped(_center.SaveTask.Exception?.ToString() ?? "Unknown Error");
                break;
            }
            default:
                ImGui.Dummy(new Vector2(1, ImGui.GetFrameHeight()));
                break;
        }

        ImGui.NewLine();

        using var child2 = ImRaii.Child("图像");
        if (child2)
            _center.Draw(_textures, imageSize);
    }

    private void InvokeChange(Mod? mod, string path)
    {
        if (mod == null)
            return;

        if (!_editor.Files.Tex.FindFirst(r => string.Equals(r.File.FullName, path, StringComparison.OrdinalIgnoreCase),
                out var registry))
            return;

        _communicator.ModFileChanged.Invoke(mod, registry);
    }

    private void OpenSaveAsDialog(string defaultExtension)
    {
        var fileName = Path.GetFileNameWithoutExtension(_left.Path.Length > 0 ? _left.Path : _right.Path);
        _fileDialog.OpenSavePicker("保存纹理为TEX、DDS或PNG格式...", "纹理{.png,.dds,.tex},.tex,.dds,.png", fileName, defaultExtension,
            (a, b) =>
            {
                if (a)
                {
                    _center.SaveAs(null, _textures, b, (CombinedTexture.TextureSaveType)_currentSaveAs, _addMipMaps);
                    InvokeChange(Mod, b);
                    if (b == _left.Path)
                        AddReloadTask(_left.Path, false);
                    else if (b == _right.Path)
                        AddReloadTask(_right.Path, true);
                }
            }, Mod!.ModPath.FullName, _forceTextureStartPath);
        _forceTextureStartPath = false;
    }

    private void AddReloadTask(string path, bool right)
    {
        _center.SaveTask.ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully)
                return;

            var tex = right ? _right : _left;

            if (tex.Path != path)
                return;

            _framework.RunOnFrameworkThread(() => tex.Reload(_textures));
        }, TaskScheduler.Default);
    }

    private Vector2 GetChildWidth()
    {
        var windowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - ImGui.GetTextLineHeight();
        if (_overlayCollapsed)
        {
            var width = windowWidth - ImGui.GetStyle().FramePadding.X * 3;
            return new Vector2(width / 2, -1);
        }

        return new Vector2((windowWidth - ImGui.GetStyle().FramePadding.X * 5) / 3, -1);
    }

    private void DrawTextureTab()
    {
        using var tab = ImRaii.TabItem("纹理");
        if (!tab)
            return;

        try
        {
            _dragDropManager.CreateImGuiSource("TextureDragDrop",
                m => m.Extensions.Any(e => ValidTextureExtensions.Contains(e.ToLowerInvariant())), m =>
                {
                    if (!GetFirstTexture(m.Files, out var file))
                        return false;

                    ImGui.TextUnformatted($"拖拽纹理进行编辑： {Path.GetFileName(file)}");
                    return true;
                });
            var childWidth = GetChildWidth();
            var imageSize  = new Vector2(childWidth.X - ImGui.GetStyle().FramePadding.X * 2);
            DrawInputChild( "输入纹理", _left, childWidth, imageSize);
            ImGui.SameLine();
            DrawOutputChild(childWidth, imageSize);
            if (!_overlayCollapsed)
            {
                ImGui.SameLine();
                DrawInputChild( "覆盖纹理", _right, childWidth, imageSize);
            }

            ImGui.SameLine();
            DrawOverlayCollapseButton();
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"在绘制纹理时发生了未知错误：\n{e}");
        }
    }

    private void DrawOverlayCollapseButton()
    {
        var (label, tooltip) = _overlayCollapsed
            ? (">", "显示覆盖纹理面板，可以在其中导入其他纹理作为主纹理的覆盖。")
            : ("<", "隐藏覆盖纹理面板并清除当前加载的覆盖纹理（如果有）。");
        if (ImGui.Button(label, new Vector2(ImGui.GetTextLineHeight(), ImGui.GetContentRegionAvail().Y)))
            _overlayCollapsed = !_overlayCollapsed;

        ImGuiUtil.HoverTooltip(tooltip);
    }

    private static bool GetFirstTexture(IEnumerable<string> files, [NotNullWhen(true)] out string? file)
    {
        file = files.FirstOrDefault(f => ValidTextureExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        return file != null;
    }

    private static readonly string[] ValidTextureExtensions =
    {
        ".png",
        ".dds",
        ".tex",
    };
}
