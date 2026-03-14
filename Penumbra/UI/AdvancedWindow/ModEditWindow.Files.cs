using Dalamud.Interface;
using ImSharp;
using Luna;
using Penumbra.Mods.Editor;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly HashSet<FileRegistry> _selectedFiles = new(256);
    private readonly HashSet<Utf8GamePath> _cutPaths      = [];
    private          string                _fileFilter    = string.Empty;
    private          bool                  _showGamePaths = true;
    private          string                _gamePathEdit  = string.Empty;
    private          int                   _fileIdx       = -1;
    private          int                   _pathIdx       = -1;
    private          int                   _folderSkip;
    private          bool                  _overviewMode;
    private readonly OverviewTable         _overviewTable;

    private bool CheckFilter(FileRegistry registry)
        => _fileFilter.Length is 0 || registry.File.FullName.Contains(_fileFilter, StringComparison.OrdinalIgnoreCase);

    private bool CheckFilter((int, FileRegistry) p)
        => CheckFilter(p.Item2);

    /// <summary>
    /// 检查文件是否应该被隐藏（基于文件类型过滤设置）
    /// </summary>
    /// <param name="registry">文件注册信息</param>
    /// <returns>如果文件应该被隐藏则返回true</returns>
    private bool ShouldHideFile(FileRegistry registry)
    {
        var extension = Path.GetExtension(registry.File.FullName).ToLowerInvariant();
        return extension switch
        {
            ".dds" => _config.HideDdsFiles,
            ".png" => _config.HidePngFiles,
            ".jpg" or ".jpeg" => _config.HideJpegFiles,
            ".json" => _config.HideJsonFiles,
            ".tga" => _config.HideTgaFiles,
            ".bmp" => _config.HideBmpFiles,
            ".gif" => _config.HideGifFiles,
            ".tiff" or ".tif" => _config.HideTiffFiles,
            ".webp" => _config.HideWebpFiles,
            ".xcp" => _config.HideXcpFiles,
            _ => false
        };
    }

    /// <summary>
    /// 检查文件是否应该被隐藏（基于文件类型过滤设置）- 用于总览模式
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <returns>如果文件应该被隐藏则返回true</returns>
    private bool ShouldHideFile(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".dds" => _config.HideDdsFiles,
            ".png" => _config.HidePngFiles,
            ".jpg" or ".jpeg" => _config.HideJpegFiles,
            ".json" => _config.HideJsonFiles,
            ".tga" => _config.HideTgaFiles,
            ".bmp" => _config.HideBmpFiles,
            ".gif" => _config.HideGifFiles,
            ".tiff" or ".tif" => _config.HideTiffFiles,
            ".webp" => _config.HideWebpFiles,
            ".xcp" => _config.HideXcpFiles,
            _ => false
        };
    }

    private void DrawFileTab()
    {
        using var tab = Im.TabBar.BeginItem("文件重定向"u8);
        if (!tab)
            return;

        DrawOptionSelectHeader();
        DrawButtonHeader();

        if (!_overviewMode)
            DrawFileManagementNormal();

        using var child = Im.Child.Begin("##files"u8, Im.ContentRegion.Available, true);
        if (!child)
            return;

        if (_overviewMode)
            _overviewTable.Draw();
        else
            DrawFilesNormalMode();
    }


    private void DrawFilesNormalMode()
    {
        using var table = Im.Table.Begin("##table"u8, 1);
        if (!table)
            return;

        foreach (var (i, registry) in _editor.Files.Available.Index().Where(CheckFilter).Where(p => !ShouldHideFile(p.Item2)))
        {
            using var id = Im.Id.Push(i);
            table.NextColumn();

            DrawSelectable(registry, i);

            if (!_showGamePaths)
                continue;

            using var indent = Im.Indent(50f);
            for (var j = 0; j < registry.SubModUsage.Count; ++j)
            {
                var (subMod, gamePath) = registry.SubModUsage[j];
                if (subMod != _editor.Option)
                    continue;

                PrintGamePath(i, j, registry, subMod, gamePath);
            }

            PrintNewGamePath(i, registry, _editor.Option!);
        }
    }

    private static string DrawFileTooltip(FileRegistry registry, ColorId color)
    {
        var (text, groupCount) = color switch
        {
            ColorId.ConflictingMod => (null, 0),
            ColorId.NewMod         => ([registry.SubModUsage[0].Item1.GetName()], 1),
            ColorId.InheritedMod   => GetMulti(),
            _                      => (null, 0),
        };

        if (text is not null && Im.Item.Hovered())
        {
            using var tt = Im.Tooltip.Begin();
            using var c  = ImGuiColor.Text.PushDefault();
            Im.Text(StringU8.Join((byte)'\n', text));
        }


        return (groupCount, registry.SubModUsage.Count) switch
        {
            (0, 0)   => "(未使用)",
            (1, 1)   => "(使用了 1 次)",
            (1, > 1) => $"(在 1 组中使用了 {registry.SubModUsage.Count} 次)",
            _        => $"(在 {groupCount} 组中使用了 {registry.SubModUsage.Count} 次",
        };

        (IEnumerable<string>, int) GetMulti()
        {
            var groups = registry.SubModUsage.GroupBy(s => s.Item1).ToArray();
            return (groups.Select(g => g.Key.GetName()), groups.Length);
        }
    }

    private void DrawSelectable(FileRegistry registry, int i)
    {
        var selected = _selectedFiles.Contains(registry);
        var color = registry.SubModUsage.Count == 0             ? ColorId.ConflictingMod :
            registry.CurrentUsage == registry.SubModUsage.Count ? ColorId.NewMod : ColorId.InheritedMod;
        using (ImGuiColor.Text.Push(color.Value()))
        {
            if (Im.Selectable(registry.RelPath.Path.Span, selected))
            {
                if (selected)
                    _selectedFiles.Remove(registry);
                else
                    _selectedFiles.Add(registry);
            }

            if (Im.Item.RightClicked())
                Im.Popup.Open("context"u8);

            var rightText = DrawFileTooltip(registry, color);

            Im.Line.Same();
            ImEx.TextRightAligned(rightText);
        }

        DrawContextMenu(registry, i);
    }

    private void DrawContextMenu(FileRegistry registry, int i)
    {
        using var context = Im.Popup.Begin("context"u8);
        if (!context)
            return;

        if (Im.Selectable("Copy Full File Path"u8))
            Im.Clipboard.Set(registry.File.FullName);

        using (Im.Disabled(registry.CurrentUsage is 0))
        {
            if (Im.Selectable("复制游戏路径"u8))
            {
                _cutPaths.Clear();
                for (var j = 0; j < registry.SubModUsage.Count; ++j)
                {
                    if (registry.SubModUsage[j].Item1 != _editor.Option)
                        continue;

                    _cutPaths.Add(registry.SubModUsage[j].Item2);
                }
            }
        }

        using (Im.Disabled(registry.CurrentUsage is 0))
        {
            if (Im.Selectable("剪切游戏路径"u8))
            {
                _cutPaths.Clear();
                for (var j = 0; j < registry.SubModUsage.Count; ++j)
                {
                    if (registry.SubModUsage[j].Item1 != _editor.Option)
                        continue;

                    _cutPaths.Add(registry.SubModUsage[j].Item2);
                    _editor.FileEditor.SetGamePath(_editor.Option, i, j--, Utf8GamePath.Empty);
                }
            }
        }

        using (Im.Disabled(_cutPaths.Count is 0))
        {
            if (Im.Selectable("粘贴游戏路径"u8))
                foreach (var path in _cutPaths)
                    _editor.FileEditor.SetGamePath(_editor.Option!, i, -1, path);
        }
    }

    private void PrintGamePath(int i, int j, FileRegistry registry, IModDataContainer _, Utf8GamePath gamePath)
    {
        using var id = Im.Id.Push(j);
        Im.Table.NextColumn();
        var tmp = _fileIdx == i && _pathIdx == j ? _gamePathEdit : gamePath.ToString();
        var pos = Im.Cursor.X - Im.Style.FrameHeight;
        Im.Item.SetNextWidth(-1);
        if (Im.Input.Text(StringU8.Empty, ref tmp, maxLength: Utf8GamePath.MaxGamePathLength))
        {
            _fileIdx      = i;
            _pathIdx      = j;
            _gamePathEdit = tmp;
        }

        Im.Tooltip.OnHover("从此模组中完全移除了此路径。");

        if (Im.Item.DeactivatedAfterEdit)
        {
            if (Utf8GamePath.FromString(_gamePathEdit, out var path))
                _editor.FileEditor.SetGamePath(_editor.Option!, _fileIdx, _pathIdx, path);

            _fileIdx = -1;
            _pathIdx = -1;
        }
        else if (_fileIdx == i
              && _pathIdx == j
              && (!Utf8GamePath.FromString(_gamePathEdit, out var path)
                  || !path.IsEmpty && !path.Equals(gamePath) && !_editor.FileEditor.CanAddGamePath(path)))
        {
            Im.Line.Same();
            Im.Cursor.X = pos;
            ImEx.Icon.Draw(FontAwesomeIcon.TimesCircle.Icon(), Rgba32.Red);
        }
        else if (tmp.Length > 0 && Path.GetExtension(tmp) != registry.File.Extension)
        {
            Im.Line.Same();
            Im.Cursor.X = pos;
            ImEx.Icon.Draw(FontAwesomeIcon.ExclamationCircle.Icon(), new Rgba32(0xFF00B0B0));
            Im.Tooltip.OnHover("The game path and the file do not have the same extension."u8);
        }
    }

    private void PrintNewGamePath(int i, FileRegistry registry, IModDataContainer _)
    {
        var tmp = _fileIdx == i && _pathIdx == -1 ? _gamePathEdit : string.Empty;
        var pos = Im.Cursor.X - Im.Style.FrameHeight;
        Im.Item.SetNextWidth(-1);
        if (Im.Input.Text("##new"u8, ref tmp, "添加新路径..."u8, maxLength: Utf8GamePath.MaxGamePathLength))
        {
            _fileIdx      = i;
            _pathIdx      = -1;
            _gamePathEdit = tmp;
        }

        if (Im.Item.DeactivatedAfterEdit)
        {
            if (Utf8GamePath.FromString(_gamePathEdit, out var path) && !path.IsEmpty)
                _editor.FileEditor.SetGamePath(_editor.Option!, _fileIdx, _pathIdx, path);

            _fileIdx = -1;
            _pathIdx = -1;
        }
        else if (_fileIdx == i
              && _pathIdx == -1
              && (!Utf8GamePath.FromString(_gamePathEdit, out var path)
                  || !path.IsEmpty && !_editor.FileEditor.CanAddGamePath(path)))
        {
            Im.Line.Same();
            Im.Cursor.X = pos;
            ImEx.Icon.Draw(FontAwesomeIcon.TimesCircle.Icon(), Rgba32.Red);
        }
        else if (tmp.Length > 0 && Path.GetExtension(tmp) != registry.File.Extension)
        {
            Im.Line.Same();
            Im.Cursor.X = pos;
            ImEx.Icon.Draw(FontAwesomeIcon.ExclamationCircle.Icon(), new Rgba32(0xFF00B0B0));
            Im.Tooltip.OnHover("游戏路径和文件的扩展名不一致。");
        }
    }

    private void DrawButtonHeader()
    {
        Im.Line.New();

        using var spacing = ImStyleDouble.ItemSpacing.Push(new Vector2(3 * Im.Style.GlobalScale, 0));
        Im.Item.SetNextWidthScaled(30);
        Im.Drag("##skippedFolders"u8, ref _folderSkip, 0, 10, 0.01f);
        Im.Tooltip.OnHover("从文件路径自动构建游戏路径时，跳过指定数量的文件夹。");
        Im.Line.Same();
        spacing.Pop();
        if (Im.Button("添加路径"u8))
            _editor.FileEditor.AddPathsToSelected(_editor.Option!, _editor.Files.Available.Where(_selectedFiles.Contains), _folderSkip);

        Im.Tooltip.OnHover(
            "在当前选项（指'刷新数据'右边的模组选项）选中的所有文件中，添加模组文件路径替换游戏路径，可在前面设置数值跳过指定数量的文件夹。");


        Im.Line.Same();
        if (Im.Button("移除路径"u8))
            _editor.FileEditor.RemovePathsFromSelected(_editor.Option!, _editor.Files.Available.Where(_selectedFiles.Contains));

        Im.Tooltip.OnHover("移除当前选项中所选文件的替换游戏路径。");


        Im.Line.Same();
        var active = _config.DeleteModModifier.IsActive();
        var tt =
            "从你的文件系统中完全删除选中的所有文件，但不删除替换游戏路径。\n！！！注意，此操作无法恢复！！！";
        if (_selectedFiles.Count is 0)
            tt += "\n\n没有文件被删除。";
        else if (!active)
            tt += $"\n\nHold {_config.DeleteModModifier} to delete.";

        if (ImEx.Button("删除选中的文件"u8, Vector2.Zero, tt, _selectedFiles.Count is 0 || !active))
            _editor.FileEditor.DeleteFiles(_editor.Mod!, _editor.Option!, _editor.Files.Available.Where(_selectedFiles.Contains));

        Im.Line.Same();
        var changes = _editor.FileEditor.Changes;
        var tt2     = changes ? "将当前文件设置应用到选中的文件。"u8 : "没作出任何修改。"u8;
        if (ImEx.Button("应用修改"u8, Vector2.Zero, tt2, !changes))
        {
            var failedFiles = _editor.FileEditor.Apply(_editor.Mod!, _editor.Option!);
            if (failedFiles > 0)
                Penumbra.Log.Information($"应用{failedFiles}文件重定向到{_editor.Option!.GetFullName()}失败。");
        }


        Im.Line.Same();
        var label  = changes ? "撤销修改" : "重新加载文件";
        var length = new Vector2(Im.Font.CalculateSize("     撤销修改     "u8).X, 0);
        if (Im.Button(label, length))
            _editor.FileEditor.Revert(_editor.Mod!, _editor.Option!);

        Im.Tooltip.OnHover("恢复自上次的文件、选项重载或数据刷新以来所有可恢复的修改。");

        Im.Line.Same();
        Im.Checkbox("总览模式"u8, ref _overviewMode);
    }

    private void DrawFileManagementNormal()
    {
        // 1. 计数文本
        var totalCount  = _editor.Files.Available.Count;
        var hiddenCount = _editor.Files.Available.Count(ShouldHideFile);
        var countText   = $"已选中{_selectedFiles.Count} / {totalCount}个文件" + (hiddenCount > 0 ? $"（{hiddenCount}隐藏）" : "");

        // 2. 按钮与筛选
        Im.Item.SetNextWidthScaled(250);
        Im.Input.Text("##filter"u8, ref _fileFilter, "筛选路径..."u8);
        Im.Line.Same();
        Im.Checkbox("显示游戏路径"u8, ref _showGamePaths);
        Im.Line.Same();
        if (Im.Button("取消所有选择"u8))
            _selectedFiles.Clear();

        Im.Line.Same();
        if (Im.Button("选择可见项"u8))
            _selectedFiles.UnionWith(_editor.Files.Available.Where(CheckFilter).Where(f => !ShouldHideFile(f)));

        Im.Line.Same();
        if (Im.Button("选择未使用项"u8))
            _selectedFiles.UnionWith(_editor.Files.Available.Where(f => f.SubModUsage.Count == 0).Where(f => !ShouldHideFile(f)));

        Im.Line.Same();
        if (Im.Button("选择已使用项"u8))
            _selectedFiles.UnionWith(_editor.Files.Available.Where(f => f.CurrentUsage > 0).Where(f => !ShouldHideFile(f)));

        Im.Line.Same();
        if (Im.Button("文件类型过滤"u8))
            Im.Popup.Open("fileTypeFilterPopupNormal"u8);

        Im.Tooltip.OnHover("设置要隐藏的文件类型");
        Im.Line.Same();
        ImEx.TextRightAligned(countText);

        using (var popup = Im.Popup.Begin("fileTypeFilterPopupNormal"u8))
        {
            if (popup)
                DrawFileTypeFilterOptions();
        }
    }

    /// <summary>
    /// 绘制文件类型过滤选项
    /// </summary>
    private void DrawFileTypeFilterOptions()
    {
        var changed = false;

        var hideDds  = _config.HideDdsFiles;
        var hidePng  = _config.HidePngFiles;
        var hideJpeg = _config.HideJpegFiles;
        var hideJson = _config.HideJsonFiles;
        var hideTga  = _config.HideTgaFiles;
        var hideBmp  = _config.HideBmpFiles;
        var hideGif  = _config.HideGifFiles;
        var hideTiff = _config.HideTiffFiles;
        var hideWebp = _config.HideWebpFiles;
        var hideXcp  = _config.HideXcpFiles;

        changed |= Im.Checkbox("隐藏 DDS 文件"u8, ref hideDds);
        changed |= Im.Checkbox("隐藏 PNG 文件"u8, ref hidePng);
        changed |= Im.Checkbox("隐藏 JPEG 文件"u8, ref hideJpeg);
        changed |= Im.Checkbox("隐藏 JSON 文件"u8, ref hideJson);
        changed |= Im.Checkbox("隐藏 TGA 文件"u8, ref hideTga);
        changed |= Im.Checkbox("隐藏 BMP 文件"u8, ref hideBmp);
        changed |= Im.Checkbox("隐藏 GIF 文件"u8, ref hideGif);
        changed |= Im.Checkbox("隐藏 TIFF 文件"u8, ref hideTiff);
        changed |= Im.Checkbox("隐藏 WebP 文件"u8, ref hideWebp);
        changed |= Im.Checkbox("隐藏 XCP 文件"u8, ref hideXcp);

        if (changed)
        {
            _config.HideDdsFiles  = hideDds;
            _config.HidePngFiles  = hidePng;
            _config.HideJpegFiles = hideJpeg;
            _config.HideJsonFiles = hideJson;
            _config.HideTgaFiles  = hideTga;
            _config.HideBmpFiles  = hideBmp;
            _config.HideGifFiles  = hideGif;
            _config.HideTiffFiles = hideTiff;
            _config.HideWebpFiles = hideWebp;
            _config.HideXcpFiles  = hideXcp;
            _config.Save();
        }
    }
}
