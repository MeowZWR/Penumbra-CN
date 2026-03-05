using ImSharp;
using Luna;
using Penumbra.Collections.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.Tabs;

public sealed class CollectionButtonFooter : ButtonFooter
{
    public CollectionButtonFooter(CollectionManager collectionManager, CommunicatorService communicator, Configuration configuration,
        TutorialService tutorial, IncognitoService incognito)
    {
        Buttons.AddButton(new AddButton(collectionManager.Storage),                                             100);
        Buttons.AddButton(new DuplicateButton(collectionManager.Storage, collectionManager.Active),             50);
        Buttons.AddButton(new DeleteButton(collectionManager.Storage, collectionManager.Active, configuration), 0);
    }

    public int Count
        => Buttons.Count;

    public sealed class AddButton(CollectionStorage collections) : BaseIconButton<AwesomeIcon>
    {
        public override AwesomeIcon Icon
            => LunaStyle.AddObjectIcon;

        public override bool HasTooltip
            => true;

        public override void DrawTooltip()
            => Im.Text("新建一个空合集。"u8);

        public override void OnClick()
            => Im.Popup.Open("NewCollection"u8);

        protected override void PostDraw()
        {
            if (!InputPopup.OpenName("NewCollection"u8, out var newCollectionName))
                return;

            collections.AddCollection(newCollectionName, null);
        }
    }

    public sealed class DeleteButton(CollectionStorage collections, ActiveCollections active, Configuration config)
        : BaseIconButton<AwesomeIcon>
    {
        public override AwesomeIcon Icon
            => LunaStyle.DeleteIcon;

        public override bool HasTooltip
            => true;

        public override bool Enabled
            => collections.DefaultNamed != active.Current
             && config.DeleteModModifier.IsActive();

        public override void DrawTooltip()
        {
            Im.Text("删除当前合集。"u8);
            if (collections.DefaultNamed == active.Current)
                Im.Text("默认合集不能被删除。"u8);
            else if (!config.DeleteModModifier.IsActive())
                Im.Text($"按住 {config.DeleteModModifier} 点击删除当前合集。");
        }

        public override void OnClick()
            => collections.RemoveCollection(active.Current);
    }

    public sealed class DuplicateButton(CollectionStorage collections, ActiveCollections active) : BaseIconButton<AwesomeIcon>
    {
        public override AwesomeIcon Icon
            => LunaStyle.DuplicateIcon;

        public override bool HasTooltip
            => true;

        public override void DrawTooltip()
            => Im.Text("复制当前选中的合集到新的合集。"u8);

        public override void OnClick()
            => Im.Popup.Open("DuplicateCollection"u8);

        protected override void PostDraw()
        {
            if (!InputPopup.OpenName("DuplicateCollection"u8, out var newCollectionName))
                return;

            collections.AddCollection(newCollectionName, active.Current);
        }
    }
}
