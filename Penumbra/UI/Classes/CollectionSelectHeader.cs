using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Interop.PathResolving;
using Penumbra.Mods;
using Penumbra.UI.CollectionTab;

namespace Penumbra.UI.Classes;

public class CollectionSelectHeader : IUiService
{
    private readonly CollectionCombo    _collectionCombo;
    private readonly ActiveCollections  _activeCollections;
    private readonly TutorialService    _tutorial;
    private readonly ModSelection       _selection;
    private readonly CollectionResolver _resolver;
    private readonly Configuration      _config;

    public CollectionSelectHeader(CollectionManager collectionManager, TutorialService tutorial, ModSelection selection,
        CollectionResolver resolver, Configuration config)
    {
        _tutorial          = tutorial;
        _selection         = selection;
        _resolver          = resolver;
        _config            = config;
        _activeCollections = collectionManager.Active;
        _collectionCombo   = new CollectionCombo(collectionManager, () => collectionManager.Storage.OrderBy(c => c.Identity.Name).ToList());
    }

    /// <summary> Draw the header line that can quick switch between collections. </summary>
    public void Draw(bool spacing)
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 0)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(0, spacing ? ImGui.GetStyle().ItemSpacing.Y : 0));
        DrawTemporaryCheckbox();
        ImGui.SameLine();
        var comboWidth = ImGui.GetContentRegionAvail().X / 4f;
        var buttonSize = new Vector2(comboWidth * 3f / 4f, 0f);
        using (var _ = ImRaii.Group())
        {
            DrawCollectionButton(buttonSize, GetDefaultCollectionInfo(),   1);
            DrawCollectionButton(buttonSize, GetInterfaceCollectionInfo(), 2);
            DrawCollectionButton(buttonSize, GetPlayerCollectionInfo(),    3);
            DrawCollectionButton(buttonSize, GetInheritedCollectionInfo(), 4);

            _collectionCombo.Draw("##collectionSelector", comboWidth, ColorId.SelectedCollection.Value());
        }

        _tutorial.OpenTutorial(BasicTutorialSteps.CollectionSelectors);

        if (!_activeCollections.CurrentCollectionInUse)
            ImGuiUtil.DrawTextButton("当前选中的合集未在任何地方使用。", -Vector2.UnitX, Colors.PressEnterWarningBg);
    }

    private void DrawTemporaryCheckbox()
    {
        var hold = _config.IncognitoModifier.IsActive();
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, ImUtf8.GlobalScale))
        {
            var tint = _config.DefaultTemporaryMode
                ? ImGuiCol.Text.Tinted(ColorId.TemporaryModSettingsTint)
                : ImGui.GetColorU32(ImGuiCol.TextDisabled);
            using var color = ImRaii.PushColor(ImGuiCol.ButtonHovered, ImGui.GetColorU32(ImGuiCol.FrameBg), !hold)
                .Push(ImGuiCol.ButtonActive, ImGui.GetColorU32(ImGuiCol.FrameBg), !hold)
                .Push(ImGuiCol.Border,       tint,                                _config.DefaultTemporaryMode);
            if (ImUtf8.IconButton(FontAwesomeIcon.Stopwatch, ""u8, default, false, tint, ImGui.GetColorU32(ImGuiCol.FrameBg)) && hold)
            {
                _config.DefaultTemporaryMode = !_config.DefaultTemporaryMode;
                _config.Save();
            }
        }

        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled,
            "切换临时设置模式，在此模式下，您所做的所有更改将首先创建为临时设置，如果需要，可以将其设为永久设置。\n"u8);
        if (!hold)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"按住 {_config.DeleteModModifier} 并点击以切换。");
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
            CollectionState.Selected => (collection, collection.Identity.Name,
                "已将配置的基础合集选择为当前操作的合集。", true),
            CollectionState.Available => (collection, collection.Identity.Name,
                $"选择被配置给基础合集使用的合集[{collection.Identity.Name}]作为当前可操作的合集。", false),
            _ => throw new Exception("不可能发生。"),
        };
    }

    private (ModCollection?, string, string, bool) GetPlayerCollectionInfo()
    {
        var collection = _resolver.PlayerCollection();
        return CheckCollection(collection) switch
        {
            CollectionState.Empty => (collection, "无", "加载的玩家角色已被配置为不使用模组。", true),
            CollectionState.Selected => (collection, collection.Identity.Name,
                "配置为用于当前玩家角色的合集已被选择为当前操作合集。", true),
            CollectionState.Available => (collection, collection.Identity.Name,
                $"选择分配给当前玩家的合集[{collection.Identity.Name}]作为当前可操作的合集。",
                false),
            _ => throw new Exception("不可能发生。"),
        };
    }

    private (ModCollection?, string, string, bool) GetInterfaceCollectionInfo()
    {
        var collection = _activeCollections.Interface;
        return CheckCollection(collection) switch
        {
            CollectionState.Empty => (collection, "无", "界面合集已被配置为不使用模组。", true),
            CollectionState.Selected => (collection, collection.Identity.Name,
                "配置为用于游戏界面的合集已被选择为当前操作合集。", true),
            CollectionState.Available => (collection, collection.Identity.Name,
                $"选择分配给界面的合集[{collection.Identity.Name}]作为当前可操作的合集。", false),
            _ => throw new Exception("不可能发生。"),
        };
    }

    private (ModCollection?, string, string, bool) GetInheritedCollectionInfo()
    {
        var collection = _selection.Mod == null ? null : _selection.Collection;
        return CheckCollection(collection, true) switch
        {
            CollectionState.Unavailable => (null, "未继承",
                "选中的模组的设置未继承自其他合集。", true),
            CollectionState.Available => (collection, collection!.Identity.Name,
                $"当前选中模组设置继承自[{collection!.Identity.Name}]，点击切换到此合集作为当前可操作的合集。",
                false),
            _ => throw new Exception("不可能发生。"),
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
