using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using OtterGui;
using OtterGui.Compression;
using OtterGui.Custom;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Api;
using Penumbra.Interop.Services;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab;

namespace Penumbra.UI.Tabs;

public class SettingsTab : ITab
{
    public const int RootDirectoryMaxLength = 64;

    public ReadOnlySpan<byte> Label
        => "插件设置"u8;

    private readonly Configuration               _config;
    private readonly FontReloader                _fontReloader;
    private readonly TutorialService             _tutorial;
    private readonly Penumbra                    _penumbra;
    private readonly FileDialogService           _fileDialog;
    private readonly ModManager                  _modManager;
    private readonly ModExportManager            _modExportManager;
    private readonly ModFileSystemSelector       _selector;
    private readonly CharacterUtility            _characterUtility;
    private readonly ResidentResourceManager     _residentResources;
    private readonly HttpApi                     _httpApi;
    private readonly DalamudSubstitutionProvider _dalamudSubstitutionProvider;
    private readonly FileCompactor               _compactor;
    private readonly DalamudConfigService        _dalamudConfig;
    private readonly DalamudPluginInterface      _pluginInterface;
    private readonly IDataManager                _gameData;
    private readonly PredefinedTagManager        _predefinedTagManager;
    private readonly CrashHandlerService         _crashService;

    private int _minimumX = int.MaxValue;
    private int _minimumY = int.MaxValue;

    private readonly TagButtons _sharedTags = new();

    public SettingsTab(DalamudPluginInterface pluginInterface, Configuration config, FontReloader fontReloader, TutorialService tutorial,
        Penumbra penumbra, FileDialogService fileDialog, ModManager modManager, ModFileSystemSelector selector,
        CharacterUtility characterUtility, ResidentResourceManager residentResources, ModExportManager modExportManager, HttpApi httpApi,
        DalamudSubstitutionProvider dalamudSubstitutionProvider, FileCompactor compactor, DalamudConfigService dalamudConfig,
        IDataManager gameData, PredefinedTagManager predefinedTagConfig, CrashHandlerService crashService)
    {
        _pluginInterface             = pluginInterface;
        _config                      = config;
        _fontReloader                = fontReloader;
        _tutorial                    = tutorial;
        _penumbra                    = penumbra;
        _fileDialog                  = fileDialog;
        _modManager                  = modManager;
        _selector                    = selector;
        _characterUtility            = characterUtility;
        _residentResources           = residentResources;
        _modExportManager            = modExportManager;
        _httpApi                     = httpApi;
        _dalamudSubstitutionProvider = dalamudSubstitutionProvider;
        _compactor                   = compactor;
        _dalamudConfig               = dalamudConfig;
        _gameData                    = gameData;
        if (_compactor.CanCompact)
            _compactor.Enabled = _config.UseFileSystemCompression;
        _predefinedTagManager = predefinedTagConfig;
        _crashService         = crashService;
    }

    public void DrawHeader()
    {
        _tutorial.OpenTutorial(BasicTutorialSteps.Fin);
        _tutorial.OpenTutorial(BasicTutorialSteps.Faq1);
        _tutorial.OpenTutorial(BasicTutorialSteps.Faq2);
    }

    public void DrawContent()
    {
        using var child = ImRaii.Child("##SettingsTab", -Vector2.One, false);
        if (!child)
            return;

        DrawEnabledBox();
        EphemeralCheckbox("锁定主窗口", "防止主窗口被调整大小或移动。", _config.Ephemeral.FixMainWindow,
            v => _config.Ephemeral.FixMainWindow = v);

        ImGui.NewLine();
        DrawRootFolder();
        DrawDirectoryButtons();
        ImGui.NewLine();
        ImGui.NewLine();

        DrawGeneralSettings();
        DrawColorSettings();
        DrawPredefinedTagsSection();
        DrawAdvancedSettings();
        DrawSupportButtons();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Checkbox(string label, string tooltip, bool current, Action<bool> setter)
    {
        using var id  = ImRaii.PushId(label);
        var       tmp = current;
        if (ImGui.Checkbox(string.Empty, ref tmp) && tmp != current)
        {
            setter(tmp);
            _config.Save();
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker(label, tooltip);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void EphemeralCheckbox(string label, string tooltip, bool current, Action<bool> setter)
    {
        using var id  = ImRaii.PushId(label);
        var       tmp = current;
        if (ImGui.Checkbox(string.Empty, ref tmp) && tmp != current)
        {
            setter(tmp);
            _config.Ephemeral.Save();
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker(label, tooltip);
    }

    #region Main Settings

    /// <summary>
    /// Do not change the directory without explicitly pressing enter or this button.
    /// Shows up only if the current input does not correspond to the current directory.
    /// </summary>
    private bool DrawPressEnterWarning(string newName, string old, float width, bool saved, bool selected)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Button, Colors.PressEnterWarningBg);
        var       w     = new Vector2(width, 0);
        var (text, valid) = CheckRootDirectoryPath(newName, old, selected);

        return (ImGui.Button(text, w) || saved) && valid;
    }

    /// <summary> Check a potential new root directory for validity and return the button text and whether it is valid. </summary>
    private (string Text, bool Valid) CheckRootDirectoryPath(string newName, string old, bool selected)
    {
        static bool IsSubPathOf(string basePath, string subPath)
        {
            if (basePath.Length == 0)
                return false;

            var rel = Path.GetRelativePath(basePath, subPath);
            return rel == "." || !rel.StartsWith('.') && !Path.IsPathRooted(rel);
        }

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

        if (Functions.GetDownloadsFolder(out var downloads) && IsSubPathOf(downloads, newName))
            return ("路径不允许放在下载文件夹。", false);

        var gameDir = _gameData.GameData.DataPath.Parent!.Parent!.FullName;
        if (IsSubPathOf(gameDir, newName))
            return ("路径不允许放在游戏目录。", false);

        return selected
            ? ($"按下回车或单击此处保存(当前你目录：{old})", true)
            : ($"点击此处保存(当前目录：{old})", true);
    }

    /// <summary> Changing the base mod directory. </summary>
    private string? _newModDirectory;

    /// <summary>
    /// Draw a directory picker button that toggles the directory picker.
    /// Selecting a directory does behave the same as writing in the text input, i.e. needs to be saved.
    /// </summary>
    private void DrawDirectoryPickerButton()
    {
        if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Folder.ToIconString(), UiHelpers.IconButtonSize,
                "通过对话框选择一个目录。", false, true))
            return;

        _newModDirectory ??= _config.ModDirectory;
        // Use the current input as start directory if it exists,
        // otherwise the current mod directory, otherwise the current application directory.
        var startDir = Directory.Exists(_newModDirectory)
            ? _newModDirectory
            : Directory.Exists(_config.ModDirectory)
                ? _config.ModDirectory
                : ".";

        _fileDialog.OpenFolderPicker( "选择模组目录", (b, s) => _newModDirectory = b ? s : _newModDirectory, startDir, false);
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
        using (ImRaii.Group())
        {
            ImGui.SetNextItemWidth(UiHelpers.InputTextMinusButton3);
            using (ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale, !_modManager.Valid))
            {
                using var color = ImRaii.PushColor(ImGuiCol.Border, Colors.RegexWarningBorder)
                    .Push(ImGuiCol.TextDisabled, Colors.RegexWarningBorder, !_modManager.Valid);
                save = ImGui.InputTextWithHint("##rootDirectory", "Enter Root Directory here (MANDATORY)...", ref _newModDirectory,
                    RootDirectoryMaxLength, ImGuiInputTextFlags.EnterReturnsTrue);
            }

            selected = ImGui.IsItemActive();
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(UiHelpers.ScaleX3, 0));
            ImGui.SameLine();
            DrawDirectoryPickerButton();
            style.Pop();
            ImGui.SameLine();

            const string tt = "这是Penumbra即将存储提取到的模组文件的地方。\n"
              + "TTMP文件不会被复制，而是被解压到这里。\n"
              + "此目录需要你有读写权限。\n"
              + "建议将此目录放置于读写速度快的硬盘上，最好是固态硬盘。\n"
              + "它还应该放在逻辑驱动器的根目录附近，总之此文件夹的总路径越短越好。\n"
              + "绝对不要将此目录放在卫月目录或其子目录中。";
            ImGuiComponents.HelpMarker(tt);
            _tutorial.OpenTutorial(BasicTutorialSteps.GeneralTooltips);
            ImGui.SameLine();
            ImGui.TextUnformatted("根目录");
            ImGuiUtil.HoverTooltip(tt);
        }

        _tutorial.OpenTutorial(BasicTutorialSteps.ModDirectory);
        ImGui.SameLine();
        var pos = ImGui.GetCursorPosX();
        ImGui.NewLine();

        if (_config.ModDirectory != _newModDirectory
         && _newModDirectory.Length != 0
         && DrawPressEnterWarning(_newModDirectory, _config.ModDirectory, pos, save, selected))
            _modManager.DiscoverMods(_newModDirectory);
    }

    /// <summary> Draw the Open Directory and Rediscovery buttons.</summary>
    private void DrawDirectoryButtons()
    {
        UiHelpers.DrawOpenDirectoryButton(0, _modManager.BasePath, _modManager.Valid);
        ImGui.SameLine();
        var tt = _modManager.Valid
            ? "强制Penumbra完全重扫模组根目录，相当于重启Penumbra。"
            : "当前选择的文件夹无效。请选择其他文件夹。";
        if (ImGuiUtil.DrawDisabledButton( "重新扫描模组", Vector2.Zero, tt, !_modManager.Valid))
            _modManager.DiscoverMods();
    }

    /// <summary> Draw the Enable Mods Checkbox.</summary>
    private void DrawEnabledBox()
    {
        var enabled = _config.EnableMods;
        if (ImGui.Checkbox( "启用模组", ref enabled))
            _penumbra.SetEnabled(enabled);

        _tutorial.OpenTutorial(BasicTutorialSteps.EnableMods);
    }

    #endregion

    #region General Settings

    /// <summary> Draw all settings pertaining to the Mod Selector. </summary>
    private void DrawGeneralSettings()
    {
        if (!ImGui.CollapsingHeader("常规设置"))
        {
            _tutorial.OpenTutorial(BasicTutorialSteps.GeneralSettings);
            return;
        }

        _tutorial.OpenTutorial(BasicTutorialSteps.GeneralSettings);

        DrawHidingSettings();
        UiHelpers.DefaultLineSpace();

        DrawMiscSettings();
        UiHelpers.DefaultLineSpace();

        DrawIdentificationSettings();
        UiHelpers.DefaultLineSpace();

        DrawModSelectorSettings();
        UiHelpers.DefaultLineSpace();

        DrawModHandlingSettings();
        ImGui.NewLine();
    }

    private int _singleGroupRadioMax = int.MaxValue;

    /// <summary> Draw a selection for the maximum number of single select options displayed as a radio toggle. </summary>
    private void DrawSingleSelectRadioMax()
    {
        if (_singleGroupRadioMax == int.MaxValue)
            _singleGroupRadioMax = _config.SingleGroupRadioMax;

        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X);
        if (ImGui.DragInt("##SingleSelectRadioMax", ref _singleGroupRadioMax, 0.01f, 1))
            _singleGroupRadioMax = Math.Max(1, _singleGroupRadioMax);

        if (ImGui.IsItemDeactivated())
        {
            if (_singleGroupRadioMax != _config.SingleGroupRadioMax)
            {
                _config.SingleGroupRadioMax = _singleGroupRadioMax;
                _config.Save();
            }

            _singleGroupRadioMax = int.MaxValue;
        }

        ImGuiUtil.LabeledHelpMarker( "单选项组单选项显示上限",
            "如果单选项组的选项数量等于或多于此处设定的值，将收起变更为下拉菜单。\n"
          + "少于此值的单选项组仍会展开显示。");
    }

    private int _collapsibleGroupMin = int.MaxValue;

    /// <summary> Draw a selection for the minimum number of options after which a group is drawn as collapsible. </summary>
    private void DrawCollapsibleGroupMin()
    {
        if (_collapsibleGroupMin == int.MaxValue)
            _collapsibleGroupMin = _config.OptionGroupCollapsibleMin;

        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X);
        if (ImGui.DragInt("##CollapsibleGroupMin", ref _collapsibleGroupMin, 0.01f, 1))
            _collapsibleGroupMin = Math.Max(2, _collapsibleGroupMin);

        if (ImGui.IsItemDeactivated())
        {
            if (_collapsibleGroupMin != _config.OptionGroupCollapsibleMin)
            {
                _config.OptionGroupCollapsibleMin = _collapsibleGroupMin;
                _config.Save();
            }

            _collapsibleGroupMin = int.MaxValue;
        }

        ImGuiUtil.LabeledHelpMarker("选项组折叠设置",
            "选项组选项数量高于此值时在选项组上方添加一个展开/折叠按钮。");
    }


    /// <summary> Draw the window hiding state checkboxes.  </summary>
    private void DrawHidingSettings()
    {
        Checkbox("游戏启动时自动开启设置窗口", "在启动游戏后，Penumbra主窗口应该打开还是关闭。",
            _config.OpenWindowAtStart,               v => _config.OpenWindowAtStart = v);

        Checkbox("隐藏游戏UI时，隐藏设置窗口",
            "手动隐藏游戏UI时，隐藏Penumbra的主窗口。", _config.HideUiWhenUiHidden,
            v =>
            {
                _config.HideUiWhenUiHidden                   = v;
                _pluginInterface.UiBuilder.DisableUserUiHide = !v;
            });
        Checkbox( "过场动画时，隐藏设置窗口",
            "在观看过场动画时，隐藏Penumbra的主窗口。", _config.HideUiInCutscenes,
            v =>
            {
                _config.HideUiInCutscenes                        = v;
                _pluginInterface.UiBuilder.DisableCutsceneUiHide = !v;
            });
        Checkbox( "进入集体动作(GPose)模式时，隐藏设置窗口",
            "进入集体动作模式时，隐藏Penumbra主窗口。", _config.HideUiInGPose,
            v =>
            {
                _config.HideUiInGPose                         = v;
                _pluginInterface.UiBuilder.DisableGposeUiHide = !v;
            });
    }

    /// <summary> Draw all settings that do not fit into other categories. </summary>
    private void DrawMiscSettings()
    {
        Checkbox( "使用聊天命令后，将成功运行的消息输出到聊天窗口",
            "聊天命令通常只在运行失败时输出消息到聊天窗口，但也可以在成功运行时输出消息供你确认。你可以在此处禁用这个功能。",
            _config.PrintSuccessfulCommandsToChat, v => _config.PrintSuccessfulCommandsToChat = v);
        Checkbox( "在模组界面中隐藏重绘栏", "隐藏模组选项卡下模组界面底部的重绘栏。",
            _config.HideRedrawBar,               v => _config.HideRedrawBar = v);
        Checkbox("隐藏更改项目筛选图标", "隐藏在更改项目（包括模组面板里的更改项目）选项卡中的一行筛选图标。",
            _config.HideChangedItemFilters,   v =>
            {
                _config.HideChangedItemFilters = v;
                if (v)
                {
                    _config.Ephemeral.ChangedItemFilter = ChangedItemDrawer.AllFlags;
                    _config.Ephemeral.Save();
                }
            });
        Checkbox("在更改项目中忽略机工副手",
            "在更改项目标签中忽略所有以太转换器（机工副手），因为对它们的任何更改都会同时更改所有这些项目。\n\n"
          + "更改此选项会重新扫描您的模组，以便更新所有已更改的项目。",
            _config.HideMachinistOffhandFromChangedItems, v =>
            {
                _config.HideMachinistOffhandFromChangedItems = v;
                _modManager.DiscoverMods();
            });
        Checkbox("隐藏模组选择器优先级数字标识",
            "如果模组选择器里的模组优先级不是0，而且有足够的空间显示，则在模组名称后添加优先级数字标识。勾选此选项后隐藏这个标识。",
            _config.HidePrioritiesInSelector, v => _config.HidePrioritiesInSelector = v);
        DrawSingleSelectRadioMax();
        DrawCollapsibleGroupMin();
    }

    /// <summary> Draw all settings pertaining to actor identification for collections. </summary>
    private void DrawIdentificationSettings()
    {
        Checkbox("允许其他插件的UI使用界面合集",
            "允许其他卫月插件在调用UI材质时使用界面合集中的文件。",
            _dalamudSubstitutionProvider.Enabled, _dalamudSubstitutionProvider.Set);
        Checkbox($"在角色窗口中使用{TutorialService.AssignedCollections}",
            "如果选中，以你的玩家名字命名的独立角色合集或你的角色组合集将在你的主角色窗口中生效。",
            _config.UseCharacterCollectionInMainWindow, v => _config.UseCharacterCollectionInMainWindow = v);
        Checkbox($"在冒险者铭牌中使用{TutorialService.AssignedCollections}",
            "如果选中，在查看冒险者铭牌时，根据冒险者的姓名，为其使用合适的合集。",
            _config.UseCharacterCollectionsInCards, v => _config.UseCharacterCollectionsInCards = v);
        Checkbox($"在试穿窗口中使用{TutorialService.AssignedCollections}",
            "如果选中，在试穿、染色、幻化窗口中使用基于你的角色名字的独立合集。",
            _config.UseCharacterCollectionInTryOn, v => _config.UseCharacterCollectionInTryOn = v);
        Checkbox( "在调查窗口中不使用模组", "在角色调查窗口中使用空合集，不管是什么角色。\n"
          + "优先于下一个选项。", _config.UseNoModsInInspect, v => _config.UseNoModsInInspect = v);
        Checkbox($"在调查窗口中使用{TutorialService.AssignedCollections}",
            "根据当前调查的角色的名称，为其使用符合角色名称的合集。",
            _config.UseCharacterCollectionInInspect, v => _config.UseCharacterCollectionInInspect = v);
        Checkbox($"基于所有者使用{TutorialService.AssignedCollections}",
            "使用所有者的名字来决定其坐骑、宠物、时尚配饰、战斗伙伴使用适当的角色合集。",
            _config.UseOwnerNameForCharacterCollection, v => _config.UseOwnerNameForCharacterCollection = v);
    }

    /// <summary> Different supported sort modes as a combo. </summary>
    private void DrawFolderSortType()
    {
        var sortMode = _config.SortMode;
        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X);
        using (var combo = ImRaii.Combo("##sortMode", sortMode.Name))
        {
            if (combo)
                foreach (var val in Configuration.Constants.ValidSortModes)
                {
                    if (ImGui.Selectable(val.Name, val.GetType() == sortMode.GetType()) && val.GetType() != sortMode.GetType())
                    {
                        _config.SortMode = val;
                        _selector.SetFilterDirty();
                        _config.Save();
                    }

                    ImGuiUtil.HoverTooltip(val.Description);
                }
        }

        ImGuiUtil.LabeledHelpMarker( "模组排序", "选择模组选项卡中模组选择器的默认排序方式。" );
    }

    private float _absoluteSelectorSize = float.NaN;

    /// <summary> Draw a selector for the absolute size of the mod selector in pixels. </summary>
    private void DrawAbsoluteSizeSelector()
    {
        if (float.IsNaN(_absoluteSelectorSize))
            _absoluteSelectorSize = _config.ModSelectorAbsoluteSize;

        if (ImGuiUtil.DragFloat("##absoluteSize", ref _absoluteSelectorSize, UiHelpers.InputTextWidth.X, 1,
                Configuration.Constants.MinAbsoluteSize, Configuration.Constants.MaxAbsoluteSize, "%.0f")
         && _absoluteSelectorSize != _config.ModSelectorAbsoluteSize)
        {
            _config.ModSelectorAbsoluteSize = _absoluteSelectorSize;
            _config.Save();
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker( "模组选择器的绝对尺寸",
            "模组选项卡中模组选择器的最小绝对尺寸（以像素为单位）。" );
    }

    private int _relativeSelectorSize = int.MaxValue;

    /// <summary> Draw a selector for the relative size of the mod selector as a percentage and a toggle to enable relative sizing. </summary>
    private void DrawRelativeSizeSelector()
    {
        var scaleModSelector = _config.ScaleModSelector;
        if (ImGui.Checkbox( "模组选择器随主窗口大小缩放", ref scaleModSelector))
        {
            _config.ScaleModSelector = scaleModSelector;
            _config.Save();
        }

        ImGui.SameLine();
        if (_relativeSelectorSize == int.MaxValue)
            _relativeSelectorSize = _config.ModSelectorScaledSize;
        if (ImGuiUtil.DragInt("##relativeSize", ref _relativeSelectorSize, UiHelpers.InputTextWidth.X - ImGui.GetCursorPosX(), 0.1f,
                Configuration.Constants.MinScaledSize, Configuration.Constants.MaxScaledSize, "%i%%")
         && _relativeSelectorSize != _config.ModSelectorScaledSize)
        {
            _config.ModSelectorScaledSize = _relativeSelectorSize;
            _config.Save();
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker( "模组选择器尺寸占比",
            "这将使模组选择器的宽度与主窗口宽度成比例，而不是保持固定宽度。" );
    }

    private void DrawRenameSettings()
    {
        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X);
        using (var combo = ImRaii.Combo("##renameSettings", _config.ShowRename.GetData().Name))
        {
            if (combo)
                foreach (var value in Enum.GetValues<RenameField>())
                {
                    var (name, desc) = value.GetData();
                    if (ImGui.Selectable(name, _config.ShowRename == value))
                    {
                        _config.ShowRename = value;
                        _selector.SetRenameSearchPath(value);
                        _config.Save();
                    }

                    ImGuiUtil.HoverTooltip(desc);
                }
        }

        ImGui.SameLine();
        const string tt =
            "Select which of the two renaming input fields are visible when opening the right-click context menu of a mod in the mod selector.";
        ImGuiComponents.HelpMarker(tt);
        ImGui.SameLine();
        ImGui.TextUnformatted("Rename Fields in Mod Context Menu");
        ImGuiUtil.HoverTooltip(tt);
    }

    /// <summary> Draw all settings pertaining to the mod selector. </summary>
    private void DrawModSelectorSettings()
    {
        DrawFolderSortType();
        DrawAbsoluteSizeSelector();
        DrawRelativeSizeSelector();
        DrawRenameSettings();
        Checkbox("默认展开折叠组", "打开模组选择器时，默认展开全部折叠组，否则最小化全部折叠组。",
            _config.OpenFoldersByDefault,   v =>
            {
                _config.OpenFoldersByDefault = v;
                _selector.SetFilterDirty();
            });

        Widget.DoubleModifierSelector( "模组删除组合键",
            "在点击删除模组按钮时，选择是否需要使用组合键才令删除生效。防止误点。", UiHelpers.InputTextWidth.X,
            _config.DeleteModModifier,
            v =>
            {
                _config.DeleteModModifier = v;
                _config.Save();
            });
    }

    /// <summary> Draw all settings pertaining to import and export of mods. </summary>
    private void DrawModHandlingSettings()
    {
        Checkbox("导入时替换非标准符号",
            "导入模组时，将模组和选项名称中的所有非ASCII符号替换为下划线。", _config.ReplaceNonAsciiOnImport,
            v => _config.ReplaceNonAsciiOnImport = v);
        Checkbox("打开导入窗口时始终使用默认目录",
            "每次都在此处指定的目录位置打开导入窗口，不使用上一次的路径。",
            _config.AlwaysOpenDefaultImport, v => _config.AlwaysOpenDefaultImport = v);
        DrawDefaultModImportPath();
        DrawDefaultModAuthor();
        DrawDefaultModImportFolder();
        DrawDefaultModExportPath();
    }


    /// <summary> Draw input for the default import path for a mod. </summary>
    private void DrawDefaultModImportPath()
    {
        var       tmp     = _config.DefaultModImportPath;
        var       spacing = new Vector2(UiHelpers.ScaleX3);
        using var style   = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);

        ImGui.SetNextItemWidth(UiHelpers.InputTextMinusButton3);
        if (ImGui.InputText("##defaultModImport", ref tmp, 256))
            _config.DefaultModImportPath = tmp;

        if (ImGui.IsItemDeactivatedAfterEdit())
            _config.Save();

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton($"{FontAwesomeIcon.Folder.ToIconString()}##import", UiHelpers.IconButtonSize,
                "点击打开选择目录对话框。", false, true))
        {
            var startDir = _config.DefaultModImportPath.Length > 0 && Directory.Exists(_config.DefaultModImportPath)
                ? _config.DefaultModImportPath
                : Directory.Exists(_config.ModDirectory)
                    ? _config.ModDirectory
                    : null;

            _fileDialog.OpenFolderPicker( "选择默认导入目录", (b, s) =>
            {
                if (!b)
                    return;

                _config.DefaultModImportPath = s;
                _config.Save();
            }, startDir, false);
        }

        style.Pop();
        ImGuiUtil.LabeledHelpMarker( "模组默认导入目录",
            "设置首次使用文件选择器导入模组时打开的目录。" );
    }

    private string _tempExportDirectory = string.Empty;

    /// <summary> Draw input for the default export/backup path for mods. </summary>
    private void DrawDefaultModExportPath()
    {
        var       tmp     = _config.ExportDirectory;
        var       spacing = new Vector2(UiHelpers.ScaleX3);
        using var style   = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);
        ImGui.SetNextItemWidth(UiHelpers.InputTextMinusButton3);
        if (ImGui.InputText("##defaultModExport", ref tmp, 256))
            _tempExportDirectory = tmp;

        if (ImGui.IsItemDeactivatedAfterEdit())
            _modExportManager.UpdateExportDirectory(_tempExportDirectory);

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton($"{FontAwesomeIcon.Folder.ToIconString()}##export", UiHelpers.IconButtonSize,
                "点击打开选择目录对话框。", false, true))
        {
            var startDir = _config.ExportDirectory.Length > 0 && Directory.Exists(_config.ExportDirectory)
                ? _config.ExportDirectory
                : Directory.Exists(_config.ModDirectory)
                    ? _config.ModDirectory
                    : null;
            _fileDialog.OpenFolderPicker( "选择默认导出目录", (b, s) =>
            {
                if (b)
                    _modExportManager.UpdateExportDirectory(s);
            }, startDir, false);
        }

        style.Pop();
        ImGuiUtil.LabeledHelpMarker( "默认模组导出目录",
            "设置用于备份模组与恢复备份的路径。\n"
          + "留空则使用根目录。" );
    }

    /// <summary> Draw input for the default name to input as author into newly generated mods. </summary>
    private void DrawDefaultModAuthor()
    {
        var tmp = _config.DefaultModAuthor;
        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X);
        if (ImGui.InputText("##defaultAuthor", ref tmp, 64))
            _config.DefaultModAuthor = tmp;

        if (ImGui.IsItemDeactivatedAfterEdit())
            _config.Save();

        ImGuiUtil.LabeledHelpMarker( "默认模组作者", "为新创建的模组设置一个默认的作者名字。" );
    }

    /// <summary> Draw input for the default folder to sort put newly imported mods into. </summary>
    private void DrawDefaultModImportFolder()
    {
        var tmp = _config.DefaultImportFolder;
        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X);
        if (ImGui.InputText("##defaultImportFolder", ref tmp, 64))
            _config.DefaultImportFolder = tmp;

        if (ImGui.IsItemDeactivatedAfterEdit())
            _config.Save();

        ImGuiUtil.LabeledHelpMarker( "默认模组导入折叠组",
            "导入新模组后，模组默认进入以此名称命名的折叠组。\n留空则导入到根目录。" );
    }

    #endregion

    /// <summary> Draw the entire Color subsection. </summary>
    private void DrawColorSettings()
    {
        if (!ImGui.CollapsingHeader( "配色设置" ) )
            return;

        foreach (var color in Enum.GetValues<ColorId>())
        {
            var (defaultColor, name, description) = color.Data();
            var currentColor = _config.Colors.GetValueOrDefault(color, defaultColor);
            if (Widget.ColorPicker(name, description, currentColor, c => _config.Colors[color] = c, defaultColor))
                _config.Save();
        }

        ImGui.NewLine();
    }

    #region Advanced Settings

    /// <summary> Draw all advanced settings. </summary>
    private void DrawAdvancedSettings()
    {
        var header = ImGui.CollapsingHeader( "高级设置" );

        if (!header)
            return;

        DrawCrashHandler();
        DrawMinimumDimensionConfig();
        Checkbox("在导入时自动清除重复文件",
            "导入时自动清除模组中的重复文件。这将使模组文件的占用变小，但会删除（二进制相同的）文件。",
            _config.AutoDeduplicateOnImport, v => _config.AutoDeduplicateOnImport = v);
        DrawCompressionBox();
        Checkbox("在导入时保持默认的元数据修改",
            "在正常情况下，元数据修改的值（有时是由TexTools导出的）与游戏默认的值相同时，将被抛弃。"
          + "切换此选项以保留它们 - 假如你认为某个模组中的某个选项在先前的选项中被禁用了元数据的修改。",
            _config.KeepDefaultMetaChanges, v => _config.KeepDefaultMetaChanges = v);
        DrawWaitForPluginsReflection();
        DrawEnableHttpApiBox();
        DrawEnableDebugModeBox();
        DrawReloadResourceButton();
        DrawReloadFontsButton();
        ImGui.NewLine();
    }

    private void DrawCrashHandler()
    {
        Checkbox("启用Penumbra崩溃记录（实验性功能）",
            "使Penumbra能够启动一个二级进程，记录一些游戏活动，这可能对诊断与Penumbra相关的游戏崩溃有帮助，也可能没帮助。",
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

        Checkbox( "使用文件系统压缩",
            "使用这个Windows功能（压缩驱动器）可以明显地减少计算机上模组文件的存储大小。\n这会提高CPU负担减少硬盘负担，对硬盘负担大CPU负担小的电脑性能有益。对硬盘负担小CPU负担大的电脑则可能减少性能。",
            _config.UseFileSystemCompression,
            v =>
            {
                _config.UseFileSystemCompression = v;
                _compactor.Enabled               = v;
            });
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton( "压缩现有文件", Vector2.Zero,
                "尝试压缩根目录中的所有文件。这需要一段时间。",
                _compactor.MassCompactRunning || !_modManager.Valid))
            _compactor.StartMassCompact(_modManager.BasePath.EnumerateFiles("*.*", SearchOption.AllDirectories), CompressionAlgorithm.Xpress8K);

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton( "解压缩现有文件", Vector2.Zero,
                "尝试解压缩根目录中的所有文件。这需要一段时间。",
                _compactor.MassCompactRunning || !_modManager.Valid))
            _compactor.StartMassCompact(_modManager.BasePath.EnumerateFiles("*.*", SearchOption.AllDirectories), CompressionAlgorithm.None);

        if (_compactor.MassCompactRunning)
        {
            ImGui.ProgressBar((float)_compactor.CurrentIndex / _compactor.TotalFiles,
                new Vector2(ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X - UiHelpers.IconButtonSize.X,
                    ImGui.GetFrameHeight()),
                _compactor.CurrentFile?.FullName[(_modManager.BasePath.FullName.Length + 1)..] ?? "Gathering Files...");
            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Ban.ToIconString(), UiHelpers.IconButtonSize, "Cancel the mass action.",
                    !_compactor.MassCompactRunning, true))
                _compactor.CancelMassCompact();
        }
        else
        {
            ImGui.Dummy(UiHelpers.IconButtonSize);
        }
    }

    /// <summary> Draw two integral inputs for minimum dimensions of this window. </summary>
    private void DrawMinimumDimensionConfig()
    {
        var x = _minimumX == int.MaxValue ? (int)_config.MinimumSize.X : _minimumX;
        var y = _minimumY == int.MaxValue ? (int)_config.MinimumSize.Y : _minimumY;

        var warning = x < Configuration.Constants.MinimumSizeX
            ? y < Configuration.Constants.MinimumSizeY
                ? "尺寸小于默认值：不建议。"
                : "宽度小于默认值：不建议。"
            : y < Configuration.Constants.MinimumSizeY
                ? "高度小于默认值：不建议。"
                : string.Empty;
        var buttonWidth = UiHelpers.InputTextWidth.X / 2.5f;
        ImGui.SetNextItemWidth(buttonWidth);
        if (ImGui.DragInt("##xMinSize", ref x, 0.1f, 500, 1500))
            _minimumX = x;
        var edited = ImGui.IsItemDeactivatedAfterEdit();

        ImGui.SameLine();
        ImGui.SetNextItemWidth(buttonWidth);
        if (ImGui.DragInt("##yMinSize", ref y, 0.1f, 300, 1500))
            _minimumY = y;
        edited |= ImGui.IsItemDeactivatedAfterEdit();

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("重置##resetMinSize", new Vector2(buttonWidth / 2 - ImGui.GetStyle().ItemSpacing.X * 2, 0),
                $"将最小尺寸重置为({Configuration.Constants.MinimumSizeX}, {Configuration.Constants.MinimumSizeY}).",
                x == Configuration.Constants.MinimumSizeX && y == Configuration.Constants.MinimumSizeY))
        {
            x      = Configuration.Constants.MinimumSizeX;
            y      = Configuration.Constants.MinimumSizeY;
            edited = true;
        }

        ImGuiUtil.LabeledHelpMarker("窗口最小尺寸",
            "设置此窗口的最小尺寸。不建议将值设置地比默认最小尺寸更小，可能导致窗口看起来很糟很混乱。");

        if (warning.Length > 0)
            ImGuiUtil.DrawTextButton(warning, UiHelpers.InputTextWidth, Colors.PressEnterWarningBg);
        else
            ImGui.NewLine();

        if (!edited)
            return;

        _config.MinimumSize = new Vector2(x, y);
        _minimumX           = int.MaxValue;
        _minimumY           = int.MaxValue;
        _config.Save();
    }

    /// <summary> Draw a checkbox for the HTTP API that creates and destroys the web server when toggled. </summary>
    private void DrawEnableHttpApiBox()
    {
        var http = _config.EnableHttpApi;
        if (ImGui.Checkbox("##http", ref http))
        {
            if (http)
                _httpApi.CreateWebServer();
            else
                _httpApi.ShutdownWebServer();

            _config.EnableHttpApi = http;
            _config.Save();
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker( "启用 HTTP API",
            "允许其他程序（如Anamnesis）使用Penumbra的功能，比如请求重绘。" );
    }

    /// <summary> Draw a checkbox to toggle Debug mode. </summary>
    private void DrawEnableDebugModeBox()
    {
        var tmp = _config.DebugMode;
        if (ImGui.Checkbox("##debugMode", ref tmp) && tmp != _config.DebugMode)
        {
            _config.DebugMode = tmp;
            _config.Save();
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker( "启用调试模式",
            "[DEBUG] 启用‘调试’和‘资源管理器’选项卡，操作一些额外数据。在插件加载时也会自动打开设置窗口。" );
    }

    /// <summary> Draw a button that reloads resident resources. </summary>
    private void DrawReloadResourceButton()
    {
        if (ImGuiUtil.DrawDisabledButton( "重新加载常驻资源", Vector2.Zero,
                "重新加载游戏一直保存在内存中的一些特定文件。\n你通常不需要做这件事。",
                !_characterUtility.Ready))
            _residentResources.Reload();
    }

    /// <summary> Draw a button that reloads fonts. </summary>
    private void DrawReloadFontsButton()
    {
        if (ImGuiUtil.DrawDisabledButton( "重新加载字体", Vector2.Zero, "强制游戏重新加载调用的字体文件。", !_fontReloader.Valid))
            _fontReloader.Reload();
    }

    /// <summary> Draw a checkbox that toggles the dalamud setting to wait for plugins on open. </summary>
    private void DrawWaitForPluginsReflection()
    {
        if (!_dalamudConfig.GetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, out bool value))
        {
            using var disabled = ImRaii.Disabled();
            Checkbox("在游戏加载之前等待插件加载 (已禁用，无法访问Dalamud设置。）", string.Empty, false, _ => { });
        }
        else
        {
            Checkbox("在游戏加载之前等待插件加载",
                "有些模组需要修改在游戏开始时加载一次的文件之后再也不会加载的文件。\n"
              + "游戏文件加载后Penumbra才加载该文件可能会导致出现问题。\n"
              + "这个设置将导致游戏等待，直到Penumbra里的某些模组完成加载，使这些模组（一般在基础合集中）能够正常生效。\n\n"
              + "这将更改Dalamud设置(命令 /xlsettings) -> 基本配置中的设置。",
                value,
                v => _dalamudConfig.SetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, v, "doWaitForPluginsOnStartup"));
        }
    }

    #endregion

    /// <summary> Draw the support button group on the right-hand side of the window. </summary>
    private void DrawSupportButtons()
    {
        var width = ImGui.CalcTextSize(UiHelpers.SupportInfoButtonText).X + ImGui.GetStyle().FramePadding.X * 2;
        var xPos  = ImGui.GetWindowWidth() - width;
        // Respect the scroll bar width.
        if (ImGui.GetScrollMaxY() > 0)
            xPos -= ImGui.GetStyle().ScrollbarSize + ImGui.GetStyle().FramePadding.X;

        ImGui.SetCursorPos(new Vector2(xPos, ImGui.GetFrameHeightWithSpacing()));
        UiHelpers.DrawSupportButton(_penumbra);

        ImGui.SetCursorPos(new Vector2(xPos, 0));
        CustomGui.DrawDiscordButton(Penumbra.Messager, width);

        ImGui.SetCursorPos(new Vector2(xPos, 2 * ImGui.GetFrameHeightWithSpacing()));
        CustomGui.DrawGuideButton(Penumbra.Messager, width);

        ImGui.SetCursorPos(new Vector2(xPos, 3 * ImGui.GetFrameHeightWithSpacing()));
        CustomGui.DrawCNDiscordButton( Penumbra.Messager, width );

        ImGui.SetCursorPos(new Vector2(xPos, 4 * ImGui.GetFrameHeightWithSpacing()));
        if (ImGui.Button("重新启动教程", new Vector2(width, 0)))
        {
            _config.Ephemeral.TutorialStep = 0;
            _config.Ephemeral.Save();
        }

        ImGui.SetCursorPos(new Vector2(xPos, 5 * ImGui.GetFrameHeightWithSpacing()));
        if (ImGui.Button("查看更新日志", new Vector2(width, 0)))
            _penumbra.ForceChangelogOpen();
    }

    private void DrawPredefinedTagsSection()
    {
        if (!ImGui.CollapsingHeader("标签"))
            return;

        var tagIdx = _sharedTags.Draw("预定义标签：",
            "可以通过鼠标单击来添加或移除的预定义标签。", _predefinedTagManager,
            out var editedTag);

        if (tagIdx >= 0)
            _predefinedTagManager.ChangeSharedTag(tagIdx, editedTag);
    }
}
