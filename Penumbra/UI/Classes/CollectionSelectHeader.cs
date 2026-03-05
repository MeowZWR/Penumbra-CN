using Dalamud.Interface;
using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Interop.PathResolving;
using Penumbra.Mods;
using Penumbra.UI.CollectionTab;
using CollectionTuple =
    ImSharp.RefTuple<Penumbra.Collections.ModCollection?, ImSharp.Utf8StringHandler<ImSharp.LabelStringHandlerBuffer>,
        ImSharp.Utf8StringHandler<ImSharp.TextStringHandlerBuffer>, bool>;

namespace Penumbra.UI.Classes;

public class CollectionSelectHeader(
    CollectionManager collectionManager,
    TutorialService tutorial,
    ModSelection selection,
    CollectionResolver resolver,
    Configuration config,
    CollectionCombo combo)
    : IHeader
{
    private readonly        ActiveCollections _activeCollections = collectionManager.Active;
    private static readonly AwesomeIcon       Icon               = FontAwesomeIcon.Stopwatch;

    /// <summary> Draw the header line that can quick switch between collections. </summary>
    public void Draw(bool spacing)
    {
        using var style = ImStyleSingle.FrameRounding.Push(0)
            .Push(ImStyleDouble.ItemSpacing, new Vector2(0, spacing ? Im.Style.ItemSpacing.Y : 0));
        DrawTemporaryCheckbox();
        Im.Line.Same();
        var comboWidth = Im.ContentRegion.Available.X / 4f;
        var buttonSize = new Vector2(comboWidth * 3f / 4f, 0f);
        using (var _ = Im.Group())
        {
            DrawCollectionButton(buttonSize, GetDefaultCollectionInfo(),   1);
            DrawCollectionButton(buttonSize, GetInterfaceCollectionInfo(), 2);
            DrawCollectionButton(buttonSize, GetPlayerCollectionInfo(),    3);
            DrawCollectionButton(buttonSize, GetInheritedCollectionInfo(), 4);

            combo.Draw("##collectionSelector"u8, comboWidth, ColorId.SelectedCollection.Value());
        }

        tutorial.OpenTutorial(BasicTutorialSteps.CollectionSelectors);

        if (!_activeCollections.CurrentCollectionInUse)
            ImEx.TextFramed("当前选中的合集未在任何地方使用。"u8, Im.ContentRegion.Available with { Y = 0 },
                Colors.PressEnterWarningBg);
    }

    private void DrawTemporaryCheckbox()
    {
        var hold = config.IncognitoModifier.IsActive();
        var tint = config.DefaultTemporaryMode
            ? Rgba32.TintColor(Im.Style[ImGuiColor.Text], ColorId.TemporaryModSettingsTint.Value().ToVector())
            : Im.Style[ImGuiColor.TextDisabled];
        var frameBg = Im.Style[ImGuiColor.FrameBackground];

        using (ImStyleBorder.Frame.Push(tint)
                   .Push(ImGuiColor.ButtonHovered, frameBg, !hold)
                   .Push(ImGuiColor.ButtonActive,  frameBg, !hold))
        {
            if (ImEx.Icon.Button(Icon, buttonColor: frameBg, textColor: tint) && hold)
            {
                config.DefaultTemporaryMode = !config.DefaultTemporaryMode;
                config.Save();
            }
        }

        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled,
            "切换临时设置模式，在此模式下，您所做的所有更改将首先创建为临时设置，如果需要，可以将其设为永久设置。"u8, true);
        if (!hold)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"\n按住 {config.IncognitoModifier} 并点击以切换。", true);
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
        if (collection is null)
            return CollectionState.Unavailable;
        if (collection == ModCollection.Empty)
            return CollectionState.Empty;
        if (collection == _activeCollections.Current)
            return inheritance ? CollectionState.Unavailable : CollectionState.Selected;

        return CollectionState.Available;
    }

    private CollectionTuple GetDefaultCollectionInfo()
    {
        var collection = _activeCollections.Default;
        return CheckCollection(collection) switch
        {
            CollectionState.Empty => new CollectionTuple(collection, "无"u8, "基础合集已被配置为不使用模组。"u8, true),
            CollectionState.Selected => new CollectionTuple(collection, collection.Identity.Name,
                "已将配置的基础合集选择为当前操作的合集。"u8, true),
            CollectionState.Available => new CollectionTuple(collection, collection.Identity.Name,
                $"选择被配置给基础合集使用的合集[{collection.Identity.Name}]作为当前可操作的合集。", false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private CollectionTuple GetPlayerCollectionInfo()
    {
        var collection = resolver.PlayerCollection();
        return CheckCollection(collection) switch
        {
            CollectionState.Empty => new CollectionTuple(collection, "无"u8, "加载的玩家角色已被配置为不使用模组。"u8,
                true),
            CollectionState.Selected => new CollectionTuple(collection, collection.Identity.Name,
                "配置为用于当前玩家角色的合集已被选择为当前操作合集。"u8, true),
            CollectionState.Available => new CollectionTuple(collection, collection.Identity.Name,
                $"选择分配给当前玩家的合集[{collection.Identity.Name}]作为当前可操作的合集。",
                false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private CollectionTuple GetInterfaceCollectionInfo()
    {
        var collection = _activeCollections.Interface;
        return CheckCollection(collection) switch
        {
            CollectionState.Empty => new CollectionTuple(collection, "无"u8, "界面合集已被配置为不使用模组。"u8,
                true),
            CollectionState.Selected => new CollectionTuple(collection, collection.Identity.Name,
                "配置为用于游戏界面的合集已被选择为当前操作合集。"u8, true),
            CollectionState.Available => new CollectionTuple(collection, collection.Identity.Name,
                $"选择分配给界面的合集[{collection.Identity.Name}]作为当前可操作的合集。", false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private CollectionTuple GetInheritedCollectionInfo()
    {
        var collection = selection.Mod is null ? null : selection.Collection;
        return CheckCollection(collection, true) switch
        {
            CollectionState.Unavailable => new CollectionTuple(null, "未继承"u8,
                "选中的模组的设置未继承自其他合集。"u8, true),
            CollectionState.Available => new CollectionTuple(collection, collection!.Identity.Name,
                $"当前选中模组设置继承自[{collection.Identity.Name}]，点击切换到此合集作为当前可操作的合集。",
                false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private void DrawCollectionButton(Vector2 buttonWidth, in CollectionTuple tuple, int id)
    {
        var (collection, name, tooltip, disabled) = tuple;
        using var _ = Im.Id.Push(id);
        if (ImEx.Button(name, buttonWidth, StringU8.Empty, disabled))
            _activeCollections.SetCollection(collection!, CollectionType.Current);
        Im.Tooltip.OnHover(ref tooltip, HoveredFlags.AllowWhenDisabled, true);
        Im.Line.Same();
    }

    public bool Collapsed
        => false;

    public void Draw(Vector2 size)
    {
        using var style = ImStyleDouble.ItemSpacing.Push(Vector2.Zero);
        DrawTemporaryCheckbox();
        Im.Line.Same();
        var comboWidth = (size.X - Im.Style.FrameHeight) / 4f;
        var buttonSize = new Vector2(comboWidth * 3f / 4f, 0f);
        using (var _ = Im.Group())
        {
            DrawCollectionButton(buttonSize, GetDefaultCollectionInfo(),   1);
            DrawCollectionButton(buttonSize, GetInterfaceCollectionInfo(), 2);
            DrawCollectionButton(buttonSize, GetPlayerCollectionInfo(),    3);
            DrawCollectionButton(buttonSize, GetInheritedCollectionInfo(), 4);

            combo.Draw("##collectionSelector"u8, comboWidth, ColorId.SelectedCollection.Value());
        }

        tutorial.OpenTutorial(BasicTutorialSteps.CollectionSelectors);

        if (!_activeCollections.CurrentCollectionInUse)
            ImEx.TextFramed("当前选中的合集未在任何地方使用。"u8, Im.ContentRegion.Available with { Y = 0 },
                Colors.PressEnterWarningBg);
    }
}
