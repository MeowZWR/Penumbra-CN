using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Mods;

namespace Penumbra.UI.ManagementTab;

public sealed class FailedModNotification(Services.MessageService service, UiNavigator navigator)
    : AmassingNotification<(string Mod, Exception Error)>(service), IService
{
    public void AddMissingMeta(Mod mod)
        => AddObject((mod.ModPath.Name, new FileNotFoundException("未找到元数据。\n\n"
          + "所述文件夹并非已安装的模组，请将 Penumbra 根目录留给 Penumbra 使用，勿在其中放置您自己的文件夹。\n\n"
          + "删除此文件夹或将其移出根目录即可消除此警告。", Path.Combine(mod.ModPath.FullName, "meta.json"))));

    public void AddInvalidMeta(Mod mod, Exception ex)
        => AddObject((mod.ModPath.Name, ex));

    public override NotificationType NotificationType
        => NotificationType.Error;

    public override string NotificationTitle
        => $"{Count} 个模组加载失败";

    // TODO: add management tab for this
    public override string NotificationMessage
        => "有一个或多个模组未能加载。\n\n请在「消息」页签中查看详情。\n\n后续将添加专门的管理页签，以便更便捷地处理此类情况。";

    public override void NotificationActions(INotificationDrawArgs args)
    {
        var width = Im.ContentRegion.Available with { Y = 0 };
        width.X = (width.X - Im.Style.ItemInnerSpacing.X) / 2;
        if (Im.Button("打开消息"u8, width))
            navigator.OpenTo(TabType.Messages);
        Im.Line.SameInner();
        if (ImEx.Button("打开模组管理"u8, width, true))
            navigator.OpenTo(ManagementTabType.ReservedFiles); // TODO
    }

    protected override StoredNotification CreateStored(in (string Mod, Exception Error) @object)
        => new Stored(this, @object.Mod, @object.Error);

    private sealed class Stored(FailedModNotification parent, string mod, Exception error) : StoredNotification(parent, (mod, error))
    {
        public override string LogMessage { get; } = $"模组「{mod}」加载失败：\n{error}";

        public override StringU8 StoredMessage { get; } = new(error is FileNotFoundException
            ? $"[{mod}] 加载失败：未找到元数据。"
            : $"[{mod}] 加载失败：读取元数据时出错。");

        public override StringU8 StoredTooltip { get; } = new($"{error}");
    }
}
