using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.UI.ModsTab.Settings;

namespace Penumbra.UI.ModsTab.Optimized;

public sealed class OptimizedModGroupDrawer(
    Configuration config,
    CollectionManager collectionManager,
    CommunicatorService communicator)
    : IUiService
{
    private ModSettingContext     _context;
    private TemporaryModSettings? _tempSettings;
    private bool                  _temporary;
    private bool                  _locked;
    private ModSettingsCache      _cache = null!;
    private PageLayout            _pageLayout;
    private bool                  _setMultiState;
    private IModGroup?            _setStateGroup;

    private readonly record struct PageLayout(float ComboWidth, float CardWidth, float InCardComboWidth, float Indent, float CardPad);

    private readonly record struct Nesting(float Offset, bool InsideCard);

    public void Draw(ModSettingsCache cache, Mod mod, ModSettings settings, TemporaryModSettings? tempSettings)
    {
        if (cache.VisiblePages.Count is 0)
        {
            communicator.PostSettingsPanelDraw.Invoke(new PostSettingsPanelDraw.Arguments(mod));
            return;
        }

        _context      = new ModSettingContext(mod, tempSettings ?? settings);
        _tempSettings = tempSettings;
        _temporary    = tempSettings is not null;
        _locked       = (tempSettings?.Lock ?? 0) > 0;
        _cache        = cache;

        using var id = Im.Id.Push("OptimizedOptions"u8);
        if (cache.VisiblePages.Count > 1 && config.Ui.DisplayPages)
            DrawTabbedPages(cache);
        else
            DrawContinuousPages(cache);

        communicator.PostSettingsPanelDraw.Invoke(new PostSettingsPanelDraw.Arguments(mod));
    }

    private void DrawTabbedPages(ModSettingsCache cache)
    {
        Im.Dummy(0);
        using var tabBar = Im.TabBar.Begin("##pages"u8, TabBarFlags.FittingPolicyScroll);
        if (!tabBar)
            return;

        foreach (var page in cache.VisiblePages)
        {
            using var id      = Im.Id.Push(page.Id);
            using var tabItem = tabBar.Item(page.Name, TabItemFlags.NoPushId);
            if (!tabItem)
                continue;

            using var child = Im.Child.Begin("##page"u8, false, WindowFlags.NoSavedSettings);
            if (child)
                DrawGroups(page.VisibleGroups);
        }
    }

    private void DrawContinuousPages(ModSettingsCache cache)
    {
        foreach (var page in cache.VisiblePages)
        {
            using var id = Im.Id.Push(page.Id);
            if (cache.VisiblePages.Count is 1)
            {
                DrawGroups(page.VisibleGroups);
                continue;
            }

            Im.Tree.SetNextOpen(true, Condition.FirstUseEver);
            if (Im.Tree.Header(page.Name))
                DrawGroups(page.VisibleGroups);
        }
    }

    private void DrawGroups(IReadOnlyList<ModSettingGroup> groups)
    {
        _pageLayout = CalculatePageLayout(groups);
        UiHelpers.DefaultLineSpace();
        foreach (var group in groups.OrderByDescending(IsCompactCombo))
            DrawGroup(group, default);
        UiHelpers.DefaultLineSpace();
    }

    private PageLayout CalculatePageLayout(IReadOnlyList<ModSettingGroup> roots)
    {
        var indent            = Im.Style.IndentSpacing;
        var cardPad           = Im.Style.FrameHeight * 0.5f;
        var minimumComboWidth = config.Ui.ModSettingMinimumComboWidth * Im.Style.GlobalScale;
        var maximumComboWidth = config.Ui.ModSettingMaximumComboWidth * Im.Style.GlobalScale;
        var comboWidth        = minimumComboWidth;
        var cardWidth         = 0f;
        var inCardComboWidth  = 0f;
        var inCardComboRows   = new List<(float Offset, float LabelExtra, float RightChrome)>();
        var seen              = new HashSet<int>();

        foreach (var group in roots)
            Accumulate(group, 0, false);

        inCardComboWidth = Math.Min(inCardComboWidth, maximumComboWidth);
        foreach (var (offset, labelExtra, rightChrome) in inCardComboRows)
            cardWidth = Math.Max(cardWidth, offset + inCardComboWidth + labelExtra + rightChrome);

        comboWidth = Math.Clamp(comboWidth, minimumComboWidth, maximumComboWidth);
        return new PageLayout(comboWidth, cardWidth, inCardComboWidth, indent, cardPad);

        void Accumulate(ModSettingGroup group, float offset, bool insideCard)
        {
            if (!seen.Add(group.Group.Index))
                return;

            if (IsCompactCombo(group))
            {
                if (insideCard)
                {
                    inCardComboWidth = Math.Max(inCardComboWidth, ComboPreviewWidth(group));
                    inCardComboRows.Add((offset, ComboLabelExtra(group), cardPad));
                }
                else
                {
                    comboWidth = Math.Max(comboWidth, group.ComboWidth + offset);
                }

                var childOffset = offset + indent;
                foreach (var child in NestedGroups(group))
                    Accumulate(child, childOffset, insideCard);
                return;
            }

            var rightChrome = insideCard ? cardPad : 0;
            cardWidth = Math.Max(cardWidth, CalculateCardContentWidth(group) + offset + rightChrome);
            var nestedOffset = offset + cardPad + indent;
            foreach (var option in group.VisibleChildren.OfType<ModSettingOption>())
            {
                foreach (var child in option.VisibleChildren)
                    Accumulate(child, nestedOffset, true);
            }

            var groupChildOffset = offset + indent;
            foreach (var child in NestedGroups(group))
                Accumulate(child, groupChildOffset, false);
        }
    }

    private static float CalculateCardContentWidth(ModSettingGroup group)
        => Math.Max(CalculateCardHeaderWidth(group), CalculateCardOptionWidth(group));

    private static float CalculateCardHeaderWidth(ModSettingGroup group)
        => group.Name.CalculateSize().X
         + Math.Max(ImEx.Icon.CalculateSize(LunaStyle.TreeCollapseIcon).X, ImEx.Icon.CalculateSize(LunaStyle.TreeExpandIcon).X)
         + Im.Style.ItemInnerSpacing.X
         + 2 * Im.Style.FrameHeight
         + 2 * Im.Style.ItemSpacing.X
         + (group.Description.IsEmpty ? 0 : Im.Style.ItemInnerSpacing.X + LunaStyle.HelpMarker.CalculateSize().X);

    private static float CalculateCardOptionWidth(ModSettingGroup group)
        => group.VisibleChildren
            .OfType<ModSettingOption>()
            .Select(o => o.Width + Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X)
            .DefaultIfEmpty(0)
            .Max()
         + 2 * Im.Style.FrameHeight;

    private static bool IsCompactCombo(ModSettingGroup group)
        => group.Behaviour is GroupDrawBehaviour.SingleSelection && group.IsCombo;

    private static IEnumerable<ModSettingGroup> NestedGroups(ModSettingGroup group)
        => group.VisibleChildren.OfType<ModSettingGroup>();

    private static IEnumerable<ModSettingOption> ComboOptions(ModSettingGroup group)
        => group.VisibleChildren.Take(group.NumOptions).OfType<ModSettingOption>();

    private static float ComboPreviewWidth(ModSettingGroup group)
        => ComboOptions(group)
            .Select(o => o.Width)
            .DefaultIfEmpty(0)
            .Max()
         + Im.Style.FrameHeight
         + 2 * Im.Style.FramePadding.X;

    private static float ComboLabelExtra(ModSettingGroup group)
    {
        var extra = Im.Style.ItemInnerSpacing.X + group.Name.CalculateSize().X;
        if (!group.Description.IsEmpty)
            extra += Im.Style.ItemInnerSpacing.X + LunaStyle.HelpMarker.CalculateSize().X;
        return extra;
    }

    private Vector2 DrawGroup(ModSettingGroup group, Nesting nesting)
    {
        using var id = Im.Id.Push(group.Group.Index);
        var attach = IsCompactCombo(group)
            ? DrawCompactSingleGroup(group, nesting)
            : DrawExpandedGroup(group, nesting);

        if (group.Space)
            UiHelpers.DefaultLineSpace();

        return attach;
    }

    private Vector2 DrawCompactSingleGroup(ModSettingGroup group, Nesting nesting)
    {
        var setting = GetModSetting(group.Group);
        var options = ComboOptions(group).ToList();
        if (options.Count is 0)
            return Im.Cursor.ScreenPosition;

        var current    = group.AllOptions[setting.AsIndex];
        var comboWidth = nesting.InsideCard
            ? RemainingWidth(_pageLayout.InCardComboWidth, 0, 0)
            : RemainingWidth(_pageLayout.ComboWidth, nesting.Offset, 0);
        Im.Item.SetNextWidth(comboWidth);
        using (ImGuiColor.Text.Push(current.Color))
        using (Im.Disabled(group.Disabled || _locked))
        using (var combo = Im.Combo.Begin("##value"u8, current.Name))
        {
            if (combo)
            {
                foreach (var option in options)
                {
                    using var optionId = Im.Id.Push(option.Data.Index);
                    using var color    = ImGuiColor.Text.Push(option.Color);
                    using var disabled = Im.Disabled(option.Disabled);
                    if (Im.Selectable(option.Name, option.Data.Index == setting.AsIndex))
                        SetModSetting(group.Group, Setting.Single(option.Data.Index));
                    DrawOptionHelpMarker(option, aligned: false);
                }
            }
        }

        var comboMin = Im.Item.UpperLeftCorner;
        var comboMax = Im.Item.LowerRightCorner;
        if (!current.Description.IsEmpty)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, current.Description);
        ModSettingDrawNode.AddUniqueNameTooltip(current);
        HandleComboMouseWheel(group, options, setting);
        Im.Line.SameInner();
        Im.Text(group.Name);
        var nameHovered = Im.Item.Hovered();
        DrawGroupContextMenu(group, nameHovered);
        if (group.Description.IsEmpty)
        {
            ModSettingDrawNode.AddUniqueNameTooltip(group);
        }
        else
        {
            Im.Line.SameInner();
            LunaStyle.DrawAlignedHelpMarker(group.Description, treatAsHovered: nameHovered);
            ModSettingDrawNode.AddUniqueNameTooltip(group, nameHovered);
        }

        DrawNestedGroups(NestedGroups(group),
            new Nesting(nesting.Offset + _pageLayout.Indent, nesting.InsideCard), comboMax.Y);
        return new Vector2(comboMin.X, (comboMin.Y + comboMax.Y) * 0.5f);
    }

    private Vector2 DrawExpandedGroup(ModSettingGroup group, Nesting nesting)
    {
        var stateId         = Im.Id.Get("Expanded"u8);
        var defaultExpanded = !group.Group.Layout.HasFlag(ModSettingsLayout.DefaultClosed);
        var expanded        = _cache.Storage.GetBool(stateId, defaultExpanded);
        var rightChrome = nesting.InsideCard ? _pageLayout.CardPad : 0;
        var cardWidth   = RemainingWidth(_pageLayout.CardWidth, nesting.Offset, rightChrome);
        var headerMin   = Im.Cursor.ScreenPosition;
        var headerIcon  = expanded ? LunaStyle.TreeCollapseIcon : LunaStyle.TreeExpandIcon;
        using (ImStyleSingle.ChildBorderThickness.Push(_cache.BorderWidth / 2))
        using (var frame = ImEx.FramedGroup($"{group.Name}", headerIcon, LunaStyle.HelpMarker,
                   group.Description, minimumSize: new Vector2(cardWidth, 0)))
        {
            var headerMax = headerMin + new Vector2(Math.Max(cardWidth, frame.MinimumWidth), Im.Style.FrameHeight);
            if (Im.InvisibleButton("##header"u8, new Rectangle(headerMin, headerMax)))
            {
                expanded = !expanded;
                _cache.Storage.SetBool(stateId, expanded);
            }

            var headerHovered = Im.Item.Hovered();
            if (headerHovered && !group.Description.IsEmpty)
                Im.Tooltip.Set(group.Description);
            ModSettingDrawNode.AddUniqueNameTooltip(group, headerHovered);
            DrawGroupContextMenu(group, headerHovered);

            if (expanded)
            {
                var childNesting = new Nesting(nesting.Offset + _pageLayout.CardPad + _pageLayout.Indent, true);
                foreach (var option in group.VisibleChildren.OfType<ModSettingOption>())
                {
                    DrawOption(option);
                    DrawNestedGroups(option.VisibleChildren, childNesting, Im.Item.LowerRightCorner.Y);
                }
            }
        }

        var itemMax    = Im.Item.LowerRightCorner;
        var attach     = CardTreeAttach(headerMin, itemMax);
        if (expanded)
        {
            var groupChildren = NestedGroups(group).ToList();
            if (groupChildren.Count > 0)
            {
                var treeX = headerMin.X + _pageLayout.Indent * 0.5f;
                DrawNestedGroups(groupChildren, new Nesting(nesting.Offset + _pageLayout.Indent, false), itemMax.Y, treeX);
            }
        }

        return attach;
    }

    private static Vector2 CardTreeAttach(Vector2 itemMin, Vector2 itemMax)
    {
        var frameHeight = Im.Style.FrameHeight;
        var frameMin    = itemMin + new Vector2(frameHeight / 8f, frameHeight / 2f);
        return new Vector2(frameMin.X, (frameMin.Y + itemMax.Y) * 0.5f);
    }

    private float RemainingWidth(float columnWidth, float offset, float rightChrome)
        => Math.Max(0, Math.Min(Im.ContentRegion.Available.X, columnWidth - offset - rightChrome));

    private void DrawNestedGroups(IEnumerable<ModSettingGroup> children, Nesting nesting, float parentAnchorY,
        float? treeOriginX = null)
    {
        var list = children as IList<ModSettingGroup> ?? children.ToList();
        if (list.Count is 0)
            return;

        if (Im.Cursor.ScreenPosition.Y + 1f < Im.Item.LowerRightCorner.Y)
            Im.Line.New();

        var parentLeft = Im.Cursor.ScreenPosition.X;
        using var indent = Im.Indent();
        var childLeft    = Im.Cursor.ScreenPosition.X;
        var treeX        = treeOriginX ?? MathF.Round((parentLeft + childLeft) * 0.5f);
        var lineWidth    = _cache.BorderWidth;
        var color        = ImGuiColor.Border.Get();
        var lastY        = parentAnchorY;
        foreach (var child in list)
        {
            var attach = DrawGroup(child, nesting);
            Im.Window.DrawList.Shape.Line(new Vector2(treeX, attach.Y), attach, color, lineWidth);
            lastY = attach.Y;
        }

        if (lastY > parentAnchorY)
            Im.Window.DrawList.Shape.Line(new Vector2(treeX, parentAnchorY), new Vector2(treeX, lastY), color, lineWidth);
    }

    private void DrawOption(ModSettingOption option)
    {
        using var id       = Im.Id.Push(option.Data.Index);
        var       setting  = GetModSetting(option.Data.Group);
        var       selected = option.Data.Group.Behaviour is GroupDrawBehaviour.SingleSelection
            ? setting.AsIndex == option.Data.Index
            : setting.HasFlag(option.Data.Index);

        using (ImGuiColor.Text.Push(option.Color))
        using (Im.Disabled(option.Disabled || _locked))
        {
            var changed = option.Data.Group.Behaviour is GroupDrawBehaviour.SingleSelection
                ? Im.RadioButton(option.HideLabel ? "##option"u8 : option.Name, selected)
                : Im.Checkbox(option.HideLabel ? "##option"u8 : option.Name, ref selected);
            if (changed)
            {
                var newSetting = option.Data.Group.Behaviour is GroupDrawBehaviour.SingleSelection
                    ? Setting.Single(option.Data.Index)
                    : setting.SetBit(option.Data.Index, selected);
                SetModSetting(option.Data.Group, newSetting);
            }
        }

        DrawOptionHelpMarker(option);
        if (option.Separator)
            LunaStyle.DrawSeparator();
        if (option.Space)
            UiHelpers.DefaultLineSpace();
    }

    private static void DrawOptionHelpMarker(ModSettingOption option, bool aligned = true)
    {
        if (!option.Description.IsEmpty)
        {
            var treatAsHovered = Im.Item.Hovered(HoveredFlags.AllowWhenDisabled);
            Im.Line.SameInner();
            if (aligned)
                LunaStyle.DrawAlignedHelpMarker(option.Description, treatAsHovered: treatAsHovered);
            else
                LunaStyle.DrawHelpMarker(option.Description, treatAsHovered: treatAsHovered);
            ModSettingDrawNode.AddUniqueNameTooltip(option, treatAsHovered);
        }
        else
        {
            ModSettingDrawNode.AddUniqueNameTooltip(option);
        }
    }

    private void HandleComboMouseWheel(ModSettingGroup group, IReadOnlyList<ModSettingOption> options, Setting setting)
    {
        if (group.Disabled || _locked || options.Count is 0)
            return;
        if (!Im.Item.Hovered() || !MouseWheelType.Control.CheckMouseWheel())
            return;

        Im.Item.SetUsingMouseWheel();
        var delta = (int)Im.Io.MouseWheel;
        if (delta is 0)
            return;

        var selectable = options.Where(o => !o.Disabled).ToList();
        if (selectable.Count is 0)
            return;

        var currentIdx = -1;
        for (var i = 0; i < selectable.Count; ++i)
        {
            if (selectable[i].Data.Index == setting.AsIndex)
            {
                currentIdx = i;
                break;
            }
        }

        var newIdx = ImUtility.ApplyMouseWheelDelta(delta, currentIdx, selectable.Count);
        if (newIdx < 0 || newIdx == currentIdx)
            return;

        SetModSetting(group.Group, Setting.Single(selectable[newIdx].Data.Index));
    }

    private void DrawGroupContextMenu(ModSettingGroup group, bool hovered)
    {
        if (group.Disabled)
            return;

        ApplyMultiState(group);

        if (hovered && Im.Mouse.IsClicked(MouseButton.Right))
            Im.Popup.Open("##multiState"u8);

        using var context = Im.Popup.Begin("##multiState"u8);
        if (!context)
            return;

        if (Im.Menu.Item("启用所有子选项"u8))
            SetMultiState(group.Group, true);
        if (Im.Menu.Item("禁用所有子选项"u8))
            SetMultiState(group.Group, false);
    }

    private void SetMultiState(IModGroup group, bool state)
    {
        _setStateGroup = group;
        _setMultiState = state;
    }

    private void ApplyMultiState(ModSettingGroup group)
    {
        if (_setStateGroup != group.Group)
            return;

        if (SetAllOptions(group, _setMultiState))
            return;

        _setStateGroup = null;
    }

    private bool SetAllOptions(ModSettingDataNode node, bool state)
    {
        if (node is not ModSettingGroup group || group.Disabled)
            return false;

        var initialSetting = GetModSetting(group.Group);
        var setting        = initialSetting;
        var changes        = false;
        foreach (var child in group.VisibleChildren)
        {
            if (child is ModSettingOption option)
            {
                if (option is { Radio: false, Disabled: false })
                    setting = setting.SetBit(option.Data.Index, state);

                foreach (var subgroup in option.VisibleChildren)
                    changes |= SetAllOptions(subgroup, state);
            }
            else if (child is ModSettingGroup subgroup)
            {
                changes |= SetAllOptions(subgroup, state);
            }
        }

        changes |= setting != initialSetting;
        SetModSetting(group.Group, setting);
        return changes;
    }

    private Setting GetModSetting(IModGroup group)
        => _context.Settings.IsEmpty ? group.DefaultSettings : _context.Settings.Settings[group.Index];

    private ModCollection Current
        => collectionManager.Active.Current;

    private void SetModSetting(IModGroup group, Setting setting)
    {
        if (_temporary || config.Main.DefaultTemporaryMode)
        {
            _tempSettings                         ??= new TemporaryModSettings(group.Mod, _context.Settings);
            _tempSettings.ForceInherit            =   false;
            _tempSettings.Settings[group.Index] =   setting;
            collectionManager.Editor.SetTemporarySettings(Current, group.Mod, _tempSettings);
        }
        else
        {
            collectionManager.Editor.SetModSetting(Current, group.Mod, group.Index, setting);
        }
    }
}
