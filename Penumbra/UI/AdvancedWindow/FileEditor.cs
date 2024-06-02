using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Plugin.Services;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Compression;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.GameData.Files;
using Penumbra.Mods.Editor;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public class FileEditor<T>(
    ModEditWindow owner,
    CommunicatorService communicator,
    IDataManager gameData,
    Configuration config,
    FileCompactor compactor,
    FileDialogService fileDialog,
    string tabName,
    string fileType,
    Func<IReadOnlyList<FileRegistry>> getFiles,
    Func<T, bool, bool> drawEdit,
    Func<string> getInitialPath,
    Func<byte[], string, bool, T?> parseFile)
    : IDisposable
    where T : class, IWritable
{
    public void Draw()
    {
        using var tab = ImRaii.TabItem(tabName);
        if (!tab)
        {
            _quickImport = null;
            return;
        }

        ImGui.NewLine();
        DrawFileSelectCombo();
        SaveButton();
        ImGui.SameLine();
        ResetButton();
        ImGui.SameLine();
        RedrawOnSaveBox();
        ImGui.SameLine();
        DefaultInput();
        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));

        DrawFilePanel();
    }

    private void RedrawOnSaveBox()
    {
        var redraw = config.Ephemeral.ForceRedrawOnFileChange;
        if (ImGui.Checkbox("保存时重绘", ref redraw))
        {
            config.Ephemeral.ForceRedrawOnFileChange = redraw;
            config.Ephemeral.Save();
        }

        ImGuiUtil.HoverTooltip("每当你在这里保存文件时，强制重新绘制你的玩家角色。");
    }

    public void Dispose()
    {
        (_currentFile as IDisposable)?.Dispose();
        _currentFile = null;
        (_defaultFile as IDisposable)?.Dispose();
        _defaultFile = null;
    }

    private FileRegistry? _currentPath;
    private T?            _currentFile;
    private Exception?    _currentException;
    private bool          _changed;

    private string       _defaultPath = string.Empty;
    private bool         _inInput;
    private Utf8GamePath _defaultPathUtf8;
    private bool         _isDefaultPathUtf8Valid;
    private T?           _defaultFile;
    private Exception?   _defaultException;

    private readonly Combo _combo = new(config, getFiles);

    private ModEditWindow.QuickImportAction? _quickImport;

    private void DefaultInput()
    {
        using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { X = UiHelpers.ScaleX3 });
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 2 * (UiHelpers.ScaleX3 + ImGui.GetFrameHeight()));
        ImGui.InputTextWithHint("##defaultInput", "输入游戏路径来比对...", ref _defaultPath, Utf8GamePath.MaxGamePathLength);
        _inInput = ImGui.IsItemActive();
        if (ImGui.IsItemDeactivatedAfterEdit() && _defaultPath.Length > 0)
        {
            _isDefaultPathUtf8Valid = Utf8GamePath.FromString(_defaultPath, out _defaultPathUtf8, true);
            _quickImport            = null;
            fileDialog.Reset();
            try
            {
                var file = gameData.GetFile(_defaultPath);
                if (file != null)
                {
                    _defaultException = null;
                    (_defaultFile as IDisposable)?.Dispose();
                    _defaultFile = null; // Avoid double disposal if an exception occurs during the parsing of the new file.
                    _defaultFile = parseFile(file.Data, _defaultPath, false);
                }
                else
                {
                    _defaultFile      = null;
                    _defaultException = new Exception("文件不存在。");
                }
            }
            catch (Exception e)
            {
                _defaultFile      = null;
                _defaultException = e;
            }
        }

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Save.ToIconString(), new Vector2(ImGui.GetFrameHeight()), "导出此文件。",
                _defaultFile == null, true))
            fileDialog.OpenSavePicker($"导出 {_defaultPath} 到...", fileType, Path.GetFileNameWithoutExtension(_defaultPath), fileType,
                (success, name) =>
                {
                    if (!success)
                        return;

                    try
                    {
                        compactor.WriteAllBytes(name, _defaultFile?.Write() ?? throw new Exception("文件无效。"));
                    }
                    catch (Exception e)
                    {
                        Penumbra.Messager.NotificationMessage(e, $"无法导出 {_defaultPath}。", NotificationType.Error);
                    }
                }, getInitialPath(), false);

        _quickImport ??=
            ModEditWindow.QuickImportAction.Prepare(owner, _isDefaultPathUtf8Valid ? _defaultPathUtf8 : Utf8GamePath.Empty, _defaultFile);
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.FileImport.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
                $"添加此文件的副本到 {_quickImport.OptionName}.", !_quickImport.CanExecute, true))
        {
            try
            {
                UpdateCurrentFile(_quickImport.Execute());
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"无法添加 {_quickImport.GamePath} 的副本到 {_quickImport.OptionName}:\n{e}");
            }

            _quickImport = null;
        }
    }

    public void Reset()
    {
        _currentException = null;
        _currentPath      = null;
        (_currentFile as IDisposable)?.Dispose();
        _currentFile = null;
        _changed     = false;
    }

    private void DrawFileSelectCombo()
    {
        if (_combo.Draw("##fileSelect", _currentPath?.RelPath.ToString() ?? $"选择 {fileType} 文件...", string.Empty,
                ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight())
         && _combo.CurrentSelection != null)
            UpdateCurrentFile(_combo.CurrentSelection);
    }

    private void UpdateCurrentFile(FileRegistry path)
    {
        if (ReferenceEquals(_currentPath, path))
            return;

        _changed          = false;
        _currentPath      = path;
        _currentException = null;
        try
        {
            var bytes = File.ReadAllBytes(_currentPath.File.FullName);
            (_currentFile as IDisposable)?.Dispose();
            _currentFile = null; // Avoid double disposal if an exception occurs during the parsing of the new file.
            _currentFile = parseFile(bytes, _currentPath.File.FullName, true);
        }
        catch (Exception e)
        {
            (_currentFile as IDisposable)?.Dispose();
            _currentFile      = null;
            _currentException = e;
        }
    }

    private void SaveButton()
    {
        var canSave = _changed && _currentFile is { Valid: true };
        if (ImGuiUtil.DrawDisabledButton("保存到文件", Vector2.Zero,
                $"保存选中的{fileType}文件，应用所有修改。此操作不可恢复。", !canSave))
        {
            compactor.WriteAllBytes(_currentPath!.File.FullName, _currentFile!.Write());
            if (owner.Mod != null)
                communicator.ModFileChanged.Invoke(owner.Mod, _currentPath);
            _changed = false;
        }
    }

    private void ResetButton()
    {
        if (ImGuiUtil.DrawDisabledButton("重置修改", Vector2.Zero,
                $"重置对{fileType}文件做的所有修改。", !_changed))
        {
            var tmp = _currentPath;
            _currentPath = null;
            UpdateCurrentFile(tmp!);
        }
    }

    private void DrawFilePanel()
    {
        using var child = ImRaii.Child("##filePanel", -Vector2.One, true);
        if (!child)
            return;

        if (_currentPath != null)
        {
            if (_currentFile == null)
            {
                ImGui.TextUnformatted($"无法解析选中的 {fileType} 文件。");
                if (_currentException != null)
                {
                    using var tab = ImRaii.PushIndent();
                    ImGuiUtil.TextWrapped(_currentException.ToString());
                }
            }
            else
            {
                using var id = ImRaii.PushId(0);
                _changed |= drawEdit(_currentFile, false);
            }
        }

        if (!_inInput && _defaultPath.Length > 0)
        {
            if (_currentPath != null)
            {
                ImGui.NewLine();
                ImGui.NewLine();
                ImGui.TextUnformatted($"Preview of {_defaultPath}:");
                ImGui.Separator();
            }

            if (_defaultFile == null)
            {
                ImGui.TextUnformatted($"Could not parse provided {fileType} game file:\n");
                if (_defaultException != null)
                {
                    using var tab = ImRaii.PushIndent();
                    ImGuiUtil.TextWrapped(_defaultException.ToString());
                }
            }
            else
            {
                using var id = ImRaii.PushId(1);
                drawEdit(_defaultFile, true);
            }
        }
    }

    private class Combo : FilterComboCache<FileRegistry>
    {
        private readonly Configuration _config;

        public Combo(Configuration config, Func<IReadOnlyList<FileRegistry>> generator)
            : base(generator, MouseWheelType.None, Penumbra.Log)
            => _config = config;

        protected override bool DrawSelectable(int globalIdx, bool selected)
        {
            var  file = Items[globalIdx];
            bool ret;
            using (var c = ImRaii.PushColor(ImGuiCol.Text, ColorId.HandledConflictMod.Value(), file.IsOnPlayer))
            {
                ret = ImGui.Selectable(file.RelPath.ToString(), selected);
            }

            if (ImGui.IsItemHovered())
            {
                using var tt = ImRaii.Tooltip();
                ImGui.TextUnformatted("All Game Paths");
                ImGui.Separator();
                using var t = ImRaii.Table("##Tooltip", 2, ImGuiTableFlags.SizingFixedFit);
                foreach (var (option, gamePath) in file.SubModUsage)
                {
                    ImGui.TableNextColumn();
                    UiHelpers.Text(gamePath.Path);
                    ImGui.TableNextColumn();
                    using var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.ItemId.Value());
                    ImGui.TextUnformatted(option.GetFullName());
                }
            }

            if (file.SubModUsage.Count > 0)
            {
                ImGui.SameLine();
                using var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.ItemId.Value());
                ImGuiUtil.RightAlign(file.SubModUsage[0].Item2.Path.ToString());
            }

            return ret;
        }

        protected override bool IsVisible(int globalIndex, LowerString filter)
            => filter.IsContained(Items[globalIndex].File.FullName)
             || Items[globalIndex].SubModUsage.Any(f => filter.IsContained(f.Item2.ToString()));
    }
}
