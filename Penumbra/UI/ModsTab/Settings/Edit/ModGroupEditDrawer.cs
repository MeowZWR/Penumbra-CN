using Dalamud.Interface;
using ImSharp;
using Luna;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Settings;

public interface IModGroupEditDrawer
{
    public void Draw();
}

public sealed class ModGroupEditDrawer(
    ModManager modManager,
    DescriptionEditPopup descriptionPopup,
    LayoutEditPopup layoutPopup,
    ConditionEditPopup conditionPopup,
    ImcChecker imcChecker) : IUiService
{
    private static ReadOnlySpan<byte> AcrossGroupsLabel
        => "##DragOptionAcross"u8;

    private static ReadOnlySpan<byte> InsideGroupLabel
        => "##DragOptionInside"u8;

    internal readonly ImcChecker    ImcChecker  = imcChecker;
    internal readonly ModManager    ModManager  = modManager;
    internal readonly Queue<Action> ActionQueue = new();

    internal Vector2 OptionIdxSelectable;
    internal Vector2 AvailableWidth;
    internal float   PriorityWidth;

    internal string?    NewOptionName;
    private  IModGroup? _newOptionGroup;

    private Vector2 _buttonSize;
    private float   _groupNameWidth;
    private float   _optionNameWidth;
    private float   _spacing;

    private string?    _currentGroupName;
    private IModGroup? _currentGroupEdited;
    private bool       _isGroupNameValid = true;

    private IModGroup?  _dragDropGroup;
    private IModOption? _dragDropOption;
    private bool        _draggingAcross;

    private IModObject?                    _dragDropCondition;
    private ICondition<ModSettingContext>? _copiedCondition;

    private ModSettingsLayout? _copiedLayout;
    private IModObject?        _copiedParent;
    private int?               _copiedColor;
    private bool               _didCopyParent;

    public void Draw(GroupNameCache cache, Mod mod)
    {
        PrepareStyle();

        using var id = Im.Id.Push("ge"u8);
        foreach (var (groupIdx, group) in mod.Groups.Index())
            DrawGroup(cache, group, groupIdx);

        while (ActionQueue.TryDequeue(out var action))
            action.Invoke();
    }

    private void DrawGroup(GroupNameCache cache, IModGroup group, int idx)
    {
        using var id    = Im.Id.Push(idx);
        using var frame = ImEx.FramedGroup($"组 #{idx + 1}");
        DrawGroupNameRow(cache, group, idx);
        group.EditDrawer(this).Draw();
    }

    private void DrawGroupNameRow(GroupNameCache cache, IModGroup group, int idx)
    {
        DrawGroupName(group);
        Im.Line.SameInner();
        DrawGroupMoveButtons(group, idx);
        Im.Line.SameInner();
        DrawGroupDescription(group);
        Im.Line.SameInner();
        DrawGroupLayout(group);
        Im.Line.SameInner();
        DrawGroupConditions(group);
        Im.Line.SameInner();
        DrawGroupDelete(group);
        Im.Line.SameInner();
        DrawGroupPriority(group);
        Im.Line.SameInner();
        DrawGroupPage(cache, group);
    }

    private void DrawGroupName(IModGroup group)
    {
        var text = _currentGroupEdited == group ? _currentGroupName ?? group.Name : group.Name;
        Im.Item.SetNextWidth(_groupNameWidth);
        using var border = ImStyleBorder.Frame.Push(Colors.RegexWarningBorder, Im.Style.GlobalScale * 2, !_isGroupNameValid);
        if (Im.Input.Text("##GroupName"u8, ref text))
        {
            _currentGroupEdited = group;
            _currentGroupName   = text;
            _isGroupNameValid   = text == group.Name || ModGroupEditor.VerifyFileName(group.Mod, group, text, false);
        }

        if (Im.Item.Deactivated)
        {
            if (_currentGroupName != null && _isGroupNameValid)
                ModManager.OptionEditor.RenameModGroup(group, _currentGroupName);
            _currentGroupName   = null;
            _currentGroupEdited = null;
            _isGroupNameValid   = true;
        }

        var tt = _isGroupNameValid
            ? "更改组名称。"u8
            : "当前名称不能用于此组。"u8;
        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, tt);
    }

    private void DrawGroupDelete(IModGroup group)
    {
        if (ImEx.Icon.Button(LunaStyle.DeleteIcon, !LunaStyle.Modifier.Destructive))
            ActionQueue.Enqueue(() => ModManager.OptionEditor.DeleteModGroup(group));

        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "删除此选项组。"u8);
        if (!LunaStyle.Modifier.Destructive)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {LunaStyle.Modifier.Destructive} 点击删除。");
    }

    private void DrawGroupPriority(IModGroup group)
    {
        Im.Item.SetNextWidth(PriorityWidth);
        if (ImEx.InputOnDeactivation.Scalar("##GroupPriority"u8, group.Priority.Value, out var newPriority))
            ModManager.OptionEditor.ChangeGroupPriority(group, new ModPriority(newPriority));
        Im.Tooltip.OnHover("组优先级"u8);
    }

    private void DrawGroupPage(GroupNameCache cache, IModGroup group)
    {
        Im.Item.SetNextWidth(PriorityWidth);
        if (ImEx.InputOnDeactivation.Scalar("##GroupPage"u8, group.Page + 1, out var newPage))
            ModManager.OptionEditor.SetPage(group, newPage - 1);
        Im.Tooltip.OnHover(
            "此组所在的页面。若该组有父组，则忽略此设置。\n\n注意此处显示的数字比 JSON 文件中存储的数字大 1。"u8);
        Im.Line.SameInner();
        ImEx.TextFrameAligned(cache.ShowPages ? cache.Pages[group.Page].Name.Utf8 : "(未使用)"u8);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawGroupDescription(IModGroup group)
    {
        if (ImEx.Icon.Button(LunaStyle.EditIcon, "编辑组描述。"u8,
                textColor: group.Description.Length > 0 ? LunaStyle.FavoriteColor : ColorParameter.Default))
            descriptionPopup.Open(group);
        DrawDescriptionInteraction(group);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawGroupLayout(IModGroup group)
    {
        if (ImEx.Icon.Button(LunaStyle.LayoutIcon, "编辑组布局设置。"u8,
                textColor: group.Layout is not 0 || group.ParentSetting is not null ? LunaStyle.FavoriteColor : ColorParameter.Default))
            layoutPopup.Open(group);
        DrawLayoutInteraction(group);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawGroupConditions(IModGroup group)
    {
        if (ImEx.Icon.Button(LunaStyle.ConditionIcon, "编辑组条件。"u8,
                textColor: group.Condition is not null ? LunaStyle.FavoriteColor : ColorParameter.Default))
            conditionPopup.Open(group);

        DrawConditionInteraction(group);
    }

    private void DrawGroupMoveButtons(IModGroup group, int idx)
    {
        var isFirst = idx is 0;
        if (ImEx.Icon.Button(FontAwesomeIcon.ArrowUp.Icon(), isFirst))
            ActionQueue.Enqueue(() => ModManager.OptionEditor.MoveModGroup(group, idx - 1));

        if (isFirst)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "到顶了。"u8);
        else
            Im.Tooltip.OnHover($"将此组上移到组 {idx}。");


        Im.Line.SameInner();
        var isLast = idx == group.Mod.Groups.Count - 1;
        if (ImEx.Icon.Button(FontAwesomeIcon.ArrowDown.Icon(), isLast))
            ActionQueue.Enqueue(() => ModManager.OptionEditor.MoveModGroup(group, idx + 1));

        if (isLast)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "到底了。"u8);
        else
            Im.Tooltip.OnHover($"将此组下移到组 {idx + 2}。");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionPosition(IModGroup group, IModOption option, int optionIdx)
    {
        Im.Cursor.FrameAlign();
        Im.Selectable($"选项 #{optionIdx + 1}", size: OptionIdxSelectable);
        Target(group, optionIdx);
        Source(option);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionDefaultSingleBehaviour(IModGroup group, IModOption option, int optionIdx)
    {
        var isDefaultOption = group.DefaultSettings.AsIndex == optionIdx;
        if (Im.RadioButton("##default"u8, isDefaultOption))
            ModManager.OptionEditor.ChangeModGroupDefaultOption(group, Setting.Single(optionIdx));
        Im.Tooltip.OnHover($"将 {option.Name} 设为此组的默认选项。");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionDefaultMultiBehaviour(IModGroup group, IModOption option, int optionIdx)
    {
        var isDefaultOption = group.DefaultSettings.HasFlag(optionIdx);
        if (Im.Checkbox("##default"u8, ref isDefaultOption))
            ModManager.OptionEditor.ChangeModGroupDefaultOption(group, group.DefaultSettings.SetBit(optionIdx, isDefaultOption));
        Im.Tooltip.OnHover($"{(isDefaultOption ? "禁用"u8 : "启用"u8)} {option.Name} 作为此组的默认状态。");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionButtons(IModOption option)
    {
        DrawOptionDescription(option);
        Im.Line.SameInner();
        DrawOptionLayout(option);
        Im.Line.SameInner();
        DrawOptionConditions(option);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawOptionDescription(IModOption option)
    {
        if (ImEx.Icon.Button(LunaStyle.EditIcon, "编辑选项描述。"u8,
                textColor: option.Description.Length > 0 ? LunaStyle.FavoriteColor : ColorParameter.Default))
            descriptionPopup.Open(option);
        DrawDescriptionInteraction(option);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawOptionLayout(IModOption option)
    {
        if (ImEx.Icon.Button(LunaStyle.LayoutIcon, "编辑选项布局设置。"u8,
                textColor: option.Layout is not 0 || option.ColorAsInteger is not 0 ? LunaStyle.FavoriteColor : ColorParameter.Default))
            layoutPopup.Open(option);
        DrawLayoutInteraction(option);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawOptionConditions(IModOption option)
    {
        if (ImEx.Icon.Button(LunaStyle.ConditionIcon, "编辑选项条件。"u8,
                textColor: option.Condition is not null ? LunaStyle.FavoriteColor : ColorParameter.Default))
            conditionPopup.Open(option);
        DrawConditionInteraction(option);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionPriority(MultiSubMod option)
    {
        Im.Item.SetNextWidth(PriorityWidth);
        if (ImEx.InputOnDeactivation.Scalar("##Priority"u8, option.Priority.Value, out var newValue))
            ModManager.OptionEditor.MultiEditor.ChangeOptionPriority(option, new ModPriority(newValue));
        Im.Tooltip.OnHover("选项优先级。"u8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionName(IModOption option)
    {
        Im.Item.SetNextWidth(_optionNameWidth);
        if (ImEx.InputOnDeactivation.Text("##Name"u8, option.Name, out string newName))
            ModManager.OptionEditor.RenameOption(option, newName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionDelete(IModOption option)
    {
        if (ImEx.Icon.Button(LunaStyle.DeleteIcon, !LunaStyle.Modifier.Destructive))
            ActionQueue.Enqueue(() => ModManager.OptionEditor.DeleteOption(option));

        if (LunaStyle.Modifier.Destructive)
            Im.Tooltip.OnHover("删除此选项。"u8);
        else
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled,
                $"删除此选项。\n按住 {LunaStyle.Modifier.Destructive} 点击删除。");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string DrawNewOptionBase(IModGroup group, int count)
    {
        Im.Cursor.FrameAlign();
        Im.Selectable($"选项 #{count + 1}", size: OptionIdxSelectable);
        Target(group, count);

        Im.Line.SameInner();
        Im.FrameDummy();

        Im.Line.SameInner();
        Im.Item.SetNextWidth(_optionNameWidth);
        var newName = _newOptionGroup == group
            ? NewOptionName ?? string.Empty
            : string.Empty;
        if (Im.Input.Text("##newOption"u8, ref newName, "添加新选项..."u8))
        {
            NewOptionName   = newName;
            _newOptionGroup = group;
        }

        Im.Line.SameInner();
        return newName;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Source(IModOption option)
    {
        using var source = Im.DragDrop.Source();
        if (!source)
            return;

        var across = option.Group is ITexToolsGroup;

        if (!source.SetPayload(across ? AcrossGroupsLabel : InsideGroupLabel))
        {
            _dragDropGroup  = option.Group;
            _dragDropOption = option;
            _draggingAcross = across;
        }

        Im.Text($"正在拖动组 {option.Group.Name} 的选项 {option.Name}...");
    }

    private void Target(IModGroup group, int optionIdx)
    {
        if (_dragDropGroup != group
         && (!_draggingAcross || _dragDropGroup is not null && group is MultiModGroup { Options.Count: >= IModGroup.MaxMultiOptions }))
            return;

        using var target = Im.DragDrop.Target();
        if (!target.IsDropping(_draggingAcross ? AcrossGroupsLabel : InsideGroupLabel))
            return;

        if (_dragDropGroup is not null && _dragDropOption is not null)
        {
            if (_dragDropGroup == group)
            {
                var sourceOption = _dragDropOption;
                ActionQueue.Enqueue(() => ModManager.OptionEditor.MoveOption(sourceOption, optionIdx));
            }
            else
            {
                // Move from one group to another by deleting, then adding, then moving the option.
                var sourceOption = _dragDropOption;
                ActionQueue.Enqueue(() =>
                {
                    ModManager.OptionEditor.DeleteOption(sourceOption);
                    if (ModManager.OptionEditor.AddOption(group, sourceOption) is { } newOption)
                        ModManager.OptionEditor.MoveOption(newOption, optionIdx);
                });
            }
        }

        _dragDropGroup  = null;
        _dragDropOption = null;
        _draggingAcross = false;
    }

    private void DrawDescriptionInteraction(IModObject @object)
    {
        using var menu = Im.Popup.BeginContextItem();
        if (!menu)
            return;

        using (Im.Disabled(@object.Description.Length is 0))
        {
            if (!Im.Menu.Item("清除"u8))
                return;

            if (@object is IModGroup g)
                ModManager.OptionEditor.ChangeGroupDescription(g, string.Empty);
            else
                ModManager.OptionEditor.ChangeOptionDescription((IModOption)@object, string.Empty);
        }
    }

    private void DrawLayoutInteraction(IModObject @object)
    {
        using var menu = Im.Popup.BeginContextItem();
        if (!menu)
            return;

        using (Im.Disabled(@object.Layout is 0))
        {
            if (Im.Menu.Item("复制"u8))
            {
                _copiedLayout  = @object.Layout;
                _didCopyParent = @object is IModGroup;
                _copiedParent  = @object is IModGroup g ? g.ParentSetting : null;
                _copiedColor   = @object is IModOption o ? o.ColorAsInteger : null;
            }
        }

        using (Im.Disabled(_copiedLayout is null))
        {
            if (Im.Menu.Item("粘贴"u8))
            {
                ModManager.OptionEditor.SetLayout(@object, _copiedLayout!.Value);
                if (_didCopyParent && @object is IModGroup g && CycleChecker.Check(g, _copiedParent))
                    ModManager.OptionEditor.SetParent(g, _copiedParent);
                if (_copiedColor.HasValue && @object is IModOption o)
                    ModManager.OptionEditor.SetColor(o, _copiedColor.Value);
            }
        }

        using (Im.Disabled(@object.Layout is 0))
        {
            if (Im.Menu.Item("清除"u8))
            {
                ModManager.OptionEditor.SetLayout(@object, 0);
                if (@object is IModGroup g)
                    ModManager.OptionEditor.SetParent(g, null);
                if (@object is IModOption o)
                    ModManager.OptionEditor.SetColor(o, 0);
            }
        }
    }

    private void DrawConditionInteraction(IModObject @object)
    {
        using (var menu = Im.Popup.BeginContextItem())
        {
            if (menu)
            {
                using (Im.Disabled(@object.Condition is null))
                {
                    if (Im.Menu.Item("复制"u8))
                        _copiedCondition = @object.Condition!.DeepCopy();
                }

                using (Im.Disabled(_copiedCondition is null))
                {
                    if (Im.Menu.Item("粘贴"u8))
                        ModManager.OptionEditor.SetCondition(@object, _copiedCondition!.DeepCopy(), false);
                }

                using (Im.Disabled(@object.Condition is null))
                {
                    if (Im.Menu.Item("清除"u8))
                        ModManager.OptionEditor.SetCondition(@object, null, false);
                }
            }
        }

        using (var drag = Im.DragDrop.Source())
        {
            if (drag)
            {
                drag.SetPayload("Condition"u8);
                _dragDropCondition = @object;
                Im.Text($"正在拖动 {@object.Name} 的条件...");
            }
        }

        if (_dragDropCondition is not null)
        {
            using var drag = Im.DragDrop.Target();
            if (drag.IsDropping("Condition"u8))
                ModManager.OptionEditor.SetCondition(@object, _dragDropCondition.Condition?.DeepCopy(), false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PrepareStyle()
    {
        var totalWidth = 400f * Im.Style.GlobalScale;
        _buttonSize         = new Vector2(Im.Style.FrameHeight);
        PriorityWidth       = 50 * Im.Style.GlobalScale;
        AvailableWidth      = new Vector2(totalWidth + 5 * _spacing + 4 * _buttonSize.X + PriorityWidth, 0);
        _groupNameWidth     = totalWidth - 5 * (_buttonSize.X + _spacing);
        _spacing            = Im.Style.ItemInnerSpacing.X;
        OptionIdxSelectable = Im.Font.CalculateSize("选项 #88。"u8);
        _optionNameWidth    = totalWidth - OptionIdxSelectable.X - 4 * _buttonSize.X - 5 * _spacing;
    }
}
