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
    : AmassingNotification<(string Path, string Mod)>(service), IService
{
    public bool IsRedirectionSupported(Utf8GamePath path, IMod mod, bool temporaryCollection)
    {
        if (ReservedFiles.Files.ContainsKey((uint)path.Path.Crc32))
        {
            if (!temporaryCollection)
                AddObject((path.ToString(), mod.Name));
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

    public override NotificationType NotificationType
        => NotificationType.Warning;

    public override string NotificationMessage
        => "这些文件的重定向已被禁用，意外的替换会导致崩溃。\n\n"
          + "查看「模组管理」->「保留文件」以获取更多详细信息。";

    public override string NotificationTitle
        => $"检测到 {Count} 个保留文件";

    protected override StoredNotification CreateStored(in (string Path, string Mod) @object)
        => new Stored(this, @object.Path, @object.Mod);

    public override void NotificationActions(INotificationDrawArgs args)
    {
        var width = Im.ContentRegion.Available with { Y = 0 };
        width.X = (width.X - Im.Style.ItemInnerSpacing.X) / 2;
        if (Im.Button("打开消息"u8, width))
            navigator.OpenTo(TabType.Messages);
        Im.Line.SameInner();
        if (Im.Button("打开模组管理"u8, width))
            navigator.OpenTo(ManagementTabType.ReservedFiles);
    }

    private sealed class Stored(ReservedFileNotification parent, string file, string mod) : StoredNotification(parent, (file, mod))
    {
        public override string   LogMessage    { get; } = $"已停止模组「{mod}」中对「{file}」的重定向。";
        public override StringU8 StoredMessage { get; } = new($"{file}（模组「{mod}」）：保留文件重定向");
        public override StringU8 StoredTooltip { get; } = new($"文件：{file}\n模组：「{mod}」");
    }
}
