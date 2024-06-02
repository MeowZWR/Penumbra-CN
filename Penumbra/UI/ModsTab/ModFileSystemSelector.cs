using Dalamud.Interface;
using Dalamud.Interface.DragDrop;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Plugin.Services;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Filesystem;
using OtterGui.FileSystem.Selector;
using OtterGui.Raii;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.Services;
using Penumbra.UI.Classes;
using MessageService = Penumbra.Services.MessageService;

namespace Penumbra.UI.ModsTab;

public sealed class ModFileSystemSelector : FileSystemSelector<Mod, ModFileSystemSelector.ModState>
{
    private readonly CommunicatorService _communicator;
    private readonly MessageService      _messager;
    private readonly Configuration       _config;
    private readonly FileDialogService   _fileDialog;
    private readonly ModManager          _modManager;
    private readonly CollectionManager   _collectionManager;
    private readonly TutorialService     _tutorial;
    private readonly ModImportManager    _modImportManager;
    private readonly IDragDropManager    _dragDrop;
    public           ModSettings         SelectedSettings          { get; private set; } = ModSettings.Empty;
    public           ModCollection       SelectedSettingCollection { get; private set; } = ModCollection.Empty;

    public ModFileSystemSelector(IKeyState keyState, CommunicatorService communicator, ModFileSystem fileSystem, ModManager modManager,
        CollectionManager collectionManager, Configuration config, TutorialService tutorial, FileDialogService fileDialog,
        MessageService messager, ModImportManager modImportManager, IDragDropManager dragDrop)
        : base(fileSystem, keyState, Penumbra.Log, HandleException, allowMultipleSelection: true)
    {
        _communicator      = communicator;
        _modManager        = modManager;
        _collectionManager = collectionManager;
        _config            = config;
        _tutorial          = tutorial;
        _fileDialog        = fileDialog;
        _messager          = messager;
        _modImportManager  = modImportManager;
        _dragDrop          = dragDrop;

        // @formatter:off
        SubscribeRightClickFolder(EnableDescendants, 10);
        SubscribeRightClickFolder(DisableDescendants, 10);
        SubscribeRightClickFolder(InheritDescendants, 15);
        SubscribeRightClickFolder(OwnDescendants, 15);
        SubscribeRightClickFolder(SetDefaultImportFolder, 100);
        SubscribeRightClickFolder(f => SetQuickMove(f, 0, _config.QuickMoveFolder1, s => { _config.QuickMoveFolder1 = s; _config.Save(); }), 110);
        SubscribeRightClickFolder(f => SetQuickMove(f, 1, _config.QuickMoveFolder2, s => { _config.QuickMoveFolder2 = s; _config.Save(); }), 120);
        SubscribeRightClickFolder(f => SetQuickMove(f, 2, _config.QuickMoveFolder3, s => { _config.QuickMoveFolder3 = s; _config.Save(); }), 130);
        SubscribeRightClickLeaf(ToggleLeafFavorite);
        SubscribeRightClickLeaf(l => QuickMove(l, _config.QuickMoveFolder1, _config.QuickMoveFolder2, _config.QuickMoveFolder3));
        SubscribeRightClickMain(ClearDefaultImportFolder, 100);
        SubscribeRightClickMain(() => ClearQuickMove(0, _config.QuickMoveFolder1, () => {_config.QuickMoveFolder1 = string.Empty; _config.Save();}), 110);
        SubscribeRightClickMain(() => ClearQuickMove(1, _config.QuickMoveFolder2, () => {_config.QuickMoveFolder2 = string.Empty; _config.Save();}), 120);
        SubscribeRightClickMain(() => ClearQuickMove(2, _config.QuickMoveFolder3, () => {_config.QuickMoveFolder3 = string.Empty; _config.Save();}), 130);
        UnsubscribeRightClickLeaf(RenameLeaf);
        SetRenameSearchPath(_config.ShowRename);
        AddButton(AddNewModButton,    0);
        AddButton(AddImportModButton, 1);
        AddButton(AddHelpButton,      2);
        AddButton(DeleteModButton,    1000);
        // @formatter:on
        SetFilterTooltip();

        SelectionChanged += OnSelectionChange;
        if (_config.Ephemeral.LastModPath.Length > 0)
        {
            var mod = _modManager.FirstOrDefault(m
                => string.Equals(m.Identifier, _config.Ephemeral.LastModPath, StringComparison.OrdinalIgnoreCase));
            if (mod != null)
                SelectByValue(mod);
        }

        _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.ModFileSystemSelector);
        _communicator.ModSettingChanged.Subscribe(OnSettingChange, ModSettingChanged.Priority.ModFileSystemSelector);
        _communicator.CollectionInheritanceChanged.Subscribe(OnInheritanceChange, CollectionInheritanceChanged.Priority.ModFileSystemSelector);
        _communicator.ModDataChanged.Subscribe(OnModDataChange, ModDataChanged.Priority.ModFileSystemSelector);
        _communicator.ModDiscoveryStarted.Subscribe(StoreCurrentSelection, ModDiscoveryStarted.Priority.ModFileSystemSelector);
        _communicator.ModDiscoveryFinished.Subscribe(RestoreLastSelection, ModDiscoveryFinished.Priority.ModFileSystemSelector);
        OnCollectionChange(CollectionType.Current, null, _collectionManager.Active.Current, "");
    }

    public void SetRenameSearchPath(RenameField value)
    {
        switch (value)
        {
            case RenameField.RenameSearchPath:
                SubscribeRightClickLeaf(RenameLeafMod, 1000);
                UnsubscribeRightClickLeaf(RenameMod);
                break;
            case RenameField.RenameData:
                UnsubscribeRightClickLeaf(RenameLeafMod);
                SubscribeRightClickLeaf(RenameMod, 1000);
                break;
            case RenameField.BothSearchPathPrio:
                UnsubscribeRightClickLeaf(RenameLeafMod);
                UnsubscribeRightClickLeaf(RenameMod);
                SubscribeRightClickLeaf(RenameLeafMod, 1001);
                SubscribeRightClickLeaf(RenameMod,     1000);
                break;
            case RenameField.BothDataPrio:
                UnsubscribeRightClickLeaf(RenameLeafMod);
                UnsubscribeRightClickLeaf(RenameMod);
                SubscribeRightClickLeaf(RenameLeafMod, 1000);
                SubscribeRightClickLeaf(RenameMod,     1001);
                break;
            default:
                UnsubscribeRightClickLeaf(RenameLeafMod);
                UnsubscribeRightClickLeaf(RenameMod);
                break;
        }
    }

    private static readonly string[] ValidModExtensions =
    [
        ".ttmp",
        ".ttmp2",
        ".pmp",
        ".zip",
        ".rar",
        ".7z",
    ];

    public new void Draw(float width)
    {
        _dragDrop.CreateImGuiSource("ModDragDrop", m => m.Extensions.Any(e => ValidModExtensions.Contains(e.ToLowerInvariant())), m =>
        {
            ImGui.TextUnformatted($"拖拽到模组选择器进行导入：\n\t{string.Join("\n\t", m.Files.Select(Path.GetFileName))}");
            return true;
        });
        base.Draw(width);
        if (_dragDrop.CreateImGuiTarget("ModDragDrop", out var files, out _))
            _modImportManager.AddUnpack(files.Where(f => ValidModExtensions.Contains(Path.GetExtension(f.ToLowerInvariant()))));
    }

    public override void Dispose()
    {
        base.Dispose();
        _communicator.ModDiscoveryStarted.Unsubscribe(StoreCurrentSelection);
        _communicator.ModDiscoveryFinished.Unsubscribe(RestoreLastSelection);
        _communicator.ModDataChanged.Unsubscribe(OnModDataChange);
        _communicator.ModSettingChanged.Unsubscribe(OnSettingChange);
        _communicator.CollectionInheritanceChanged.Unsubscribe(OnInheritanceChange);
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
    }

    public new ModFileSystem.Leaf? SelectedLeaf
        => base.SelectedLeaf;

    #region Interface

    // Customization points.
    public override ISortMode<Mod> SortMode
        => _config.SortMode;

    protected override uint ExpandedFolderColor
        => ColorId.FolderExpanded.Value();

    protected override uint CollapsedFolderColor
        => ColorId.FolderCollapsed.Value();

    protected override uint FolderLineColor
        => ColorId.FolderLine.Value();

    protected override bool FoldersDefaultOpen
        => _config.OpenFoldersByDefault;

    protected override void DrawPopups()
    {
        DrawHelpPopup();

        if( ImGuiUtil.OpenNameField( "创建新模组", ref _newModName ) )
        {
            var newDir = _modManager.Creator.CreateEmptyMod(_modManager.BasePath, _newModName);
            if (newDir != null)
            {
                _modManager.AddMod(newDir);
                _newModName = string.Empty;
            }
        }

        while (_modImportManager.AddUnpackedMod(out var mod))
        {
            MoveModToDefaultDirectory(mod);
            SelectByValue(mod);
        }
    }

    protected override void DrawLeafName(FileSystem<Mod>.Leaf leaf, in ModState state, bool selected)
    {
        var flags = selected ? ImGuiTreeNodeFlags.Selected | LeafFlags : LeafFlags;
        using var c = ImRaii.PushColor(ImGuiCol.Text, state.Color.Value())
            .Push(ImGuiCol.HeaderHovered, 0x4000FFFF, leaf.Value.Favorite);
        using var id = ImRaii.PushId(leaf.Value.Index);
        ImRaii.TreeNode(leaf.Value.Name, flags).Dispose();
        if (ImGui.IsItemClicked(ImGuiMouseButton.Middle))
        {
            _modManager.SetKnown(leaf.Value);
            var (setting, collection) = _collectionManager.Active.Current[leaf.Value.Index];
            if (_config.DeleteModModifier.ForcedModifier(new DoubleModifier(ModifierHotkey.Control, ModifierHotkey.Shift)).IsActive())
            {
                _collectionManager.Editor.SetModInheritance(_collectionManager.Active.Current, leaf.Value, true);
            }
            else
            {
                var inherited = collection != _collectionManager.Active.Current;
                if (inherited)
                    _collectionManager.Editor.SetModInheritance(_collectionManager.Active.Current, leaf.Value, false);
                _collectionManager.Editor.SetModState(_collectionManager.Active.Current, leaf.Value, setting is not { Enabled: true });
            }
        }

        if (!state.Priority.IsDefault && !_config.HidePrioritiesInSelector)
        {
            var line           = ImGui.GetItemRectMin().Y;
            var itemPos        = ImGui.GetItemRectMax().X;
            var maxWidth       = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
            var priorityString = $"[{state.Priority}]";
            var size           = ImGui.CalcTextSize(priorityString).X;
            var remainingSpace = maxWidth - itemPos;
            var offset         = remainingSpace - size;
            if (ImGui.GetScrollMaxY() == 0)
                offset -= ImGui.GetStyle().ItemInnerSpacing.X;

            if (offset > ImGui.GetStyle().ItemSpacing.X)
                ImGui.GetWindowDrawList().AddText(new Vector2(itemPos + offset, line), ColorId.SelectorPriority.Value(), priorityString);
        }
    }


    // Add custom context menu items.
    private void EnableDescendants(ModFileSystem.Folder folder)
    {
        if( ImGui.MenuItem( "启用子项" ) )
            SetDescendants(folder, true);
    }

    private void DisableDescendants(ModFileSystem.Folder folder)
    {
        if( ImGui.MenuItem( "禁用子项" ) )
            SetDescendants(folder, false);
    }

    private void InheritDescendants(ModFileSystem.Folder folder)
    {
        if( ImGui.MenuItem( "继承子项" ) )
            SetDescendants(folder, true, true);
    }

    private void OwnDescendants(ModFileSystem.Folder folder)
    {
        if( ImGui.MenuItem( "停止继承子项" ) )
            SetDescendants(folder, false, true);
    }

    private void ToggleLeafFavorite(FileSystem<Mod>.Leaf mod)
    {
        if( ImGui.MenuItem( mod.Value.Favorite ? "移除收藏" : "标记为收藏" ) )
            _modManager.DataEditor.ChangeModFavorite(mod.Value, !mod.Value.Favorite);
    }

    private void SetDefaultImportFolder(ModFileSystem.Folder folder)
    {
        if( ImGui.MenuItem( "设置为默认导入折叠组" ) )
            return;

        var newName = folder.FullName();
        if (newName == _config.DefaultImportFolder)
            return;

        _config.DefaultImportFolder = newName;
        _config.Save();
    }

    private void ClearDefaultImportFolder()
    {
        if (!ImGui.MenuItem("清理默认导入折叠组") || _config.DefaultImportFolder.Length <= 0)
            return;

        _config.DefaultImportFolder = string.Empty;
        _config.Save();
    }

    private string _newModName = string.Empty;

    private void AddNewModButton(Vector2 size)
    {
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), size, "创建命名一个空白模组。",
                !_modManager.Valid, true))
            ImGui.OpenPopup( "创建新模组" );
    }

    /// <summary> Add an import mods button that opens a file selector. </summary>
    private void AddImportModButton(Vector2 size)
    {
        var button = ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.FileImport.ToIconString(), size,
            "导入单个或多个来自TexTools、Penumbra创建的模组。", !_modManager.Valid, true);
        _tutorial.OpenTutorial(BasicTutorialSteps.ModImport);
        if (!button)
            return;

        var modPath = _config.DefaultModImportPath.Length > 0
            ? _config.DefaultModImportPath
            : _config.ModDirectory.Length > 0
                ? _config.ModDirectory
                : null;

        _fileDialog.OpenFilePicker( "导入模组文件",
            "模组文件{.ttmp,.ttmp2,.pmp},TexTools模组文件{.ttmp,.ttmp2},Penumbra模组文件{.pmp},压缩文件{.zip,.7z,.rar}", (s, f) =>
            {
                if (!s)
                    return;

                _modImportManager.AddUnpack(f);
            }, 0, modPath, _config.AlwaysOpenDefaultImport);
    }

    private void RenameLeafMod(ModFileSystem.Leaf leaf)
    {
        ImGui.Separator();
        RenameLeaf(leaf);
    }

    private void RenameMod(ModFileSystem.Leaf leaf)
    {
        ImGui.Separator();
        var currentName = leaf.Value.Name.Text;
        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere(0);
        ImGui.TextUnformatted("重命名模组：");
        if (ImGui.InputText("##RenameMod", ref currentName, 256, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            _modManager.DataEditor.ChangeModName(leaf.Value, currentName);
            ImGui.CloseCurrentPopup();
        }

        ImGuiUtil.HoverTooltip("在此输入新名称以重命名已更改的模组。");
    }

    private void DeleteModButton(Vector2 size)
        => DeleteSelectionButton( size, _config.DeleteModModifier, "模组", "模组", _modManager.DeleteMod );

    private void AddHelpButton(Vector2 size)
    {
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.QuestionCircle.ToIconString(), size, "查看帮助文档。", false, true ) )
            ImGui.OpenPopup("ExtendedHelp");

        _tutorial.OpenTutorial(BasicTutorialSteps.AdvancedHelp);
    }

    private void SetDescendants(ModFileSystem.Folder folder, bool enabled, bool inherit = false)
    {
        var mods = folder.GetAllDescendants(ISortMode<Mod>.Lexicographical).OfType<ModFileSystem.Leaf>().Select(l =>
        {
            // Any mod handled here should not stay new.
            _modManager.SetKnown(l.Value);
            return l.Value;
        });

        if (inherit)
            _collectionManager.Editor.SetMultipleModInheritances(_collectionManager.Active.Current, mods, enabled);
        else
            _collectionManager.Editor.SetMultipleModStates(_collectionManager.Active.Current, mods, enabled);
    }

    /// <summary>
    /// If a default import folder is setup, try to move the given mod in there.
    /// If the folder does not exist, create it if possible.
    /// </summary>
    /// <param name="mod"></param>
    private void MoveModToDefaultDirectory(Mod mod)
    {
        if (_config.DefaultImportFolder.Length == 0)
            return;

        try
        {
            var leaf = FileSystem.Root.GetChildren(ISortMode<Mod>.Lexicographical)
                .FirstOrDefault(f => f is FileSystem<Mod>.Leaf l && l.Value == mod);
            if (leaf == null)
                throw new Exception("Mod was not found at root.");

            var folder = FileSystem.FindOrCreateAllFolders(_config.DefaultImportFolder);
            FileSystem.Move(leaf, folder);
        }
        catch (Exception e)
        {
            _messager.NotificationMessage(e,
                $"Could not move newly imported mod {mod.Name} to default import folder {_config.DefaultImportFolder}.",
                NotificationType.Warning);
        }
    }

    private void DrawHelpPopup()
    {
        ImGuiUtil.HelpPopup("ExtendedHelp", new Vector2(1000 * UiHelpers.Scale, 38.5f * ImGui.GetTextLineHeightWithSpacing()), () =>
        {
            ImGui.Dummy(Vector2.UnitY * ImGui.GetTextLineHeight());
            ImGui.TextUnformatted( "模组管理" );
            ImGui.BulletText( "你可以通过使用此行按钮来创建一个空白模组或导入模组。" );
            using var indent = ImRaii.PushIndent();
            ImGui.BulletText( "支持导入的格式为：.ttmp, .ttmp2, .pmp。" );
            ImGui.BulletText(
                "也支持.zip, .7z or .rar压缩包, 但必须是Penumbra类型的含有正确元数据的模组压缩包。" );
            indent.Pop(1);
            ImGui.BulletText( "你也可以创建空白的模组文件或删除模组。" );
            ImGui.BulletText( "要进一步编辑模组，请使用模组面板中的编辑选项卡或高级编辑弹出的窗口面板。" );
            ImGui.Dummy(Vector2.UnitY * ImGui.GetTextLineHeight());
            ImGui.TextUnformatted( "模组选择器" );
            ImGui.BulletText( "选中一个模组查看更多信息或修改设置。" );
            ImGui.BulletText( "模组名字会按你的设置显示符合他们在当前合集中状态的颜色：" );
            indent.Push();
            ImGuiUtil.BulletTextColored(ColorId.EnabledMod.Value(),           "在当前合集中已启用。");
            ImGuiUtil.BulletTextColored(ColorId.DisabledMod.Value(),          "在当前合集中已禁用。");
            ImGuiUtil.BulletTextColored(ColorId.InheritedMod.Value(),         "因从另一个合集继承而启用。");
            ImGuiUtil.BulletTextColored(ColorId.InheritedDisabledMod.Value(), "因从另一个合集继承而禁用。");
            ImGuiUtil.BulletTextColored(ColorId.UndefinedMod.Value(),         "未在所有继承的合集中配置。");
            ImGuiUtil.BulletTextColored(ColorId.NewMod.Value(),
                "在此次会话中导入的新模组，会在启用模组或Penumbra重新加载后取消标记。" );
            ImGuiUtil.BulletTextColored(ColorId.HandledConflictMod.Value(),
                "该模组已启用，但和其他已启用的模组冲突，并处于不同的优先级（举例：设置不同优先级后冲突已解决）。" );
            ImGuiUtil.BulletTextColored(ColorId.ConflictingMod.Value(),
                "该模组已启用，但和其他已启用的模组冲突，并处于同一优先级。" );
            ImGuiUtil.BulletTextColored(ColorId.FolderExpanded.Value(), "展开折叠组。" );
            ImGuiUtil.BulletTextColored(ColorId.FolderCollapsed.Value(), "最小化折叠组。" );
            indent.Pop(1);
            ImGui.BulletText("中键点击一个模组，如果是禁用则启用，如果是启用则禁用。");
            indent.Push();
            ImGui.BulletText(
                $"按住{_config.DeleteModModifier.ForcedModifier(new DoubleModifier(ModifierHotkey.Control, ModifierHotkey.Shift))}同时点击鼠标中键使其继承，放弃设置。");
            indent.Pop(1);
            ImGui.BulletText("右键点击一个模组并输入字符进行排序（默认按模组名称排）。可以使用相同的编号。");
            indent.Push();
            ImGui.BulletText("输入的排序字符不同于模组名称，不会被显示出来，仅用于排序。");
            ImGui.BulletText(
                "如果输入的排序字符中包含斜杠('/'), 斜杠前的字符将被转换为折叠组的名称，这样你就可以对模组进行分类管理。" );
            indent.Pop(1);
            ImGui.BulletText(
                "你可以直接拖拽模组或者折叠组到另一个已经存在的折叠组中，拖拽到折叠组名称上或者里面的模组名称上效果一样。" );
            indent.Push();
            ImGui.BulletText(
                "你可以通过按住CTRL+单击同时选中多个模组和折叠组，然后一次性拖动所有模组和折叠组。");
            ImGui.BulletText(
                "拖动和移动折叠组时，单独选择折叠组中的模组将被忽略，不会被直接移动到目标位置。");
            indent.Pop(1);
            ImGui.BulletText("右键单击折叠组打开菜单选项。");
            ImGui.BulletText("在空白处右键可以选择展开或最小化所有折叠组。");
            ImGui.BulletText("在模组列表上方的筛选框中可以输入模组名字或路径中包含的字符来进行筛选。");
            indent.Push();
            ImGui.BulletText( "你可以输入 n:[文字] 按名字筛选。" );
            ImGui.BulletText( "你可以输入 c:[文字] 按修改的物品名称来筛选。" );
            ImGui.BulletText( "你可以输入 a:[文字] 按作者名称来筛选。" );
            indent.Pop(1);
            ImGui.BulletText( "使用输入框旁边的下拉菜单来筛选满足特定条件的模组。" );
        });
    }

    private static void HandleException(Exception e)
        => Penumbra.Messager.NotificationMessage(e, e.Message, NotificationType.Warning);

    #endregion

    #region Automatic cache update functions.

    private void OnSettingChange(ModCollection collection, ModSettingChange type, Mod? mod, Setting oldValue, int groupIdx, bool inherited)
    {
        if (collection != _collectionManager.Active.Current)
            return;

        SetFilterDirty();
        if (mod == Selected)
            OnSelectionChange(Selected, Selected, default);
    }

    private void OnModDataChange(ModDataChangeType type, Mod mod, string? oldName)
    {
        switch (type)
        {
            case ModDataChangeType.Name:
            case ModDataChangeType.Author:
            case ModDataChangeType.ModTags:
            case ModDataChangeType.LocalTags:
            case ModDataChangeType.Favorite:
                SetFilterDirty();
                break;
        }
    }

    private void OnInheritanceChange(ModCollection collection, bool _)
    {
        if (collection != _collectionManager.Active.Current)
            return;

        SetFilterDirty();
        OnSelectionChange(Selected, Selected, default);
    }

    private void OnCollectionChange(CollectionType collectionType, ModCollection? oldCollection, ModCollection? newCollection, string _)
    {
        if (collectionType is not CollectionType.Current || oldCollection == newCollection)
            return;

        SetFilterDirty();
        OnSelectionChange(Selected, Selected, default);
    }

    private void OnSelectionChange(Mod? _1, Mod? newSelection, in ModState _2)
    {
        if (newSelection == null)
        {
            SelectedSettings          = ModSettings.Empty;
            SelectedSettingCollection = ModCollection.Empty;
        }
        else
        {
            (var settings, SelectedSettingCollection) = _collectionManager.Active.Current[newSelection.Index];
            SelectedSettings                          = settings ?? ModSettings.Empty;
        }

        var name = newSelection?.Identifier ?? string.Empty;
        if (name != _config.Ephemeral.LastModPath)
        {
            _config.Ephemeral.LastModPath = name;
            _config.Ephemeral.Save();
        }
    }

    // Keep selections across rediscoveries if possible.
    private string _lastSelectedDirectory = string.Empty;

    private void StoreCurrentSelection()
    {
        _lastSelectedDirectory = Selected?.ModPath.FullName ?? string.Empty;
        ClearSelection();
    }

    private void RestoreLastSelection()
    {
        if (_lastSelectedDirectory.Length <= 0)
            return;

        var leaf = (ModFileSystem.Leaf?)FileSystem.Root.GetAllDescendants(ISortMode<Mod>.Lexicographical)
            .FirstOrDefault(l => l is ModFileSystem.Leaf m && m.Value.ModPath.FullName == _lastSelectedDirectory);
        Select(leaf, AllowMultipleSelection);
        _lastSelectedDirectory = string.Empty;
    }

    #endregion

    #region Filters

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ModState
    {
        public ColorId     Color;
        public ModPriority Priority;
    }

    private const StringComparison                  IgnoreCase   = StringComparison.OrdinalIgnoreCase;
    private       LowerString                       _modFilter   = LowerString.Empty;
    private       int                               _filterType  = -1;
    private       ModFilter                         _stateFilter = ModFilterExtensions.UnfilteredStateMods;
    private       ChangedItemDrawer.ChangedItemIcon _slotFilter  = 0;

    private void SetFilterTooltip()
    {
        FilterTooltip = "输入模组名字或路径中包含的字符来进行筛选。\n"
          + "输入 c:[文字] 按修改的物品名称筛选。\n"
          + "输入 t:[文字] 按模组标签筛选。\n"
          + "输入 n:[文字] 按模组名字筛选。\n"
          + "输入 a:[文字] 按作者名称筛选。"
          + $"输入 s:[文字] 按模组修改的物品的类别(1-{ChangedItemDrawer.NumCategories + 1}或不完整的类别名称)来进行筛选。\n"
          + "使用[None]作为占位符值仅匹配空列表或名称。";
    }

    /// <summary> Appropriately identify and set the string filter and its type. </summary>
    protected override bool ChangeFilter(string filterValue)
    {
        (_modFilter, _filterType) = filterValue.Length switch
        {
            0 => (LowerString.Empty, -1),
            > 1 when filterValue[1] == ':' =>
                filterValue[0] switch
                {
                    'n' => filterValue.Length == 2 ? (LowerString.Empty, -1) : (new LowerString(filterValue[2..]), 1),
                    'N' => filterValue.Length == 2 ? (LowerString.Empty, -1) : (new LowerString(filterValue[2..]), 1),
                    'a' => filterValue.Length == 2 ? (LowerString.Empty, -1) : ParseFilter(filterValue, 2),
                    'A' => filterValue.Length == 2 ? (LowerString.Empty, -1) : ParseFilter(filterValue, 2),
                    'c' => filterValue.Length == 2 ? (LowerString.Empty, -1) : ParseFilter(filterValue, 3),
                    'C' => filterValue.Length == 2 ? (LowerString.Empty, -1) : ParseFilter(filterValue, 3),
                    't' => filterValue.Length == 2 ? (LowerString.Empty, -1) : ParseFilter(filterValue, 4),
                    'T' => filterValue.Length == 2 ? (LowerString.Empty, -1) : ParseFilter(filterValue, 4),
                    's' => filterValue.Length == 2 ? (LowerString.Empty, -1) : ParseFilter(filterValue, 5),
                    'S' => filterValue.Length == 2 ? (LowerString.Empty, -1) : ParseFilter(filterValue, 5),
                    _   => (new LowerString(filterValue), 0),
                },
            _ => (new LowerString(filterValue), 0),
        };

        return true;
    }

    private const int EmptyOffset = 128;

    private (LowerString, int) ParseFilter(string value, int id)
    {
        value = value[2..];
        var lower = new LowerString(value);
        if (id == 5 && !ChangedItemDrawer.TryParsePartial(lower.Lower, out _slotFilter))
            _slotFilter = 0;

        return (lower, lower.Lower is "none" ? id + EmptyOffset : id);
    }


    /// <summary>
    /// Check the state filter for a specific pair of has/has-not flags.
    /// Uses count == 0 to check for has-not and count != 0 for has.
    /// Returns true if it should be filtered and false if not. 
    /// </summary>
    private bool CheckFlags(int count, ModFilter hasNoFlag, ModFilter hasFlag)
    {
        return count switch
        {
            0 when _stateFilter.HasFlag(hasNoFlag) => false,
            0                                      => true,
            _ when _stateFilter.HasFlag(hasFlag)   => false,
            _                                      => true,
        };
    }

    /// <summary>
    /// The overwritten filter method also computes the state.
    /// Folders have default state and are filtered out on the direct string instead of the other options.
    /// If any filter is set, they should be hidden by default unless their children are visible,
    /// or they contain the path search string.
    /// </summary>
    protected override bool ApplyFiltersAndState(FileSystem<Mod>.IPath path, out ModState state)
    {
        if (path is ModFileSystem.Folder f)
        {
            state = default;
            return ModFilterExtensions.UnfilteredStateMods != _stateFilter
             || FilterValue.Length > 0 && !f.FullName().Contains(FilterValue, IgnoreCase);
        }

        return ApplyFiltersAndState((ModFileSystem.Leaf)path, out state);
    }

    /// <summary> Apply the string filters. </summary>
    private bool ApplyStringFilters(ModFileSystem.Leaf leaf, Mod mod)
    {
        return _filterType switch
        {
            -1              => false,
            0               => !(leaf.FullName().Contains(_modFilter.Lower, IgnoreCase) || mod.Name.Contains(_modFilter)),
            1               => !mod.Name.Contains(_modFilter),
            2               => !mod.Author.Contains(_modFilter),
            3               => !mod.LowerChangedItemsString.Contains(_modFilter.Lower),
            4               => !mod.AllTagsLower.Contains(_modFilter.Lower),
            5               => mod.ChangedItems.All(p => (ChangedItemDrawer.GetCategoryIcon(p.Key, p.Value) & _slotFilter) == 0),
            2 + EmptyOffset => !mod.Author.IsEmpty,
            3 + EmptyOffset => mod.LowerChangedItemsString.Length > 0,
            4 + EmptyOffset => mod.AllTagsLower.Length > 0,
            5 + EmptyOffset => mod.ChangedItems.Count == 0,
            _               => false, // Should never happen
        };
    }

    /// <summary> Only get the text color for a mod if no filters are set. </summary>
    private ColorId GetTextColor(Mod mod, ModSettings? settings, ModCollection collection)
    {
        if (_modManager.IsNew(mod))
            return ColorId.NewMod;

        if (settings == null)
            return ColorId.UndefinedMod;

        if (!settings.Enabled)
            return collection != _collectionManager.Active.Current ? ColorId.InheritedDisabledMod : ColorId.DisabledMod;

        var conflicts = _collectionManager.Active.Current.Conflicts(mod);
        if (conflicts.Count == 0)
            return collection != _collectionManager.Active.Current ? ColorId.InheritedMod : ColorId.EnabledMod;

        return conflicts.Any(c => !c.Solved)
            ? ColorId.ConflictingMod
            : ColorId.HandledConflictMod;
    }

    private bool CheckStateFilters(Mod mod, ModSettings? settings, ModCollection collection, ref ModState state)
    {
        var isNew = _modManager.IsNew(mod);
        // Handle mod details.
        if (CheckFlags(mod.TotalFileCount,     ModFilter.HasNoFiles,             ModFilter.HasFiles)
         || CheckFlags(mod.TotalSwapCount,     ModFilter.HasNoFileSwaps,         ModFilter.HasFileSwaps)
         || CheckFlags(mod.TotalManipulations, ModFilter.HasNoMetaManipulations, ModFilter.HasMetaManipulations)
         || CheckFlags(mod.HasOptions ? 1 : 0, ModFilter.HasNoConfig,            ModFilter.HasConfig)
         || CheckFlags(isNew ? 1 : 0,          ModFilter.NotNew,                 ModFilter.IsNew))
            return true;

        // Handle Favoritism
        if (!_stateFilter.HasFlag(ModFilter.Favorite) && mod.Favorite
         || !_stateFilter.HasFlag(ModFilter.NotFavorite) && !mod.Favorite)
            return true;

        // Handle Inheritance
        if (collection == _collectionManager.Active.Current)
        {
            if (!_stateFilter.HasFlag(ModFilter.Uninherited))
                return true;
        }
        else
        {
            state.Color = ColorId.InheritedMod;
            if (!_stateFilter.HasFlag(ModFilter.Inherited))
                return true;
        }

        // Handle settings.
        if (settings == null)
        {
            state.Color = ColorId.UndefinedMod;
            if (!_stateFilter.HasFlag(ModFilter.Undefined)
             || !_stateFilter.HasFlag(ModFilter.Disabled)
             || !_stateFilter.HasFlag(ModFilter.NoConflict))
                return true;
        }
        else if (!settings.Enabled)
        {
            state.Color = collection == _collectionManager.Active.Current ? ColorId.DisabledMod : ColorId.InheritedDisabledMod;
            if (!_stateFilter.HasFlag(ModFilter.Disabled)
             || !_stateFilter.HasFlag(ModFilter.NoConflict))
                return true;
        }
        else
        {
            if (!_stateFilter.HasFlag(ModFilter.Enabled))
                return true;

            // Conflicts can only be relevant if the mod is enabled.
            var conflicts = _collectionManager.Active.Current.Conflicts(mod);
            if (conflicts.Count > 0)
            {
                if (conflicts.Any(c => !c.Solved))
                {
                    if (!_stateFilter.HasFlag(ModFilter.UnsolvedConflict))
                        return true;

                    state.Color = ColorId.ConflictingMod;
                }
                else
                {
                    if (!_stateFilter.HasFlag(ModFilter.SolvedConflict))
                        return true;

                    state.Color = ColorId.HandledConflictMod;
                }
            }
            else if (!_stateFilter.HasFlag(ModFilter.NoConflict))
            {
                return true;
            }
        }

        // isNew color takes precedence before other colors.
        if (isNew)
            state.Color = ColorId.NewMod;

        return false;
    }

    /// <summary> Combined wrapper for handling all filters and setting state. </summary>
    private bool ApplyFiltersAndState(ModFileSystem.Leaf leaf, out ModState state)
    {
        var mod = leaf.Value;
        var (settings, collection) = _collectionManager.Active.Current[mod.Index];

        state = new ModState
        {
            Color    = ColorId.EnabledMod,
            Priority = settings?.Priority ?? ModPriority.Default,
        };
        if (ApplyStringFilters(leaf, mod))
            return true;

        if (_stateFilter != ModFilterExtensions.UnfilteredStateMods)
            return CheckStateFilters(mod, settings, collection, ref state);

        state.Color = GetTextColor(mod, settings, collection);
        return false;
    }

    private bool DrawFilterCombo(ref bool everything)
    {
        using var combo = ImRaii.Combo("##filterCombo", string.Empty,
            ImGuiComboFlags.NoPreview | ImGuiComboFlags.PopupAlignLeft | ImGuiComboFlags.HeightLargest);
        var ret = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        if (!combo)
            return ret;

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
            ImGui.GetStyle().ItemSpacing with { Y = 3 * UiHelpers.Scale });
        var flags = (int)_stateFilter;


        if (ImGui.Checkbox("全部", ref everything))
        {
            _stateFilter = everything ? ModFilterExtensions.UnfilteredStateMods : 0;
            SetFilterDirty();
        }

        ImGui.Dummy(new Vector2(0, 5 * UiHelpers.Scale));
        foreach (ModFilter flag in Enum.GetValues(typeof(ModFilter)))
        {
            if (ImGui.CheckboxFlags(flag.ToName(), ref flags, (int)flag))
            {
                _stateFilter = (ModFilter)flags;
                SetFilterDirty();
            }
        }

        return ret;
    }

    /// <summary> Add the state filter combo-button to the right of the filter box. </summary>
    protected override (float, bool) CustomFilters(float width)
    {
        var pos            = ImGui.GetCursorPos();
        var remainingWidth = width - ImGui.GetFrameHeight();
        var comboPos       = new Vector2(pos.X + remainingWidth, pos.Y);

        var everything = _stateFilter == ModFilterExtensions.UnfilteredStateMods;

        ImGui.SetCursorPos(comboPos);
        // Draw combo button
        using var color      = ImRaii.PushColor(ImGuiCol.Button, Colors.FilterActive, !everything);
        var       rightClick = DrawFilterCombo(ref everything);
        _tutorial.OpenTutorial(BasicTutorialSteps.ModFilters);
        if (rightClick)
        {
            _stateFilter = ModFilterExtensions.UnfilteredStateMods;
            SetFilterDirty();
        }

        ImGuiUtil.HoverTooltip("按激活状态筛选模组。\n右键点击以清除所有筛选。");
        ImGui.SetCursorPos(pos);
        return (remainingWidth, rightClick);
    }

    #endregion
}
