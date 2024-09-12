using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.UI.Classes;

namespace Penumbra.UI.CollectionTab;

public class InheritanceUi(CollectionManager collectionManager, IncognitoService incognito) : IUiService
{
    private const int    InheritedCollectionHeight = 9;
    private const string InheritanceDragDropLabel  = "##InheritanceMove";

    private readonly CollectionStorage  _collections = collectionManager.Storage;
    private readonly ActiveCollections  _active      = collectionManager.Active;
    private readonly InheritanceManager _inheritance = collectionManager.Inheritances;

    /// <summary> Draw the whole inheritance block. </summary>
    public void Draw()
    {
        using var id = ImRaii.PushId("##Inheritance");
        ImGuiUtil.DrawColoredText(($"{TutorialService.SelectedCollection} ", 0),
            (Name(_active.Current), ColorId.SelectedCollection.Value() | 0xFF000000), (" 继承自：", 0));
        ImGui.Dummy(Vector2.One);

        DrawCurrentCollectionInheritance();
        ImGui.SameLine();
        DrawInheritanceTrashButton();
        ImGui.SameLine();
        DrawRightText();

        DrawNewInheritanceSelection();
        ImGui.SameLine();
        if (ImGui.Button("查看关于继承功能的更多说明", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
            ImGui.OpenPopup("InheritanceHelp");

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
        using var group = ImRaii.Group();
        ImGuiUtil.TextWrapped(
            "继承是如果你想添加单个模组，不需要去修改所有合集就能跨合集使用模组基线的方法。" );
        ImGuiUtil.TextWrapped(
            "你可以在左边的组合框中添加合集名称来设置继承关系。\n继承顺序很重要，拖动已添加的合集名称来对它们进行重新排序。\n你也可以将合集名称拖拽到垃圾桶图标上进行删除操作。");
    }

    private static void DrawHelpPopup()
        => ImGuiUtil.HelpPopup("InheritanceHelp", new Vector2(700 * UiHelpers.Scale, 20 * ImGui.GetTextLineHeightWithSpacing()), () =>
        {
            ImGui.NewLine();
        	ImGui.TextUnformatted( "合集中的每个模组都可以具有三种基础状态：‘启用’，‘禁用’，‘未配置’。" );
        	ImGui.BulletText( "如果模组是‘启用’或‘禁用’，不管该合集有没有继承自其他合集，此模组都只会使用自己的设置。" );
            ImGui.BulletText(
            	"如果模组是‘未配置’的，则按此处显示的顺序来检查那些有继承的合集，包括次级继承。" );
            ImGui.BulletText(
	            "如果发现某个被继承合集中的模组为‘启用’或‘禁用’，来自该合集的设置将被使用。" );
	        ImGui.BulletText( "如果未找到此类合集，则该模组将被视为已禁用。" );
            ImGui.BulletText(
	            "左侧框中突出显示的合集（注意其颜色），不会生效，因为它已经在继承合集的次级继承中了。" );
            ImGui.NewLine();
	        ImGui.TextUnformatted( "例子" );
	        ImGui.BulletText("合集A：启用了两个模组 - Bibo+和紧身小背心。");
            ImGui.BulletText(
	            "合集B：继承自A，未配置Bibo+，启用了紧身小背心但设置与A不同。" );
	        ImGui.BulletText( "合集C：继承自A，禁用Bibo+，未配置紧身小背心。" );
	        ImGui.BulletText( "合集D：继承自C，其次继承自B，模组均未配置。" );
            using var indent = ImRaii.PushIndent();
	        ImGui.BulletText( "合集B - 使用来自A的Bibo+设置和自己的紧身小背心设置。" );
	        ImGui.BulletText( "合集C - 禁用Bibo+，使用A的紧身小背心设置。" );
            ImGui.BulletText(
	            "合集D - 禁用Bibo+，使用A的紧身小背心设置而不是B的。因为是以D -> (C -> A) -> (B -> A)的顺序来遍历合集。" );
        });


    /// <summary>
    /// If an inherited collection is expanded,
    /// draw all its flattened, distinct children in order with a tree-line.
    /// </summary>
    private void DrawInheritedChildren(ModCollection collection)
    {
        using var id     = ImRaii.PushId(collection.Index);
        using var indent = ImRaii.PushIndent();

        // Get start point for the lines (top of the selector).
        // Tree line stuff.
        var lineStart = ImGui.GetCursorScreenPos();
        var offsetX   = -ImGui.GetStyle().IndentSpacing + ImGui.GetTreeNodeToLabelSpacing() / 2;
        var drawList  = ImGui.GetWindowDrawList();
        var lineSize  = Math.Max(0, ImGui.GetStyle().IndentSpacing - 9 * UiHelpers.Scale);
        lineStart.X += offsetX;
        lineStart.Y -= 2 * UiHelpers.Scale;
        var lineEnd = lineStart;

        // Skip the collection itself.
        foreach (var inheritance in collection.GetFlattenedInheritance().Skip(1))
        {
            // Draw the child, already seen collections are colored as conflicts.
            using var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.HandledConflictMod.Value(),
                _seenInheritedCollections.Contains(inheritance));
            _seenInheritedCollections.Add(inheritance);

            ImRaii.TreeNode($"{Name(inheritance)}###{inheritance.Id}",
                ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet);
            var (minRect, maxRect) = (ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
            DrawInheritanceTreeClicks(inheritance, false);

            // Tree line stuff.
            if (minRect.X == 0)
                continue;

            // Draw the notch and increase the line length.
            var midPoint = (minRect.Y + maxRect.Y) / 2f - 1f;
            drawList.AddLine(lineStart with { Y = midPoint }, new Vector2(lineStart.X + lineSize, midPoint), Colors.MetaInfoText,
                UiHelpers.Scale);
            lineEnd.Y = midPoint;
        }

        // Finally, draw the folder line.
        drawList.AddLine(lineStart, lineEnd, Colors.MetaInfoText, UiHelpers.Scale);
    }

    /// <summary> Draw a single primary inherited collection. </summary>
    private void DrawInheritance(ModCollection collection)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.HandledConflictMod.Value(),
            _seenInheritedCollections.Contains(collection));
        _seenInheritedCollections.Add(collection);
        using var tree = ImRaii.TreeNode($"{Name(collection)}###{collection.Name}", ImGuiTreeNodeFlags.NoTreePushOnOpen);
        color.Pop();
        DrawInheritanceTreeClicks(collection, true);
        DrawInheritanceDropSource(collection);
        DrawInheritanceDropTarget(collection);

        if (tree)
            DrawInheritedChildren(collection);
        else
            // We still want to keep track of conflicts.
            _seenInheritedCollections.UnionWith(collection.GetFlattenedInheritance());
    }

    /// <summary> Draw the list box containing the current inheritance information. </summary>
    private void DrawCurrentCollectionInheritance()
    {
        using var list = ImRaii.ListBox("##inheritanceList",
            new Vector2(UiHelpers.InputTextMinusButton, ImGui.GetTextLineHeightWithSpacing() * InheritedCollectionHeight));
        if (!list)
            return;

        _seenInheritedCollections.Clear();
        _seenInheritedCollections.Add(_active.Current);
        foreach (var collection in _active.Current.DirectlyInheritsFrom.ToList())
            DrawInheritance(collection);
    }

    /// <summary> Draw a drag and drop button to delete. </summary>
    private void DrawInheritanceTrashButton()
    {
        var size        = UiHelpers.IconButtonSize with { Y = ImGui.GetTextLineHeightWithSpacing() * InheritedCollectionHeight };
        var buttonColor = ImGui.GetColorU32(ImGuiCol.Button);
        // Prevent hovering from highlighting the button.
        using var color = ImRaii.PushColor(ImGuiCol.ButtonActive, buttonColor)
            .Push(ImGuiCol.ButtonHovered, buttonColor);
        ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), size,
            "将主继承拖到此处可将其从列表中删除。", false, true);

        using var target = ImRaii.DragDropTarget();
        if (target.Success && ImGuiUtil.IsDropping(InheritanceDragDropLabel))
            _inheritanceAction = (_active.Current.DirectlyInheritsFrom.IndexOf(_movedInheritance!), -1);
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
        ImGui.SameLine();
        var inheritance = InheritanceManager.CheckValidInheritance(_active.Current, _newInheritance);
        var tt = inheritance switch
        {
            InheritanceManager.ValidInheritance.Empty     => "没有可以继承的合集。",
            InheritanceManager.ValidInheritance.Valid     => $"使{TutorialService.SelectedCollection}继承自这个合集。",
            InheritanceManager.ValidInheritance.Self      => "合集不能自我继承。",
            InheritanceManager.ValidInheritance.Contained => "已经从这个合集继承了。",
            InheritanceManager.ValidInheritance.Circle    => "从这个合集继承会导致死循环。",
            _                                             => string.Empty,
        };
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), UiHelpers.IconButtonSize, tt,
                inheritance != InheritanceManager.ValidInheritance.Valid, true)
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
        ImGui.SetNextItemWidth(UiHelpers.InputTextMinusButton);
        _newInheritance ??= _collections.FirstOrDefault(c
                => c != _active.Current && !_active.Current.DirectlyInheritsFrom.Contains(c))
         ?? ModCollection.Empty;
        using var combo = ImRaii.Combo("##newInheritance", Name(_newInheritance));
        if (!combo)
            return;

        foreach (var collection in _collections
                     .Where(c => InheritanceManager.CheckValidInheritance(_active.Current, c) == InheritanceManager.ValidInheritance.Valid)
                     .OrderBy(c => c.Name))
        {
            if (ImGui.Selectable(Name(collection), _newInheritance == collection))
                _newInheritance = collection;
        }
    }

    /// <summary>
    /// Move an inherited collection when dropped onto another.
    /// Move is delayed due to collection changes.
    /// </summary>
    private void DrawInheritanceDropTarget(ModCollection collection)
    {
        using var target = ImRaii.DragDropTarget();
        if (!target.Success || !ImGuiUtil.IsDropping(InheritanceDragDropLabel))
            return;

        if (_movedInheritance != null)
        {
            var idx1 = _active.Current.DirectlyInheritsFrom.IndexOf(_movedInheritance);
            var idx2 = _active.Current.DirectlyInheritsFrom.IndexOf(collection);
            if (idx1 >= 0 && idx2 >= 0)
                _inheritanceAction = (idx1, idx2);
        }

        _movedInheritance = null;
    }

    /// <summary> Move an inherited collection. </summary>
    private void DrawInheritanceDropSource(ModCollection collection)
    {
        using var source = ImRaii.DragDropSource();
        if (!source)
            return;

        ImGui.SetDragDropPayload(InheritanceDragDropLabel, nint.Zero, 0);
        _movedInheritance = collection;
        ImGui.TextUnformatted($"移动 {(_movedInheritance != null ? Name(_movedInheritance) : "未知")}...");
    }

    /// <summary>
    /// Ctrl + Right-Click -> Switch current collection to this (for all).
    /// Ctrl + Shift + Right-Click -> Delete this inheritance (only if withDelete).
    /// Deletion is delayed due to collection changes.
    /// </summary>
    private void DrawInheritanceTreeClicks(ModCollection collection, bool withDelete)
    {
        if (ImGui.GetIO().KeyCtrl && ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            if (withDelete && ImGui.GetIO().KeyShift)
                _inheritanceAction = (_active.Current.DirectlyInheritsFrom.IndexOf(collection), -1);
            else
                _newCurrentCollection = collection;
        }

        ImGuiUtil.HoverTooltip($"Ctrl + 右键单击 从{TutorialService.SelectedCollection}切换到这个合集。"
          + (withDelete ? "\nCtrl + Shift + 右键单击来移除这个继承。" : string.Empty));
    }

    private string Name(ModCollection collection)
        => incognito.IncognitoMode ? collection.AnonymizedName : collection.Name;
}
