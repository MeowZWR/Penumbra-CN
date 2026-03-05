using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImSharp;
using Luna;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Interop;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Interop.Services;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.Integration;
using Penumbra.UI.ModsTab;
using Penumbra.UI.ModsTab.Selector;

namespace Penumbra.UI.Tabs;

public sealed class SettingsTab : ITab<TabType>
{
    public const int RootDirectoryMaxLength = 64;

    public TabType Identifier
        => TabType.Settings;

    public ReadOnlySpan<byte> Label
        => "插件设置"u8;

    private readonly Configuration               _config;
    private readonly FontReloader                _fontReloader;
    private readonly TutorialService             _tutorial;
    private readonly Penumbra                    _penumbra;
    private readonly FileDialogService           _fileDialog;
    private readonly ModManager                  _modManager;
    private readonly FileWatcher                 _fileWatcher;
    private readonly ModExportManager            _modExportManager;
    private readonly CharacterUtility            _characterUtility;
    private readonly ResidentResourceManager     _residentResources;
    private readonly HttpApi                     _httpApi;
    private readonly DalamudSubstitutionProvider _dalamudSubstitutionProvider;
    private readonly FileCompactor               _compactor;
    private readonly DalamudConfigService        _dalamudConfig;
    private readonly IDalamudPluginInterface     _pluginInterface;
    private readonly IDataManager                _gameData;
    private readonly PredefinedTagManager        _predefinedTagManager;
    private readonly CrashHandlerService         _crashService;
    private readonly MigrationSectionDrawer      _migrationDrawer;
    private readonly CollectionAutoSelector      _autoSelector;
    private readonly AttributeHook               _attributeHook;
    private readonly PcpService                  _pcpService;
    private readonly IntegrationSettingsRegistry _integrationSettings;
    private readonly ModFileSystemDrawer         _modFileSystemDrawer;

    private string _lastCloudSyncTestedPath = string.Empty;
    private bool   _lastCloudSyncTestResult;

    public SettingsTab(IDalamudPluginInterface pluginInterface, Configuration config, FontReloader fontReloader, TutorialService tutorial,
        Penumbra penumbra, FileDialogService fileDialog, ModManager modManager, CharacterUtility characterUtility,
        ResidentResourceManager residentResources, ModExportManager modExportManager,
        FileWatcher fileWatcher, HttpApi httpApi,
        DalamudSubstitutionProvider dalamudSubstitutionProvider, FileCompactor compactor, DalamudConfigService dalamudConfig,
        IDataManager gameData, PredefinedTagManager predefinedTagConfig, CrashHandlerService crashService,
        MigrationSectionDrawer migrationDrawer, CollectionAutoSelector autoSelector, AttributeHook attributeHook, PcpService pcpService,
        IntegrationSettingsRegistry integrationSettings, ModFileSystemDrawer modFileSystemDrawer)
    {
        _pluginInterface             = pluginInterface;
        _config                      = config;
        _fontReloader                = fontReloader;
        _tutorial                    = tutorial;
        _penumbra                    = penumbra;
        _fileDialog                  = fileDialog;
        _modManager                  = modManager;
        _characterUtility            = characterUtility;
        _residentResources           = residentResources;
        _modExportManager            = modExportManager;
        _fileWatcher                 = fileWatcher;
        _httpApi                     = httpApi;
        _dalamudSubstitutionProvider = dalamudSubstitutionProvider;
        _compactor                   = compactor;
        _dalamudConfig               = dalamudConfig;
        _gameData                    = gameData;
        if (_compactor.CanCompact)
            _compactor.Enabled = _config.UseFileSystemCompression;
        _predefinedTagManager = predefinedTagConfig;
        _crashService         = crashService;
        _migrationDrawer      = migrationDrawer;
        _autoSelector         = autoSelector;
        _attributeHook        = attributeHook;
        _pcpService           = pcpService;
        _integrationSettings  = integrationSettings;
        _modFileSystemDrawer  = modFileSystemDrawer;
    }

    public void PostTabButton()
    {
        _tutorial.OpenTutorial(BasicTutorialSteps.Fin);
        _tutorial.OpenTutorial(BasicTutorialSteps.Faq1);
        _tutorial.OpenTutorial(BasicTutorialSteps.Faq2);
    }

    public void DrawContent()
    {
        using var child = Im.Child.Begin("##SettingsTab"u8, -Vector2.One);
        if (!child)
            return;

        DrawEnabledBox();
        EphemeralCheckbox("锁定主窗口"u8, "防止主窗口被调整大小或移动。"u8, _config.Ephemeral.FixMainWindow,
            v => _config.Ephemeral.FixMainWindow = v);

        Im.Line.New();
        DrawRootFolder();
        DrawDirectoryButtons();
        Im.Line.New();
        Im.Line.New();

        DrawGeneralSettings();
        _migrationDrawer.Draw();
        DrawColorSettings();
        DrawPredefinedTagsSection();
        DrawAdvancedSettings();
        _integrationSettings.Draw();
        DrawSupportButtons();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Checkbox(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, bool current, Action<bool> setter)
    {
        using var id  = Im.Id.Push(label);
        var       tmp = current;
        if (Im.Checkbox(StringU8.Empty, ref tmp) && tmp != current)
        {
            setter(tmp);
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel(label, tooltip);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void EphemeralCheckbox(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, bool current, Action<bool> setter)
    {
        using var id  = Im.Id.Push(label);
        var       tmp = current;
        if (Im.Checkbox(StringU8.Empty, ref tmp) && tmp != current)
        {
            setter(tmp);
            _config.Ephemeral.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel(label, tooltip);
    }

    #region Main Settings

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
            return ($"路径过长。最大长度为 {RootDirectoryMaxLength} 。", false);

        if (Path.GetDirectoryName(newName).IsNullOrEmpty())
            return ("路径不允许为驱动器根目录。请添加一个目录。", false);

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (IsSubPathOf(desktop, newName))
            return ("路径不允许放在桌面。", false);

        var programFiles    = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (IsSubPathOf(programFiles, newName) || IsSubPathOf(programFilesX86, newName))
            return ("路径不允许放在ProgramFiles。", false);

        var dalamud = _pluginInterface.ConfigDirectory.Parent!.Parent!;
        if (IsSubPathOf(dalamud.FullName, newName))
            return ("路径不允许放在卫月目录。", false);

        if (WindowsFunctions.GetDownloadsFolder(out var downloads) && IsSubPathOf(downloads, newName))
            return ("路径不允许放在下载文件夹。", false);

        var gameDir = _gameData.GameData.DataPath.Parent!.Parent!.FullName;
        if (IsSubPathOf(gameDir, newName))
            return ("路径不允许放在游戏目录。", false);

        if (_lastCloudSyncTestedPath != newName)
        {
            _lastCloudSyncTestResult = CloudApi.IsCloudSynced(newName);
            _lastCloudSyncTestedPath = newName;
        }

        if (_lastCloudSyncTestResult)
            return ("路径不允许放在云同步目录。", false);

        return selected
            ? ($"按下回车或单击此处保存(当前目录：{old})", true)
            : ($"单击此处保存(当前目录：{old})", true);

        static bool IsSubPathOf(string basePath, string subPath)
        {
            if (basePath.Length is 0)
                return false;

            var rel = Path.GetRelativePath(basePath, subPath);
            return rel == "." || !rel.StartsWith('.') && !Path.IsPathRooted(rel);
        }
    }

    /// <summary> Changing the base mod directory. </summary>
    private string? _newModDirectory;

    /// <summary>
    /// Draw a directory picker button that toggles the directory picker.
    /// Selecting a directory does behave the same as writing in the text input, i.e. needs to be saved.
    /// </summary>
    private void DrawDirectoryPickerButton()
    {
        if (!ImEx.Icon.Button(LunaStyle.FolderIcon, "通过对话框选择一个目录。"u8))
            return;

        _newModDirectory ??= _config.ModDirectory;
        // Use the current input as start directory if it exists,
        // otherwise the current mod directory, otherwise the current application directory.
        var startDir = Directory.Exists(_newModDirectory)
            ? _newModDirectory
            : Directory.Exists(_config.ModDirectory)
                ? _config.ModDirectory
                : ".";

        _fileDialog.OpenFolderPicker("选择模组目录", (b, s) => _newModDirectory = b ? s : _newModDirectory, startDir, false);
    }

    /// <summary>
    /// Draw the text input for the mod directory,
    /// as well as the directory picker button and the enter warning.
    /// </summary>
    private void DrawRootFolder()
    {
        if (_newModDirectory.IsNullOrEmpty())
            _newModDirectory = _config.ModDirectory;

        bool save, selected;
        using (Im.Group())
        {
            Im.Item.SetNextWidth(UiHelpers.InputTextMinusButton3);
            using (var color = ImStyleBorder.Frame.Push(Colors.RegexWarningBorder, Im.Style.GlobalScale, !_modManager.Valid))
            {
                color.Push(ImGuiColor.TextDisabled, Colors.RegexWarningBorder, !_modManager.Valid);
                save = Im.Input.Text("##rootDirectory"u8, ref _newModDirectory, "在此输入根目录（必填）..."u8,
                    InputTextFlags.EnterReturnsTrue, RootDirectoryMaxLength);
            }

            selected = Im.Item.Active;
            using var style = ImStyleDouble.ItemSpacing.Push(new Vector2(Im.Style.GlobalScale * 3, 0));
            Im.Line.Same();
            DrawDirectoryPickerButton();
            style.Pop();

            var tt = "这是Penumbra即将存储提取到的模组文件的地方。\n"u8
              + "TTMP文件不会被复制，而是被解压到这里。\n"u8
              + "此目录需要你有读写权限。\n"u8
              + "建议将此目录放置于读写速度快的硬盘上，最好是固态硬盘。\n"u8
              + "它还应该放在逻辑驱动器的根目录附近，总之此文件夹的总路径越短越好。\n"u8
              + "绝对不要将此目录放在卫月目录或其子目录中。"u8;

            Im.Line.SameInner();
            LunaStyle.DrawAlignedHelpMarker(tt);
            _tutorial.OpenTutorial(BasicTutorialSteps.GeneralTooltips);
            Im.Line.SameInner();
            Im.Text("根目录"u8);
            Im.Tooltip.OnHover(tt);
        }

        _tutorial.OpenTutorial(BasicTutorialSteps.ModDirectory);
        Im.Line.Same();
        var pos = Im.Cursor.X;
        Im.Line.New();

        if (_config.ModDirectory != _newModDirectory
         && _newModDirectory.Length is not 0
         && DrawPressEnterWarning(_newModDirectory, _config.ModDirectory, pos, save, selected))
            _modManager.DiscoverMods(_newModDirectory, out _newModDirectory);
    }

    /// <summary> Draw the Open Directory and Rediscovery buttons.</summary>
    private void DrawDirectoryButtons()
    {
        UiHelpers.DrawOpenDirectoryButton(0, _modManager.BasePath, _modManager.Valid);
        Im.Line.Same();
        var tt = _modManager.Valid
            ? "强制Penumbra完全重扫模组根目录，相当于重启Penumbra。"u8
            : "当前选择的文件夹无效。请选择其他文件夹。"u8;
        if (ImEx.Button("重新扫描模组"u8, Vector2.Zero, tt, !_modManager.Valid))
            _modManager.DiscoverMods();
    }

    /// <summary> Draw the Enable Mods Checkbox.</summary>
    private void DrawEnabledBox()
    {
        var enabled = _config.EnableMods;
        if (Im.Checkbox("启用模组"u8, ref enabled))
            _penumbra.SetEnabled(enabled);

        _tutorial.OpenTutorial(BasicTutorialSteps.EnableMods);
    }

    #endregion

    #region General Settings

    /// <summary> Draw all settings pertaining to the Mod Selector. </summary>
    private void DrawGeneralSettings()
    {
        if (!Im.Tree.Header("常规设置"u8))
        {
            _tutorial.OpenTutorial(BasicTutorialSteps.GeneralSettings);
            return;
        }

        _tutorial.OpenTutorial(BasicTutorialSteps.GeneralSettings);

        DrawHidingSettings();
        UiHelpers.DefaultLineSpace();

        DrawPreviewImagePanelSettings();
        UiHelpers.DefaultLineSpace();

        DrawMiscSettings();
        UiHelpers.DefaultLineSpace();

        DrawIdentificationSettings();
        UiHelpers.DefaultLineSpace();

        DrawModSelectorSettings();
        UiHelpers.DefaultLineSpace();

        DrawModHandlingSettings();
        UiHelpers.DefaultLineSpace();

        DrawModEditorSettings();
        Im.Line.New();
    }

    /// <summary> Draw a selection for the maximum number of single select options displayed as a radio toggle. </summary>
    private void DrawSingleSelectRadioMax()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##SingleSelectRadioMax"u8, _config.SingleGroupRadioMax, out var newValue, 1, null, 0.01f,
                SliderFlags.AlwaysClamp))
        {
            _config.SingleGroupRadioMax = newValue;
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("单选项组单选项显示上限"u8,
            "如果单选项组的选项数量等于或多于此处设定的值，将收起变更为下拉菜单。\n"u8
          + "少于此值的单选项组仍会展开显示。"u8);
    }

    /// <summary> Draw a selection for the minimum number of options after which a group is drawn as collapsible. </summary>
    private void DrawCollapsibleGroupMin()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Drag("##CollapsibleGroupMin"u8, _config.OptionGroupCollapsibleMin, out var newValue, 2, null, 0.01f,
                SliderFlags.AlwaysClamp))
        {
            _config.OptionGroupCollapsibleMin = newValue;
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("选项组折叠设置"u8,
            "选项组选项数量高于此值时在选项组上方添加一个展开/折叠按钮。"u8);
    }


    /// <summary> Draw the window hiding state checkboxes.  </summary>
    private void DrawHidingSettings()
    {
        Checkbox("游戏启动时自动开启设置窗口"u8, "在启动游戏后，Penumbra主窗口应该打开还是关闭。"u8,
            _config.OpenWindowAtStart,                 v => _config.OpenWindowAtStart = v);

        Checkbox("隐藏游戏UI时，隐藏设置窗口"u8,
            "手动隐藏游戏UI时，隐藏Penumbra的主窗口。"u8, _config.HideUiWhenUiHidden,
            v =>
            {
                _config.HideUiWhenUiHidden                   = v;
                _pluginInterface.UiBuilder.DisableUserUiHide = !v;
            });
        Checkbox("进入过场动画时，隐藏设置窗口"u8,
            "在观看过场动画时，隐藏Penumbra的主窗口。"u8, _config.HideUiInCutscenes,
            v =>
            {
                _config.HideUiInCutscenes                        = v;
                _pluginInterface.UiBuilder.DisableCutsceneUiHide = !v;
            });
        Checkbox("进入集体动作(GPose)模式时，隐藏设置窗口"u8,
            "进入集体动作模式时，隐藏Penumbra主窗口。"u8, _config.HideUiInGPose,
            v =>
            {
                _config.HideUiInGPose                         = v;
                _pluginInterface.UiBuilder.DisableGposeUiHide = !v;
            });

        Im.Separator();
        Checkbox("Remember Mod Filters Across Sessions"u8,
            "Whether filters in the Mods tab should remember their input and start with their respective lists filtered identically to the last session."u8,
            _config.RememberModFilters, v => _config.RememberModFilters = v);
        Checkbox("Remember Collection Filters Across Sessions"u8,
            "Whether filters in the Collections tab should remember their input and start with their respective lists filtered identically to the last session."u8,
            _config.RememberCollectionFilters, v => _config.RememberCollectionFilters = v);
        Checkbox("Remember Changed Items Filters Across Sessions"u8,
            "Whether filters in the Changed Items tab should remember their input and start with their respective lists filtered identically to the last session."u8,
            _config.RememberChangedItemFilters, v => _config.RememberChangedItemFilters = v);
        Checkbox("Remember Effective Changes Filters Across Sessions"u8,
            "Whether filters in the Effective Changes tab should remember their input and start with their respective lists filtered identically to the last session."u8,
            _config.RememberEffectiveChangesFilters, v => _config.RememberEffectiveChangesFilters = v);
        Checkbox("Remember On-Screen Filters Across Sessions"u8,
            "Whether filters in the On-Screen tab should remember their input and start with their respective lists filtered identically to the last session."u8,
            _config.RememberOnScreenFilters, v => _config.RememberOnScreenFilters = v);
        Checkbox("Remember Resource Manager Filters Across Sessions"u8,
            "Whether filters in the Resource Manager tab should remember their input and start with their respective lists filtered identically to the last session."u8,
            _config.RememberResourceManagerFilters, v => _config.RememberResourceManagerFilters = v);
    }

    /// <summary> Draw the Preview Image Panel state checkboxes.  </summary>
    private void DrawPreviewImagePanelSettings()
    {
        using var group = Im.Group();
        using var table = Im.Table.Begin("##previewImagePanelSettings"u8, 2, TableFlags.SizingFixedFit | TableFlags.NoSavedSettings);
        if (!table)
            return;

        // 预览面板开关
        table.NextColumn();
        var showPreviewPanel = _config.ShowModPreviewPanel;
        if (Im.Checkbox("##显示预览面板"u8, ref showPreviewPanel))
        {
            _config.ShowModPreviewPanel = showPreviewPanel;
            _config.Save();
        }
        LunaStyle.DrawAlignedHelpMarkerLabel("显示预览面板"u8, "在模组设置面板中显示预览图片面板。"u8);

        if (showPreviewPanel)
        {
            // 保存预览面板状态
            table.NextColumn();
            table.NextColumn();
            var saveState = _config.SavePreviewPanelState;
            if (Im.Checkbox("##保存预览面板显示状态"u8, ref saveState))
            {
                _config.SavePreviewPanelState = saveState;
                _config.Save();
            }
            LunaStyle.DrawAlignedHelpMarkerLabel("保存预览面板显示状态"u8, "保存预览面板的展开或隐藏状态，不一定有效"u8);

            // 使用预览配置中的绘制方法
            // 预览面板比例
            table.NextColumn();
            table.NextColumn();
            UI.ModsTab.ModPreview.ModPreviewImagePanel.GetInstance()?.Config.DrawRatioSelector(_config);
            LunaStyle.DrawAlignedHelpMarkerLabel("预览面板比例"u8, "预览面板占总宽度的比例。"u8);

            // 预览面板最小宽度
            table.NextColumn();
            table.NextColumn();
            UI.ModsTab.ModPreview.ModPreviewImagePanel.GetInstance()?.Config.DrawMinWidthSelector(_config);
            LunaStyle.DrawAlignedHelpMarkerLabel("预览面板最小宽度"u8, "预览面板的最小宽度限制。"u8);

            // 预览面板最大宽度
            table.NextColumn();
            table.NextColumn();
            UI.ModsTab.ModPreview.ModPreviewImagePanel.GetInstance()?.Config.DrawMaxWidthSelector(_config);
            LunaStyle.DrawAlignedHelpMarkerLabel("预览面板最大宽度"u8, "预览面板的最大宽度限制。"u8);

            // 预览图片最小宽度
            table.NextColumn();
            table.NextColumn();
            UI.ModsTab.ModPreview.ModPreviewImagePanel.GetInstance()?.Config.DrawImageMinWidthSelector(_config);
            LunaStyle.DrawAlignedHelpMarkerLabel("预览图片最小宽度"u8, "预览图片的最小显示宽度，影响图片的排列方式。"u8);

            // 预览面板最大内存使用量
            table.NextColumn();
            table.NextColumn();
            var maxMemoryMB = (int)(_config.PreviewPanelMaxMemory / (1024L * 1024L));
            Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
            if (ImEx.InputOnDeactivation.Drag("##图片缓存最大使用内存 (MB)"u8, maxMemoryMB, out maxMemoryMB, 128, 2048, 1f))
            {
                _config.PreviewPanelMaxMemory = maxMemoryMB * 1024L * 1024L;
                _config.Save();
            }
            LunaStyle.DrawAlignedHelpMarkerLabel("图片缓存最大使用内存 (MB)"u8, "预览面板缓存图片可使用的最大内存量。"u8);

            // 显示当前内存使用量
            var currentMemoryUsage = UI.ModsTab.ModPreview.ModPreviewImagePanel.GetCurrentMemoryUsage();
            if (currentMemoryUsage > 0)
            {
                Im.Line.Same();
                Im.TextDisabled($"(当前使用: {currentMemoryUsage / (1024 * 1024):F1} MB)");
            }

            // 预览面板图片间距
            table.NextColumn();
            table.NextColumn();
            UI.ModsTab.ModPreview.ModPreviewImagePanel.GetInstance()?.Config.DrawSpacingSelector(_config);
            LunaStyle.DrawAlignedHelpMarkerLabel("图片间距"u8, "预览面板中图片之间的垂直间距。"u8);
        }
    }

    /// <summary> Draw all settings that do not fit into other categories. </summary>
    private void DrawMiscSettings()
    {
        Checkbox("自动选择角色关联合集"u8,
            "每次登录时，自动选择与当前角色关联的合集作为当前编辑的合集。"u8,
            _config.AutoSelectCollection, _autoSelector.SetAutomaticSelection);
        Checkbox("将成功运行的消息输出到聊天窗口"u8,
            "聊天命令通常只在运行失败时输出消息到聊天窗口，但也可以在成功运行时输出消息供你确认。你可以在此处禁用这个功能。"u8,
            _config.PrintSuccessfulCommandsToChat, v => _config.PrintSuccessfulCommandsToChat = v);
        Checkbox("在模组界面中隐藏重绘栏"u8, "隐藏模组选项卡下模组界面底部的重绘栏。"u8,
            _config.HideRedrawBar,                 v => _config.HideRedrawBar = v);
        Checkbox("隐藏更改项目筛选图标"u8, "隐藏在更改项目（包括模组面板里的更改项目）选项卡中的一行筛选图标。"u8,
            _config.HideChangedItemFilters,     v =>
            {
                _config.HideChangedItemFilters = v;
                if (v)
                {
                    _config.Filters.ModChangedItemTypeFilter = ChangedItemFlagExtensions.AllFlags;
                    _config.Filters.ChangedItemTypeFilter    = ChangedItemFlagExtensions.AllFlags;
                    _config.Ephemeral.Save();
                }
            });

        ChangedItemModeExtensions.DrawCombo("##ChangedItemMode"u8, _config.ChangedItemDisplay, UiHelpers.InputTextWidth.X, v =>
        {
            _config.ChangedItemDisplay = v;
            _config.Save();
        });
        LunaStyle.DrawAlignedHelpMarkerLabel("模组更改项目显示模式"u8,
            "配置如何在模组信息面板中显示单个模组的更改项目。"u8);

        Checkbox("在更改项目中忽略机工副手"u8,
            "在更改项目标签中忽略所有以太转换器（机工副手），因为对它们的任何更改都会同时更改所有这些项目。\n\n"u8
          + "更改此选项会重新扫描您的模组，以便更新所有已更改的项目。"u8,
            _config.HideMachinistOffhandFromChangedItems, v =>
            {
                _config.HideMachinistOffhandFromChangedItems = v;
                _modManager.DiscoverMods();
            });
        Checkbox("隐藏模组选择器优先级数字标识"u8,
            "如果模组选择器里的模组优先级不是0，而且有足够的空间显示，则在模组名称后添加优先级数字标识。勾选此选项后隐藏这个标识。"u8,
            _config.HidePrioritiesInSelector, v => _config.HidePrioritiesInSelector = v);
        DrawSingleSelectRadioMax();
        DrawCollapsibleGroupMin();
    }

    /// <summary> Draw all settings pertaining to actor identification for collections. </summary>
    private void DrawIdentificationSettings()
    {
        Checkbox("允许其他插件的UI使用界面合集"u8,
            "允许其他卫月插件在调用UI材质时使用界面合集中的文件。"u8,
            _dalamudSubstitutionProvider.Enabled, _dalamudSubstitutionProvider.Set);
        Checkbox("在登陆界面中使用合集"u8,
            "如果禁用此选项，则不会对登陆界面或美容师中的角色应用任何模组。"u8,
            _config.ShowModsInLobby, v => _config.ShowModsInLobby = v);
        Checkbox("在角色窗口中使用合集"u8,
            "如果设置，则使用基于你的玩家名字命名的独立角色合集或你的角色组合集。"u8,
            _config.UseCharacterCollectionInMainWindow, v => _config.UseCharacterCollectionInMainWindow = v);
        Checkbox("在冒险者铭牌中使用合集"u8,
            "根据冒险者的姓名，为其使用合适的合集。"u8,
            _config.UseCharacterCollectionsInCards, v => _config.UseCharacterCollectionsInCards = v);
        Checkbox("在试穿窗口中使用合集"u8,
            "如果设置，则使用基于你的角色名字的独立合集。"u8,
            _config.UseCharacterCollectionInTryOn, v => _config.UseCharacterCollectionInTryOn = v);
        Checkbox("在调查窗口中不使用模组"u8,
            "使用空合集来调查角色，不管是什么角色。\n"u8
          + "优先于下一个选项。"u8, _config.UseNoModsInInspect, v => _config.UseNoModsInInspect = v);
        Checkbox("在调查窗口中使用合集"u8,
            "根据当前调查的角色的名称，为其使用符合角色名称的合集。"u8,
            _config.UseCharacterCollectionInInspect, v => _config.UseCharacterCollectionInInspect = v);
        Checkbox("基于所有者使用合集"u8,
            "使用所有者的名字来决定其坐骑、宠物、时尚配饰、战斗伙伴使用适当的角色合集。"u8,
            _config.UseOwnerNameForCharacterCollection, v => _config.UseOwnerNameForCharacterCollection = v);
    }

    /// <summary> Different supported sort modes as a combo. </summary>
    private void DrawFolderSortType()
    {
        var sortMode = _config.SortMode;
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        using (var combo = Im.Combo.Begin("##sortMode"u8, sortMode.Name))
        {
            if (combo)
                foreach (var val in ISortMode.Valid.Values)
                {
                    if (Im.Selectable(val.Name, val.Equals(sortMode)) && !val.Equals(sortMode))
                    {
                        _config.SortMode              = val;
                        _modFileSystemDrawer.SortMode = val;
                        _config.Save();
                    }

                    Im.Tooltip.OnHover(val.Description);
                }
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("模组排序方式"u8, "选择模组选项卡中模组选择器的默认排序方式。"u8);
    }

    private void DrawRenameSettings()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        using (var combo = Im.Combo.Begin("##renameSettings"u8, _config.ShowRename.ToNameU8()))
        {
            if (combo)
                foreach (var value in RenameField.Values)
                {
                    if (Im.Selectable(value.ToNameU8(), _config.ShowRename == value))
                        _config.ShowRename = value;

                    Im.Tooltip.OnHover(value.Tooltip());
                }
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("模组上下文菜单中的重命名字段"u8,
            "选择在模组选择器中打开模组右键上下文菜单时可见的两个重命名输入字段中的哪一个。"u8);
    }

    /// <summary> Draw all settings pertaining to the mod selector. </summary>
    private void DrawModSelectorSettings()
    {
        DrawFolderSortType();
        DrawRenameSettings();
        Checkbox("默认展开折叠组"u8, "打开模组选择器时，默认展开全部折叠组，否则最小化全部折叠组。"u8,
            _config.OpenFoldersByDefault,     v =>
            {
                _config.OpenFoldersByDefault = v;
                // TODO
                // SetFilterDirty
            });

        KeySelector.DoubleModifier("模组删除组合键"u8,
            "在点击删除模组按钮时，选择是否需要使用组合键才令删除生效。防止误点。"u8, UiHelpers.InputTextWidth.X,
            _config.DeleteModModifier,
            v =>
            {
                _config.DeleteModModifier = v;
                _config.Save();
            });
        KeySelector.DoubleModifier("匿名模式组合键"u8,
            "点击匿名模式或临时设置模式按钮时需要按住的组合键，防止误操作。"u8,
            UiHelpers.InputTextWidth.X,
            _config.IncognitoModifier,
            v =>
            {
                _config.IncognitoModifier = v;
                _config.Save();
            });
    }

    /// <summary> Draw all settings pertaining to import and export of mods. </summary>
    private void DrawModHandlingSettings()
    {
        Checkbox("默认使用临时设置"u8,
            "当您对合集进行任何更改时，首先将其应用为临时更改，如果您希望保留这些更改，则需要点击[转为永久]。"u8,
            _config.DefaultTemporaryMode, v => _config.DefaultTemporaryMode = v);
        Checkbox("导入时替换非标准符号"u8,
            "导入模组时，将模组和选项名称中的所有非ASCII符号替换为下划线。"u8, _config.ReplaceNonAsciiOnImport,
            v => _config.ReplaceNonAsciiOnImport = v);
        Checkbox("打开导入窗口时始终使用默认目录"u8,
            "每次都在此处指定的目录位置打开导入窗口，不使用上一次的路径。"u8,
            _config.AlwaysOpenDefaultImport, v => _config.AlwaysOpenDefaultImport = v);
        Checkbox("处理PCP文件"u8,
            "当检测到特定模组（通常以.pcp结尾，但不一定）时，Penumbra会自动尝试为该模组包创建对应角色的合集，并分配给特定角色。如果不需要自动处理可关闭此功能。"u8,
            !_config.PcpSettings.DisableHandling, v => _config.PcpSettings.DisableHandling = !v);

        var active = _config.DeleteModModifier.IsActive();
        Im.Line.Same();
        if (ImEx.Button("删除所有PCP模组"u8, default, "从模组列表中删除所有带有'PCP'标签的模组。"u8, !active))
            _pcpService.CleanPcpMods();
        if (!active)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"请在点击时按住{_config.DeleteModModifier}。");

        Im.Line.Same();
        if (ImEx.Button("删除所有PCP合集"u8, default,
                "从合集列表中删除所有名称以'PCP/'开头的合集。"u8, !active))
            _pcpService.CleanPcpCollections();
        if (!active)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"请在点击时按住{_config.DeleteModModifier}。");

        Checkbox("允许其他插件访问PCP处理功能"u8,
            "在创建或导入PCP文件时，其他插件可以添加和解释它们自己的数据到character.json文件。"u8,
            _config.PcpSettings.AllowIpc, v => _config.PcpSettings.AllowIpc = v);

        Checkbox("导入PCP文件时创建合集"u8,
            "导入PCP文件时自动创建对应的合集。"u8,
            _config.PcpSettings.CreateCollection, v => _config.PcpSettings.CreateCollection = v);

        Checkbox("导入PCP文件时分配合集"u8,
            "导入PCP文件并创建合集时，自动将该合集分配给关联角色。"u8,
            _config.PcpSettings.AssignCollection, v => _config.PcpSettings.AssignCollection = v);
        DrawDefaultModImportPath();
        DrawDefaultModAuthor();
        DrawDefaultModImportFolder();
        DrawPcpFolder();
        DrawPcpExtension();
        DrawDefaultModExportPath();
        Checkbox("启用目录监听器"u8,
            "启用文件监听器后，Penumbra会自动监听指定目录中新出现的模组文件，并在检测到新模组时弹出导入这些模组的询问弹窗。"u8,
            _config.EnableDirectoryWatch, _fileWatcher.Toggle);
        Checkbox("启用全自动导入"u8,
            "配合文件监听器，自动跳过询问弹窗并导入检测到的所有新模组。"u8,
            _config.EnableAutomaticModImport, v => _config.EnableAutomaticModImport = v);
        Checkbox("防止导出的模组被自动重新导入"u8,
            "如果自动导入目录与默认模组导出目录相同，则防止导出的模组和角色包被自动重新导入或显示询问弹窗。"u8,
            _config.PreventExportLoopback, v => _config.PreventExportLoopback = v);
        DrawFileWatcherPath();
        Checkbox("自动关闭模组导入成功的报告"u8,
            "如果所有模组都成功导入，则自动关闭报告。\n包含错误的报告仍需要手动关闭。"u8,
            _config.AutoDismissModImportSuccessReports, v => _config.AutoDismissModImportSuccessReports = v);
    }


    /// <summary> Draw input for the default import path for a mod. </summary>
    private void DrawDefaultModImportPath()
    {
        using var id      = Im.Id.Push("##dmi"u8);
        var       spacing = new Vector2(Im.Style.GlobalScale * 3);
        using var style   = ImStyleDouble.ItemSpacing.Push(spacing);

        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButton3);
        if (ImEx.InputOnDeactivation.Text(StringU8.Empty, _config.DefaultModImportPath, out string newDirectory))
        {
            _config.DefaultModImportPath = newDirectory;
            _config.Save();
        }

        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon, "通过对话框选择一个目录。"u8))
        {
            var startDir = _config.DefaultModImportPath.Length > 0 && Directory.Exists(_config.DefaultModImportPath)
                ? _config.DefaultModImportPath
                : Directory.Exists(_config.ModDirectory)
                    ? _config.ModDirectory
                    : null;

            _fileDialog.OpenFolderPicker("选择默认导入目录", (b, s) =>
            {
                if (!b)
                    return;

                _config.DefaultModImportPath = s;
                _config.Save();
            }, startDir, false);
        }

        style.Pop();
        LunaStyle.DrawAlignedHelpMarkerLabel("模组默认导入目录"u8,
            "设置首次使用文件选择器导入模组时打开的目录。"u8);
    }

    /// <summary> Draw input for the default export/backup path for mods. </summary>
    private void DrawDefaultModExportPath()
    {
        using var id      = Im.Id.Push("##dme"u8);
        var       spacing = new Vector2(Im.Style.GlobalScale * 3);
        using var style   = ImStyleDouble.ItemSpacing.Push(spacing);
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButton3);
        if (ImEx.InputOnDeactivation.Text(StringU8.Empty, _config.ExportDirectory, out string newDirectory))
            _modExportManager.UpdateExportDirectory(newDirectory);

        Im.Line.Same();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon, "通过对话框选择一个目录。"u8))
        {
            var startDir = _config.ExportDirectory.Length > 0 && Directory.Exists(_config.ExportDirectory)
                ? _config.ExportDirectory
                : Directory.Exists(_config.ModDirectory)
                    ? _config.ModDirectory
                    : null;
            _fileDialog.OpenFolderPicker("选择默认导出目录", (b, s) =>
            {
                if (b)
                    _modExportManager.UpdateExportDirectory(s);
            }, startDir, false);
        }

        style.Pop();
        LunaStyle.DrawAlignedHelpMarkerLabel("默认模组导出目录"u8,
            "设置用于备份模组与恢复备份的路径。\n"u8
          + "留空则使用根目录。"u8);
    }

    /// <summary> Draw input for the Automatic Mod import path. </summary>
    private void DrawFileWatcherPath()
    {
        using var id      = Im.Id.Push("fw"u8);
        var       spacing = new Vector2(Im.Style.GlobalScale * 3);
        using var style   = ImStyleDouble.ItemSpacing.Push(spacing);
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButton3);
        if (ImEx.InputOnDeactivation.Text(StringU8.Empty, _config.WatchDirectory, out string newDirectory, maxLength: 256))
            _fileWatcher.UpdateDirectory(newDirectory);

        Im.Line.Same();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon, "通过对话框选择一个目录。"u8))
        {
            var startDir = _config.WatchDirectory.Length > 0 && Directory.Exists(_config.WatchDirectory)
                ? _config.WatchDirectory
                : Directory.Exists(_config.ModDirectory)
                    ? _config.ModDirectory
                    : null;
            _fileDialog.OpenFolderPicker("选择自动导入目录", (b, s) =>
            {
                if (b)
                    _fileWatcher.UpdateDirectory(s);
            }, startDir, false);
        }

        style.Pop();
        LunaStyle.DrawAlignedHelpMarkerLabel("自动导入目录"u8,
            "选择文件监听器监控的目录。"u8);
    }

    /// <summary> Draw input for the default name to input as author into newly generated mods. </summary>
    private void DrawDefaultModAuthor()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##author"u8, _config.DefaultModAuthor, out string newAuthor))
        {
            _config.DefaultModAuthor = newAuthor;
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("默认模组作者"u8, "为新创建的模组设置一个默认的作者名字。"u8);
    }

    /// <summary> Draw input for the default folder to sort put newly imported mods into. </summary>
    private void DrawDefaultModImportFolder()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##importFolder"u8, _config.DefaultImportFolder, out string newFolder))
        {
            _config.DefaultImportFolder = newFolder;
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("默认模组导入折叠组"u8,
            "导入新模组后，模组默认进入以此名称命名的折叠组。\n留空则导入到根目录。"u8);
    }

    /// <summary> Draw input for the default folder to sort put newly imported mods into. </summary>
    private void DrawPcpFolder()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##pcpFolder"u8, _config.PcpSettings.FolderName, out string newFolder))
        {
            _config.PcpSettings.FolderName = newFolder;
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("默认PCP折叠组"u8,
            "导入PCP角色包时，会将其移动到该折叠组中。\n留空则导入到根目录。"u8);
    }

    private void DrawPcpExtension()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##pcpExtension"u8, _config.PcpSettings.PcpExtension, out string newExtension))
        {
            _config.PcpSettings.PcpExtension = newExtension;
            _config.Save();
        }

        Im.Line.SameInner();
        if (ImEx.Button("Reset##pcpExtension"u8, Vector2.Zero, "重置扩展名为其默认值 \".pcp\"."u8,
                _config.PcpSettings.PcpExtension is ".pcp"))
        {
            _config.PcpSettings.PcpExtension = ".pcp";
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("PCP扩展名"u8,
            "导出PCP文件时使用的扩展名。通常应该是 \".pcp\" 或 \".pmp\"。"u8);
    }


    /// <summary> Draw all settings pertaining to advanced editing of mods. </summary>
    private void DrawModEditorSettings()
    {
        Checkbox("高级编辑：编辑原始Tile UV变换"u8,
            "编辑Tile UV变换的原始矩阵组件，而不是将它们分解为缩放、旋转和剪切。"u8,
            _config.EditRawTileTransforms, v => _config.EditRawTileTransforms = v);

        Checkbox("Advanced Editing: Always Highlight Color Row Pair when Hovering Selection Button"u8,
            "Make the whole color row pair selection button highlight the pair in game, instead of just the crosshair, even without holding Control."u8,
            _config.WholePairSelectorAlwaysHighlights, v => _config.WholePairSelectorAlwaysHighlights = v);
    }

    #endregion

    /// <summary> Draw the entire Color subsection. </summary>
    private void DrawColorSettings()
    {
        if (!Im.Tree.Header("配色设置"u8))
            return;

        foreach (var color in ColorId.Values)
        {
            var (defaultColor, name, description) = color.Data();
            var currentColor = _config.Colors.GetValueOrDefault(color, defaultColor);
            if (ImEx.ColorPicker(name, description, currentColor, out var newColor, defaultColor))
            {
                _config.Colors[color] = newColor.Color;
                CacheManager.Instance.SetColorsDirty();
                _config.Save();
            }
        }

        Im.Line.New();
    }

    #region Advanced Settings

    /// <summary> Draw all advanced settings. </summary>
    private void DrawAdvancedSettings()
    {
        var header = Im.Tree.Header("高级设置"u8);

        if (!header)
            return;

        DrawCrashHandler();
        DrawMinimumDimensionConfig();
        DrawHdrRenderTargets();
        Checkbox("导入时自动清除重复文件"u8,
            "导入时自动清除模组中的重复文件。这将使模组文件的占用变小，但会删除（二进制完全相同的）文件。"u8,
            _config.AutoDeduplicateOnImport, v => _config.AutoDeduplicateOnImport = v);
        Checkbox("PMP导入时自动重复复制UI文件"u8,
            "从PMP文件导入时自动重复复制并规范化与UI有关的文件。强烈建议启用此选项，因为UI文件导入时去重会导致游戏崩溃。"u8,
            _config.AutoReduplicateUiOnImport, v => _config.AutoReduplicateUiOnImport = v);
        DrawCompressionBox();
        Checkbox("导入时保持默认的元数据修改"u8,
            "通常情况下，元数据修改的值（有时是由TexTools导出的）与游戏默认的值相同时，将被抛弃。"u8
          + "切换此选项以保留它们 - 假如你认为某个模组中的某个选项在先前的选项中被禁用了元数据的修改。"u8,
            _config.KeepDefaultMetaChanges, v => _config.KeepDefaultMetaChanges = v);
        Checkbox("启用自定义形状与属性支持"u8,
            "Penumbra将允许对模组模型的自定义形状键和属性进行识别与合并。"u8,
            _config.EnableCustomShapes, _attributeHook.SetState);
        DrawWaitForPluginsReflection();
        DrawEnableHttpApiBox();
        DrawEnableDebugModeBox();
        Im.Separator();
        DrawReloadResourceButton();
        DrawReloadFontsButton();
        Im.Line.New();
    }

    private void DrawCrashHandler()
    {
        Checkbox("启用Penumbra崩溃记录（实验性功能）"u8,
            "使Penumbra能够启动一个二级进程，记录一些游戏活动，这可能对诊断与Penumbra相关的游戏崩溃有帮助，也可能没帮助。"u8,
            _config.UseCrashHandler ?? false,
            v =>
            {
                if (v)
                    _crashService.Enable();
                else
                    _crashService.Disable();
            });
    }

    private void DrawCompressionBox()
    {
        if (!_compactor.CanCompact)
            return;

        Checkbox("使用文件系统压缩"u8,
            "使用 Windows 功能（压缩驱动器）可以明显地减少计算机上模组文件的存储大小。\n会提高CPU负担减少硬盘负担，对硬盘负担大CPU负担小的电脑性能有益。对硬盘负担小CPU负担大的电脑则可能减少性能。"u8,
            _config.UseFileSystemCompression,
            v =>
            {
                _config.UseFileSystemCompression = v;
                _compactor.Enabled               = v;
            });
        Im.Line.Same();
        if (ImEx.Button("压缩现有文件"u8, Vector2.Zero,
                "尝试压缩根目录中的所有文件。这需要一段时间。"u8,
                _compactor.MassCompactRunning || !_modManager.Valid))
            _compactor.StartMassCompact(_modManager.BasePath.EnumerateFiles("*.*", SearchOption.AllDirectories), CompressionAlgorithm.Xpress8K,
                true);

        Im.Line.Same();
        if (ImEx.Button("解压缩现有文件"u8, Vector2.Zero,
                "尝试解压缩根目录中的所有文件。这需要一段时间。"u8,
                _compactor.MassCompactRunning || !_modManager.Valid))
            _compactor.StartMassCompact(_modManager.BasePath.EnumerateFiles("*.*", SearchOption.AllDirectories), CompressionAlgorithm.None,
                true);

        if (_compactor.MassCompactRunning)
        {
            Im.ProgressBar((float)_compactor.CurrentIndex / _compactor.TotalFiles,
                new Vector2(Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X - UiHelpers.IconButtonSize.X,
                    Im.Style.FrameHeight),
                _compactor.CurrentFile?.FullName[(_modManager.BasePath.FullName.Length + 1)..] ?? "正在收集文件...");
            Im.Line.Same();
            if (ImEx.Icon.Button(LunaStyle.CancelIcon, "取消此批量操作。"u8, !_compactor.MassCompactRunning))
                _compactor.CancelMassCompact();
        }
        else
        {
            Im.FrameDummy();
        }
    }

    /// <summary> Draw two integral inputs for minimum dimensions of this window. </summary>
    private void DrawMinimumDimensionConfig()
    {
        var warning = _config.MinimumSize.X < Configuration.Constants.MinimumSizeX
            ? _config.MinimumSize.Y < Configuration.Constants.MinimumSizeY
                ? "尺寸小于默认值：这可能看起来不理想。"u8
                : "宽度小于默认值：这可能看起来不理想。"u8
            : _config.MinimumSize.Y < Configuration.Constants.MinimumSizeY
                ? "高度小于默认值：这可能看起来不理想。"u8
                : StringU8.Empty;
        var buttonWidth = UiHelpers.InputTextWidth.X / 2.5f;
        Im.Item.SetNextWidth(buttonWidth);
        if (ImEx.InputOnDeactivation.Drag("##xMinSize"u8, (int)_config.MinimumSize.X, out var newX, 500, 1500, 0.1f))
        {
            _config.MinimumSize.X = newX;
            _config.Save();
        }

        Im.Line.Same();
        Im.Item.SetNextWidth(buttonWidth);
        if (ImEx.InputOnDeactivation.Drag("##yMinSize"u8, (int)_config.MinimumSize.Y, out var newY, 300, 1500, 0.1f))
        {
            _config.MinimumSize.Y = newY;
            _config.Save();
        }

        Im.Line.Same();
        if (ImEx.Button("Reset##resetMinSize"u8, new Vector2(buttonWidth / 2 - Im.Style.ItemSpacing.X * 2, 0),
                $"将最小尺寸重置为({Configuration.Constants.MinimumSizeX}, {Configuration.Constants.MinimumSizeY})。",
                _config.MinimumSize is { X: Configuration.Constants.MinimumSizeX, Y: Configuration.Constants.MinimumSizeY }))
        {
            _config.MinimumSize = new Vector2(Configuration.Constants.MinimumSizeX, Configuration.Constants.MinimumSizeY);
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("窗口最小尺寸"u8,
            "设置此窗口的最小尺寸。不建议将值设置地比默认最小尺寸更小，可能导致窗口看起来很糟很混乱。"u8);

        if (warning.Length > 0)
            ImEx.TextFramed(warning, UiHelpers.InputTextWidth, Colors.PressEnterWarningBg);
        else
            Im.Line.New();
    }

    private void DrawHdrRenderTargets()
    {
        Im.Item.SetNextWidth(Im.Font.CalculateSize("M"u8).X * 5.0f + Im.Style.FrameHeight);
        using (var combo = Im.Combo.Begin("##hdrRenderTarget"u8, _config.HdrRenderTargets ? "HDR"u8 : "SDR"u8))
        {
            if (combo)
            {
                if (Im.Selectable("HDR"u8, _config.HdrRenderTargets) && !_config.HdrRenderTargets)
                {
                    _config.HdrRenderTargets = true;
                    _config.Save();
                }

                if (Im.Selectable("SDR"u8, !_config.HdrRenderTargets) && _config.HdrRenderTargets)
                {
                    _config.HdrRenderTargets = false;
                    _config.Save();
                }
            }
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("漫反射动态范围"u8,
            "设置材质中漫反射颜色可用的动态范围，以避免产生视觉伪影。\n"u8
          + "更改此设置需要重启游戏。此设置仅在启用[启动时等待插件]时有效。"u8);
    }

    /// <summary> Draw a checkbox for the HTTP API that creates and destroys the web server when toggled. </summary>
    private void DrawEnableHttpApiBox()
    {
        var http = _config.EnableHttpApi;
        if (Im.Checkbox("##http"u8, ref http))
        {
            if (http)
                _httpApi.CreateWebServer();
            else
                _httpApi.ShutdownWebServer();

            _config.EnableHttpApi = http;
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("启用 HTTP API"u8,
            "允许其他程序（如Anamnesis）使用Penumbra的功能，比如请求重绘。"u8);
    }

    /// <summary> Draw a checkbox to toggle Debug mode. </summary>
    private void DrawEnableDebugModeBox()
    {
        var tmp = _config.DebugMode;
        if (Im.Checkbox("##debugMode"u8, ref tmp) && tmp != _config.DebugMode)
        {
            _config.DebugMode = tmp;
            _config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("启用调试模式"u8,
            "[DEBUG] 启用调试和资源管理器选项卡，操作一些额外数据。在插件加载时也会自动打开设置窗口。"u8);
    }

    /// <summary> Draw a button that reloads resident resources. </summary>
    private void DrawReloadResourceButton()
    {
        if (ImEx.Button("重新加载常驻资源"u8, Vector2.Zero,
                "重新加载一些始终保留在内存中的游戏特定文件。\n通常不需要执行此操作。"u8,
                !_characterUtility.Ready))
            _residentResources.Reload();
    }

    /// <summary> Draw a button that reloads fonts. </summary>
    private void DrawReloadFontsButton()
    {
        if (ImEx.Button("重新加载字体"u8, Vector2.Zero, "强制游戏重新加载调用的字体文件。"u8, !_fontReloader.Valid))
            _fontReloader.Reload();
    }


    /// <summary> Draw a checkbox that toggles the dalamud setting to wait for plugins on open. </summary>
    private void DrawWaitForPluginsReflection()
    {
        if (!_dalamudConfig.GetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, out bool value))
        {
            using var disabled = Im.Disabled();
            Checkbox("在游戏加载之前等待插件加载 (已禁用，无法访问Dalamud设置。）"u8, StringU8.Empty, false, _ => { });
        }
        else
        {
            Checkbox("在游戏加载之前等待插件加载"u8,
                "有些模组需要在游戏开始时加载一次，之后不再加载的文件。\n"u8
              + "游戏文件加载后Penumbra才加载该文件可能会导致出现问题。\n"u8
              + "这个设置将导致游戏等待，直到Penumbra里的某些模组完成加载，使这些模组（一般在基础合集中）能够正常生效。\n\n"u8
              + "这将更改Dalamud设置(命令 /xlsettings) -> 基本配置中的设置。"u8,
                value,
                v => _dalamudConfig.SetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, v, "doWaitForPluginsOnStartup"));
        }
    }

    #endregion

    /// <summary> Draw the support button group on the right-hand side of the window. </summary>
    private void DrawSupportButtons()
    {
        var width = Im.Font.CalculateSize(UiHelpers.SupportInfoButtonText).X + Im.Style.FramePadding.X * 2;
        var xPos  = Im.Window.Width - width;
        // Respect the scroll bar width.
        if (Im.Scroll.MaximumY > 0)
            xPos -= Im.Style.ScrollbarSize + Im.Style.FramePadding.X;

        Im.Cursor.Position = new Vector2(xPos, 0);
        SupportButton.DiscordSplit(Penumbra.Messager, new Vector2(width, 0));

        Im.Cursor.Position = new Vector2(xPos, 1 * Im.Style.FrameHeightWithSpacing);
        SupportButton.ModSites(Penumbra.Messager, new Vector2(width, 0));

        Im.Cursor.Position = new Vector2(xPos, 2 * Im.Style.FrameHeightWithSpacing);
        SupportButton.GuideTutorial(Penumbra.Messager, new Vector2(width, 0), () => {
            _config.Ephemeral.TutorialStep = 0;
            _config.Ephemeral.Save();
        });

        Im.Cursor.Position = new Vector2(xPos, 3 * Im.Style.FrameHeightWithSpacing);
        UiHelpers.DrawSupportButton(_penumbra);

        Im.Cursor.Position = new Vector2(xPos, 4 * Im.Style.FrameHeightWithSpacing);
        if (Im.Button("查看更新日志"u8, new Vector2(width, 0)))
            _penumbra.ForceChangelogOpen();

        Im.Cursor.Position = new Vector2(xPos, 5 * Im.Style.FrameHeightWithSpacing);
        SupportButton.KoFiPatreon(Penumbra.Messager, new Vector2(width, 0));
    }

    private void DrawPredefinedTagsSection()
    {
        if (!Im.Tree.Header("标签设置"u8))
            return;

        var tagIdx = TagButtons.Draw("预定义标签："u8,
            "可以通过鼠标单击来添加或移除的预定义标签。"u8, _predefinedTagManager,
            out var editedTag);

        if (tagIdx >= 0)
            _predefinedTagManager.ChangeSharedTag(tagIdx, editedTag);
    }
}
