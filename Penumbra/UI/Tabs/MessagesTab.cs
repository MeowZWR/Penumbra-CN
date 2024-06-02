﻿using OtterGui.Widgets;
using Penumbra.Services;

namespace Penumbra.UI.Tabs;

public class MessagesTab(MessageService messages) : ITab
{
    public ReadOnlySpan<byte> Label
        => "消息"u8;

    public bool IsVisible
        => messages.Count > 0;

    public void DrawContent()
        => messages.Draw();
}
