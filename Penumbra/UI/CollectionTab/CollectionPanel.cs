using Dalamud.Interface.Components;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.Tabs;

namespace Penumbra.UI.CollectionTab;

public sealed class CollectionPanel(
    IDalamudPluginInterface pi,
    CommunicatorService communicator,
    CollectionManager manager,
    CollectionSelector selector,
    ActorManager actors,
    ITargetManager targets,
    ModStorage mods,
    SaveService saveService,
    IncognitoService incognito,
    Configuration config)
    : IDisposable, IPanel
{
    private readonly CollectionStorage _collections = manager.Storage;
    private readonly ActiveCollections _active = manager.Active;
    private readonly IndividualAssignmentUi _individualAssignmentUi = new(communicator, actors, manager);
    private readonly InheritanceUi _inheritanceUi = new(manager, incognito);
    private readonly IFontHandle _nameFont = pi.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Jupiter23));

    private static readonly IReadOnlyDictionary<CollectionType, (StringU8 Name, Vector4 Border)> Buttons      = CreateButtons();
    private static readonly IReadOnlyList<(CollectionType, bool, bool, StringU8, Vector4)>       AdvancedTree = CreateTree();
    private readonly        List<(CollectionType Type, ActorIdentifier Identifier)>              _inUseCache  = [];

    private int _draggedIndividualAssignment = -1;

    public void Dispose()
    {
        _individualAssignmentUi.Dispose();
        _nameFont.Dispose();
    }

    /// <summary> Draw the panel containing beginners information and simple assignments. </summary>
    public void DrawSimple()
    {
        Im.TextWrapped("合集是一组模组配置的集合。您可以拥有任意数量的合集。\n"u8
          + "您当前在模组选项卡中正在编辑的合集可以在这里选择并高亮显示。\n"u8);
        Im.TextWrapped(
            "有可以分配这些合集的功能，因此不同的模组配置适用于不同的对象。\n"u8
          + "您可以通过点击功能或拖动合集来将现有的合集分配给这些功能。"u8);
        Im.Separator();

        var buttonWidth = new Vector2(200 * Im.Style.GlobalScale, 2 * Im.Style.FrameHeightWithSpacing);
        using var style = Im.Style.Push(ImStyleDouble.ButtonTextAlign, Vector2.Zero)
            .Push(ImStyleSingle.FrameBorderThickness, Im.Style.GlobalScale);
        DrawSimpleCollectionButton(CollectionType.Default,                  buttonWidth);
        DrawSimpleCollectionButton(CollectionType.Interface,                buttonWidth);
        DrawSimpleCollectionButton(CollectionType.Yourself,                 buttonWidth);
        DrawSimpleCollectionButton(CollectionType.MalePlayerCharacter,      buttonWidth);
        DrawSimpleCollectionButton(CollectionType.FemalePlayerCharacter,    buttonWidth);
        DrawSimpleCollectionButton(CollectionType.MaleNonPlayerCharacter,   buttonWidth);
        DrawSimpleCollectionButton(CollectionType.FemaleNonPlayerCharacter, buttonWidth);

        ImEx.TextMultiColored("独立 "u8, ColorId.NewMod.Value())
            .Then("分配优先级高于其他任何分配，并且只能应用于一个特定的角色或怪物。"u8)
            .End();
        Im.Dummy(1);

        var specialWidth = buttonWidth with { X = 275 * Im.Style.GlobalScale };
        DrawCurrentCharacter(specialWidth);
        Im.Line.Same();
        DrawCurrentTarget(specialWidth);
        DrawIndividualCollections(buttonWidth);

        var first = true;

        Button(CollectionType.NonPlayerChild);
        Button(CollectionType.NonPlayerElderly);
        foreach (var race in SubRace.Values.Skip(1))
        {
            Button(CollectionTypeExtensions.FromParts(race, Gender.Male,   false));
            Button(CollectionTypeExtensions.FromParts(race, Gender.Female, false));
            Button(CollectionTypeExtensions.FromParts(race, Gender.Male,   true));
            Button(CollectionTypeExtensions.FromParts(race, Gender.Female, true));
        }

        return;

        void Button(CollectionType type)
        {
            var (name, border) = Buttons[type];
            var collection = _active.ByType(type);
            if (collection == null)
                return;

            if (first)
            {
                Im.Separator();
                Im.Text("当前激活的高级分配"u8);
                first = false;
            }

            DrawButton(name, type, buttonWidth, border, ActorIdentifier.Invalid, 's', collection);
            Im.Line.Same();
            if (Im.ContentRegion.Available.X < buttonWidth.X + Im.Style.ItemSpacing.X + Im.Style.WindowPadding.X)
                Im.Line.New();
        }
    }

    /// <summary> Draw the panel containing new and existing individual assignments. </summary>
    public void DrawIndividualPanel()
    {
        using var style = ImStyleDouble.ButtonTextAlign.Push(Vector2.Zero)
            .Push(ImStyleSingle.FrameBorderThickness, Im.Style.GlobalScale);
        var width = new Vector2(300 * Im.Style.GlobalScale, 2 * Im.Style.TextHeightWithSpacing);

        Im.Dummy(Vector2.One);
        DrawCurrentCharacter(width);
        Im.Line.Same();
        DrawCurrentTarget(width);
        Im.Separator();
        Im.Dummy(Vector2.One);
        style.Pop();
        _individualAssignmentUi.DrawWorldCombo(width.X / 2);
        Im.Line.Same();
        _individualAssignmentUi.DrawNewPlayerCollection(width.X);

        _individualAssignmentUi.DrawObjectKindCombo(width.X / 2);
        Im.Line.Same();
        _individualAssignmentUi.DrawNewNpcCollection(width.X);
        Im.Line.Same();
        ImGuiComponents.HelpMarker(
            "战斗和事件中的NPC可能会因为同名而应用于多个ID。这取决于您的语言设置。如果您更改了客户端语言，请检查您的合集是否仍然正确分配。");
        Im.Dummy(Vector2.One);
        Im.Separator();
        style.Push(ImStyleSingle.FrameBorderThickness, Im.Style.GlobalScale);

        DrawNewPlayer(width);
        Im.Line.Same();
        Im.TextWrapped("同时检查常规设置中与玩家相关的用户界面选项以及继承关系。"u8);
        Im.Separator();

        DrawNewRetainer(width);
        Im.Line.Same();
        Im.TextWrapped("传唤铃雇员适用于人体模特，但不适用于户外雇员，因为后者只携带其主人的名字。"u8);
        Im.Separator();

        DrawNewNpc(width);
        Im.Line.Same();
        Im.TextWrapped("部分NPC也会出现在战斗和事件中，如果想要两种场合都生效需要手动分别设置。"u8);
        Im.Separator();

        DrawNewOwned(width);
        Im.Line.Same();
        Im.TextWrapped("属于玩家的NPC优先级高于不属于玩家的同类型NPC。"u8);
        Im.Separator();

        DrawIndividualCollections(width with { X = 200 * Im.Style.GlobalScale });
    }

    /// <summary> Draw the panel containing all special group assignments. </summary>
    public void DrawGroupPanel()
    {
        Im.Dummy(Vector2.One);
        using var table = Im.Table.Begin("##advanced"u8, 4, TableFlags.SizingFixedFit | TableFlags.RowBackground);
        if (!table)
            return;

        using var style = ImStyleDouble.ButtonTextAlign.Push(Vector2.Zero)
            .Push(ImStyleSingle.FrameBorderThickness, Im.Style.GlobalScale);

        var buttonWidth = new Vector2(150 * Im.Style.GlobalScale, 2 * Im.Style.TextHeightWithSpacing);
        var dummy       = new Vector2(1,                          0);

        foreach (var (type, pre, post, name, border) in AdvancedTree)
        {
            table.NextColumn();
            if (type is CollectionType.Inactive)
                continue;

            if (pre)
                Im.Dummy(dummy);
            DrawAssignmentButton(type, buttonWidth, name, border);
            if (post)
                Im.Dummy(dummy);
        }
    }

    /// <summary> Draw the collection detail panel with inheritance, visible mod settings and statistics. </summary>
    public void DrawDetailsPanel()
    {
        var collection = _active.Current;
        DrawCollectionName(collection);
        DrawStatistics(collection);
        DrawCollectionData(collection);
        _inheritanceUi.Draw();
        Im.Separator();
        DrawInactiveSettingsList(collection);
        DrawSettingsList(collection);
    }

    private void DrawCollectionData(ModCollection collection)
    {
        Im.Dummy(Vector2.Zero);
        using (Im.Group())
        {
            ImEx.TextFrameAligned("名称"u8);
            ImEx.TextFrameAligned("标识符"u8);
        }

        Im.Line.Same();
        using (Im.Group())
        {
            var width = Im.ContentRegion.Available.X;
            using (Im.Disabled(_collections.DefaultNamed == collection))
            {
                using var style = ImStyleDouble.ButtonTextAlign.Push(new Vector2(0, 0.5f));
                Im.Item.SetNextWidth(width);
                if (ImEx.InputOnDeactivation.Text("##name"u8, collection.Identity.Name, out string newName))
                    _collections.RenameCollection(collection, newName);
            }

            if (_collections.DefaultNamed == collection)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "默认合集无法重命名。"u8);

            var identifier = collection.Identity.Identifier;
            var fileName   = saveService.FileNames.CollectionFile(collection);
            using (Im.Font.PushMono())
            {
                if (Im.Button(collection.Identity.Identifier, new Vector2(width, 0)))
                    try
                    {
                        Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        Penumbra.Messager.NotificationMessage(ex, $"无法打开文件 {fileName}。", $"无法打开文件 {fileName}",
                            NotificationType.Warning);
                    }
            }

            if (Im.Item.RightClicked())
                Im.Clipboard.Set(identifier);

            Im.Tooltip.OnHover(
                $"在您选择的 .json 编辑器中打开包含此设计的文件\n\t{fileName}\n。\n\n右键点击以复制标识符到剪贴板。");
        }

        Im.Dummy(Vector2.Zero);
        Im.Separator();
        Im.Dummy(Vector2.Zero);
    }

    private void DrawContext(bool open, ModCollection? collection, CollectionType type, ActorIdentifier identifier, StringU8 text, char suffix)
    {
        var label = $"{type}{text}{suffix}";
        if (open)
            Im.Popup.Open(label);

        using var context = Im.Popup.Begin(label);
        if (!context)
            return;

        using (ImGuiColor.Text.Push(LunaStyle.DiscordColor))
        {
            if (Im.Menu.Item("不使用模组"u8))
                _active.SetCollection(ModCollection.Empty, type, _active.Individuals.GetGroup(identifier));
        }

        if (collection is not null && type.CanBeRemoved())
        {
            using var color = ImGuiColor.Text.Push(Colors.RegexWarningBorder);
            if (Im.Menu.Item("移除此分配"u8))
                _active.SetCollection(null, type, _active.Individuals.GetGroup(identifier));
        }

        foreach (var coll in _collections.OrderBy(c => c.Identity.Name))
        {
            if (coll != collection && Im.Menu.Item($"使用 {coll.Identity.Name}。"))
                _active.SetCollection(coll, type, _active.Individuals.GetGroup(identifier));
        }
    }

    private void DrawButton(StringU8 text, CollectionType type, Vector2 width, Rgba32 borderColor, ActorIdentifier id, char suffix,
        ModCollection? collection = null)
    {
        using var group      = Im.Group();
        var       invalid    = type is CollectionType.Individual && !id.IsValid;
        var       redundancy = _active.RedundancyCheck(type, id);
        collection ??= _active.ByType(type, id);
        using var color = ImGuiColor.Button.Push(
                collection is null
                    ? ColorId.NoAssignment.Value()
                    : redundancy.Length > 0
                        ? ColorId.RedundantAssignment.Value()
                        : collection == _active.Current
                            ? ColorId.SelectedCollection.Value()
                            : collection == ModCollection.Empty
                                ? ColorId.NoModsAssignment.Value()
                                : ImGuiColor.Button.Get(), !invalid)
            .Push(ImGuiColor.Border, borderColor == 0 ? ImGuiColor.TextDisabled.Get().Color : borderColor);
        using var disabled = Im.Disabled(invalid);
        var       button   = Im.Button(text, width) || Im.Item.RightClicked();
        var       hovered  = redundancy.Length > 0 && Im.Item.Hovered();
        DrawIndividualDragSource(text, id);
        DrawIndividualDragTarget(id);
        if (!invalid)
        {
            selector.DragTargetAssignment(type, id);
            var name    = Name(collection);
            var size    = Im.Font.CalculateSize(name);
            var textPos = Im.Item.LowerRightCorner - size - Im.Style.FramePadding;
            Im.Window.DrawList.Text(textPos, ImGuiColor.Text.Get().Color, name);
            DrawContext(button, collection, type, id, text, suffix);
        }

        if (hovered)
            Im.Tooltip.Set(redundancy);
    }

    private void DrawIndividualDragSource(ReadOnlySpan<byte> text, ActorIdentifier id)
    {
        if (!id.IsValid)
            return;

        using var source = Im.DragDrop.Source();
        if (!source)
            return;

        Im.DragDrop.SetPayload("DragIndividual"u8);
        Im.Text($"重新排序 {text}...");
        _draggedIndividualAssignment = _active.Individuals.Index(id);
    }

    private void DrawIndividualDragTarget(ActorIdentifier id)
    {
        if (!id.IsValid)
            return;

        using var target = Im.DragDrop.Target();
        if (!target || !target.IsDropping("DragIndividual"u8))
            return;

        var currentIdx = _active.Individuals.Index(id);
        if (_draggedIndividualAssignment != -1 && currentIdx != -1)
            _active.MoveIndividualCollection(_draggedIndividualAssignment, currentIdx);
        _draggedIndividualAssignment = -1;
    }

    private void DrawSimpleCollectionButton(CollectionType type, Vector2 width)
    {
        DrawButton(new StringU8(type.ToName()), type, width, 0, ActorIdentifier.Invalid, 's');
        Im.Line.Same();
        using (Im.Group())
        {
            Im.TextWrapped(type.ToDescription());
            switch (type)
            {
                case CollectionType.Default: Im.Text("优先级低于所有其他分配。"u8); break;
                case CollectionType.Yourself:
                    ImEx.TextMultiColored("优先级低于 "u8)
                        .Then("独立 "u8, ColorId.NewMod.Value().Color)
                        .Then("分配。"u8)
                        .End();
                    break;
                case CollectionType.MalePlayerCharacter:
                    ImEx.TextMultiColored("优先级低于 "u8)
                        .Then("男性种族玩家"u8, LunaStyle.DiscordColor)
                        .Then(", "u8)
                        .Then("你的角色"u8, ColorId.HandledConflictMod.Value().Color)
                        .Then("或 "u8)
                        .Then("独立 "u8, ColorId.NewMod.Value().Color)
                        .Then("分配。"u8)
                        .End();
                    break;
                case CollectionType.FemalePlayerCharacter:
                    ImEx.TextMultiColored("优先级低于 "u8)
                        .Then("女性种族玩家"u8, LunaStyle.ReniColorActive)
                        .Then(", "u8)
                        .Then("你的角色"u8, ColorId.HandledConflictMod.Value().Color)
                        .Then("或 "u8)
                        .Then("独立 "u8, ColorId.NewMod.Value().Color)
                        .Then("分配。"u8)
                        .End();
                    break;
                case CollectionType.MaleNonPlayerCharacter:
                    ImEx.TextMultiColored("优先级低于 "u8)
                        .Then("男性种族NPC"u8, LunaStyle.DiscordColor)
                        .Then(", "u8)
                        .Then("儿童"u8, ColorId.FolderLine.Value().Color)
                        .Then(", "u8)
                        .Then("老年人"u8, Colors.MetaInfoText)
                        .Then(", 或 "u8)
                        .Then("独立 "u8, ColorId.NewMod.Value().Color)
                        .Then("分配。"u8)
                        .End();
                    break;
                case CollectionType.FemaleNonPlayerCharacter:
                    ImEx.TextMultiColored("优先级低于 "u8)
                        .Then("女性种族NPC"u8, LunaStyle.ReniColorActive)
                        .Then(", "u8)
                        .Then("儿童"u8, ColorId.FolderLine.Value().Color)
                        .Then(", "u8)
                        .Then("老年人"u8, Colors.MetaInfoText)
                        .Then(", 或 "u8)
                        .Then("独立 "u8, ColorId.NewMod.Value().Color)
                        .Then("分配。"u8)
                        .End();
                    break;
            }
        }

        Im.Separator();
    }

    private void DrawAssignmentButton(CollectionType type, Vector2 width, StringU8 name, Vector4 color)
        => DrawButton(name, type, width, color, ActorIdentifier.Invalid, 's', _active.ByType(type));

    /// <summary> Respect incognito mode for names of identifiers. </summary>
    private StringU8 Name(ActorIdentifier id, string? name)
        => incognito.IncognitoMode && id.Type is IdentifierType.Player or IdentifierType.Owned
            ? new StringU8(id.Incognito(name))
            : name is not null
                ? new StringU8(name)
                : new StringU8($"{id}");

    /// <summary> Respect incognito mode for names of collections. </summary>
    private string Name(ModCollection? collection)
        => collection is null                 ? "未分配" :
            collection == ModCollection.Empty ? "不使用模组" :
            incognito.IncognitoMode           ? collection.Identity.AnonymizedName : collection.Identity.Name;

    private void DrawIndividualButton(string intro, Vector2 width, string tooltip, char suffix, params ActorIdentifier[] identifiers)
    {
        if (identifiers.Length > 0 && identifiers[0].IsValid)
        {
            DrawButton(new StringU8($"{intro} ({Name(identifiers[0], null)})"), CollectionType.Individual, width, 0, identifiers[0], suffix);
        }
        else
        {
            if (tooltip.Length == 0 && identifiers.Length > 0)
                tooltip = $"当前目标 {identifiers[0].PlayerName} 无效，无法分配。";
            DrawButton(new StringU8($"{intro} (无效)"), CollectionType.Individual, width, 0, ActorIdentifier.Invalid, suffix);
        }

        Im.Tooltip.OnHover(tooltip);
    }

    private void DrawCurrentCharacter(Vector2 width)
        => DrawIndividualButton("当前角色", width, string.Empty, 'c', actors.GetCurrentPlayer());

    private void DrawCurrentTarget(Vector2 width)
        => DrawIndividualButton("当前目标", width, string.Empty, 't',
            actors.FromObject(targets.Target, false, true, true));

    private void DrawNewPlayer(Vector2 width)
        => DrawIndividualButton("添加玩家分配", width, _individualAssignmentUi.PlayerTooltip, 'p',
            _individualAssignmentUi.PlayerIdentifiers.FirstOrDefault());

    private void DrawNewRetainer(Vector2 width)
        => DrawIndividualButton("添加传唤铃雇员分配", width, _individualAssignmentUi.RetainerTooltip, 'r',
            _individualAssignmentUi.RetainerIdentifiers.FirstOrDefault());

    private void DrawNewNpc(Vector2 width)
        => DrawIndividualButton("添加NPC分配", width, _individualAssignmentUi.NpcTooltip, 'n',
            _individualAssignmentUi.NpcIdentifiers.FirstOrDefault());

    private void DrawNewOwned(Vector2 width)
        => DrawIndividualButton("添加玩家所属NPC分配", width, _individualAssignmentUi.OwnedTooltip, 'o',
            _individualAssignmentUi.OwnedIdentifiers.FirstOrDefault());

    private void DrawIndividualCollections(Vector2 width)
    {
        for (var i = 0; i < _active.Individuals.Count; ++i)
        {
            var (name, ids, coll) = _active.Individuals.Assignments[i];
            DrawButton(Name(ids[0], name), CollectionType.Individual, width, 0, ids[0], 'i', coll);

            Im.Line.Same();
            if (Im.ContentRegion.Available.X < width.X + Im.Style.ItemSpacing.X + Im.Style.WindowPadding.X
             && i < _active.Individuals.Count - 1)
                Im.Line.New();
        }

        if (_active.Individuals.Count > 0)
            Im.Line.New();
    }

    private void DrawCollectionName(ModCollection collection)
    {
        Im.Dummy(Vector2.One);
        using var style = ImStyleBorder.Frame.Push(Colors.MetaInfoText, 2 * Im.Style.GlobalScale);
        using var f     = _nameFont.Push();
        var       name  = Name(collection);
        var       size  = Im.Font.CalculateSize(name).X;
        var       pos   = Im.ContentRegion.Available.X - size + Im.Style.FramePadding.X * 2;
        if (pos > 0)
            Im.Cursor.X = pos / 2;
        ImEx.TextFramed(name, Vector2.Zero, 0);
        Im.Dummy(Vector2.One);
    }

    private void DrawStatistics(ModCollection collection)
    {
        GatherInUse(collection);
        Im.Separator();

        var buttonHeight = 2 * Im.Style.TextHeightWithSpacing;
        if (_inUseCache.Count == 0 && collection.Inheritance.DirectlyInheritedBy.Count == 0)
        {
            Im.Dummy(Vector2.One);
            using var f = _nameFont.Push();
            ImEx.TextFramed("合集未使用。"u8, Im.ContentRegion.Available with { Y = buttonHeight },
                Colors.PressEnterWarningBg);
            Im.Dummy(Vector2.One);
            Im.Separator();
        }
        else
        {
            var buttonWidth = new Vector2(175 * Im.Style.GlobalScale, buttonHeight);
            DrawInUseStatistics(collection, buttonWidth);
            DrawInheritanceStatistics(collection);
        }
    }

    private void GatherInUse(ModCollection collection)
    {
        _inUseCache.Clear();
        foreach (var special in CollectionTypeExtensions.Special.Select(t => t.Item1)
                     .Prepend(CollectionType.Default)
                     .Prepend(CollectionType.Interface)
                     .Where(t => _active.ByType(t) == collection))
            _inUseCache.Add((special, ActorIdentifier.Invalid));

        foreach (var (_, id, _) in _active.Individuals.Assignments.Where(t
                     => t.Collection == collection && t.Identifiers.Count > 0 && t.Identifiers[0].IsValid))
            _inUseCache.Add((CollectionType.Individual, id[0]));
    }

    private void DrawInUseStatistics(ModCollection collection, Vector2 buttonWidth)
    {
        if (_inUseCache.Count <= 0)
            return;

        using (ImStyleDouble.FramePadding.Push(Vector2.Zero))
        {
            ImEx.TextFramed("使用者"u8, Im.ContentRegion.Available with { Y = 0 }, 0);
        }

        using var style = ImStyleSingle.FrameBorderThickness.Push(Im.Style.GlobalScale)
            .Push(ImStyleDouble.ButtonTextAlign, Vector2.Zero);

        foreach (var (idx, (type, id)) in _inUseCache.Index())
        {
            var name  = type is CollectionType.Individual ? Name(id, null) : Buttons[type].Name;
            var color = Buttons.TryGetValue(type, out var p) ? p.Border : Vector4.Zero;
            DrawButton(name, type, buttonWidth, color, id, 's', collection);
            Im.Line.Same();
            if (Im.ContentRegion.Available.X < buttonWidth.X + Im.Style.ItemSpacing.X + Im.Style.WindowPadding.X
             && idx != _inUseCache.Count - 1)
                Im.Line.New();
        }

        Im.Line.New();
        Im.Dummy(Vector2.One);
        Im.Separator();
    }

    private void DrawInheritanceStatistics(ModCollection collection)
    {
        if (collection.Inheritance.DirectlyInheritedBy.Count <= 0)
            return;

        using (ImStyleDouble.FramePadding.Push(Vector2.Zero))
        {
            ImEx.TextFramed("继承者"u8, Im.ContentRegion.Available with { Y = 0 }, 0);
        }

        using var f     = _nameFont.Push();
        using var style = ImStyleBorder.Frame.Push(Colors.MetaInfoText);
        ImEx.TextFramed(Name(collection.Inheritance.DirectlyInheritedBy[0]), Vector2.Zero, 0);
        var constOffset = (Im.Style.FramePadding.X + Im.Style.GlobalScale) * 2
          + Im.Style.ItemSpacing.X
          + Im.Style.WindowPadding.X;
        foreach (var parent in collection.Inheritance.DirectlyInheritedBy.Skip(1))
        {
            var name = Name(parent);
            var size = Im.Font.CalculateSize(name).X;
            Im.Line.Same();
            if (constOffset + size >= Im.ContentRegion.Available.X)
                Im.Line.New();
            ImEx.TextFramed(name, Vector2.Zero, 0);
        }

        Im.Dummy(Vector2.One);
        Im.Separator();
    }

    private void DrawSettingsList(ModCollection collection)
    {
        Im.Dummy(Vector2.One);
        var       size  = Im.ContentRegion.Available with { Y = 10 * Im.Style.FrameHeightWithSpacing };
        using var table = Im.Table.Begin("##activeSettings"u8, 4, TableFlags.ScrollY | TableFlags.RowBackground, size);
        if (!table)
            return;

        table.SetupScrollFreeze(0, 1);
        table.SetupColumn("模组名称"u8,       TableColumnFlags.WidthStretch);
        table.SetupColumn("继承自"u8, TableColumnFlags.WidthFixed, 5f * Im.Style.FrameHeight);
        table.SetupColumn("状态"u8,          TableColumnFlags.WidthFixed, 1.75f * Im.Style.FrameHeight);
        table.SetupColumn("优先级"u8,       TableColumnFlags.WidthFixed, 2.5f * Im.Style.FrameHeight);
        table.HeaderRow();

        foreach (var (mod, (settings, parent)) in mods.Select(m => (m, collection.GetInheritedSettings(m.Index)))
                     .Where(t => t.Item2.Settings != null)
                     .OrderBy(t => t.m.Name))
        {
            table.NextColumn();
            ImEx.CopyOnClickSelectable(mod.Name);
            table.NextColumn();
            if (parent != collection)
                Im.Text(Name(parent));
            table.NextColumn();
            var enabled = settings!.Enabled;
            using (Im.Disabled())
            {
                Im.Checkbox("##check"u8, ref enabled);
            }

            table.NextColumn();
            ImEx.TextRightAligned($"{settings.Priority}", Im.Style.WindowPadding.X);
        }
    }

    private void DrawInactiveSettingsList(ModCollection collection)
    {
        if (collection.Settings.Unused.Count is 0)
            return;

        Im.Dummy(Vector2.One);
        if (Im.Button(collection.Settings.Unused.Count > 1
                ? $"清理所有 {collection.Settings.Unused.Count} 个已删除的模组的设置。"
                : "清除当前未使用的已删除的模组设置。"u8, Im.ContentRegion.Available with { Y = 0 }))
            _collections.CleanUnavailableSettings(collection);

        Im.Dummy(Vector2.One);

        var size = Im.ContentRegion.Available with { Y = Math.Min(10, collection.Settings.Unused.Count + 1) * Im.Style.FrameHeightWithSpacing };
        using var table = Im.Table.Begin("##inactiveSettings"u8, 4, TableFlags.ScrollY | TableFlags.RowBackground, size);
        if (!table)
            return;

        table.SetupScrollFreeze(0, 1);
        table.SetupColumn(StringU8.Empty,            TableColumnFlags.WidthFixed, UiHelpers.IconButtonSize.X);
        table.SetupColumn("未使用模组标识符"u8,       TableColumnFlags.WidthStretch);
        table.SetupColumn("状态"u8,                  TableColumnFlags.WidthFixed, 1.75f * Im.Style.FrameHeight);
        table.SetupColumn("优先级"u8,                TableColumnFlags.WidthFixed, 2.5f * Im.Style.FrameHeight);
        table.HeaderRow();
        string? delete = null;
        foreach (var (name, settings) in collection.Settings.Unused.OrderBy(n => n.Key))
        {
            using var id = Im.Id.Push(name);
            table.NextColumn();
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "删除这条已未使用的设置。"u8))
                delete = name;
            table.NextColumn();
            ImEx.CopyOnClickSelectable(name);
            table.NextColumn();
            var enabled = settings.Enabled;
            using (Im.Disabled())
            {
                Im.Checkbox("##check"u8, ref enabled);
            }

            table.NextColumn();
            ImEx.TextRightAligned($"{settings.Priority}", Im.Style.WindowPadding.X);
        }

        _collections.CleanUnavailableSetting(collection, delete);
        Im.Separator();
    }

    /// <summary> Create names and border colors for special assignments. </summary>
    private static IReadOnlyDictionary<CollectionType, (StringU8 Name, Vector4 Border)> CreateButtons()
    {
        var ret = CollectionType.Values.ToDictionary(t => t, t => (new StringU8(t.ToName()), Vector4.Zero));

        foreach (var race in SubRace.Values.Skip(1))
        {
            Rgba32 color = race switch
            {
                SubRace.Midlander       => 0xAA5C9FE4u,
                SubRace.Highlander      => 0xAA5C9FE4u,
                SubRace.Wildwood        => 0xAA5C9F49u,
                SubRace.Duskwight       => 0xAA5C9F49u,
                SubRace.Plainsfolk      => 0xAAEF8CB6u,
                SubRace.Dunesfolk       => 0xAAEF8CB6u,
                SubRace.SeekerOfTheSun  => 0xAA8CEFECu,
                SubRace.KeeperOfTheMoon => 0xAA8CEFECu,
                SubRace.Seawolf         => 0xAAEFE68Cu,
                SubRace.Hellsguard      => 0xAAEFE68Cu,
                SubRace.Raen            => 0xAAB5EF8Cu,
                SubRace.Xaela           => 0xAAB5EF8Cu,
                SubRace.Helion          => 0xAAFFFFFFu,
                SubRace.Lost            => 0xAAFFFFFFu,
                SubRace.Rava            => 0xAA607FA7u,
                SubRace.Veena           => 0xAA607FA7u,
                _                       => 0u,
            };

            ret[CollectionTypeExtensions.FromParts(race, Gender.Male,   false)] = (new StringU8($"♂ {race.ToShortName()}"), color.ToVector());
            ret[CollectionTypeExtensions.FromParts(race, Gender.Female, false)] = (new StringU8($"♀ {race.ToShortName()}"), color.ToVector());
            ret[CollectionTypeExtensions.FromParts(race, Gender.Male, true)] =
                (new StringU8($"♂ {race.ToShortName()} (NPC)"), color.ToVector());
            ret[CollectionTypeExtensions.FromParts(race, Gender.Female, true)] =
                (new StringU8($"♀ {race.ToShortName()} (NPC)"), color.ToVector());
        }

        ret[CollectionType.MalePlayerCharacter]      = (new StringU8("♂ 男性玩家"), Vector4.Zero);
        ret[CollectionType.FemalePlayerCharacter]    = (new StringU8("♀ 女性玩家"), Vector4.Zero);
        ret[CollectionType.MaleNonPlayerCharacter]   = (new StringU8("♂ 男性NPC"), Vector4.Zero);
        ret[CollectionType.FemaleNonPlayerCharacter] = (new StringU8("♀ 女性NPC"), Vector4.Zero);
        return ret;
    }

    /// <summary> Create the special assignment tree in order and with free spaces. </summary>
    private static List<(CollectionType, bool, bool, StringU8, Vector4)> CreateTree()
    {
        var ret = new List<(CollectionType, bool, bool, StringU8, Vector4)>(Buttons.Count);

        Add(CollectionType.Default,                  false, false);
        Add(CollectionType.Interface,                false, false);
        Add(CollectionType.Inactive,                 false, false);
        Add(CollectionType.Inactive,                 false, false);
        Add(CollectionType.Yourself,                 false, true);
        Add(CollectionType.Inactive,                 false, true);
        Add(CollectionType.NonPlayerChild,           false, true);
        Add(CollectionType.NonPlayerElderly,         false, true);
        Add(CollectionType.MalePlayerCharacter,      true,  true);
        Add(CollectionType.FemalePlayerCharacter,    true,  true);
        Add(CollectionType.MaleNonPlayerCharacter,   true,  true);
        Add(CollectionType.FemaleNonPlayerCharacter, true,  true);
        var pre = true;
        foreach (var race in SubRace.Values.Skip(1))
        {
            Add(CollectionTypeExtensions.FromParts(race, Gender.Male,   false), pre, !pre);
            Add(CollectionTypeExtensions.FromParts(race, Gender.Female, false), pre, !pre);
            Add(CollectionTypeExtensions.FromParts(race, Gender.Male,   true),  pre, !pre);
            Add(CollectionTypeExtensions.FromParts(race, Gender.Female, true),  pre, !pre);
            pre = !pre;
        }

        return ret;

        void Add(CollectionType type, bool localPre, bool post)
        {
            var (name, border) = Buttons[type];
            ret.Add((type, localPre, post, name, border));
        }
    }

    public ReadOnlySpan<byte> Id
        => "cp"u8;

    public void Draw()
    {
        switch (config.Ephemeral.CollectionPanel)
        {
            case CollectionPanelMode.SimpleAssignment:     DrawSimple(); break;
            case CollectionPanelMode.IndividualAssignment: DrawIndividualPanel(); break;
            case CollectionPanelMode.GroupAssignment:      DrawGroupPanel(); break;
            case CollectionPanelMode.Details:              DrawDetailsPanel(); break;
        }
    }
}
