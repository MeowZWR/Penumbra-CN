using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class SetQuickMoveFoldersButtons(ModFileSystemDrawer drawer) : BaseButton<IFileSystemFolder>
{
    public override ReadOnlySpan<byte> Label(in IFileSystemFolder data)
        => throw new NotImplementedException();

    public override bool DrawMenuItem(in IFileSystemFolder data)
    {
        for (var i = 0; i < UiConfig.NumQuickMoveFolders; ++i)
        {
            if (Im.Menu.Item($"设置为快速移动折叠组 #{i + 1}"))
                drawer.Config.Ui.SetQuickMoveFolder(i, data.FullPath);
            var value = drawer.Config.Ui.QuickMoveFolder(i);
            Im.Tooltip.OnHover(value.Length is 0
                ? "设置这个折叠组为快速移动的目标位置。"u8
                : $"设置这个折叠组为快速移动的目标位置而不是 {value}。");
        }

        return false;
    }
}
