using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class SetQuickMoveFoldersButtons(ModFileSystemDrawer drawer) : BaseButton<IFileSystemFolder>
{
    public override ReadOnlySpan<byte> Label(in IFileSystemFolder data)
        => throw new NotImplementedException();

    public override bool DrawMenuItem(in IFileSystemFolder data)
    {
        if (Im.Menu.Item("设置为快速移动折叠组 #1"u8))
        {
            drawer.Config.QuickMoveFolder1 = data.FullPath;
            drawer.Config.Save();
        }

        Im.Tooltip.OnHover(drawer.Config.QuickMoveFolder1.Length is 0
            ? "设置这个折叠组为快速移动的目标位置。"u8
            : $"设置这个折叠组为快速移动的目标位置而不是 {drawer.Config.QuickMoveFolder1}。");

        if (Im.Menu.Item("设置为快速移动折叠组 #2"u8))
        {
            drawer.Config.QuickMoveFolder2 = data.FullPath;
            drawer.Config.Save();
        }

        Im.Tooltip.OnHover(drawer.Config.QuickMoveFolder2.Length is 0
            ? "设置这个折叠组为快速移动的目标位置。"u8
            : $"设置这个折叠组为快速移动的目标位置而不是 {drawer.Config.QuickMoveFolder2}。");

        if (Im.Menu.Item("设置为快速移动折叠组 #3"u8))
        {
            drawer.Config.QuickMoveFolder3 = data.FullPath;
            drawer.Config.Save();
        }

        Im.Tooltip.OnHover(drawer.Config.QuickMoveFolder3.Length is 0
            ? "设置这个折叠组为快速移动的目标位置。"u8
            : $"设置这个折叠组为快速移动的目标位置而不是 {drawer.Config.QuickMoveFolder3}。");
        return false;
    }
}
