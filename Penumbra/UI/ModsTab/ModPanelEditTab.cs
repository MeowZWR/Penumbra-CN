using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal.Notifications;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using OtterGui.Classes;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.AdvancedWindow;
using Penumbra.Mods.Settings;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.UI.ModsTab.Groups;

namespace Penumbra.UI.ModsTab;

public class ModPanelEditTab(
    ModManager modManager,
    ModFileSystemSelector selector,
    ModFileSystem fileSystem,
    Services.MessageService messager,
    ModEditWindow editWindow,
    ModEditor editor,
    FilenameService filenames,
    ModExportManager modExportManager,
    Configuration config,
    PredefinedTagManager predefinedTagManager,
    ModGroupEditDrawer groupEditDrawer,
    DescriptionEditPopup descriptionPopup,
    AddGroupDrawer addGroupDrawer)
    : ITab
{
    private readonly TagButtons _modTags = new();

    private ModFileSystem.Leaf _leaf = null!;
    private Mod                _mod  = null!;

    public ReadOnlySpan<byte> Label
        => "编辑模组"u8;

    public void DrawContent()
    {
        using var child = ImRaii.Child("##editChild", -Vector2.One);
        if (!child)
            return;

        _leaf = selector.SelectedLeaf!;
        _mod  = selector.Selected!;

        EditButtons();
        EditRegularMeta();
        UiHelpers.DefaultLineSpace();

        if (Input.Text( "模组路径（排序用）", Input.Path, Input.None, _leaf.FullName(), out var newPath, 256, UiHelpers.InputTextWidth.X))
            try
            {
                fileSystem.RenameAndMove(_leaf, newPath);
            }
            catch (Exception e)
            {
                messager.NotificationMessage(e.Message, NotificationType.Warning, false);
            }

        UiHelpers.DefaultLineSpace();
        var sharedTagsEnabled     = predefinedTagManager.Count > 0;
        var sharedTagButtonOffset = sharedTagsEnabled ? ImGui.GetFrameHeight() + ImGui.GetStyle().FramePadding.X : 0;
        var tagIdx = _modTags.Draw("模组标签：", "点击修改，或添加新标签。空白标签会被移除。", _mod.ModTags,
            out var editedTag, rightEndOffset: sharedTagButtonOffset);
        if (tagIdx >= 0)
            modManager.DataEditor.ChangeModTag(_mod, tagIdx, editedTag);

        if (sharedTagsEnabled)
            predefinedTagManager.DrawAddFromSharedTagsAndUpdateTags(selector.Selected!.LocalTags, selector.Selected!.ModTags, false,
                selector.Selected!);

        UiHelpers.DefaultLineSpace();
        addGroupDrawer.Draw(_mod, UiHelpers.InputTextWidth.X);
        UiHelpers.DefaultLineSpace();

        groupEditDrawer.Draw(_mod);
        descriptionPopup.Draw();
    }

    public void Reset()
    {
        MoveDirectory.Reset();
        Input.Reset();
    }

    /// <summary> The general edit row for non-detailed mod edits. </summary>
    private void EditButtons()
    {
        var buttonSize   = new Vector2(150 * UiHelpers.Scale, 0);
        var folderExists = Directory.Exists(_mod.ModPath.FullName);
        var tt = folderExists
            ? $"在操作系统指定的文件浏览器中打开目录：\"{_mod.ModPath.FullName}\" 。"
            : $"模组目录：\"{_mod.ModPath.FullName}\" 不存在。";
        if (ImGuiUtil.DrawDisabledButton( "打开模组目录", buttonSize, tt, !folderExists))
            Process.Start(new ProcessStartInfo(_mod.ModPath.FullName) { UseShellExecute = true });

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton( "重新加载模组", buttonSize, "从文件中重新加载当前模组。\n"
              + "如果模组目录或元文件不再存在，或者如果新的模组名称为空，则会删除模组。",
                false))
            modManager.ReloadMod(_mod);

        BackupButtons(buttonSize);
        MoveDirectory.Draw(modManager, _mod, buttonSize);

        UiHelpers.DefaultLineSpace();
        DrawUpdateBibo(buttonSize);

        UiHelpers.DefaultLineSpace();
    }

    private void DrawUpdateBibo(Vector2 buttonSize)
    {
        if (ImGui.Button( "更新Bibo材质", buttonSize))
        {
            editor.LoadMod(_mod);
            editor.MdlMaterialEditor.ReplaceAllMaterials("bibo",     "b");
            editor.MdlMaterialEditor.ReplaceAllMaterials("bibopube", "c");
            editor.MdlMaterialEditor.SaveAllModels(editor.Compactor);
            editWindow.UpdateModels();
        }

        ImGuiUtil.HoverTooltip(
            "此模组中的每个模型，以'_b'或'_c'结尾的所有材质名称后缀分别改为'_bibo'和'_bibopube'后缀。\n"
          + "如果不存在此类模型或此类材质，不会执行任何操作。\n"
          + "使用这个按钮来升级使用旧Bibo的模组。\n"
          + "进入'高级编辑'窗口可以对材质分配进行更精确的控制。" );
    }

    private void BackupButtons(Vector2 buttonSize)
    {
        var backup = new ModBackup(modExportManager, _mod);
        var tt = ModBackup.CreatingBackup
            ? "已经创建了一个备份。"
            : backup.Exists
                ? $"用当前模组覆盖当前备份：\"{backup.Name}\"。"
                : $"创建一个备份压缩包到：\"{backup.Name}\"。";
        if (ImGuiUtil.DrawDisabledButton( "创建备份", buttonSize, tt, ModBackup.CreatingBackup))
            backup.CreateAsync();

        ImGui.SameLine();
        tt = backup.Exists
            ? $"删除存在的备份文件\"{backup.Name}\" (点击时按住{config.DeleteModModifier})"
            : $"备份文件\"{backup.Name}\"不存在。";
        if (ImGuiUtil.DrawDisabledButton("删除备份", buttonSize, tt, !backup.Exists || !config.DeleteModModifier.IsActive()))
            backup.Delete();

        tt = backup.Exists
            ? $"从备份文件\"{backup.Name}\"恢复模组。(点击时按住{config.DeleteModModifier})"
            : $"备份文件\"{backup.Name}\"不存在。";
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("从备份恢复", buttonSize, tt, !backup.Exists || !config.DeleteModModifier.IsActive()))
            backup.Restore(modManager);
        if (backup.Exists)
        {
            ImGui.SameLine();
            using (var font = ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.TextUnformatted(FontAwesomeIcon.CheckCircle.ToIconString());
            }

            ImGuiUtil.HoverTooltip($"备份已存在于 \"{backup.Name}\".");
        }
    }

    /// <summary> Anything about editing the regular meta information about the mod. </summary>
    private void EditRegularMeta()
    {
        if (Input.Text("模组名称", Input.Name, Input.None, _mod.Name, out var newName, 256, UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModName(_mod, newName);

        if (Input.Text("作者", Input.Author, Input.None, _mod.Author, out var newAuthor, 256, UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModAuthor(_mod, newAuthor);

        if (Input.Text( "版本", Input.Version, Input.None, _mod.Version, out var newVersion, 32,
                UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModVersion(_mod, newVersion);

        if (Input.Text( "网址", Input.Website, Input.None, _mod.Website, out var newWebsite, 256,
                UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModWebsite(_mod, newWebsite);

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(UiHelpers.ScaleX3));

        var reducedSize = new Vector2(UiHelpers.InputTextMinusButton3, 0);
        if (ImGui.Button("编辑描述", reducedSize))
            descriptionPopup.Open(_mod);


        ImGui.SameLine();
        var fileExists = File.Exists(filenames.ModMetaPath(_mod));
        var tt = fileExists
            ? "在指定的文本编辑器中打开元数据json文件。"
            : "元数据json文件不存在。";
        if (ImGuiUtil.DrawDisabledButton($"{FontAwesomeIcon.FileExport.ToIconString()}##metaFile", UiHelpers.IconButtonSize, tt,
                !fileExists, true))
            Process.Start(new ProcessStartInfo(filenames.ModMetaPath(_mod)) { UseShellExecute = true });

        DrawOpenDefaultMod();
    }

    private void DrawOpenDefaultMod()
    {
        var file       = filenames.OptionGroupFile(_mod, -1, false);
        var fileExists = File.Exists(file);
        var tt = fileExists
            ? "Open the default mod data file in the text editor of your choice."
            : "The default mod data file does not exist.";
        if (ImGuiUtil.DrawDisabledButton("Open Default Data", UiHelpers.InputTextWidth, tt, !fileExists))
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
    }


    /// <summary> A text input for the new directory name and a button to apply the move. </summary>
    private static class MoveDirectory
    {
        private static string?           _currentModDirectory;
        private static NewDirectoryState _state = NewDirectoryState.Identical;

        public static void Reset()
        {
            _currentModDirectory = null;
            _state               = NewDirectoryState.Identical;
        }

        public static void Draw(ModManager modManager, Mod mod, Vector2 buttonSize)
        {
            ImGui.SetNextItemWidth(buttonSize.X * 2 + ImGui.GetStyle().ItemSpacing.X);
            var tmp = _currentModDirectory ?? mod.ModPath.Name;
            if (ImGui.InputText("##newModMove", ref tmp, 64))
            {
                _currentModDirectory = tmp;
                _state               = modManager.NewDirectoryValid(mod.ModPath.Name, _currentModDirectory, out _);
            }

            var (disabled, tt) = _state switch
            {
                NewDirectoryState.Identical      => (true, "当前目录名称与新目录名称相同。"),
                NewDirectoryState.Empty          => (true, "请先输入一个目录名称。"),
                NewDirectoryState.NonExisting    => (false, $"将模组从 {mod.ModPath.Name} 移动到 {_currentModDirectory}."),
                NewDirectoryState.ExistsEmpty    => (false, $"将模组从 {mod.ModPath.Name} 移动到 {_currentModDirectory}."),
                NewDirectoryState.ExistsNonEmpty => (true, $"{_currentModDirectory} 已存在并且不是空目录。"),
                NewDirectoryState.ExistsAsFile   => (true, $"{_currentModDirectory} 已经以文件形式存在。"),
                NewDirectoryState.ContainsInvalidSymbols => (true,
                    $"{_currentModDirectory} 包含不被游戏接受的非法字符。"),
                _ => (true, "未知错误。"),
            };
            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton( "重命名模组目录", buttonSize, tt, disabled) && _currentModDirectory != null)
            {
                modManager.MoveModDirectory(mod, _currentModDirectory);
                Reset();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "模组目录名称用于对应存储的设置和排序顺序，它不会影响任何显示内容。\n"
              + "目前，这不能用于预先存在的文件夹，并且不支持合并或覆盖。");
        }
    }

    /// <summary> Handles input text and integers in separate fields without buffers for every single one. </summary>
    private static class Input
    {
        // Special field indices to reuse the same string buffer.
        public const int None        = -1;
        public const int Name        = -2;
        public const int Author      = -3;
        public const int Version     = -4;
        public const int Website     = -5;
        public const int Path        = -6;
        public const int Description = -7;

        // Temporary strings
        private static string?      _currentEdit;
        private static ModPriority? _currentGroupPriority;
        private static int          _currentField = None;
        private static int          _optionIndex  = None;

        public static void Reset()
        {
            _currentEdit          = null;
            _currentGroupPriority = null;
            _currentField         = None;
            _optionIndex          = None;
        }

        public static bool Text(string label, int field, int option, string oldValue, out string value, uint maxLength, float width)
        {
            var tmp = field == _currentField && option == _optionIndex ? _currentEdit ?? oldValue : oldValue;
            ImGui.SetNextItemWidth(width);

            if (ImGui.InputText(label, ref tmp, maxLength))
            {
                _currentEdit  = tmp;
                _optionIndex  = option;
                _currentField = field;
            }

            if (ImGui.IsItemDeactivatedAfterEdit() && _currentEdit != null)
            {
                var ret = _currentEdit != oldValue;
                value = _currentEdit;
                Reset();
                return ret;
            }

            value = string.Empty;
            return false;
        }

        public static bool Priority(string label, int field, int option, ModPriority oldValue, out ModPriority value, float width)
        {
            var tmp = (field == _currentField && option == _optionIndex ? _currentGroupPriority ?? oldValue : oldValue).Value;
            ImGui.SetNextItemWidth(width);
            if (ImGui.InputInt(label, ref tmp, 0, 0))
            {
                _currentGroupPriority = new ModPriority(tmp);
                _optionIndex          = option;
                _currentField         = field;
            }

            if (ImGui.IsItemDeactivatedAfterEdit() && _currentGroupPriority != null)
            {
                var ret = _currentGroupPriority != oldValue;
                value = _currentGroupPriority.Value;
                Reset();
                return ret;
            }

            value = ModPriority.Default;
            return false;
        }
    }
}
