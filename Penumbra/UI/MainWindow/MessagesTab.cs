using Luna;
using Penumbra.Api.Enums;
using Penumbra.Services;

namespace Penumbra.UI.Tabs;

public sealed class MessagesTab(PenumbraMessager messages) : ITab<TabType>
{
    public ReadOnlySpan<byte> Label
        => "消息"u8;

    public bool IsVisible
        => messages.Count > 0;

    public void DrawContent()
        => messages.DrawNotificationLog();

    public TabType Identifier
        => TabType.Messages;
}
