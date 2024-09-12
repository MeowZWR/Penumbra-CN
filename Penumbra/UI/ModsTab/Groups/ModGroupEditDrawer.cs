using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using OtterGui.Text.EndObjects;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Groups;

public sealed class ModGroupEditDrawer(
    ModManager modManager,
    Configuration config,
    FilenameService filenames,
    DescriptionEditPopup descriptionPopup,
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
    private bool    _deleteEnabled;

    private string?      _currentGroupName;
    private ModPriority? _currentGroupPriority;
    private IModGroup?   _currentGroupEdited;
    private bool         _isGroupNameValid = true;

    private IModGroup?  _dragDropGroup;
    private IModOption? _dragDropOption;
    private bool        _draggingAcross;

    public void Draw(Mod mod)
    {
        PrepareStyle();

        using var id = ImUtf8.PushId("##GroupEdit"u8);
        foreach (var (group, groupIdx) in mod.Groups.WithIndex())
            DrawGroup(group, groupIdx);

        while (ActionQueue.TryDequeue(out var action))
            action.Invoke();
    }

    private void DrawGroup(IModGroup group, int idx)
    {
        using var id    = ImUtf8.PushId(idx);
        using var frame = ImRaii.FramedGroup($"组 #{idx + 1}");
        DrawGroupNameRow(group, idx);
        group.EditDrawer(this).Draw();
    }

    private void DrawGroupNameRow(IModGroup group, int idx)
    {
        DrawGroupName(group);
        ImUtf8.SameLineInner();
        DrawGroupMoveButtons(group, idx);
        ImUtf8.SameLineInner();
        DrawGroupOpenFile(group, idx);
        ImUtf8.SameLineInner();
        DrawGroupDescription(group);
        ImUtf8.SameLineInner();
        DrawGroupDelete(group);
        ImUtf8.SameLineInner();
        DrawGroupPriority(group);
    }

    private void DrawGroupName(IModGroup group)
    {
        var text = _currentGroupEdited == group ? _currentGroupName ?? group.Name : group.Name;
        ImGui.SetNextItemWidth(_groupNameWidth);
        using var border = ImRaii.PushFrameBorder(UiHelpers.ScaleX2, Colors.RegexWarningBorder, !_isGroupNameValid);
        if (ImUtf8.InputText("##GroupName"u8, ref text))
        {
            _currentGroupEdited = group;
            _currentGroupName   = text;
            _isGroupNameValid   = text == group.Name || ModGroupEditor.VerifyFileName(group.Mod, group, text, false);
        }

        if (ImGui.IsItemDeactivated())
        {
            if (_currentGroupName != null && _isGroupNameValid)
                ModManager.OptionEditor.RenameModGroup(group, _currentGroupName);
            _currentGroupName   = null;
            _currentGroupEdited = null;
            _isGroupNameValid   = true;
        }

        var tt = _isGroupNameValid
            ? "修改组名称"u8
            : "当前名称不能用于此组。"u8;
        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, tt);
    }

    private void DrawGroupDelete(IModGroup group)
    {
        if (ImUtf8.IconButton(FontAwesomeIcon.Trash, !_deleteEnabled))
            ActionQueue.Enqueue(() => ModManager.OptionEditor.DeleteModGroup(group));

        if (_deleteEnabled)
            ImUtf8.HoverTooltip("删除此选项组。"u8);
        else
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled,
                $"删除此选项组。\n按住{config.DeleteModModifier}并点击。");
    }

    private void DrawGroupPriority(IModGroup group)
    {
        var priority = _currentGroupEdited == group
            ? (_currentGroupPriority ?? group.Priority).Value
            : group.Priority.Value;
        ImGui.SetNextItemWidth(PriorityWidth);
        if (ImGui.InputInt("##GroupPriority", ref priority, 0, 0))
        {
            _currentGroupEdited   = group;
            _currentGroupPriority = new ModPriority(priority);
        }

        if (ImGui.IsItemDeactivated())
        {
            if (_currentGroupPriority.HasValue)
                ModManager.OptionEditor.ChangeGroupPriority(group, _currentGroupPriority.Value);
            _currentGroupEdited   = null;
            _currentGroupPriority = null;
        }

        ImGuiUtil.HoverTooltip("组优先级");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawGroupDescription(IModGroup group)
    {
        if (ImUtf8.IconButton(FontAwesomeIcon.Edit, "编辑选项组描述"u8))
            descriptionPopup.Open(group);
    }

    private void DrawGroupMoveButtons(IModGroup group, int idx)
    {
        var isFirst = idx == 0;
        if (ImUtf8.IconButton(FontAwesomeIcon.ArrowUp, isFirst))
            ActionQueue.Enqueue(() => ModManager.OptionEditor.MoveModGroup(group, idx - 1));

        if (isFirst)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, "到顶了。"u8);
        else
            ImUtf8.HoverTooltip($"移动此组到组 #{idx}之上。");


        ImUtf8.SameLineInner();
        var isLast = idx == group.Mod.Groups.Count - 1;
        if (ImUtf8.IconButton(FontAwesomeIcon.ArrowDown, isLast))
            ActionQueue.Enqueue(() => ModManager.OptionEditor.MoveModGroup(group, idx + 1));

        if (isLast)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, "到底了。"u8);
        else
            ImUtf8.HoverTooltip($"移动此组到组 #{idx + 2}之下。");
    }

    private void DrawGroupOpenFile(IModGroup group, int idx)
    {
        var fileName   = filenames.OptionGroupFile(group.Mod, idx, config.ReplaceNonAsciiOnImport);
        var fileExists = File.Exists(fileName);
        if (ImUtf8.IconButton(FontAwesomeIcon.FileExport, !fileExists))
            try
            {
                Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
            }
            catch (Exception e)
            {
                Penumbra.Messager.NotificationMessage(e, "无法打开编辑器。", NotificationType.Error);
            }

        if (fileExists)
            ImUtf8.HoverTooltip($"在您选择的文本编辑器中打开 {group.Name} 的 JSON 文件。");
        else
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"{group.Name} 的 JSON 文件不存在。");
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionPosition(IModGroup group, IModOption option, int optionIdx)
    {
        ImGui.AlignTextToFramePadding();
        ImUtf8.Selectable($"选项 #{optionIdx + 1}", false, size: OptionIdxSelectable);
        Target(group, optionIdx);
        Source(option);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionDefaultSingleBehaviour(IModGroup group, IModOption option, int optionIdx)
    {
        var isDefaultOption = group.DefaultSettings.AsIndex == optionIdx;
        if (ImUtf8.RadioButton("##default"u8, isDefaultOption))
            ModManager.OptionEditor.ChangeModGroupDefaultOption(group, Setting.Single(optionIdx));
        ImUtf8.HoverTooltip($"将 {option.Name} 设置为此组的默认选项。");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionDefaultMultiBehaviour(IModGroup group, IModOption option, int optionIdx)
    {
        var isDefaultOption = group.DefaultSettings.HasFlag(optionIdx);
        if (ImUtf8.Checkbox("##default"u8, ref isDefaultOption))
            ModManager.OptionEditor.ChangeModGroupDefaultOption(group, group.DefaultSettings.SetBit(optionIdx, isDefaultOption));
        ImUtf8.HoverTooltip($"{(isDefaultOption ? "在此组中默认禁用"u8 : "在此组中默认启用"u8)} {option.Name}。");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionDescription(IModOption option)
    {
        if (ImUtf8.IconButton(FontAwesomeIcon.Edit, "编辑选项描述。"u8))
            descriptionPopup.Open(option);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionPriority(MultiSubMod option)
    {
        var priority = option.Priority.Value;
        ImGui.SetNextItemWidth(PriorityWidth);
        if (ImUtf8.InputScalarOnDeactivated("##Priority"u8, ref priority))
            ModManager.OptionEditor.MultiEditor.ChangeOptionPriority(option, new ModPriority(priority));
        ImUtf8.HoverTooltip("选项在模组内的优先级。"u8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionName(IModOption option)
    {
        var name = option.Name;
        ImGui.SetNextItemWidth(_optionNameWidth);
        if (ImUtf8.InputTextOnDeactivated("##Name"u8, ref name))
            ModManager.OptionEditor.RenameOption(option, name);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionDelete(IModOption option)
    {
        if (ImUtf8.IconButton(FontAwesomeIcon.Trash, !_deleteEnabled))
            ActionQueue.Enqueue(() => ModManager.OptionEditor.DeleteOption(option));

        if (_deleteEnabled)
            ImUtf8.HoverTooltip("删除此选项"u8);
        else
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled,
                $"删除此选项\n按住{config.DeleteModModifier}并点击。");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string DrawNewOptionBase(IModGroup group, int count)
    {
        ImUtf8.Selectable($"选项 #{count + 1}", false, size: OptionIdxSelectable);
        Target(group, count);

        ImUtf8.SameLineInner();
        ImUtf8.IconDummy();

        ImUtf8.SameLineInner();
        ImGui.SetNextItemWidth(_optionNameWidth);
        var newName = _newOptionGroup == group
            ? NewOptionName ?? string.Empty
            : string.Empty;
        if (ImUtf8.InputText("##newOption"u8, ref newName, "添加新选项..."u8))
        {
            NewOptionName   = newName;
            _newOptionGroup = group;
        }

        ImUtf8.SameLineInner();
        return newName;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Source(IModOption option)
    {
        using var source = ImUtf8.DragDropSource();
        if (!source)
            return;

        var across = option.Group is ITexToolsGroup;

        if (!DragDropSource.SetPayload(across ? AcrossGroupsLabel : InsideGroupLabel))
        {
            _dragDropGroup  = option.Group;
            _dragDropOption = option;
            _draggingAcross = across;
        }

        ImUtf8.Text($"从组 {option.Group.Name} 中拖拽选项 {option.Name}...");
    }

    private void Target(IModGroup group, int optionIdx)
    {
        if (_dragDropGroup != group
         && (!_draggingAcross || (_dragDropGroup != null && group is MultiModGroup { Options.Count: >= IModGroup.MaxMultiOptions })))
            return;

        using var target = ImUtf8.DragDropTarget();
        if (!target.IsDropping(_draggingAcross ? AcrossGroupsLabel : InsideGroupLabel))
            return;

        if (_dragDropGroup != null && _dragDropOption != null)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PrepareStyle()
    {
        var totalWidth = 400f * ImUtf8.GlobalScale;
        _buttonSize         = new Vector2(ImUtf8.FrameHeight);
        PriorityWidth       = 50 * ImUtf8.GlobalScale;
        AvailableWidth      = new Vector2(totalWidth + 3 * _spacing + 2 * _buttonSize.X + PriorityWidth, 0);
        _groupNameWidth     = totalWidth - 3 * (_buttonSize.X + _spacing);
        _spacing            = ImGui.GetStyle().ItemInnerSpacing.X;
        OptionIdxSelectable = ImUtf8.CalcTextSize("选项 #88."u8);
        _optionNameWidth    = totalWidth - OptionIdxSelectable.X - _buttonSize.X - 2 * _spacing;
        _deleteEnabled      = config.DeleteModModifier.IsActive();
    }
}
