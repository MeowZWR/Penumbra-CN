using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.UI.Classes;

namespace Penumbra.UI.CollectionTab;

public class InheritanceUi(CollectionManager collectionManager, IncognitoService incognito) : IUiService
{
    private const int InheritedCollectionHeight = 9;

    private static ReadOnlySpan<byte> InheritanceDragDropLabel
        => "##InheritanceMove"u8;

    private readonly CollectionStorage  _collections = collectionManager.Storage;
    private readonly ActiveCollections  _active      = collectionManager.Active;
    private readonly InheritanceManager _inheritance = collectionManager.Inheritances;

    /// <summary> Draw the whole inheritance block. </summary>
    public void Draw()
    {
        using var id = Im.Id.Push("##Inheritance"u8);
        ImEx.TextMultiColored("选择的合集"u8)
            .Then(Name(_active.Current), ColorId.SelectedCollection.Value().FullAlpha().Color)
            .Then(" 继承自："u8)
            .End();
        Im.Dummy(Vector2.One);

        DrawCurrentCollectionInheritance();
        Im.Line.Same();
        DrawInheritanceTrashButton();
        Im.Line.Same();
        DrawRightText();

        DrawNewInheritanceSelection();
        Im.Line.Same();
        if (Im.Button("查看关于继承功能的更多说明"u8, Im.ContentRegion.Available with { Y = 0 }))
            Im.Popup.Open("InheritanceHelp"u8);

        DrawHelpPopup();
        DelayedActions();
    }

    // Keep for reuse.
    private readonly HashSet<ModCollection> _seenInheritedCollections = new(32);

    // Execute changes only outside of loops.
    private ModCollection? _newInheritance;
    private ModCollection? _movedInheritance;
    private (int, int)?    _inheritanceAction;
    private ModCollection? _newCurrentCollection;

    private static void DrawRightText()
    {
        using var group = Im.Group();
        Im.TextWrapped(
            "继承是一种跨多个合集使用模组基线的方法，只需添加单个模组而不需要修改所有合集。"u8);
        Im.TextWrapped(
            "你可以在左边的组合框中添加合集名称来设置继承关系。\n继承顺序很重要，拖动已添加的合集名称来对它们进行重新排序。\n你也可以将合集名称拖拽到垃圾桶图标上进行删除操作。"u8);
    }

    private static void DrawHelpPopup()
        => ImEx.HelpPopup("InheritanceHelp"u8, new Vector2(1000 * Im.Style.GlobalScale, 20 * Im.Style.TextHeightWithSpacing), () =>
        {
            Im.Line.New();
            Im.Text("合集中的每个模组都可以具有三种基础状态：‘启用’，‘禁用’，‘未配置’。"u8);
            Im.BulletText("如果模组是‘启用’或‘禁用’，不管该合集有没有继承自其他合集，此模组都只会使用自己的设置。"u8);
            Im.BulletText(
                "如果模组是‘未配置’的，则按此处显示的顺序来检查那些有继承的合集，包括次级继承。"u8);
            Im.BulletText(
                "如果发现某个被继承合集中的模组为‘启用’或‘禁用’，来自该合集的设置将被使用。"u8);
            Im.BulletText("如果未找到此类合集，则该模组将被视为已禁用。"u8);
            Im.BulletText(
                "左侧框中高亮显示的合集（注意其颜色），不会生效，因为它已经在继承合集的次级继承中了。"u8);
            Im.Line.New();
            Im.Text("例子"u8);
            Im.BulletText("合集A：启用了两个模组 - Bibo+和紧身小背心。"u8);
            Im.BulletText(
                "合集B：继承自A，未配置Bibo+，启用了紧身小背心但设置与A不同。"u8);
            Im.BulletText("合集C：继承自A，禁用Bibo+，未配置紧身小背心。"u8);
            Im.BulletText("合集D：继承自C，其次继承自B，模组均未配置。"u8);
            using var indent = Im.Indent();
            Im.BulletText("合集B - 使用来自A的Bibo+设置和自己的紧身小背心设置。"u8);
            Im.BulletText("合集C - 禁用Bibo+，使用A的紧身小背心设置。"u8);
            Im.BulletText(
                "合集D - 禁用Bibo+，使用A的紧身小背心设置而不是B的。因为是以D -> (C -> A) -> (B -> A)的顺序来遍历合集。"u8);
        });


    /// <summary>
    /// If an inherited collection is expanded,
    /// draw all its flattened, distinct children in order with a tree-line.
    /// </summary>
    private void DrawInheritedChildren(ModCollection collection)
    {
        using var id     = Im.Id.Push(collection.Identity.Index);
        using var indent = Im.Indent();

        // Get start point for the lines (top of the selector).
        // Tree line stuff.
        var lineStart = Im.Cursor.ScreenPosition;
        var offsetX   = -Im.Style.IndentSpacing + Im.Style.TreeNodeToLabelSpacing / 2;
        var drawList  = Im.Window.DrawList.Shape;
        var lineSize  = Math.Max(0, Im.Style.IndentSpacing - 9 * Im.Style.GlobalScale);
        lineStart.X += offsetX;
        lineStart.Y -= 2 * Im.Style.GlobalScale;
        var lineEnd = lineStart;

        // Skip the collection itself.
        foreach (var inheritance in collection.Inheritance.FlatHierarchy.Skip(1))
        {
            // Draw the child, already seen collections are colored as conflicts.
            using var color = ImGuiColor.Text.Push(ColorId.HandledConflictMod.Value(),
                _seenInheritedCollections.Contains(inheritance));
            _seenInheritedCollections.Add(inheritance);

            Im.Tree.Leaf($"{Name(inheritance)}###{inheritance.Identity.Id}", TreeNodeFlags.NoTreePushOnOpen);
            var (minRect, maxRect) = (Im.Item.UpperLeftCorner, Im.Item.LowerRightCorner);
            DrawInheritanceTreeClicks(inheritance, false);

            // Tree line stuff.
            if (minRect.X == 0)
                continue;

            // Draw the notch and increase the line length.
            var midPoint = (minRect.Y + maxRect.Y) / 2f - 1f;
            drawList.Line(lineStart with { Y = midPoint }, new Vector2(lineStart.X + lineSize, midPoint), Colors.MetaInfoText,
                Im.Style.GlobalScale);
            lineEnd.Y = midPoint;
        }

        // Finally, draw the folder line.
        drawList.Line(lineStart, lineEnd, Colors.MetaInfoText, Im.Style.GlobalScale);
    }

    /// <summary> Draw a single primary inherited collection. </summary>
    private void DrawInheritance(ModCollection collection)
    {
        using var color = ImGuiColor.Text.Push(ColorId.HandledConflictMod.Value(),
            _seenInheritedCollections.Contains(collection));
        _seenInheritedCollections.Add(collection);
        using var tree = Im.Tree.Node($"{Name(collection)}###{collection.Identity.Name}", TreeNodeFlags.NoTreePushOnOpen);
        color.Pop();
        DrawInheritanceTreeClicks(collection, true);
        DrawInheritanceDropSource(collection);
        DrawInheritanceDropTarget(collection);

        if (tree)
            DrawInheritedChildren(collection);
        else
            // We still want to keep track of conflicts.
            _seenInheritedCollections.UnionWith(collection.Inheritance.FlatHierarchy);
    }

    /// <summary> Draw the list box containing the current inheritance information. </summary>
    private void DrawCurrentCollectionInheritance()
    {
        using var list = Im.ListBox.Begin("##inheritanceList"u8,
            new Vector2(UiHelpers.InputTextMinusButton, Im.Style.TextHeightWithSpacing * InheritedCollectionHeight));
        if (!list)
            return;

        _seenInheritedCollections.Clear();
        _seenInheritedCollections.Add(_active.Current);
        foreach (var collection in _active.Current.Inheritance.DirectlyInheritsFrom.ToList())
            DrawInheritance(collection);
    }

    /// <summary> Draw a drag and drop button to delete. </summary>
    private void DrawInheritanceTrashButton()
    {
        var size        = UiHelpers.IconButtonSize with { Y = Im.Style.TextHeightWithSpacing * InheritedCollectionHeight };
        var buttonColor = Im.Style[ImGuiColor.Button];
        // Prevent hovering from highlighting the button.
        using var color = ImGuiColor.ButtonActive.Push(buttonColor)
            .Push(ImGuiColor.ButtonHovered, buttonColor);
        ImEx.Icon.Button(LunaStyle.DeleteIcon, "将主继承拖到此处可将其从列表中删除。"u8, size);

        using var target = Im.DragDrop.Target();
        if (target.Success && target.IsDropping(InheritanceDragDropLabel))
            _inheritanceAction = (_active.Current.Inheritance.DirectlyInheritsFrom.IndexOf(_movedInheritance!), -1);
    }

    /// <summary>
    /// Set the current collection, or delete or move an inheritance if the action was triggered during iteration.
    /// Can not be done during iteration to keep collections unchanged.
    /// </summary>
    private void DelayedActions()
    {
        if (_newCurrentCollection != null)
        {
            _active.SetCollection(_newCurrentCollection, CollectionType.Current);
            _newCurrentCollection = null;
        }

        if (_inheritanceAction == null)
            return;

        if (_inheritanceAction.Value.Item1 >= 0)
        {
            if (_inheritanceAction.Value.Item2 == -1)
                _inheritance.RemoveInheritance(_active.Current, _inheritanceAction.Value.Item1);
            else
                _inheritance.MoveInheritance(_active.Current, _inheritanceAction.Value.Item1, _inheritanceAction.Value.Item2);
        }

        _inheritanceAction = null;
    }

    /// <summary>
    /// Draw the selector to add new inheritances.
    /// The add button is only available if the selected collection can actually be added.
    /// </summary>
    private void DrawNewInheritanceSelection()
    {
        DrawNewInheritanceCombo();
        Im.Line.Same();
        var inheritance = InheritanceManager.CheckValidInheritance(_active.Current, _newInheritance);
        var tt = inheritance switch
        {
            InheritanceManager.ValidInheritance.Empty     => "没有可以继承的合集。",
            InheritanceManager.ValidInheritance.Valid     => $"使选择的合集继承自这个合集。",
            InheritanceManager.ValidInheritance.Self      => "合集不能自我继承。",
            InheritanceManager.ValidInheritance.Contained => "已经从这个合集继承了。",
            InheritanceManager.ValidInheritance.Circle    => "从这个合集继承会导致死循环。",
            _                                             => string.Empty,
        };
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, inheritance is not InheritanceManager.ValidInheritance.Valid)
         && _inheritance.AddInheritance(_active.Current, _newInheritance!))
            _newInheritance = null;

        if (inheritance != InheritanceManager.ValidInheritance.Valid)
            _newInheritance = null;
    }

    /// <summary>
    /// Draw the combo to select new potential inheritances.
    /// Only valid inheritances are drawn in the preview, or nothing if no inheritance is available.
    /// </summary>
    private void DrawNewInheritanceCombo()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButton);
        _newInheritance ??= _collections.FirstOrDefault(c
                => c != _active.Current && !_active.Current.Inheritance.DirectlyInheritsFrom.Contains(c))
         ?? ModCollection.Empty;
        using var combo = Im.Combo.Begin("##newInheritance"u8, Name(_newInheritance));
        if (!combo)
            return;

        foreach (var collection in _collections
                     .Where(c => InheritanceManager.CheckValidInheritance(_active.Current, c) == InheritanceManager.ValidInheritance.Valid)
                     .OrderBy(c => c.Identity.Name))
        {
            if (Im.Selectable(Name(collection), _newInheritance == collection))
                _newInheritance = collection;
        }
    }

    /// <summary>
    /// Move an inherited collection when dropped onto another.
    /// Move is delayed due to collection changes.
    /// </summary>
    private void DrawInheritanceDropTarget(ModCollection collection)
    {
        using var target = Im.DragDrop.Target();
        if (!target.Success || !target.IsDropping(InheritanceDragDropLabel))
            return;

        if (_movedInheritance != null)
        {
            var idx1 = _active.Current.Inheritance.DirectlyInheritsFrom.IndexOf(_movedInheritance);
            var idx2 = _active.Current.Inheritance.DirectlyInheritsFrom.IndexOf(collection);
            if (idx1 >= 0 && idx2 >= 0)
                _inheritanceAction = (idx1, idx2);
        }

        _movedInheritance = null;
    }

    /// <summary> Move an inherited collection. </summary>
    private void DrawInheritanceDropSource(ModCollection collection)
    {
        using var source = Im.DragDrop.Source();
        if (!source)
            return;

        source.SetPayload(InheritanceDragDropLabel);
        _movedInheritance = collection;
        Im.Text($"移动 {(_movedInheritance != null ? Name(_movedInheritance) : "未知")}...");
    }

    /// <summary>
    /// Ctrl + Right-Click -> Switch current collection to this (for all).
    /// Ctrl + Shift + Right-Click -> Delete this inheritance (only if withDelete).
    /// Deletion is delayed due to collection changes.
    /// </summary>
    private void DrawInheritanceTreeClicks(ModCollection collection, bool withDelete)
    {
        if (Im.Io.KeyControl && Im.Item.RightClicked())
        {
            if (withDelete && Im.Io.KeyShift)
                _inheritanceAction = (_active.Current.Inheritance.DirectlyInheritsFrom.IndexOf(collection), -1);
            else
                _newCurrentCollection = collection;
        }

        Im.Tooltip.OnHover(
            $"Ctrl + 右键单击 将选择的合集切换到这个合集。{(withDelete ? "\nCtrl + Shift + 右键单击 删除这个继承。" : StringU8.Empty)}");
    }

    private string Name(ModCollection collection)
        => incognito.IncognitoMode ? collection.Identity.AnonymizedName : collection.Identity.Name;
}
