using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Files;
using Penumbra.Mods;

namespace Penumbra.UI.ManagementTab;

public sealed class FailedModNotification(Services.PenumbraMessager service, UiNavigator navigator)
    : AmassingNotification<(string Mod, Exception Error)>(service), IService
{
    public void Add(Mod mod, Exception ex)
        => AddObject((mod.ModPath.Name, ex));

    public override NotificationType NotificationType
        => NotificationType.Error;

    public override string NotificationTitle
        => $"{Count} 个模组加载失败";

    public override string NotificationMessage
        => "有一个或多个模组未能加载。\n\n请在「消息」页签中查看详情。查看「模组管理」->「损坏的模组」以修复问题。";

    public override void NotificationActions(INotificationDrawArgs args)
    {
        var width = Im.ContentRegion.Available with { Y = 0 };
        width.X = (width.X - Im.Style.ItemInnerSpacing.X) / 2;
        if (Im.Button("打开消息"u8, width))
            navigator.OpenTo(TabType.Messages);
        Im.Line.SameInner();
        if (ImEx.Button("打开模组管理"u8, width))
            navigator.OpenTo(ManagementTabType.BrokenMods);
    }

    protected override StoredNotification CreateStored(in (string Mod, Exception Error) @object)
        => new Stored(this, @object.Mod, @object.Error);

    private sealed class Stored(FailedModNotification parent, string mod, Exception error) : StoredNotification(parent, (mod, error))
    {
        public override string LogMessage { get; } = $"模组「{mod}」加载失败：\n{error}";

        public override StringU8 StoredMessage { get; } = FromException(mod, error);

        private static StringU8 FromException(string mod, Exception error)
            => error switch
            {
                MetaMissingException => new StringU8($"[{mod}] 加载失败：未找到元数据。"),
                InvalidMetaException => new StringU8($"[{mod}] 加载失败：读取元数据时出错。"),
                MissingFeatureException => new StringU8($"[{mod}] 加载失败：不支持所需的功能。"),
                AggregateException { InnerExceptions.Count: 1 } aggregate => FromException(mod, aggregate.InnerExceptions.First()),
                AggregateException { InnerExceptions.Count: > 1 } aggregate when aggregate.InnerExceptions.First() is InvalidMetaException
                    or MissingFeatureException => FromException(mod, aggregate.InnerExceptions.First()),
                _ => new StringU8($"[{mod}] 加载失败：{error.Message}"),
            };

        public override StringU8 StoredTooltip { get; } = new(
            error is MetaMissingException
                ? "所述文件夹并非已安装的模组，请将 Penumbra 根目录留给 Penumbra 使用，勿在其中放置您自己的文件夹。\n\n"
              + "删除此文件夹或将其移出根目录即可消除此警告。"
                : $"{error}");
    }
}
