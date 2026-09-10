using Dalamud.Utility;
using ImSharp;
using Luna;
using Penumbra.Files;
using Penumbra.Interop;
using Penumbra.Mods.Manager;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public sealed class MainSettings(
    FilenameService fileNames,
    TutorialService tutorial,
    MainConfig config,
    FileDialogService fileDialog,
    ModManager modManager) : IUiService
{
    private const int RootDirectoryMaxLength = 64;

    /// <summary> Changing the base mod directory. </summary>
    private string? _newModDirectory;

    private string _lastCloudSyncTestedPath = string.Empty;
    private bool   _lastCloudSyncTestResult;

    public void DrawHeader()
    {
        DrawEnabledBox();
        Im.Line.New();
        Im.Line.New();

        DrawRootFolder();
        DrawDirectoryButtons();
        Im.Line.New();
        Im.Line.New();
    }

    public void DrawGeneralSettings()
    {
        KeySelector.DoubleModifier("破坏性操作组合键"u8,
            "执行无法轻易恢复的破坏性操作（例如删除）时需要按住的组合键。"u8,
            UiHelpers.InputTextWidth.X, config.DestructiveModifier, v => config.DestructiveModifier = v);
        KeySelector.DoubleModifier("防误触组合键"u8,
            "执行一般可撤销、但不希望被误点的操作（例如隐身模式或临时设置模式开关）时需要按住的组合键。"u8,
            UiHelpers.InputTextWidth.X, config.MisclickModifier, v => config.MisclickModifier = v);
        if (SettingsTab.Checkbox("将成功运行的消息输出到聊天窗口"u8,
                "聊天命令通常只在运行失败时输出消息到聊天窗口，但也可以在成功运行时输出消息供你确认。你可以在此处禁用这个功能。"u8,
                config.PrintSuccessfulCommandsToChat))
            config.PrintSuccessfulCommandsToChat ^= true;

        if (SettingsTab.Checkbox("默认使用临时设置"u8,
                "当你对合集做出任何更改时，先作为临时更改应用，需要点击“设为永久”才会保留。\n\n也可以直接在模组选项卡中更改此项。"u8,
                config.DefaultTemporaryMode))
            config.DefaultTemporaryMode ^= true;

        Im.Line.Spacing();
    }


    /// <summary> Draw the Enable Mods Checkbox.</summary>
    private void DrawEnabledBox()
    {
        if (Im.Checkbox("启用模组"u8, config.EnableMods))
            config.EnableMods ^= true;

        tutorial.OpenTutorial(BasicTutorialSteps.EnableMods);
    }

    /// <summary>
    /// Do not change the directory without explicitly pressing enter or this button.
    /// Shows up only if the current input does not correspond to the current directory.
    /// </summary>
    private bool DrawPressEnterWarning(string newName, string old, float width, bool saved, bool selected)
    {
        using var color = ImGuiColor.Button.Push(Colors.PressEnterWarningBg);
        var (text, valid) = CheckRootDirectoryPath(newName, old, selected);
        var w = new Vector2(Math.Max(width, Im.Font.CalculateButtonSize(text).X), 0);
        return (Im.Button(text, w) || saved) && valid;
    }

    /// <summary> Check a potential new root directory for validity and return the button text and whether it is valid. </summary>
    private (string Text, bool Valid) CheckRootDirectoryPath(string newName, string old, bool selected)
    {
        if (newName.Length > RootDirectoryMaxLength)
            return ($"路径过长。最大长度为 {RootDirectoryMaxLength}。", false);

        if (Path.GetDirectoryName(newName).IsNullOrEmpty())
            return ("不允许将路径设置在驱动器根目录。请添加一个子目录。", false);

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (IsSubPathOf(desktop, newName))
            return ("不允许将路径设置在桌面上。", false);

        var programFiles    = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (IsSubPathOf(programFiles, newName) || IsSubPathOf(programFilesX86, newName))
            return ("不允许将路径设置在 Program Files 中。", false);

        var dalamud = Path.GetDirectoryName(Path.GetDirectoryName(fileNames.ConfigurationDirectory))!;
        if (IsSubPathOf(dalamud, newName))
            return ("不允许将路径设置在 Dalamud 目录内。", false);

        if (WindowsFunctions.GetDownloadsFolder(out var downloads) && IsSubPathOf(downloads, newName))
            return ("不允许将路径设置在下载文件夹内。", false);

        var gameDir = Path.GetDirectoryName(Path.GetDirectoryName(fileNames.GameDataDirectory))!;
        if (IsSubPathOf(gameDir, newName))
            return ("不允许将路径设置在游戏文件夹内。", false);

        if (_lastCloudSyncTestedPath != newName)
        {
            _lastCloudSyncTestResult = CloudApi.IsCloudSynced(newName);
            _lastCloudSyncTestedPath = newName;
        }

        if (_lastCloudSyncTestResult)
            return ("不允许将路径设置在云同步文件夹中。", false);

        return selected
            ? ($"按 Enter 或点击此处保存（当前目录：{old}）", true)
            : ($"点击此处保存（当前目录：{old}）", true);

        static bool IsSubPathOf(string basePath, string subPath)
        {
            if (basePath.Length is 0)
                return false;

            var rel = Path.GetRelativePath(basePath, subPath);
            return rel == "." || !rel.StartsWith('.') && !Path.IsPathRooted(rel);
        }
    }

    /// <summary>
    /// Draw a directory picker button that toggles the directory picker.
    /// Selecting a directory does behave the same as writing in the text input, i.e. needs to be saved.
    /// </summary>
    private void DrawDirectoryPickerButton()
    {
        if (!ImEx.Icon.Button(LunaStyle.FolderIcon, "通过对话框选择目录。"u8))
            return;

        _newModDirectory ??= config.ModDirectory;
        // Use the current input as start directory if it exists,
        // otherwise the current mod directory, otherwise the current application directory.
        var startDir = Directory.Exists(_newModDirectory)
            ? _newModDirectory
            : Directory.Exists(config.ModDirectory)
                ? config.ModDirectory
                : ".";

        fileDialog.OpenFolderPicker("选择模组目录", (b, s) => _newModDirectory = b ? s : _newModDirectory, startDir, false);
    }

    /// <summary>
    /// Draw the text input for the mod directory,
    /// as well as the directory picker button and the enter warning.
    /// </summary>
    private void DrawRootFolder()
    {
        if (_newModDirectory.IsNullOrEmpty())
            _newModDirectory = config.ModDirectory;

        bool save, selected;
        using (Im.Group())
        {
            Im.Item.SetNextWidth(UiHelpers.InputTextMinusButtonInner);
            using (var color = ImStyleBorder.Frame.Push(Colors.RegexWarningBorder, Im.Style.GlobalScale, !modManager.Valid))
            {
                color.Push(ImGuiColor.TextDisabled, Colors.RegexWarningBorder, !modManager.Valid);
                save = Im.Input.Text("##rootDirectory"u8, ref _newModDirectory, "在此输入根目录（必填）..."u8,
                    InputTextFlags.EnterReturnsTrue, RootDirectoryMaxLength);
            }

            selected = Im.Item.Active;
            Im.Line.SameInner();
            DrawDirectoryPickerButton();

            var tt = "Penumbra 将在此存储解压后的模组文件。\n"u8
              + "TTMP 文件不会被复制，只会解压。\n"u8
              + "此目录需要可访问，并且你需要有写入权限。\n"u8
              + "建议将此目录放在较快的硬盘上，最好是 SSD。\n"u8
              + "也应尽量靠近逻辑驱动器的根目录——到此文件夹的总路径越短越好。\n"u8
              + "绝对不要放在 Dalamud 目录或其任何子目录中。"u8;

            Im.Line.SameInner();
            LunaStyle.DrawAlignedHelpMarker(tt);
            tutorial.OpenTutorial(BasicTutorialSteps.GeneralTooltips);
            Im.Line.SameInner();
            Im.Text("根目录"u8);
            Im.Tooltip.OnHover(tt);
        }

        tutorial.OpenTutorial(BasicTutorialSteps.ModDirectory);
        Im.Line.Same();
        var pos = Im.Cursor.X;
        Im.Line.New();

        if (config.ModDirectory != _newModDirectory
         && _newModDirectory.Length is not 0
         && DrawPressEnterWarning(_newModDirectory, config.ModDirectory, pos, save, selected))
            modManager.DiscoverMods(_newModDirectory, out _newModDirectory);
    }

    /// <summary> Draw the Open Directory and Rediscovery buttons.</summary>
    private void DrawDirectoryButtons()
    {
        UiHelpers.DrawOpenDirectoryButton(0, modManager.BasePath, modManager.Valid);
        Im.Line.Same();
        var tt = modManager.Valid
            ? "强制 Penumbra 完全重扫模组根目录，相当于重启 Penumbra。"u8
            : "当前选择的文件夹无效。请选择其他文件夹。"u8;
        if (ImEx.Button("重新扫描模组"u8, Vector2.Zero, tt, !modManager.Valid))
            modManager.DiscoverMods();
    }
}
