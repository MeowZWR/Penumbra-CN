using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class ClearQuickMoveFoldersButtons(ModFileSystemDrawer drawer) : BaseButton
{
    public override ReadOnlySpan<byte> Label
        => throw new NotImplementedException();

    public override bool DrawMenuItem()
    {
        if (drawer.Config.QuickMoveFolder1.Length > 0)
        {
            if (Im.Menu.Item("清理快速移动折叠组 #1"u8))
            {
                drawer.Config.QuickMoveFolder1 = string.Empty;
                drawer.Config.Save();
            }

            Im.Tooltip.OnHover($"清理当前快速移动分配的折叠组 {drawer.Config.QuickMoveFolder1}。");
        }


        if (drawer.Config.QuickMoveFolder2.Length > 0)
        {
            if (Im.Menu.Item("清理快速移动折叠组 #2"u8))
            {
                drawer.Config.QuickMoveFolder2 = string.Empty;
                drawer.Config.Save();
            }

            Im.Tooltip.OnHover($"清理当前快速移动分配的折叠组 {drawer.Config.QuickMoveFolder2}。");
        }


        if (drawer.Config.QuickMoveFolder3.Length > 0)
        {
            if (Im.Menu.Item("清理快速移动折叠组 #3"u8))
            {
                drawer.Config.QuickMoveFolder3 = string.Empty;
                drawer.Config.Save();
            }

            Im.Tooltip.OnHover($"清理当前快速移动分配的折叠组 {drawer.Config.QuickMoveFolder3}。");
        }

        return false;
    }
}
