using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Interop.PathResolving;
using Penumbra.UI.CollectionTab;
using Penumbra.UI.ModsTab;

namespace Penumbra.UI.Classes;

public class CollectionSelectHeader
{
    private readonly CollectionCombo       _collectionCombo;
    private readonly ActiveCollections     _activeCollections;
    private readonly TutorialService       _tutorial;
    private readonly ModFileSystemSelector _selector;
    private readonly CollectionResolver    _resolver;

    public CollectionSelectHeader(CollectionManager collectionManager, TutorialService tutorial, ModFileSystemSelector selector,
        CollectionResolver resolver)
    {
        _tutorial          = tutorial;
        _selector          = selector;
        _resolver          = resolver;
        _activeCollections = collectionManager.Active;
        _collectionCombo   = new CollectionCombo(collectionManager, () => collectionManager.Storage.OrderBy(c => c.Name).ToList());
    }

    /// <summary> Draw the header line that can quick switch between collections. </summary>
    public void Draw(bool spacing)
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 0)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(0, spacing ? ImGui.GetStyle().ItemSpacing.Y : 0));
        var comboWidth = ImGui.GetContentRegionAvail().X / 4f;
        var buttonSize = new Vector2(comboWidth * 3f / 4f, 0f);
        using (var _ = ImRaii.Group())
        {
            DrawCollectionButton(buttonSize, GetDefaultCollectionInfo(), 1);
            DrawCollectionButton(buttonSize, GetInterfaceCollectionInfo(), 2);
            DrawCollectionButton(buttonSize, GetPlayerCollectionInfo(), 3);
            DrawCollectionButton(buttonSize, GetInheritedCollectionInfo(), 4);

            _collectionCombo.Draw("##collectionSelector", comboWidth, ColorId.SelectedCollection.Value());
        }

        _tutorial.OpenTutorial(BasicTutorialSteps.CollectionSelectors);

        if (!_activeCollections.CurrentCollectionInUse)
            ImGuiUtil.DrawTextButton("当前选中的合集未在任何地方使用。", -Vector2.UnitX, Colors.PressEnterWarningBg);
    }

    private enum CollectionState
    {
        Empty,
        Selected,
        Unavailable,
        Available,
    }

    private CollectionState CheckCollection(ModCollection? collection, bool inheritance = false)
    {
        if (collection == null)
            return CollectionState.Unavailable;
        if (collection == ModCollection.Empty)
            return CollectionState.Empty;
        if (collection == _activeCollections.Current)
            return inheritance ? CollectionState.Unavailable : CollectionState.Selected;

        return CollectionState.Available;
    }

    private (ModCollection?, string, string, bool) GetDefaultCollectionInfo()
    {
        var collection = _activeCollections.Default;
        return CheckCollection(collection) switch
        {
            CollectionState.Empty => (collection, "无", "基础合集已被配置为不使用模组。", true),
            CollectionState.Selected => (collection, collection.Name,
                "已将配置的基础合集选择为当前操作的合集。", true),
            CollectionState.Available => (collection, collection.Name,
                $"选择被配置给基础合集使用的合集[{collection.Name}]作为当前可操作的合集。", false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private (ModCollection?, string, string, bool) GetPlayerCollectionInfo()
    {
        var collection = _resolver.PlayerCollection();
        return CheckCollection(collection) switch
        {
            CollectionState.Empty => (collection, "无", "加载的玩家角色已被配置为不使用模组。", true),
            CollectionState.Selected => (collection, collection.Name,
                "配置为用于当前玩家角色的合集已被选择为当前操作合集。", true),
            CollectionState.Available => (collection, collection.Name,
                $"选择分配给当前玩家的合集[{collection.Name}]作为当前可操作的合集。", false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private (ModCollection?, string, string, bool) GetInterfaceCollectionInfo()
    {
        var collection = _activeCollections.Interface;
        return CheckCollection(collection) switch
        {
            CollectionState.Empty => (collection, "无", "界面合集已被配置为不使用模组。", true),
            CollectionState.Selected => (collection, collection.Name,
                "配置为用于游戏界面的合集已被选择为当前操作合集。", true),
            CollectionState.Available => (collection, collection.Name,
                $"选择分配给界面的合集[{collection.Name}]作为当前可操作的合集。", false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private (ModCollection?, string, string, bool) GetInheritedCollectionInfo()
    {
        var collection = _selector.Selected == null ? null : _selector.SelectedSettingCollection;
        return CheckCollection(collection, true) switch
        {
            CollectionState.Unavailable => (null, "未继承",
                "选中的模组的设置未继承自其他合集。", true),
            CollectionState.Available => (collection, collection!.Name,
                $"当前选中模组设置继承自[{collection!.Name}]，点击切换到此合集作为当前可操作的合集。",
                false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private void DrawCollectionButton(Vector2 buttonWidth, (ModCollection?, string, string, bool) tuple, int id)
    {
        var (collection, name, tooltip, disabled) = tuple;
        using var _ = ImRaii.PushId(id);
        if (ImGuiUtil.DrawDisabledButton(name, buttonWidth, tooltip, disabled))
            _activeCollections.SetCollection(collection!, CollectionType.Current);
        ImGui.SameLine();
    }
}
