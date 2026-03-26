using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.Services;
using Penumbra.UI.ModsTab.Groups;

namespace Penumbra.UI.ModsTab;

public class ModPanelEditTab(
    ModManager modManager,
    ModFileSystem fileSystem,
    Services.MessageService messager,
    FilenameService filenames,
    ModExportManager modExportManager,
    Configuration config,
    PredefinedTagManager predefinedTagManager,
    ModGroupEditDrawer groupEditDrawer,
    DescriptionEditPopup descriptionPopup,
    AddGroupDrawer addGroupDrawer)
    : ITab<ModPanelTab>
{
    private IFileSystemData<Mod> _leaf             = null!;
    private Mod                  _mod              = null!;
    private bool                 _groupReorderMode = false;
    private IModGroup?           _draggedGroup     = null;


    public ReadOnlySpan<byte> Label
        => "编辑模组"u8;

    public ModPanelTab Identifier
        => ModPanelTab.Edit;

    public void DrawContent()
    {
        using var child = Im.Child.Begin("##editChild"u8, Im.ContentRegion.Available);
        if (!child)
            return;

        _leaf = (IFileSystemData<Mod>)fileSystem.Selection.Selection!;
        _mod  = _leaf.Value;

        EditButtons();
        EditRegularMeta();
        UiHelpers.DefaultLineSpace();
        EditLocalData();
        UiHelpers.DefaultLineSpace();

        if (Input.Text("模组路径（用于排序）"u8, Input.Path, Input.None, _leaf.FullPath, out var newPath, UiHelpers.InputTextWidth.X))
            try
            {
                fileSystem.RenameAndMove(_leaf, newPath);
            }
            catch (Exception e)
            {
                messager.NotificationMessage(e.Message, NotificationType.Warning, false);
            }

        UiHelpers.DefaultLineSpace();

        FeatureChecker.DrawFeatureFlagInput(modManager.DataEditor, _mod, UiHelpers.InputTextWidth.X);

        UiHelpers.DefaultLineSpace();
        var sharedTagsEnabled     = predefinedTagManager.Enabled;
        var sharedTagButtonOffset = sharedTagsEnabled ? Im.Style.FrameHeight + Im.Style.FramePadding.X : 0;
        var tagIdx = TagButtons.Draw("模组标签： "u8, "点击标签进行编辑，或添加新标签。空标签将被移除。"u8, _mod.ModTags,
            out var editedTag, rightEndOffset: sharedTagButtonOffset);
        if (tagIdx >= 0)
            modManager.DataEditor.ChangeModTag(_mod, tagIdx, editedTag);

        if (sharedTagsEnabled)
            predefinedTagManager.DrawAddFromSharedTagsAndUpdateTags(_mod, false);

        UiHelpers.DefaultLineSpace();
        if (Im.Tree.Header("组编辑"u8))
        {
            UiHelpers.DefaultLineSpace();
            addGroupDrawer.Draw(_mod, UiHelpers.InputTextWidth.X);
            UiHelpers.DefaultLineSpace();

            if (Im.RadioButton("组编辑模式"u8, !_groupReorderMode))
                _groupReorderMode = false;
            Im.Line.SameInner();
            if (Im.RadioButton("组排序模式"u8, _groupReorderMode))
                _groupReorderMode = true;

            UiHelpers.DefaultLineSpace();

            if (_groupReorderMode)
                DrawGroupReordering(_mod);
            else
                groupEditDrawer.Draw(_mod);
        }

        descriptionPopup.Draw();
    }

    public void Reset()
    {
        MoveDirectory.Reset();
        Input.Reset();
    }

    private void DrawGroupReordering(Mod mod)
    {
        using var table = Im.Table.Begin("##reorder"u8, 5, TableFlags.BordersOuter | TableFlags.RowBackground);
        if (!table)
            return;

        table.SetupColumn("索引"u8, TableColumnFlags.WidthFixed, Im.Font.CalculateSize("组 #00  "u8).X);
        table.SetupColumn("组"u8, TableColumnFlags.WidthStretch);
        table.SetupColumn("类型"u8, TableColumnFlags.WidthFixed, Im.Font.CalculateSize("合并  "u8).X);
        table.SetupColumn("选项"u8, TableColumnFlags.WidthFixed, Im.Font.CalculateSize("1000 选项  "u8).X);
        table.SetupColumn("优先级##actions"u8, TableColumnFlags.WidthFixed, Im.Style.FrameHeight * 3 + Im.Style.ItemInnerSpacing.X);
        table.HeaderRow();

        var        active   = config.DeleteModModifier.IsActive();
        using var  clip     = new Im.ListClipper(mod.Groups.Count, Im.Style.FrameHeightWithSpacing);
        IModGroup? deletion = null;
        foreach(var i in clip)
        {
            using var id    = Im.Id.Push(i);
            var       group = mod.Groups[i];
            table.DrawFrameColumn($"组 #{i + 1:D2}");

            table.NextColumn();
            Im.Selectable(group.Name);
            using (var source = Im.DragDrop.Source())
            {
                if (source)
                {
                    source.SetPayload("##group"u8);
                    _draggedGroup = group;
                    Im.Text($"拖拽组 #{i + 1} - {group.Name}...");
                }
            }

            using (var target = Im.DragDrop.Target())
            {
                if (target.IsDropping("##group"u8) && _draggedGroup is not null)
                {
                    modManager.OptionEditor.MoveModGroup(_draggedGroup, i);
                    _draggedGroup = null;
                }
            }


            table.DrawFrameColumn($"{group.Type}");

            table.DrawFrameColumn($"{group.Options.Count} 个选项");

            table.NextColumn();
            Im.Item.SetNextWidth(2 * Im.Style.FrameHeight);
            if (ImEx.InputOnDeactivation.Scalar("##prio"u8, group.Priority.Value, out var newPriority))
                modManager.OptionEditor.ChangeGroupPriority(group, new ModPriority(newPriority));
            Im.Line.SameInner();
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "删除此选项组。"u8, !active))
                deletion = group;

            if (!active)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteModModifier} 删除。");
        }
        if (deletion is not null)
            modManager.OptionEditor.DeleteModGroup(deletion);
    }

    /// <summary> The general edit row for non-detailed mod edits. </summary>
    private void EditButtons()
    {
        var buttonSize   = new Vector2(150 * Im.Style.GlobalScale, 0);
        var folderExists = Directory.Exists(_mod.ModPath.FullName);
        if (ImEx.Button("打开模组目录"u8, buttonSize, folderExists
                ? $"在您选择的文件浏览器中打开 \"{_mod.ModPath.FullName}\"。"
                : $"模组目录 \"{_mod.ModPath.FullName}\" 不存在。", !folderExists))
            Process.Start(new ProcessStartInfo(_mod.ModPath.FullName) { UseShellExecute = true });

        Im.Line.Same();
        if (ImEx.Button("重新加载模组"u8, buttonSize, "从其文件重新加载当前模组。\n"u8
              + "如果模组目录或元数据文件不存在，或者新模组名称为空，则模组会被删除。"u8,
                false))
            modManager.ReloadMod(_mod);

        BackupButtons(buttonSize);
        MoveDirectory.Draw(modManager, _mod, buttonSize);

        UiHelpers.DefaultLineSpace();
    }

    private void BackupButtons(Vector2 buttonSize)
    {
        var backup = new ModBackup(modExportManager, _mod);
        if (ImEx.Button("备份模组"u8, buttonSize, ModBackup.CreatingBackup
                ? "正在备份模组。"
                : backup.Exists
                    ? $"用当前模组覆盖当前备份的模组 \"{backup.Name}\"。"
                    : $"创建当前模组的备份压缩包到 \"{backup.Name}\"。", ModBackup.CreatingBackup))
            _ = backup.CreateAsync();

        if (Im.Item.RightClicked())
            Im.Popup.Open("context"u8);

        Im.Line.Same();
        if (ImEx.Button("删除导出"u8, buttonSize, backup.Exists
                ? $"删除存在的备份文件：\"{backup.Name}\" (点击时按住{config.DeleteModModifier})。"
                : $"备份文件\"{backup.Name}\"不存在。", !backup.Exists || !config.DeleteModModifier.IsActive()))
            backup.Delete();

        Im.Line.Same();
        if (ImEx.Button("从备份恢复"u8, buttonSize, backup.Exists
                ? $"从备份文件：\"{backup.Name}\" 恢复模组 (点击时按住{config.DeleteModModifier})。"
                : $"备份文件\"{backup.Name}\"不存在。", !backup.Exists || !config.DeleteModModifier.IsActive()))
            backup.Restore(modManager);
        if (backup.Exists)
        {
            Im.Line.Same();
            ImEx.Icon.Draw(FontAwesomeIcon.CheckCircle.Icon());
            Im.Tooltip.OnHover($"备份已存在于 \"{backup.Name}\"。");
        }

        using var context = Im.Popup.Begin("context"u8);
        if (!context)
            return;

        if (Im.Selectable("打开备份目录"u8))
            Process.Start(new ProcessStartInfo(modExportManager.ExportDirectory.FullName) { UseShellExecute = true });
    }

    /// <summary> Anything about editing the regular meta information about the mod. </summary>
    private void EditRegularMeta()
    {
        if (Input.Text("模组名称"u8, Input.Name, Input.None, _mod.Name, out var newName, UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModName(_mod, newName);

        if (Input.Text("作者"u8, Input.Author, Input.None, _mod.Author, out var newAuthor, UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModAuthor(_mod, newAuthor);

        if (Input.Text("版本"u8, Input.Version, Input.None, _mod.Version, out var newVersion,
                UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModVersion(_mod, newVersion);

        if (Input.Text("网址"u8, Input.Website, Input.None, _mod.Website, out var newWebsite,
                UiHelpers.InputTextWidth.X))
            modManager.DataEditor.ChangeModWebsite(_mod, newWebsite);

        using var style = ImStyleDouble.ItemSpacing.Push(new Vector2(Im.Style.GlobalScale * 3));

        var reducedSize = new Vector2(UiHelpers.InputTextMinusButton3, 0);
        if (Im.Button("编辑描述"u8, reducedSize))
            descriptionPopup.Open(_mod);


        Im.Line.Same();
        var fileExists = File.Exists(filenames.ModMetaPath(_mod));
        var tt = fileExists
            ? "在您选择的文本编辑器中打开元数据json文件。"u8
            : "元数据json文件不存在。"u8;
        using (Im.Id.Push("meta"u8))
        {
            if (ImEx.Icon.Button(LunaStyle.FileExportIcon, tt, !fileExists))
                Process.Start(new ProcessStartInfo(filenames.ModMetaPath(_mod)) { UseShellExecute = true });
        }

        DrawOpenDefaultMod();
    }

    private void EditLocalData()
    {
        DrawImportDate();
        ImEx.TextFramed($"{DateTimeOffset.FromUnixTimeMilliseconds(_mod.LastConfigEdit).ToLocalTime():yyyy/MM/dd HH:mm}",
            UiHelpers.InputTextWidth with { Y = 0 }, ImGuiColor.FrameBackground.Get(0.5f));
        Im.Line.SameInner();
        Im.Text("最后一次配置编辑"u8);
    }

    private void DrawImportDate()
    {
        ImEx.TextFramed($"{DateTimeOffset.FromUnixTimeMilliseconds(_mod.ImportDate).ToLocalTime():yyyy/MM/dd HH:mm}",
            new Vector2(UiHelpers.InputTextMinusButton3, 0), ImGuiColor.FrameBackground.Get(0.5f));
        Im.Line.Same(0, 3 * Im.Style.GlobalScale);

        var canRefresh = config.DeleteModModifier.IsActive();
        if (ImEx.Icon.Button(LunaStyle.RefreshIcon, canRefresh
                    ? "重置导入日期为当前日期和时间。"u8
                    : $"重置导入日期为当前日期和时间。\n点击时按住 {config.DeleteModModifier} 刷新。",
                !canRefresh))
            modManager.DataEditor.ResetModImportDate(_mod);
        Im.Line.SameInner();
        Im.Text("导入日期"u8);
    }

    private void DrawOpenDefaultMod()
    {
        var file       = filenames.OptionGroupFile(_mod, -1, false);
        var fileExists = File.Exists(file);
        var tt = fileExists
            ? "在您选择的文本编辑器中打开默认模组数据文件。"u8
            : "默认模组数据文件不存在。"u8;
        if (ImEx.Button("打开默认数据"u8, UiHelpers.InputTextWidth, tt, !fileExists))
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
            Im.Item.SetNextWidth(buttonSize.X * 2 + Im.Style.ItemSpacing.X);
            var tmp = _currentModDirectory ?? mod.ModPath.Name;
            if (Im.Input.Text("##newModMove"u8, ref tmp))
            {
                _currentModDirectory = tmp;
                _state               = modManager.NewDirectoryValid(mod.ModPath.Name, _currentModDirectory, out _);
            }

            var (disabled, tt) = _state switch
            {
                NewDirectoryState.Identical      => (true, "当前目录名称与新目录名称相同。"),
                NewDirectoryState.Empty          => (true, "请先输入新目录名称。"),
                NewDirectoryState.NonExisting    => (false, $"将模组从 {mod.ModPath.Name} 移动到 {_currentModDirectory}。"),
                NewDirectoryState.ExistsEmpty    => (false, $"将模组从 {mod.ModPath.Name} 移动到 {_currentModDirectory}。"),
                NewDirectoryState.ExistsNonEmpty => (true, $"{_currentModDirectory} 已存在并且不是空目录。"),
                NewDirectoryState.ExistsAsFile   => (true, $"{_currentModDirectory} 已经以文件形式存在。"),
                NewDirectoryState.ContainsInvalidSymbols => (true,
                    $"{_currentModDirectory} 包含不被游戏接受的非法字符。"),
                _ => (true, "未知错误。"),
            };
            Im.Line.Same();
            if (ImEx.Button("重命名模组目录"u8, buttonSize, tt, disabled) && _currentModDirectory is not null)
            {
                modManager.MoveModDirectory(mod, _currentModDirectory);
                Reset();
            }

            Im.Line.SameInner();
            LunaStyle.DrawAlignedHelpMarker(StringU8.Empty,
                "模组目录名称用于对应存储的设置和排序顺序，它不会影响任何显示内容。\n"u8
              + "目前，这不能用于预先存在的文件夹，并且不支持合并或覆盖。"u8);
        }
    }

    /// <summary> Handles input text and integers in separate fields without buffers for every single one. </summary>
    private static class Input
    {
        // Special field indices to reuse the same string buffer.
        public const int None    = -1;
        public const int Name    = -2;
        public const int Author  = -3;
        public const int Version = -4;
        public const int Website = -5;
        public const int Path    = -6;

        // Temporary strings
        private static string? _currentEdit;
        private static int     _currentField = None;
        private static int     _optionIndex  = None;

        public static void Reset()
        {
            _currentEdit  = null;
            _currentField = None;
            _optionIndex  = None;
        }

        public static bool Text(ReadOnlySpan<byte> label, int field, int option, string oldValue, out string value, float width)
        {
            var tmp = field == _currentField && option == _optionIndex ? _currentEdit ?? oldValue : oldValue;
            Im.Item.SetNextWidth(width);

            if (Im.Input.Text(label, ref tmp))
            {
                _currentEdit  = tmp;
                _optionIndex  = option;
                _currentField = field;
            }

            if (Im.Item.DeactivatedAfterEdit && _currentEdit is not null)
            {
                var ret = _currentEdit != oldValue;
                value = _currentEdit;
                Reset();
                return ret;
            }

            value = string.Empty;
            return false;
        }
    }
}
