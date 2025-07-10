using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Extensions;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.Mods.Editor;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly HashSet<FileRegistry> _selectedFiles = new(256);
    private readonly HashSet<Utf8GamePath> _cutPaths      = [];
    private          LowerString           _fileFilter    = LowerString.Empty;
    private          bool                  _showGamePaths = true;
    private          string                _gamePathEdit  = string.Empty;
    private          int                   _fileIdx       = -1;
    private          int                   _pathIdx       = -1;
    private          int                   _folderSkip;
    private          bool                  _overviewMode;

    private LowerString _fileOverviewFilter1 = LowerString.Empty;
    private LowerString _fileOverviewFilter2 = LowerString.Empty;
    private LowerString _fileOverviewFilter3 = LowerString.Empty;

    private bool CheckFilter(FileRegistry registry)
        => _fileFilter.IsEmpty || registry.File.FullName.Contains(_fileFilter.Lower, StringComparison.OrdinalIgnoreCase);

    private bool CheckFilter((FileRegistry, int) p)
        => CheckFilter(p.Item1);

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
            _ => false
        };
    }

    private void DrawFileTab()
    {
        using var tab = ImRaii.TabItem("文件重定向");
        if (!tab)
            return;

        DrawOptionSelectHeader();
        DrawButtonHeader();

        if (_overviewMode)
            DrawFileManagementOverview();
        else
            DrawFileManagementNormal();

        using var child = ImRaii.Child("##files", -Vector2.One, true);
        if (!child)
            return;

        if (_overviewMode)
            DrawFilesOverviewMode();
        else
            DrawFilesNormalMode();
    }

    private void DrawFilesOverviewMode()
    {
        var height = ImGui.GetTextLineHeightWithSpacing() + 2 * ImGui.GetStyle().CellPadding.Y;
        var skips  = ImGuiClip.GetNecessarySkips(height);

        using var list = ImRaii.Table("##table", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV, -Vector2.One);

        if (!list)
            return;

        var width = ImGui.GetContentRegionAvail().X / 8;

        ImGui.TableSetupColumn("##file",   ImGuiTableColumnFlags.WidthFixed, width * 3);
        ImGui.TableSetupColumn("##path",   ImGuiTableColumnFlags.WidthFixed, width * 3 + ImGui.GetStyle().FrameBorderSize);
        ImGui.TableSetupColumn("##option", ImGuiTableColumnFlags.WidthFixed, width * 2);

        var idx = 0;

        var files = _editor.Files.Available
            .Where(f => !ShouldHideFile(f)) // 应用文件类型过滤
            .SelectMany(f =>
            {
                var file = f.RelPath.ToString();
                return f.SubModUsage.Count == 0
                    ? Enumerable.Repeat((file, "Unused", string.Empty, 0x40000080u), 1)
                    : f.SubModUsage.Select(s => (file, s.Item2.ToString(), s.Item1.GetFullName(),
                        _editor.Option! == s.Item1 && Mod!.HasOptions ? 0x40008000u : 0u));
            });

        void DrawLine((string, string, string, uint) data)
        {
            using var id = ImRaii.PushId(idx++);
            ImGui.TableNextColumn();
            if (data.Item4 != 0)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, data.Item4);

            ImGuiUtil.CopyOnClickSelectable(data.Item1);
            ImGui.TableNextColumn();
            if (data.Item4 != 0)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, data.Item4);

            ImGuiUtil.CopyOnClickSelectable(data.Item2);
            ImGui.TableNextColumn();
            if (data.Item4 != 0)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, data.Item4);

            ImGuiUtil.CopyOnClickSelectable(data.Item3);
        }

        bool Filter((string, string, string, uint) data)
            => _fileOverviewFilter1.IsContained(data.Item1)
             && _fileOverviewFilter2.IsContained(data.Item2)
             && _fileOverviewFilter3.IsContained(data.Item3);

        var end = ImGuiClip.FilteredClippedDraw(files, skips, Filter, DrawLine);
        ImGuiClip.DrawEndDummy(end, height);
    }

    private void DrawFilesNormalMode()
    {
        using var list = ImRaii.Table("##table", 1);

        if (!list)
            return;

        foreach (var (registry, i) in _editor.Files.Available.WithIndex().Where(CheckFilter).Where(p => !ShouldHideFile(p.Item1)))
        {
            using var id = ImRaii.PushId(i);
            ImGui.TableNextColumn();

            DrawSelectable(registry, i);

            if (!_showGamePaths)
                continue;

            using var indent = ImRaii.PushIndent(50f);
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

        if (text != null && ImGui.IsItemHovered())
        {
            using var tt = ImUtf8.Tooltip();
            using var c  = ImRaii.DefaultColors();
            ImUtf8.Text(string.Join('\n', text));
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
        using (ImRaii.PushColor(ImGuiCol.Text, color.Value()))
        {
            if (UiHelpers.Selectable(registry.RelPath.Path, selected))
            {
                if (selected)
                    _selectedFiles.Remove(registry);
                else
                    _selectedFiles.Add(registry);
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                ImUtf8.OpenPopup("context"u8);

            var rightText = DrawFileTooltip(registry, color);

            ImGui.SameLine();
            ImGuiUtil.RightAlign(rightText);
        }

        DrawContextMenu(registry, i);
    }

    private void DrawContextMenu(FileRegistry registry, int i)
    {
        using var context = ImUtf8.Popup("context"u8);
        if (!context)
            return;

        if (ImUtf8.Selectable("Copy Full File Path"))
            ImUtf8.SetClipboardText(registry.File.FullName);

        using (ImRaii.Disabled(registry.CurrentUsage == 0))
        {
            if (ImUtf8.Selectable("复制游戏路径"u8))
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

        using (ImRaii.Disabled(registry.CurrentUsage == 0))
        {
            if (ImUtf8.Selectable("剪切游戏路径"u8))
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

        using (ImRaii.Disabled(_cutPaths.Count == 0))
        {
            if (ImUtf8.Selectable("粘贴游戏路径"u8))
                foreach (var path in _cutPaths)
                    _editor.FileEditor.SetGamePath(_editor.Option!, i, -1, path);
        }
    }

    private void PrintGamePath(int i, int j, FileRegistry registry, IModDataContainer subMod, Utf8GamePath gamePath)
    {
        using var id = ImRaii.PushId(j);
        ImGui.TableNextColumn();
        var tmp = _fileIdx == i && _pathIdx == j ? _gamePathEdit : gamePath.ToString();
        var pos = ImGui.GetCursorPosX() - ImGui.GetFrameHeight();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText(string.Empty, ref tmp, Utf8GamePath.MaxGamePathLength))
        {
            _fileIdx      = i;
            _pathIdx      = j;
            _gamePathEdit = tmp;
        }

        ImGuiUtil.HoverTooltip("从此模组中完全移除了此路径。");

        if (ImGui.IsItemDeactivatedAfterEdit())
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
            ImGui.SameLine();
            ImGui.SetCursorPosX(pos);
            using var font = ImRaii.PushFont(UiBuilder.IconFont);
            ImGuiUtil.TextColored(0xFF0000FF, FontAwesomeIcon.TimesCircle.ToIconString());
        }
    }

    private void PrintNewGamePath(int i, FileRegistry registry, IModDataContainer subMod)
    {
        var tmp = _fileIdx == i && _pathIdx == -1 ? _gamePathEdit : string.Empty;
        var pos = ImGui.GetCursorPosX() - ImGui.GetFrameHeight();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##new", "添加新路径...", ref tmp, Utf8GamePath.MaxGamePathLength))
        {
            _fileIdx      = i;
            _pathIdx      = -1;
            _gamePathEdit = tmp;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
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
            ImGui.SameLine();
            ImGui.SetCursorPosX(pos);
            using var font = ImRaii.PushFont(UiBuilder.IconFont);
            ImGuiUtil.TextColored(0xFF0000FF, FontAwesomeIcon.TimesCircle.ToIconString());
        }
    }

    private void DrawButtonHeader()
    {
        ImGui.NewLine();

        using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(3 * UiHelpers.Scale, 0));
        ImGui.SetNextItemWidth(30 * UiHelpers.Scale);
        ImGui.DragInt("##skippedFolders", ref _folderSkip, 0.01f, 0, 10);
        ImGuiUtil.HoverTooltip("从文件路径自动构建游戏路径时，跳过指定数量的文件夹。");
        ImGui.SameLine();
        spacing.Pop();
        if (ImGui.Button("添加路径"))
            _editor.FileEditor.AddPathsToSelected(_editor.Option!, _editor.Files.Available.Where(_selectedFiles.Contains), _folderSkip);

        ImGuiUtil.HoverTooltip(
            "在当前选项（指'刷新数据'右边的模组选项）选中的所有文件中，添加模组文件路径替换游戏路径，可在前面设置数值跳过指定数量的文件夹。");


        ImGui.SameLine();
        if (ImGui.Button("移除路径"))
            _editor.FileEditor.RemovePathsFromSelected(_editor.Option!, _editor.Files.Available.Where(_selectedFiles.Contains));

        ImGuiUtil.HoverTooltip("移除当前选项中所选文件的替换游戏路径。");


        ImGui.SameLine();
        var active = _config.DeleteModModifier.IsActive();
        var tt =
            "从你的文件系统中完全删除选中的所有文件，但不删除替换游戏路径。\n！！！注意，此操作无法恢复！！！";
        if (_selectedFiles.Count == 0)
            tt += "\n\n没有文件被删除。";
        else if (!active)
            tt += $"\n\nHold {_config.DeleteModModifier} to delete.";

        if (ImGuiUtil.DrawDisabledButton("删除选中的文件", Vector2.Zero, tt, _selectedFiles.Count == 0 || !active))
            _editor.FileEditor.DeleteFiles(_editor.Mod!, _editor.Option!, _editor.Files.Available.Where(_selectedFiles.Contains));

        ImGui.SameLine();
        var changes = _editor.FileEditor.Changes;
        tt = changes ? "将当前文件设置应用到选中的文件。" : "没作出任何修改。";
        if (ImGuiUtil.DrawDisabledButton("应用修改", Vector2.Zero, tt, !changes))
        {
            var failedFiles = _editor.FileEditor.Apply(_editor.Mod!, _editor.Option!);
            if (failedFiles > 0)
                Penumbra.Log.Information($"应用{failedFiles}文件重定向到{_editor.Option!.GetFullName()}失败。");
        }


        ImGui.SameLine();
        var label  = changes ? "撤销修改" : "重新加载文件";
        var length = new Vector2( ImGui.CalcTextSize( "     撤销修改     " ).X, 0 );
        if (ImGui.Button(label, length))
            _editor.FileEditor.Revert(_editor.Mod!, _editor.Option!);

        ImGuiUtil.HoverTooltip("恢复自上次的文件、选项重载或数据刷新以来所有可恢复的修改。");

        ImGui.SameLine();
        ImGui.Checkbox("总览模式", ref _overviewMode);
    }

    private void DrawFileManagementNormal()
    {
        // 1. 先绘制计数文本（右上角）
        var countText = $"已选中{_selectedFiles.Count} / {_editor.Files.Available.Count}个文件。";
        var textWidth = ImGui.CalcTextSize(countText).X;
        var windowWidth = ImGui.GetWindowWidth();
        var style = ImGui.GetStyle();
        var textX = windowWidth - textWidth - style.WindowPadding.X;
        var originalPos = ImGui.GetCursorPos();
        ImGui.SetCursorPosX(textX);
        ImGui.Text(countText);
        ImGui.SetCursorPos(originalPos);

        // 2. 再绘制按钮和弹窗
        ImGui.SetNextItemWidth(250 * UiHelpers.Scale);
        LowerString.InputWithHint("##filter", "筛选路径...", ref _fileFilter, Utf8GamePath.MaxGamePathLength);
        ImGui.SameLine();
        ImGui.Checkbox("显示游戏路径", ref _showGamePaths);
        ImGui.SameLine();
        if (ImGui.Button("取消所有选择"))
            _selectedFiles.Clear();

        ImGui.SameLine();
        if (ImGui.Button("选择可见项"))
            _selectedFiles.UnionWith(_editor.Files.Available.Where(CheckFilter).Where(f => !ShouldHideFile(f)));

        ImGui.SameLine();
        if (ImGui.Button("选择未使用项"))
            _selectedFiles.UnionWith(_editor.Files.Available.Where(f => f.SubModUsage.Count == 0).Where(f => !ShouldHideFile(f)));

        ImGui.SameLine();
        if (ImGui.Button("选择已使用项"))
            _selectedFiles.UnionWith(_editor.Files.Available.Where(f => f.CurrentUsage > 0).Where(f => !ShouldHideFile(f)));

        ImGui.SameLine();
        if (ImGui.Button("文件类型过滤"))
            ImGui.OpenPopup("fileTypeFilterPopupNormal");

        ImGuiUtil.HoverTooltip("设置要隐藏的文件类型");

        // 文件类型过滤弹出窗口（普通模式）
        using var popup = ImRaii.Popup("fileTypeFilterPopupNormal");
        if (popup)
        {
            DrawFileTypeFilterOptions();
        }
    }

    private void DrawFileManagementOverview()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 0)
            .Push(ImGuiStyleVar.ItemSpacing,     Vector2.Zero)
            .Push(ImGuiStyleVar.FrameBorderSize, ImGui.GetStyle().ChildBorderSize);

        var width = ImGui.GetContentRegionAvail().X / 8;

        ImGui.SetNextItemWidth(width * 3);
        LowerString.InputWithHint( "##fileFilter", "筛选文件...", ref _fileOverviewFilter1, Utf8GamePath.MaxGamePathLength );
        ImGui.SameLine();
        ImGui.SetNextItemWidth(width * 3);
        LowerString.InputWithHint( "##pathFilter", "筛选路径...", ref _fileOverviewFilter2, Utf8GamePath.MaxGamePathLength );
        ImGui.SameLine();
        ImGui.SetNextItemWidth(width * 2);
        LowerString.InputWithHint( "##optionFilter", "筛选选项...", ref _fileOverviewFilter3, Utf8GamePath.MaxGamePathLength );
    }

    /// <summary>
    /// 绘制文件类型过滤选项
    /// </summary>
    private void DrawFileTypeFilterOptions()
    {
        var changed = false;
        
        var hideDds = _config.HideDdsFiles;
        var hidePng = _config.HidePngFiles;
        var hideJpeg = _config.HideJpegFiles;
        var hideJson = _config.HideJsonFiles;
        var hideTga = _config.HideTgaFiles;
        var hideBmp = _config.HideBmpFiles;
        var hideGif = _config.HideGifFiles;
        var hideTiff = _config.HideTiffFiles;
        var hideWebp = _config.HideWebpFiles;
        
        changed |= ImGui.Checkbox("隐藏 DDS 文件", ref hideDds);
        changed |= ImGui.Checkbox("隐藏 PNG 文件", ref hidePng);
        changed |= ImGui.Checkbox("隐藏 JPEG 文件", ref hideJpeg);
        changed |= ImGui.Checkbox("隐藏 JSON 文件", ref hideJson);
        changed |= ImGui.Checkbox("隐藏 TGA 文件", ref hideTga);
        changed |= ImGui.Checkbox("隐藏 BMP 文件", ref hideBmp);
        changed |= ImGui.Checkbox("隐藏 GIF 文件", ref hideGif);
        changed |= ImGui.Checkbox("隐藏 TIFF 文件", ref hideTiff);
        changed |= ImGui.Checkbox("隐藏 WebP 文件", ref hideWebp);

        if (changed)
        {
            _config.HideDdsFiles = hideDds;
            _config.HidePngFiles = hidePng;
            _config.HideJpegFiles = hideJpeg;
            _config.HideJsonFiles = hideJson;
            _config.HideTgaFiles = hideTga;
            _config.HideBmpFiles = hideBmp;
            _config.HideGifFiles = hideGif;
            _config.HideTiffFiles = hideTiff;
            _config.HideWebpFiles = hideWebp;
            _config.Save();
        }
    }
}
