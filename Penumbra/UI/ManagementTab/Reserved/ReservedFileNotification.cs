using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Mods.Editor;
using Penumbra.String.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class ReservedFileNotification(
    Services.MessageService service,
    UiNavigator navigator)
    : INotificationAwareMessage, IService
{
    private IActiveNotification? _currentNotification;

    public bool IsRedirectionSupported(Utf8GamePath path, IMod mod, bool temporaryCollection)
    {
        if (ReservedFiles.Files.ContainsKey((uint)path.Path.Crc32))
        {
            if (!temporaryCollection)
                AddFile(path, mod);
            return false;
        }

        var ext = path.Extension().AsciiToLower().ToString();
        switch (ext)
        {
            case ".atch" or ".eqp" or ".eqdp" or ".est" or ".gmp" or ".cmp" or ".imc":
                if (!temporaryCollection)
                    Penumbra.Messager.NotificationMessage(
                        $"不支持对模组「{mod.Name}」中的 {ext} 文件进行重定向，通常表示该模组较旧，可能无法正常工作。\n\n请提醒作者改用相应的元数据（meta）操作来实现效果。",
                        NotificationType.Warning);
                return false;
            case ".lvb" or ".lgb" or ".sgb":
                if (!temporaryCollection)
                    Penumbra.Messager.NotificationMessage(
                        $"不支持对模组「{mod.Name}」中的 {ext} 文件进行重定向，否则可能破坏游戏稳定性。\n\n该模组可能无法正常工作。",
                        NotificationType.Warning);
                return false;
            default: return true;
        }
    }

    private void AddFile(Utf8GamePath path, IMod mod)
    {
        var t = (path.ToString(), mod.Name);
        if (_gatheredFiles.Contains(t))
            return;

        _gatheredFiles.Add(t);
        service.AddMessage(new StoredNotification(this, t.Item1, t.Name), true, false, true, false);
        if (_currentNotification is null)
        {
            service.AddMessage(this, false, true, false, false);
        }
        else
        {
            _currentNotification.Title         = ((IMessage)this).NotificationTitle;
            _currentNotification.MinimizedText = _currentNotification.Title;
            _currentNotification.ExtendBy(TimeSpan.FromSeconds(30));
        }
    }

    private readonly List<(string File, string Mod)> _gatheredFiles = [];

    private NotificationType NotificationType
        => NotificationType.Warning;

    private TimeSpan NotificationDuration
        => TimeSpan.FromSeconds(30);

    NotificationType IMessage.NotificationType
        => NotificationType;

    string IMessage.NotificationMessage
        => "这些文件的重定向已被禁用，意外的替换会导致崩溃。\n\n"
          + "查看 模组管理 -> 保留文件 以获取更多详细信息。";

    TimeSpan IMessage.NotificationDuration
        => NotificationDuration;

    string IMessage.NotificationTitle
        => $"检测到 {_gatheredFiles.Count} 个保留文件";

    string IMessage.LogMessage
        => string.Empty;

    SeString IMessage.ChatMessage
        => SeString.Empty;

    StringU8 IMessage.StoredMessage
        => StringU8.Empty;

    StringU8 IMessage.StoredTooltip
        => StringU8.Empty;

    void IMessage.OnNotificationActions(INotificationDrawArgs args)
    {
        var width = Im.ContentRegion.Available with { Y = 0 };
        width.X = (width.X - Im.Style.ItemInnerSpacing.X) / 2;
        if (Im.Button("打开消息"u8, width))
            navigator.OpenTo(TabType.Messages);
        Im.Line.SameInner();
        if (Im.Button("打开模组管理"u8, width))
            navigator.OpenTo(ManagementTabType.ReservedFiles);
    }

    void INotificationAwareMessage.OnNotificationCreated(IActiveNotification notification)
    {
        _currentNotification       =  notification;
        notification.Dismiss       += OnNotificationDismissed;
        notification.MinimizedText =  _currentNotification.Title;
    }

    private void OnNotificationDismissed(INotificationDismissArgs args)
    {
        if (args.Notification != _currentNotification)
            return;

        _gatheredFiles.Clear();
        _currentNotification = null;
    }

    private sealed class StoredNotification(ReservedFileNotification parent, string file, string mod) : IMessage
    {
        public NotificationType NotificationType
            => NotificationType.Warning;

        public string NotificationMessage
            => string.Empty;

        public string NotificationTitle
            => string.Empty;

        public TimeSpan NotificationDuration
            => TimeSpan.Zero;

        public string LogMessage { get; } = $"已停止模组「{mod}」中对「{file}」的重定向。";

        public SeString ChatMessage
            => SeString.Empty;

        public StringU8 StoredMessage { get; } = new($"{file}（模组 {mod}）：保留文件重定向");
        public StringU8 StoredTooltip { get; } = new($"文件：{file}\n模组：{mod}");

        public void OnNotificationActions(INotificationDrawArgs args)
        { }

        public void OnRemoval()
        {
            parent._gatheredFiles.Remove((file, mod));
            if (parent._currentNotification is { } notification)
            {
                if (parent._gatheredFiles.Count is 0)
                    notification.DismissNow();
                notification.Title         = ((IMessage)parent).NotificationTitle;
                notification.MinimizedText = notification.Title;
            }
        }
    }
}
