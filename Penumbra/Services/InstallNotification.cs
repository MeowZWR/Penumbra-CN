using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using ImSharp;
using Penumbra.Mods.Manager;

namespace Penumbra.Services;

public class InstallNotification(ModImportManager modImportManager, string filePath, TaskCompletionSource<ModImportResult[]> tcs)
    : Luna.INotificationAwareMessage
{
    private bool _reportCancellationOnDismissal = true;

    public NotificationType NotificationType
        => NotificationType.Info;

    public string NotificationMessage
        => "发现了新的模组！";

    public TimeSpan NotificationDuration
        => TimeSpan.MaxValue;

    public string NotificationTitle { get; } = Path.GetFileNameWithoutExtension(filePath);

    public string LogMessage
        => $"发现了新的模组：{Path.GetFileName(filePath)}";

    public SeString ChatMessage
        => SeString.Empty;

    public StringU8 StoredMessage
        => StringU8.Empty;

    public StringU8 StoredTooltip
        => StringU8.Empty;

    public void OnNotificationActions(INotificationDrawArgs args)
    {
        var region     = Im.ContentRegion.Available;
        var buttonSize = new Vector2((region.X - Im.Style.ItemSpacing.X) / 2, 0);
        if (Im.Button("安装"u8, buttonSize))
        {
            _reportCancellationOnDismissal = false;
            modImportManager.AddUnpack(filePath)
                .ContinueWith(tcs.SetFromTask);
            args.Notification.DismissNow();
        }

        Im.Line.Same();
        if (Im.Button("忽略"u8, buttonSize))
            args.Notification.DismissNow();
    }

    public void OnNotificationCreated(IActiveNotification notification)
    {
        notification.Dismiss += _ =>
        {
            if (_reportCancellationOnDismissal)
                tcs.SetResult([]);
        };
    }
}
