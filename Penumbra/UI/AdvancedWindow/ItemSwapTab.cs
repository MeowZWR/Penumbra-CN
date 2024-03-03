using Dalamud.Interface.Internal.Notifications;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.ItemSwap;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Subclasses;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public class ItemSwapTab : IDisposable, ITab
{
    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;
    private readonly CollectionManager   _collectionManager;
    private readonly ModManager          _modManager;
    private readonly MetaFileManager     _metaFileManager;

    public ItemSwapTab(CommunicatorService communicator, ItemData itemService, CollectionManager collectionManager,
        ModManager modManager, ObjectIdentification identifier, MetaFileManager metaFileManager, Configuration config)
    {
        _communicator      = communicator;
        _collectionManager = collectionManager;
        _modManager        = modManager;
        _metaFileManager   = metaFileManager;
        _config            = config;
        _swapData          = new ItemSwapContainer(metaFileManager, identifier);

        _selectors = new Dictionary<SwapType, (ItemSelector Source, ItemSelector Target, string TextFrom, string TextTo)>
        {
            // @formatter:off
            [SwapType.头部装备]      = (new ItemSelector(itemService, FullEquipType.Head),   new ItemSelector(itemService, FullEquipType.Head),    "头部装备（源）", "头部装备（目标）"),
            [SwapType.身体装备]      = (new ItemSelector(itemService, FullEquipType.Body),   new ItemSelector(itemService, FullEquipType.Body),    "身体装备（源）", "身体装备（目标）"),
            [SwapType.手部装备]   = (new ItemSelector(itemService, FullEquipType.Hands),  new ItemSelector(itemService, FullEquipType.Hands),   "手部装备（源）", "手部装备（目标）"),
            [SwapType.腿部装备]    = (new ItemSelector(itemService, FullEquipType.Legs),   new ItemSelector(itemService, FullEquipType.Legs),    "腿部装备（源）", "腿部装备（目标）"),
            [SwapType.脚部装备]    = (new ItemSelector(itemService, FullEquipType.Feet),   new ItemSelector(itemService, FullEquipType.Feet),    "脚部装备（源）", "脚部装备（目标）"),
            [SwapType.耳部装备] = (new ItemSelector(itemService, FullEquipType.Ears),   new ItemSelector(itemService, FullEquipType.Ears),    "耳部装备（源）", "耳部装备（目标）"),
            [SwapType.颈部装备] = (new ItemSelector(itemService, FullEquipType.Neck),   new ItemSelector(itemService, FullEquipType.Neck),    "颈部装备（源）", "颈部装备（目标）"),
            [SwapType.腕部装备] = (new ItemSelector(itemService, FullEquipType.Wrists), new ItemSelector(itemService, FullEquipType.Wrists),  "手腕装备（源）", "手腕装备（目标）"),
            [SwapType.手指装备]     = (new ItemSelector(itemService, FullEquipType.Finger), new ItemSelector(itemService, FullEquipType.Finger),  "手指装备（源）", "手指装备（目标）"),
            // @formatter:on
        };

        _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.ItemSwapTab);
        _communicator.ModSettingChanged.Subscribe(OnSettingChange, ModSettingChanged.Priority.ItemSwapTab);
        _communicator.CollectionInheritanceChanged.Subscribe(OnInheritanceChange, CollectionInheritanceChanged.Priority.ItemSwapTab);
        _communicator.ModOptionChanged.Subscribe(OnModOptionChange, ModOptionChanged.Priority.ItemSwapTab);
    }

    /// <summary> Update the currently selected mod or its settings. </summary>
    public void UpdateMod(Mod mod, ModSettings? settings)
    {
        if (mod == _mod && settings == _modSettings)
            return;

        var oldDefaultName = $"{_mod?.Name.Text ?? "Unknown"} (Swapped)";
        if (_newModName.Length == 0 || oldDefaultName == _newModName)
            _newModName = $"{mod.Name.Text} (Swapped)";

        _mod         = mod;
        _modSettings = settings;
        _swapData.LoadMod(_mod, _modSettings);
        UpdateOption();
        _dirty = true;
    }

    public ReadOnlySpan<byte> Label
        => "Item Swap"u8;

    public void DrawContent()
    {
        ImGui.NewLine();
        DrawHeaderLine(300 * UiHelpers.Scale);
        ImGui.NewLine();

        DrawSwapBar();

        using var table = ImRaii.ListBox("##swaps", -Vector2.One);
        if (_loadException != null)
            ImGuiUtil.TextWrapped($"Could not load Customization Swap:\n{_loadException}");
        else if (_swapData.Loaded)
            foreach (var swap in _swapData.Swaps)
                DrawSwap(swap);
        else
            ImGui.TextUnformatted(NonExistentText());
    }

    public void Dispose()
    {
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
        _communicator.ModSettingChanged.Unsubscribe(OnSettingChange);
        _communicator.CollectionInheritanceChanged.Unsubscribe(OnInheritanceChange);
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
    }

    private enum SwapType
    {
        头部装备,
        身体装备,
        手部装备,
        腿部装备,
        脚部装备,
        耳部装备,
        颈部装备,
        腕部装备,
        手指装备,
        BetweenSlots,
        发型,
        Face,
        耳朵,
        尾巴,
        Weapon,
    }

    private class ItemSelector(ItemData data, FullEquipType type)
        : FilterComboCache<EquipItem>(() => data.ByType[type], MouseWheelType.None, Penumbra.Log)
    {
        protected override string ToString(EquipItem obj)
            => obj.Name;
    }

    private readonly Dictionary<SwapType, (ItemSelector Source, ItemSelector Target, string TextFrom, string TextTo)> _selectors;
    private readonly ItemSwapContainer                                                                                _swapData;

    private Mod?         _mod;
    private ModSettings? _modSettings;
    private bool         _dirty;

    private SwapType   _lastTab       = SwapType.发型;
    private Gender     _currentGender = Gender.Male;
    private ModelRace  _currentRace   = ModelRace.Midlander;
    private int        _targetId;
    private int        _sourceId;
    private Exception? _loadException;
    private EquipSlot  _slotFrom = EquipSlot.Head;
    private EquipSlot  _slotTo   = EquipSlot.Ears;

    private string     _newModName    = string.Empty;
    private string     _newGroupName  = "转换";
    private string     _newOptionName = string.Empty;
    private IModGroup? _selectedGroup;
    private bool       _subModValid;
    private bool       _useFileSwaps = true;
    private bool       _useCurrentCollection;
    private bool       _useLeftRing  = true;
    private bool       _useRightRing = true;

    private EquipItem[]? _affectedItems;

    private void UpdateState()
    {
        if (!_dirty)
            return;

        _swapData.Clear();
        _loadException = null;
        _affectedItems = null;
        try
        {
            switch (_lastTab)
            {
                case SwapType.头部装备:
                case SwapType.身体装备:
                case SwapType.手部装备:
                case SwapType.腿部装备:
                case SwapType.脚部装备:
                case SwapType.耳部装备:
                case SwapType.颈部装备:
                case SwapType.腕部装备:
                case SwapType.手指装备:
                    var values = _selectors[_lastTab];
                    if (values.Source.CurrentSelection.Type != FullEquipType.Unknown
                     && values.Target.CurrentSelection.Type != FullEquipType.Unknown)
                        _affectedItems = _swapData.LoadEquipment(values.Target.CurrentSelection, values.Source.CurrentSelection,
                            _useCurrentCollection ? _collectionManager.Active.Current : null, _useRightRing, _useLeftRing);

                    break;
                case SwapType.BetweenSlots:
                    var (_, _, selectorFrom) = GetAccessorySelector(_slotFrom, true);
                    var (_, _, selectorTo)   = GetAccessorySelector(_slotTo,   false);
                    if (selectorFrom.CurrentSelection.Valid && selectorTo.CurrentSelection.Valid)
                        _affectedItems = _swapData.LoadTypeSwap(_slotTo, selectorTo.CurrentSelection, _slotFrom, selectorFrom.CurrentSelection,
                            _useCurrentCollection ? _collectionManager.Active.Current : null);
                    break;
                case SwapType.发型 when _targetId > 0 && _sourceId > 0:
                    _swapData.LoadCustomization(_metaFileManager, BodySlot.Hair, Names.CombinedRace(_currentGender, _currentRace),
                        (PrimaryId)_sourceId,
                        (PrimaryId)_targetId,
                        _useCurrentCollection ? _collectionManager.Active.Current : null);
                    break;
                case SwapType.Face when _targetId > 0 && _sourceId > 0:
                    _swapData.LoadCustomization(_metaFileManager, BodySlot.Face, Names.CombinedRace(_currentGender, _currentRace),
                        (PrimaryId)_sourceId,
                        (PrimaryId)_targetId,
                        _useCurrentCollection ? _collectionManager.Active.Current : null);
                    break;
                case SwapType.耳朵 when _targetId > 0 && _sourceId > 0:
                    _swapData.LoadCustomization(_metaFileManager, BodySlot.Ear, Names.CombinedRace(_currentGender, ModelRace.Viera),
                        (PrimaryId)_sourceId,
                        (PrimaryId)_targetId,
                        _useCurrentCollection ? _collectionManager.Active.Current : null);
                    break;
                case SwapType.尾巴 when _targetId > 0 && _sourceId > 0:
                    _swapData.LoadCustomization(_metaFileManager, BodySlot.Tail, Names.CombinedRace(_currentGender, _currentRace),
                        (PrimaryId)_sourceId,
                        (PrimaryId)_targetId,
                        _useCurrentCollection ? _collectionManager.Active.Current : null);
                    break;
                case SwapType.Weapon: break;
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Error( $"无法获取自定义数据给{_lastTab}:\n{e}" );
            _loadException = e;
            _affectedItems = null;
            _swapData.Clear();
        }

        _dirty = false;
    }

    private static string SwapToString(Swap swap)
    {
        return swap switch
        {
            MetaSwap meta => $"{meta.SwapFrom}: {meta.SwapFrom.EntryToString()} -> {meta.SwapApplied.EntryToString()}",
            FileSwap file =>
                $"{file.Type}: {file.SwapFromRequestPath} -> {file.SwapToModded.FullName}{(file.DataWasChanged ? " (EDITED)" : string.Empty)}",
            _ => string.Empty,
        };
    }

    private string CreateDescription()
        => $"Created by swapping {_lastTab} {_sourceId} onto {_lastTab} {_targetId} for {_currentRace.ToName()} {_currentGender.ToName()}s in {_mod!.Name}.";

    private void UpdateOption()
    {
        _selectedGroup = _mod?.Groups.FirstOrDefault(g => g.Name == _newGroupName);
        _subModValid = _mod != null
         && _newGroupName.Length > 0
         && _newOptionName.Length > 0
         && (_selectedGroup?.All(o => o.Name != _newOptionName) ?? true);
    }

    private void CreateMod()
    {
        var newDir = _modManager.Creator.CreateEmptyMod(_modManager.BasePath, _newModName, CreateDescription());
        if (newDir == null)
            return;

        _modManager.AddMod(newDir);
        if (!_swapData.WriteMod(_modManager, _modManager[^1],
                _useFileSwaps ? ItemSwapContainer.WriteType.UseSwaps : ItemSwapContainer.WriteType.NoSwaps))
            _modManager.DeleteMod(_modManager[^1]);
    }

    private void CreateOption()
    {
        if (_mod == null || !_subModValid)
            return;

        var            groupCreated     = false;
        var            dirCreated       = false;
        var            optionCreated    = false;
        DirectoryInfo? optionFolderName = null;
        try
        {
            optionFolderName =
                ModCreator.NewSubFolderName(new DirectoryInfo(Path.Combine(_mod.ModPath.FullName, _selectedGroup?.Name ?? _newGroupName)),
                    _newOptionName, _config.ReplaceNonAsciiOnImport);
            if (optionFolderName?.Exists == true)
                throw new Exception($"The folder {optionFolderName.FullName} for the option already exists.");

            if (optionFolderName != null)
            {
                if (_selectedGroup == null)
                {
                    _modManager.OptionEditor.AddModGroup(_mod, GroupType.Multi, _newGroupName);
                    _selectedGroup = _mod.Groups.Last();
                    groupCreated   = true;
                }

                _modManager.OptionEditor.AddOption(_mod, _mod.Groups.IndexOf(_selectedGroup), _newOptionName);
                optionCreated    = true;
                optionFolderName = Directory.CreateDirectory(optionFolderName.FullName);
                dirCreated       = true;
                if (!_swapData.WriteMod(_modManager, _mod,
                        _useFileSwaps ? ItemSwapContainer.WriteType.UseSwaps : ItemSwapContainer.WriteType.NoSwaps,
                        optionFolderName,
                        _mod.Groups.IndexOf(_selectedGroup), _selectedGroup.Count - 1))
                    throw new Exception("模组转换文件写入失败。");
            }
        }
        catch (Exception e)
        {
            Penumbra.Messager.NotificationMessage(e, "无法新建转换选项。", NotificationType.Error, false);
            try
            {
                if (optionCreated && _selectedGroup != null)
                    _modManager.OptionEditor.DeleteOption(_mod, _mod.Groups.IndexOf(_selectedGroup), _selectedGroup.Count - 1);

                if (groupCreated)
                {
                    _modManager.OptionEditor.DeleteModGroup(_mod, _mod.Groups.IndexOf(_selectedGroup!));
                    _selectedGroup = null;
                }

                if (dirCreated && optionFolderName != null)
                    Directory.Delete(optionFolderName.FullName, true);
            }
            catch
            {
                // ignored
            }
        }

        UpdateOption();
    }

    private void DrawHeaderLine(float width)
    {
        var newModAvailable = _loadException == null && _swapData.Loaded;

        ImGui.SetNextItemWidth(width);
        if (ImGui.InputTextWithHint("##newModName", "新模组名（建议使用英文）...", ref _newModName, 64))
        { }

        ImGui.SameLine();
        var tt = !newModAvailable
            ? "你还没有设置一个转换。"
            : _newModName.Length == 0
                ? "请先为模组命名。"
                : "按给定的名称创建一个仅包含道具转换的新模组。";
        if( ImGuiUtil.DrawDisabledButton( "创建新模组", new Vector2( width / 2, 0 ), tt, !newModAvailable || _newModName.Length == 0 ) )
            CreateMod();

        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 20 * UiHelpers.Scale);
        ImGui.Checkbox( "使用'文件替换'功能", ref _useFileSwaps );
        ImGuiUtil.HoverTooltip( "如果道具转换时引用到了游戏文件，则尽可能的使用选项卡'文件替换'的功能，\n"
          + "而不是将每个单独的非默认文件都取出来写入新建的模组或选项目录。" );

        ImGui.SetNextItemWidth((width - ImGui.GetStyle().ItemSpacing.X) / 2);
        if( ImGui.InputTextWithHint( "##groupName", "组名称...", ref _newGroupName, 32 ) )
            UpdateOption();

        ImGui.SameLine();
        ImGui.SetNextItemWidth((width - ImGui.GetStyle().ItemSpacing.X) / 2);
        if( ImGui.InputTextWithHint( "##optionName", "新选项名称...", ref _newOptionName, 32 ) )
            UpdateOption();

        ImGui.SameLine();
        tt = !_subModValid
            ? "该组已存在相同名称的选项，或未指定名称。"
            : !newModAvailable
                ? "在当前模组中创建一个新选项，仅包含转换。"
                : "为道具转换在当前模组中创建一个新选项（也可能是多选项组）";
        if( ImGuiUtil.DrawDisabledButton( "创建新选项", new Vector2( width / 2, 0 ), tt, !newModAvailable || !_subModValid ) )
            CreateOption();

        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 20 * UiHelpers.Scale);
        _dirty |= ImGui.Checkbox( "使用整个合集", ref _useCurrentCollection );
        ImGuiUtil.HoverTooltip(
            "使用所选合集中所有模组的当前设置及其启用状态和继承关系"
          + "而不是忽略掉启用状态和继承关系，仅使用所选模组在所选择合集的默认设置。");
    }

    private void DrawSwapBar()
    {
        using var bar = ImRaii.TabBar("##swapBar", ImGuiTabBarFlags.None);

        DrawEquipmentSwap(SwapType.头部装备);
        DrawEquipmentSwap(SwapType.身体装备);
        DrawEquipmentSwap(SwapType.手部装备);
        DrawEquipmentSwap(SwapType.腿部装备);
        DrawEquipmentSwap(SwapType.脚部装备);
        DrawEquipmentSwap(SwapType.耳部装备);
        DrawEquipmentSwap(SwapType.颈部装备);
        DrawEquipmentSwap(SwapType.腕部装备);
        DrawEquipmentSwap(SwapType.手指装备);
        DrawAccessorySwap();
        DrawHairSwap();
        //DrawFaceSwap();
        DrawEarSwap();
        DrawTailSwap();
        //DrawWeaponSwap();
    }

    private ImRaii.IEndObject DrawTab(SwapType newTab)
    {
        using var tab = ImRaii.TabItem( newTab is SwapType.BetweenSlots ? "跨类型转换" : newTab.ToString() );
        if (tab)
        {
            _dirty   |= _lastTab != newTab;
            _lastTab =  newTab;
        }

        UpdateState();

        return tab;
    }

    private void DrawAccessorySwap()
    {
        using var tab = DrawTab(SwapType.BetweenSlots);
        if (!tab)
            return;

        using var table = ImRaii.Table("##settings", 3, ImGuiTableFlags.SizingFixedFit);
        ImGui.TableSetupColumn("##text", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("and put them on these").X);

        var (article1, article2, selector) = GetAccessorySelector(_slotFrom, true);
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted( $"转换{article1}" );

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(100 * UiHelpers.Scale);
        using( var combo = ImRaii.Combo( "##fromType", _slotFrom is EquipSlot.Head ? "头部装备" : _slotFrom.ToName() ) )
        {
            if (combo)
                foreach (var slot in EquipSlotExtensions.AccessorySlots.Prepend(EquipSlot.Head))
                {
                    if (!ImGui.Selectable(slot is EquipSlot.Head ? "头部装备" : slot.ToName(), slot == _slotFrom) || slot == _slotFrom)
                        continue;

                    _dirty    = true;
                    _slotFrom = slot;
                    if (slot == _slotTo)
                        _slotTo = EquipSlotExtensions.AccessorySlots.First(s => slot != s);
                }
        }

        ImGui.TableNextColumn();
        _dirty |= selector.Draw("##itemSource", selector.CurrentSelection.Name ?? string.Empty, string.Empty, InputWidth * 2 * UiHelpers.Scale,
            ImGui.GetTextLineHeightWithSpacing());

        (article1, _, selector) = GetAccessorySelector(_slotTo, false);
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"转换{article2}" );

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(100 * UiHelpers.Scale);
        using (var combo = ImRaii.Combo("##toType", _slotTo.ToName()))
        {
            if (combo)
                foreach (var slot in EquipSlotExtensions.AccessorySlots.Where(s => s != _slotFrom))
                {
                    if (!ImGui.Selectable(slot.ToName(), slot == _slotTo) || slot == _slotTo)
                        continue;

                    _dirty  = true;
                    _slotTo = slot;
                }
        }

        ImGui.TableNextColumn();

        _dirty |= selector.Draw("##itemTarget", selector.CurrentSelection.Name, string.Empty, InputWidth * 2 * UiHelpers.Scale,
            ImGui.GetTextLineHeightWithSpacing());
        if (_affectedItems is not { Length: > 1 })
            return;

        ImGui.SameLine();
        ImGuiUtil.DrawTextButton($"将同时在另外{_affectedItems.Length - 1}个同模物品上生效。", Vector2.Zero,
            Colors.PressEnterWarningBg);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Join('\n', _affectedItems.Where(i => !ReferenceEquals(i.Name, selector.CurrentSelection.Name))
                .Select(i => i.Name)));
    }

    private (string, string, ItemSelector) GetAccessorySelector(EquipSlot slot, bool source)
    {
        var (type, article1, article2) = slot switch
        {
            EquipSlot.Head    => (SwapType.头部装备,         "（源）", "（目标）"),
            EquipSlot.Ears    => (SwapType.耳部装备,    "（源）", "（目标）"),
            EquipSlot.Neck    => (SwapType.颈部装备,    "（源）", "（目标）"),
            EquipSlot.Wrists  => (SwapType.腕部装备,    "（源）", "（目标）"),
            EquipSlot.RFinger => (SwapType.手指装备,        "（源）", "（目标）"),
            EquipSlot.LFinger => (SwapType.手指装备,        "（源）", "（目标）"),
            _                 => (SwapType.手指装备,        "（源）", "（目标）"),
        };
        var (itemSelector, target, _, _) = _selectors[type];
        return (article1, article2, source ? itemSelector : target);
    }

    private void DrawEquipmentSwap(SwapType type)
    {
        using var tab = DrawTab(type);
        if (!tab)
            return;

        var (sourceSelector, targetSelector, text1, text2) = _selectors[type];
        using var table = ImRaii.Table("##settings", 2, ImGuiTableFlags.SizingFixedFit);
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text1);
        ImGui.TableNextColumn();
        _dirty |= sourceSelector.Draw("##itemSource", sourceSelector.CurrentSelection.Name, string.Empty, InputWidth * 2 * UiHelpers.Scale,
            ImGui.GetTextLineHeightWithSpacing());

        if (type == SwapType.手指装备)
        {
            ImGui.SameLine();
            _dirty |= ImGui.Checkbox( "转换右指", ref _useRightRing );
        }

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text2);
        ImGui.TableNextColumn();
        _dirty |= targetSelector.Draw("##itemTarget", targetSelector.CurrentSelection.Name, string.Empty, InputWidth * 2 * UiHelpers.Scale,
            ImGui.GetTextLineHeightWithSpacing());
        if (type == SwapType.手指装备)
        {
            ImGui.SameLine();
            _dirty |= ImGui.Checkbox( "转换左指", ref _useLeftRing );
        }

        if (_affectedItems is not { Length: > 1 })
            return;

        ImGui.SameLine();
        ImGuiUtil.DrawTextButton($"同时会在另外{_affectedItems.Length - 1}个同模道具上生效。", Vector2.Zero,
            Colors.PressEnterWarningBg);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Join('\n', _affectedItems.Where(i => !ReferenceEquals(i.Name, targetSelector.CurrentSelection.Name))
                .Select(i => i.Name)));
    }

    private void DrawHairSwap()
    {
        using var tab = DrawTab(SwapType.发型);
        if (!tab)
            return;

        using var table = ImRaii.Table("##settings", 2, ImGuiTableFlags.SizingFixedFit);
        DrawTargetIdInput( "将这个发型" );
        DrawSourceIdInput();
        DrawGenderInput();
    }

    private void DrawFaceSwap()
    {
        using var disabled = ImRaii.Disabled();
        using var tab      = DrawTab(SwapType.Face);
        if (!tab)
            return;

        using var table = ImRaii.Table("##settings", 2, ImGuiTableFlags.SizingFixedFit);
        DrawTargetIdInput( "将这个脸型" );
        DrawSourceIdInput();
        DrawGenderInput();
    }

    private void DrawTailSwap()
    {
        using var tab = DrawTab(SwapType.尾巴);
        if (!tab)
            return;

        using var table = ImRaii.Table("##settings", 2, ImGuiTableFlags.SizingFixedFit);
        DrawTargetIdInput( "将这个尾巴形状" );
        DrawSourceIdInput();
        DrawGenderInput( "给所有的", 2 );
    }


    private void DrawEarSwap()
    {
        using var tab = DrawTab(SwapType.耳朵);
        if (!tab)
            return;

        using var table = ImRaii.Table("##settings", 2, ImGuiTableFlags.SizingFixedFit);
        DrawTargetIdInput("将这个耳朵类型");
        DrawSourceIdInput();
        DrawGenderInput("给所有维埃拉族", 0);
    }

    private const float InputWidth = 120;

    private void DrawTargetIdInput(string text = "Take this ID")
    {
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(InputWidth * UiHelpers.Scale);
        if (ImGui.InputInt("##targetId", ref _targetId, 0, 0))
            _targetId = Math.Clamp(_targetId, 0, byte.MaxValue);

        _dirty |= ImGui.IsItemDeactivatedAfterEdit();
    }

    private void DrawSourceIdInput( string text = "转换给" )
    {
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(InputWidth * UiHelpers.Scale);
        if (ImGui.InputInt("##sourceId", ref _sourceId, 0, 0))
            _sourceId = Math.Clamp(_sourceId, 0, byte.MaxValue);

        _dirty |= ImGui.IsItemDeactivatedAfterEdit();
    }

    private void DrawGenderInput( string text = "给所有的", int drawRace = 1 )
    {
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text);

        ImGui.TableNextColumn();
        _dirty |= Combos.Gender("##Gender", InputWidth, _currentGender, out _currentGender);
        if (drawRace == 1)
        {
            ImGui.SameLine();
            _dirty |= Combos.Race("##Race", InputWidth, _currentRace, out _currentRace);
        }
        else if (drawRace == 2)
        {
            ImGui.SameLine();
            if (_currentRace is not ModelRace.Miqote and not ModelRace.AuRa and not ModelRace.Hrothgar)
                _currentRace = ModelRace.Miqote;

            _dirty |= ImGuiUtil.GenericEnumCombo("##Race", InputWidth, _currentRace, out _currentRace, new[]
                {
                    ModelRace.Miqote,
                    ModelRace.AuRa,
                    ModelRace.Hrothgar,
                },
                RaceEnumExtensions.ToName);
        }
    }

    private string NonExistentText()
        => _lastTab switch
        {
            SwapType.头部装备      => "选中的头部装备似乎不存在。",
            SwapType.身体装备      => "选中的身体装备似乎不存在。",
            SwapType.手部装备   => "选中的手部装备似乎不存在。",
            SwapType.腿部装备    => "选中的腿部装备似乎不存在。",
            SwapType.脚部装备    => "选中的脚部装备似乎不存在。",
            SwapType.耳部装备 => "选中的耳环似乎不存在。",
            SwapType.颈部装备 => "选中的项链似乎不存在。",
            SwapType.腕部装备 => "选中的手镯似乎不存在。",
            SwapType.手指装备     => "选中的戒指似乎不存在。",
            SwapType.发型     => "选中的发型似乎不存在。",
            SwapType.Face     => "选中的脸部似乎不存在。",
            SwapType.耳朵     => "选中的耳朵类型似乎不存在。",
            SwapType.尾巴     => "选中的尾巴似乎不存在。",
            SwapType.Weapon   => "选中的武器似乎不存在。",
            _                 => string.Empty,
        };

    private static void DrawSwap(Swap swap)
    {
        var       flags = swap.ChildSwaps.Count == 0 ? ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf : ImGuiTreeNodeFlags.DefaultOpen;
        using var tree  = ImRaii.TreeNode(SwapToString(swap), flags);
        if (!tree)
            return;

        foreach (var child in swap.ChildSwaps)
            DrawSwap(child);
    }

    private void OnCollectionChange(CollectionType collectionType, ModCollection? oldCollection,
        ModCollection? newCollection, string _)
    {
        if (collectionType is not CollectionType.Current || _mod == null || newCollection == null)
            return;

        UpdateMod(_mod, _mod.Index < newCollection.Settings.Count ? newCollection[_mod.Index].Settings : null);
    }

    private void OnSettingChange(ModCollection collection, ModSettingChange type, Mod? mod, int oldValue, int groupIdx, bool inherited)
    {
        if (collection != _collectionManager.Active.Current || mod != _mod)
            return;

        _swapData.LoadMod(_mod, _modSettings);
        _dirty = true;
    }

    private void OnInheritanceChange(ModCollection collection, bool _)
    {
        if (collection != _collectionManager.Active.Current || _mod == null)
            return;

        UpdateMod(_mod, collection[_mod.Index].Settings);
        _swapData.LoadMod(_mod, _modSettings);
        _dirty = true;
    }

    private void OnModOptionChange(ModOptionChangeType type, Mod mod, int a, int b, int c)
    {
        if (type is ModOptionChangeType.PrepareChange or ModOptionChangeType.GroupAdded or ModOptionChangeType.OptionAdded || mod != _mod)
            return;

        _swapData.LoadMod(_mod, _modSettings);
        UpdateOption();
        _dirty = true;
    }
}
