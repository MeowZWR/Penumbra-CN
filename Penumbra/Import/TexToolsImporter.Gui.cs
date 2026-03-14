using ImSharp;
using Penumbra.Import.Structs;
using Penumbra.UI.Classes;

namespace Penumbra.Import;

public partial class TexToolsImporter
{
    // Progress Data
    private int _currentModPackIdx;
    private int _currentOptionIdx;
    private int _currentFileIdx;

    private int    _currentNumOptions;
    private int    _currentNumFiles;
    private string _currentModName    = string.Empty;
    private string _currentGroupName  = string.Empty;
    private string _currentOptionName = string.Empty;
    private string _currentFileName   = string.Empty;

    public (string Title, string Text, float Progress, bool Ended, bool Successful) ComputeNotificationData()
    {
        if (_modPackCount is 0)
            return ("没有可导入的模组", "没有可提取的内容。", 1.0f, true, true);

        if (_modPackCount == _currentModPackIdx)
        {
            var    success = ExtractedMods.Count(t => t.Error == null);
            string title;
            if (success == ExtractedMods.Count)
            {
                title = ExtractedMods.Count switch
                {
                    1 => $"成功导入 {_currentModName}",
                    _ => "成功导入模组",
                };
            }
            else
            {
                title = ExtractedMods.Count switch
                {
                    1 => $"导入 {(string.IsNullOrEmpty(_currentModName) ? ExtractedMods[0].File.Name : _currentModName)} 失败",
                    _ => "导入模组失败",
                };
            }

            return (title, $"成功提取 {success} / {ExtractedMods.Count} 个文件。", 1.0f, true, success == ExtractedMods.Count);
        }

        if (State is ImporterState.DeduplicatingFiles)
            return ($"正在安装 {_currentModName}", "正在去重文件...", 1.0f, false, true);

        return ($"正在安装 {_currentModName}", $"提取文件 {_currentFileName}...",
            _currentNumFiles > 0 ? _currentFileIdx / (float)_currentNumFiles : 0.0f, false, true);
    }

    public bool DrawProgressInfo(Vector2 size)
    {
        if (_modPackCount is 0)
        {
            ImEx.TextCentered("没有可提取的内容。"u8);
            return true;
        }

        if (_modPackCount == _currentModPackIdx)
        {
            DrawEndState();
            return true;
        }

        Im.Line.New();
        var percentage = (float)_currentModPackIdx / _modPackCount;
        Im.ProgressBar(percentage, size, $"Mod {_currentModPackIdx + 1} / {_modPackCount}");
        Im.Line.New();
        Im.Text(State is ImporterState.DeduplicatingFiles
            ? $"正在去重 {_currentModName}..."
            : $"正在提取 {_currentModName}...");

        if (_currentNumOptions > 1)
        {
            Im.Line.New();
            Im.Line.New();
            if (_currentOptionIdx >= _currentNumOptions)
                Im.ProgressBar(1f, size, $"提取 {_currentNumOptions} 个选项");
            else
                Im.ProgressBar(_currentOptionIdx / (float)_currentNumOptions, size,
                    $"正在提取选项 {_currentOptionIdx + 1} / {_currentNumOptions}...");

            Im.Line.New();
            if (State is not ImporterState.DeduplicatingFiles)
                Im.Text(
                    $"正在提取选项 {(_currentGroupName.Length == 0 ? string.Empty : $"{_currentGroupName} - ")}{_currentOptionName}...");
        }

        Im.Line.New();
        Im.Line.New();
        if (_currentFileIdx >= _currentNumFiles)
            Im.ProgressBar(1f, size, $"提取 {_currentNumFiles} 个文件");
        else
            Im.ProgressBar(_currentFileIdx / (float)_currentNumFiles, size, $"正在提取文件 {_currentFileIdx + 1} / {_currentNumFiles}...");

        Im.Line.New();
        if (State is not ImporterState.DeduplicatingFiles)
            Im.Text($"提取文件 {_currentFileName}...");
        return false;
    }


    private void DrawEndState()
    {
        var success = ExtractedMods.Count(t => t.Error == null);

        Im.Text($"已成功提取 {success} / {ExtractedMods.Count} 个文件。");
        Im.Line.New();
        using var table = Im.Table.Begin("##files"u8, 2);
        if (!table)
            return;

        foreach (var (file, dir, ex) in ExtractedMods)
        {
            table.DrawColumn(file.Name);
            table.NextColumn();
            if (ex is null)
            {
                using var color = ImGuiColor.Text.Push(ColorId.FolderExpanded.Value());
                if (dir is null)
                    Im.Text("未知目录"u8);
                else
                    Im.Text(dir.FullName.AsSpan(_baseDirectory.FullName.Length + 1));
            }
            else
            {
                using var color = ImGuiColor.Text.Push(ColorId.ConflictingMod.Value());
                Im.Text(ex.Message);
                Im.Tooltip.OnHover($"{ex}");
            }
        }
    }

    public bool DrawCancelButton(Vector2 size)
        => ImEx.Button("取消"u8, size, StringU8.Empty, _token.IsCancellationRequested);
}
