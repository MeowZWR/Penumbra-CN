using Dalamud.Interface;
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

    private readonly record struct PageLayout(float ComboWidth, float CardWidth);

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
        foreach (var group in groups.OrderByDescending(IsSortableSingleGroup))
            DrawGroup(group);
        UiHelpers.DefaultLineSpace();
    }

    private PageLayout CalculatePageLayout(IReadOnlyList<ModSettingGroup> roots)
    {
        var groups = new List<ModSettingGroup>();
        CollectVisibleGroups(roots, groups, []);

        var minimumComboWidth = config.Ui.ModSettingMinimumComboWidth * Im.Style.GlobalScale;
        var maximumComboWidth = config.Ui.ModSettingMaximumComboWidth * Im.Style.GlobalScale;
        var comboWidth = groups
            .Where(g => g.Behaviour is GroupDrawBehaviour.SingleSelection && g.IsCombo)
            .Select(g => g.ComboWidth)
            .DefaultIfEmpty(minimumComboWidth)
            .Max();
        comboWidth = Math.Clamp(comboWidth, minimumComboWidth, maximumComboWidth);

        var cardWidth = 0f;
        foreach (var group in groups.Where(IsExpandedGroup))
        {
            var headerWidth = CalculateCardHeaderWidth(group);
            var optionWidth = group.VisibleChildren
                .OfType<ModSettingOption>()
                .Select(o => o.Width + Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X)
                .DefaultIfEmpty(0)
                .Max()
              + 2 * Im.Style.FrameHeight;
            cardWidth = Math.Max(cardWidth, Math.Max(headerWidth, optionWidth));
        }

        return new PageLayout(comboWidth, cardWidth);
    }

    private static float CalculateCardHeaderWidth(ModSettingGroup group)
        => group.Name.CalculateSize().X
         + Math.Max(ImEx.Icon.CalculateSize(LunaStyle.TreeCollapseIcon).X, ImEx.Icon.CalculateSize(LunaStyle.TreeExpandIcon).X)
         + Im.Style.ItemInnerSpacing.X
         + 2 * Im.Style.FrameHeight
         + 2 * Im.Style.ItemSpacing.X;

    private static void CollectVisibleGroups(IEnumerable<ModSettingGroup> source, List<ModSettingGroup> target, HashSet<int> seen)
    {
        foreach (var group in source)
        {
            if (!seen.Add(group.Group.Index))
                continue;

            target.Add(group);
            CollectVisibleGroups(group.VisibleChildren.OfType<ModSettingGroup>(), target, seen);
            foreach (var option in group.VisibleChildren.OfType<ModSettingOption>())
                CollectVisibleGroups(option.VisibleChildren, target, seen);
        }
    }

    private static bool IsSortableSingleGroup(ModSettingGroup group)
        => group.Behaviour is GroupDrawBehaviour.SingleSelection
        && group.IsCombo
        && !HasVisibleDependentGroups(group);

    private static bool IsExpandedGroup(ModSettingGroup group)
        => group.Behaviour is GroupDrawBehaviour.MultiSelection || !group.IsCombo;

    private static bool HasVisibleDependentGroups(ModSettingGroup group)
        => group.VisibleChildren.OfType<ModSettingGroup>().Any()
         || group.VisibleChildren.OfType<ModSettingOption>().Any(o => o.VisibleChildren.Count > 0);

    private void DrawGroup(ModSettingGroup group)
    {
        using var id = Im.Id.Push(group.Group.Index);
        if (group.Behaviour is GroupDrawBehaviour.SingleSelection && group.IsCombo)
            DrawCompactSingleGroup(group);
        else
            DrawExpandedGroup(group);

        if (group.Space)
            UiHelpers.DefaultLineSpace();
    }

    private void DrawCompactSingleGroup(ModSettingGroup group)
    {
        var setting = GetModSetting(group.Group);
        var options = group.VisibleChildren.Take(group.NumOptions).OfType<ModSettingOption>().ToList();
        if (options.Count is 0)
            return;

        var current = group.AllOptions[setting.AsIndex];
        Im.Item.SetNextWidth(_pageLayout.ComboWidth);
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
                    DrawOptionTooltip(option);
                }
            }
        }

        DrawOptionTooltip(current);
        Im.Line.SameInner();
        Im.Text(group.Name);
        Im.Tooltip.OnHover(group.Description.IsEmpty
            ? group.Name
            : $"{group.Name}\n\n{group.Description}");
        ModSettingDrawNode.AddUniqueNameTooltip(group);

        using var indent = Im.Indent();
        foreach (var child in group.VisibleChildren.OfType<ModSettingGroup>())
            DrawGroup(child);
    }

    private void DrawExpandedGroup(ModSettingGroup group)
    {
        var stateId        = Im.Id.Get("Expanded"u8);
        var defaultExpanded = !group.Group.Layout.HasFlag(ModSettingsLayout.DefaultClosed);
        var expanded       = _cache.Storage.GetBool(stateId, defaultExpanded);
        var maximumWidth   = Math.Max(0, Im.ContentRegion.Available.X);
        var ownHeaderWidth = CalculateCardHeaderWidth(group);
        var desiredWidth   = Math.Max(_pageLayout.CardWidth, ownHeaderWidth);
        var cardWidth      = Math.Min(desiredWidth, maximumWidth);
        var headerMin      = Im.Cursor.ScreenPosition;
        using var borderStyle = ImStyleSingle.ChildBorderThickness.Push(
            config.Ui.ModSettingBorderScale * Im.Style.GlobalScale / 2);
        var headerIcon = expanded ? LunaStyle.TreeCollapseIcon : LunaStyle.TreeExpandIcon;
        using var frame = ImEx.FramedGroup($"{group.Name}", headerIcon, default(AwesomeIcon),
            minimumSize: new Vector2(cardWidth, 0));
        var headerMax     = headerMin + new Vector2(Math.Max(cardWidth, frame.MinimumWidth), Im.Style.FrameHeight);
        var headerHovered = Im.Mouse.IsHoveringRectangle(headerMin, headerMax);
        if (headerHovered && !group.Description.IsEmpty)
            Im.Tooltip.Set(group.Description);
        ModSettingDrawNode.AddUniqueNameTooltip(group, headerHovered);
        if (headerHovered && Im.Mouse.IsClicked(MouseButton.Left))
        {
            expanded = !expanded;
            _cache.Storage.SetBool(stateId, expanded);
        }

        if (!expanded)
            return;

        foreach (var option in group.VisibleChildren.OfType<ModSettingOption>())
        {
            DrawOption(option);
            using var optionIndent = Im.Indent();
            foreach (var child in option.VisibleChildren)
                DrawGroup(child);
        }

        using var childIndent = Im.Indent();
        foreach (var child in group.VisibleChildren.OfType<ModSettingGroup>())
            DrawGroup(child);
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

        DrawOptionTooltip(option);
        if (option.Separator)
            LunaStyle.DrawSeparator();
        if (option.Space)
            UiHelpers.DefaultLineSpace();
    }

    private static void DrawOptionTooltip(ModSettingOption option)
    {
        if (!option.Description.IsEmpty)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, option.Description);
        ModSettingDrawNode.AddUniqueNameTooltip(option);
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
