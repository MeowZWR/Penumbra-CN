using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using OtterGui.Classes;
using Penumbra.Api.Enums;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Subclasses;
using Penumbra.Services;
using Penumbra.UI.AdvancedWindow;

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
    PredefinedTagManager predefinedTagManager)
    : ITab
{
    private readonly ModManager _modManager = modManager;

    private readonly TagButtons _modTags = new();

    private Vector2            _cellPadding = Vector2.Zero;
    private Vector2            _itemSpacing = Vector2.Zero;
    private ModFileSystem.Leaf _leaf        = null!;
    private Mod                _mod         = null!;

    public ReadOnlySpan<byte> Label
        => "编辑模组"u8;

    public void DrawContent()
    {
        using var child = ImRaii.Child("##editChild", -Vector2.One);
        if (!child)
            return;

        _leaf = selector.SelectedLeaf!;
        _mod  = selector.Selected!;

        _cellPadding = ImGui.GetStyle().CellPadding with { X = 2 * UiHelpers.Scale };
        _itemSpacing = ImGui.GetStyle().CellPadding with { X = 4 * UiHelpers.Scale };

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
            _modManager.DataEditor.ChangeModTag(_mod, tagIdx, editedTag);

        if (sharedTagsEnabled)
            predefinedTagManager.DrawAddFromSharedTagsAndUpdateTags(selector.Selected!.LocalTags, selector.Selected!.ModTags, false,
                selector.Selected!);

        UiHelpers.DefaultLineSpace();
        AddOptionGroup.Draw(filenames, _modManager, _mod, config.ReplaceNonAsciiOnImport);
        UiHelpers.DefaultLineSpace();

        for (var groupIdx = 0; groupIdx < _mod.Groups.Count; ++groupIdx)
            EditGroup(groupIdx);

        EndActions();
        DescriptionEdit.DrawPopup(_modManager);
    }

    public void Reset()
    {
        AddOptionGroup.Reset();
        MoveDirectory.Reset();
        Input.Reset();
        OptionTable.Reset();
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
            _modManager.ReloadMod(_mod);

        BackupButtons(buttonSize);
        MoveDirectory.Draw(_modManager, _mod, buttonSize);

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
            ? $"删除存在的备份文件 \"{backup.Name}\" (点击时按住{config.DeleteModModifier})."
            : $"备份文件\"{backup.Name}\"不存在。";
        if (ImGuiUtil.DrawDisabledButton("删除备份", buttonSize, tt, !backup.Exists || !config.DeleteModModifier.IsActive()))
            backup.Delete();

        tt = backup.Exists
            ? $"从备份文件\"{backup.Name}\"恢复模组。(点击时按住{ config.DeleteModModifier})."
            : $"备份文件\"{backup.Name}\"不存在。";
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("从备份恢复", buttonSize, tt, !backup.Exists || !config.DeleteModModifier.IsActive()))
            backup.Restore(_modManager);
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
        if (Input.Text( "模组名", Input.Name, Input.None, _mod.Name, out var newName, 256, UiHelpers.InputTextWidth.X))
            _modManager.DataEditor.ChangeModName(_mod, newName);

        if (Input.Text( "作者", Input.Author, Input.None, _mod.Author, out var newAuthor, 256, UiHelpers.InputTextWidth.X))
            _modManager.DataEditor.ChangeModAuthor(_mod, newAuthor);

        if (Input.Text( "版本", Input.Version, Input.None, _mod.Version, out var newVersion, 32,
                UiHelpers.InputTextWidth.X))
            _modManager.DataEditor.ChangeModVersion(_mod, newVersion);

        if (Input.Text( "网址", Input.Website, Input.None, _mod.Website, out var newWebsite, 256,
                UiHelpers.InputTextWidth.X))
            _modManager.DataEditor.ChangeModWebsite(_mod, newWebsite);

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(UiHelpers.ScaleX3));

        var reducedSize = new Vector2(UiHelpers.InputTextMinusButton3, 0);
        if (ImGui.Button( "编辑描述", reducedSize))
            _delayedActions.Enqueue(() => DescriptionEdit.OpenPopup(_mod, Input.Description));

        ImGui.SameLine();
        var fileExists = File.Exists(filenames.ModMetaPath(_mod));
        var tt = fileExists
            ? "在指定的文本编辑器中打开元数据json文件。"
            : "元数据json文件不存在。";
        if (ImGuiUtil.DrawDisabledButton($"{FontAwesomeIcon.FileExport.ToIconString()}##metaFile", UiHelpers.IconButtonSize, tt,
                !fileExists, true))
            Process.Start(new ProcessStartInfo(filenames.ModMetaPath(_mod)) { UseShellExecute = true });
    }

    /// <summary> Do some edits outside of iterations. </summary>
    private readonly Queue<Action> _delayedActions = new();

    /// <summary> Delete a marked group or option outside of iteration. </summary>
    private void EndActions()
    {
        while (_delayedActions.TryDequeue(out var action))
            action.Invoke();
    }

    /// <summary> Text input to add a new option group at the end of the current groups. </summary>
    private static class AddOptionGroup
    {
        private static string _newGroupName = string.Empty;

        public static void Reset()
            => _newGroupName = string.Empty;

        public static void Draw(FilenameService filenames, ModManager modManager, Mod mod, bool onlyAscii)
        {
            using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(UiHelpers.ScaleX3));
            ImGui.SetNextItemWidth(UiHelpers.InputTextMinusButton3);
            ImGui.InputTextWithHint("##newGroup", "添加新的选项组...", ref _newGroupName, 256);
            ImGui.SameLine();
            var defaultFile = filenames.OptionGroupFile(mod, -1, onlyAscii);
            var fileExists  = File.Exists(defaultFile);
            var tt = fileExists
                ? "在你的操作系统文本编辑器中打开默认的json选项文件。"
                : "默认的json选项文件不存在。";
            if (ImGuiUtil.DrawDisabledButton($"{FontAwesomeIcon.FileExport.ToIconString()}##defaultFile", UiHelpers.IconButtonSize, tt,
                    !fileExists, true))
                Process.Start(new ProcessStartInfo(defaultFile) { UseShellExecute = true });

            ImGui.SameLine();

            var nameValid = ModOptionEditor.VerifyFileName(mod, null, _newGroupName, false);
            tt = nameValid ? "添加新的选项组到这个模组。" : "不能添加以此命名的组。";
            if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), UiHelpers.IconButtonSize,
                    tt, !nameValid, true))
                return;

            modManager.OptionEditor.AddModGroup(mod, GroupType.Single, _newGroupName);
            Reset();
        }
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
                "模组目录名用于模组在操作系统中的存储文件、设置及排序。不会在模组选择器中显示。\n"
              + "当前不能用于预先存在的文件夹，不支持合并或覆盖。" );
        }
    }

    /// <summary> Open a popup to edit a multi-line mod or option description. </summary>
    private static class DescriptionEdit
    {
        private const  string           PopupName                = "编辑描述";
        private static string _newDescription          = string.Empty;
        private static string _oldDescription          = string.Empty;
        private static int    _newDescriptionIdx       = -1;
        private static int    _newDescriptionOptionIdx = -1;
        private static Mod?   _mod;

        public static void OpenPopup(Mod mod, int groupIdx, int optionIdx = -1)
        {
            _newDescriptionIdx       = groupIdx;
            _newDescriptionOptionIdx = optionIdx;
            _newDescription = groupIdx < 0
                ? mod.Description
                : optionIdx < 0
                    ? mod.Groups[groupIdx].Description
                    : mod.Groups[groupIdx][optionIdx].Description;
            _oldDescription = _newDescription;

            _mod = mod;
            ImGui.OpenPopup(PopupName);
        }

        public static void DrawPopup(ModManager modManager)
        {
            if (_mod == null)
                return;

            using var popup = ImRaii.Popup(PopupName);
            if (!popup)
                return;

            if (ImGui.IsWindowAppearing())
                ImGui.SetKeyboardFocusHere();

            ImGui.InputTextMultiline("##editDescription", ref _newDescription, 4096, ImGuiHelpers.ScaledVector2(800, 800));
            UiHelpers.DefaultLineSpace();

            var buttonSize = ImGuiHelpers.ScaledVector2(100, 0);
            var width = 2 * buttonSize.X
              + 4 * ImGui.GetStyle().FramePadding.X
              + ImGui.GetStyle().ItemSpacing.X;
            ImGui.SetCursorPosX((800 * UiHelpers.Scale - width) / 2);

            var tooltip = _newDescription != _oldDescription ? string.Empty : "还没有做任何修改。";

            if (ImGuiUtil.DrawDisabledButton("保存", buttonSize, tooltip, tooltip.Length > 0))
            {
                switch (_newDescriptionIdx)
                {
                    case Input.Description:
                        modManager.DataEditor.ChangeModDescription(_mod, _newDescription);
                        break;
                    case >= 0:
                        if (_newDescriptionOptionIdx < 0)
                            modManager.OptionEditor.ChangeGroupDescription(_mod, _newDescriptionIdx, _newDescription);
                        else
                            modManager.OptionEditor.ChangeOptionDescription(_mod, _newDescriptionIdx, _newDescriptionOptionIdx,
                                _newDescription);

                        break;
                }

                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (!ImGui.Button( "取消", buttonSize)
             && !ImGui.IsKeyPressed(ImGuiKey.Escape))
                return;

            _newDescriptionIdx = Input.None;
            _newDescription    = string.Empty;
            ImGui.CloseCurrentPopup();
        }
    }

    private void EditGroup(int groupIdx)
    {
        var       group = _mod.Groups[groupIdx];
        using var id    = ImRaii.PushId(groupIdx);
        using var frame = ImRaii.FramedGroup($"选项组 #{groupIdx + 1}");

        using var style = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, _cellPadding)
            .Push(ImGuiStyleVar.ItemSpacing, _itemSpacing);

        if (Input.Text("##Name", groupIdx, Input.None, group.Name, out var newGroupName, 256, UiHelpers.InputTextWidth.X))
            _modManager.OptionEditor.RenameModGroup(_mod, groupIdx, newGroupName);

        ImGuiUtil.HoverTooltip( "组名称" );
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), UiHelpers.IconButtonSize,
                "删除此选项组。\n同时按住键盘Ctrl键点击来删除。", !ImGui.GetIO().KeyCtrl, true))
            _delayedActions.Enqueue(() => _modManager.OptionEditor.DeleteModGroup(_mod, groupIdx));

        ImGui.SameLine();

        if (Input.Priority("##Priority", groupIdx, Input.None, group.Priority, out var priority, 50 * UiHelpers.Scale))
            _modManager.OptionEditor.ChangeGroupPriority(_mod, groupIdx, priority);

        ImGuiUtil.HoverTooltip( "组优先级" );

        DrawGroupCombo(group, groupIdx);
        ImGui.SameLine();

        var tt = groupIdx == 0 ? "到顶了。" : $"移动此组到[选项组 #{groupIdx}]之前。";
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.ArrowUp.ToIconString(), UiHelpers.IconButtonSize,
                tt, groupIdx == 0, true))
            _delayedActions.Enqueue(() => _modManager.OptionEditor.MoveModGroup(_mod, groupIdx, groupIdx - 1));

        ImGui.SameLine();
        tt = groupIdx == _mod.Groups.Count - 1
            ? "到底了。"
            : $"移动此组到[选项组 #{groupIdx + 2}]之后。";
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.ArrowDown.ToIconString(), UiHelpers.IconButtonSize,
                tt, groupIdx == _mod.Groups.Count - 1, true))
            _delayedActions.Enqueue(() => _modManager.OptionEditor.MoveModGroup(_mod, groupIdx, groupIdx + 1));

        ImGui.SameLine();

        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Edit.ToIconString(), UiHelpers.IconButtonSize,
                "编辑组描述", false, true))
            _delayedActions.Enqueue(() => DescriptionEdit.OpenPopup(_mod, groupIdx));

        ImGui.SameLine();
        var fileName   = filenames.OptionGroupFile(_mod, groupIdx, config.ReplaceNonAsciiOnImport);
        var fileExists = File.Exists(fileName);
        tt = fileExists
            ? $"在文本编辑器中打开 {group.Name} 的json文件。"
            : $"{group.Name} 的json文件不存在。";
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.FileExport.ToIconString(), UiHelpers.IconButtonSize, tt, !fileExists, true))
            Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });

        UiHelpers.DefaultLineSpace();

        OptionTable.Draw(this, groupIdx);
    }

    /// <summary> Draw the table displaying all options and the add new option line. </summary>
    private static class OptionTable
    {
        private const string DragDropLabel = "##DragOption";

        private static int    _newOptionNameIdx  = -1;
        private static string _newOptionName     = string.Empty;
        private static int    _dragDropGroupIdx  = -1;
        private static int    _dragDropOptionIdx = -1;

        public static void Reset()
        {
            _newOptionNameIdx  = -1;
            _newOptionName     = string.Empty;
            _dragDropGroupIdx  = -1;
            _dragDropOptionIdx = -1;
        }

        public static void Draw(ModPanelEditTab panel, int groupIdx)
        {
            using var table = ImRaii.Table(string.Empty, 6, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            var maxWidth = ImGui.CalcTextSize("Option #88.").X;
            ImGui.TableSetupColumn("idx",     ImGuiTableColumnFlags.WidthFixed, maxWidth);
            ImGui.TableSetupColumn("default", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthFixed,
                UiHelpers.InputTextWidth.X - maxWidth - 12 * UiHelpers.Scale - ImGui.GetFrameHeight() - UiHelpers.IconButtonSize.X);
            ImGui.TableSetupColumn("description", ImGuiTableColumnFlags.WidthFixed, UiHelpers.IconButtonSize.X);
            ImGui.TableSetupColumn("delete",      ImGuiTableColumnFlags.WidthFixed, UiHelpers.IconButtonSize.X);
            ImGui.TableSetupColumn("priority",    ImGuiTableColumnFlags.WidthFixed, 50 * UiHelpers.Scale);

            var group = panel._mod.Groups[groupIdx];
            for (var optionIdx = 0; optionIdx < group.Count; ++optionIdx)
                EditOption(panel, group, groupIdx, optionIdx);

            DrawNewOption(panel, groupIdx, UiHelpers.IconButtonSize);
        }

        /// <summary> Draw a line for a single option. </summary>
        private static void EditOption(ModPanelEditTab panel, IModGroup group, int groupIdx, int optionIdx)
        {
            var       option = group[optionIdx];
            using var id     = ImRaii.PushId(optionIdx);
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Selectable($"选项 #{optionIdx + 1}");
            Source(group, groupIdx, optionIdx);
            Target(panel, group, groupIdx, optionIdx);

            ImGui.TableNextColumn();


            if (group.Type == GroupType.Single)
            {
                if (ImGui.RadioButton("##default", group.DefaultSettings == optionIdx))
                    panel._modManager.OptionEditor.ChangeModGroupDefaultOption(panel._mod, groupIdx, (uint)optionIdx);

                ImGuiUtil.HoverTooltip($"将'{option.Name}' 设置为此组的默认选项。" );
            }
            else
            {
                var isDefaultOption = ((group.DefaultSettings >> optionIdx) & 1) != 0;
                if (ImGui.Checkbox("##default", ref isDefaultOption))
                    panel._modManager.OptionEditor.ChangeModGroupDefaultOption(panel._mod, groupIdx, isDefaultOption
                        ? group.DefaultSettings | (1u << optionIdx)
                        : group.DefaultSettings & ~(1u << optionIdx));

                ImGuiUtil.HoverTooltip($"将'{option.Name}'在此组中默认设置为：{( isDefaultOption ? "'禁用'" : "'启用'" )} " );
            }

            ImGui.TableNextColumn();
            if (Input.Text("##Name", groupIdx, optionIdx, option.Name, out var newOptionName, 256, -1))
                panel._modManager.OptionEditor.RenameOption(panel._mod, groupIdx, optionIdx, newOptionName);

            ImGui.TableNextColumn();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Edit.ToIconString(), UiHelpers.IconButtonSize, "编辑选项描述。",
                    false, true))
                panel._delayedActions.Enqueue(() => DescriptionEdit.OpenPopup(panel._mod, groupIdx, optionIdx));

            ImGui.TableNextColumn();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), UiHelpers.IconButtonSize,
                    "删除此选项。\n按住键盘Ctrl键点击来删除。", !ImGui.GetIO().KeyCtrl, true))
                panel._delayedActions.Enqueue(() => panel._modManager.OptionEditor.DeleteOption(panel._mod, groupIdx, optionIdx));

            ImGui.TableNextColumn();
            if (group.Type != GroupType.Multi)
                return;

            if (Input.Priority("##Priority", groupIdx, optionIdx, group.OptionPriority(optionIdx), out var priority,
                    50 * UiHelpers.Scale))
                panel._modManager.OptionEditor.ChangeOptionPriority(panel._mod, groupIdx, optionIdx, priority);

            ImGuiUtil.HoverTooltip( "选项优先级。" );
        }

        /// <summary> Draw the line to add a new option. </summary>
        private static void DrawNewOption(ModPanelEditTab panel, int groupIdx, Vector2 iconButtonSize)
        {
            var mod   = panel._mod;
            var group = mod.Groups[groupIdx];
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Selectable($"选项 #{group.Count + 1}");
            Target(panel, group, groupIdx, group.Count);
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            var tmp = _newOptionNameIdx == groupIdx ? _newOptionName : string.Empty;
            if (ImGui.InputTextWithHint("##newOption", "添加新选项...", ref tmp, 256))
            {
                _newOptionName    = tmp;
                _newOptionNameIdx = groupIdx;
            }

            ImGui.TableNextColumn();
            var canAddGroup = mod.Groups[groupIdx].Type != GroupType.Multi || mod.Groups[groupIdx].Count < IModGroup.MaxMultiOptions;
            var validName   = _newOptionName.Length > 0 && _newOptionNameIdx == groupIdx;
            var tt = canAddGroup
                ? validName ? "添加一个新选项到此组。" : "请先为新选项命名。"
                : $"不能添加超过 {IModGroup.MaxMultiOptions} 个选项到多选项组。";
            if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconButtonSize,
                    tt, !(canAddGroup && validName), true))
                return;

            panel._modManager.OptionEditor.AddOption(mod, groupIdx, _newOptionName);
            _newOptionName = string.Empty;
        }

        // Handle drag and drop to move options inside a group or into another group.
        private static void Source(IModGroup group, int groupIdx, int optionIdx)
        {
            using var source = ImRaii.DragDropSource();
            if (!source)
                return;

            if (ImGui.SetDragDropPayload(DragDropLabel, IntPtr.Zero, 0))
            {
                _dragDropGroupIdx  = groupIdx;
                _dragDropOptionIdx = optionIdx;
            }

            ImGui.TextUnformatted($"从组 {group.Name} 拖拽选项 {group[optionIdx].Name} ..." );
        }

        private static void Target(ModPanelEditTab panel, IModGroup group, int groupIdx, int optionIdx)
        {
            using var target = ImRaii.DragDropTarget();
            if (!target.Success || !ImGuiUtil.IsDropping(DragDropLabel))
                return;

            if (_dragDropGroupIdx >= 0 && _dragDropOptionIdx >= 0)
            {
                if (_dragDropGroupIdx == groupIdx)
                {
                    var sourceOption = _dragDropOptionIdx;
                    panel._delayedActions.Enqueue(
                        () => panel._modManager.OptionEditor.MoveOption(panel._mod, groupIdx, sourceOption, optionIdx));
                }
                else
                {
                    // Move from one group to another by deleting, then adding, then moving the option.
                    var sourceGroupIdx = _dragDropGroupIdx;
                    var sourceOption   = _dragDropOptionIdx;
                    var sourceGroup    = panel._mod.Groups[sourceGroupIdx];
                    var currentCount   = group.Count;
                    var option         = sourceGroup[sourceOption];
                    var priority       = sourceGroup.OptionPriority(_dragDropOptionIdx);
                    panel._delayedActions.Enqueue(() =>
                    {
                        panel._modManager.OptionEditor.DeleteOption(panel._mod, sourceGroupIdx, sourceOption);
                        panel._modManager.OptionEditor.AddOption(panel._mod, groupIdx, option, priority);
                        panel._modManager.OptionEditor.MoveOption(panel._mod, groupIdx, currentCount, optionIdx);
                    });
                }
            }

            _dragDropGroupIdx  = -1;
            _dragDropOptionIdx = -1;
        }
    }

    /// <summary> Draw a combo to select single or multi group and switch between them. </summary>
    private void DrawGroupCombo(IModGroup group, int groupIdx)
    {
        static string GroupTypeName(GroupType type)
            => type switch
            {
                GroupType.Single => "单选项组",
                GroupType.Multi  => "多选项组",
                _                => "未知",
            };

        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X - 2 * UiHelpers.IconButtonSize.X - 2 * ImGui.GetStyle().ItemSpacing.X);
        using var combo = ImRaii.Combo("##GroupType", GroupTypeName(group.Type));
        if (!combo)
            return;

        if (ImGui.Selectable(GroupTypeName(GroupType.Single), group.Type == GroupType.Single))
            _modManager.OptionEditor.ChangeModGroupType(_mod, groupIdx, GroupType.Single);

        var       canSwitchToMulti = group.Count <= IModGroup.MaxMultiOptions;
        using var style            = ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.5f, !canSwitchToMulti);
        if (ImGui.Selectable(GroupTypeName(GroupType.Multi), group.Type == GroupType.Multi) && canSwitchToMulti)
            _modManager.OptionEditor.ChangeModGroupType(_mod, groupIdx, GroupType.Multi);

        style.Pop();
        if (!canSwitchToMulti)
            ImGuiUtil.HoverTooltip($"无法将组转换为多选项组，因为数量超过 {IModGroup.MaxMultiOptions} 个选项。" );
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
        private static string? _currentEdit;
        private static int?    _currentGroupPriority;
        private static int     _currentField = None;
        private static int     _optionIndex  = None;

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

        public static bool Priority(string label, int field, int option, int oldValue, out int value, float width)
        {
            var tmp = field == _currentField && option == _optionIndex ? _currentGroupPriority ?? oldValue : oldValue;
            ImGui.SetNextItemWidth(width);
            if (ImGui.InputInt(label, ref tmp, 0, 0))
            {
                _currentGroupPriority = tmp;
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

            value = 0;
            return false;
        }
    }
}
