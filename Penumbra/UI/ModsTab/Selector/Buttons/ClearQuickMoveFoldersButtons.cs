using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class ClearQuickMoveFoldersButtons(ModFileSystemDrawer drawer) : BaseButton
{
    public override ReadOnlySpan<byte> Label
        => throw new NotImplementedException();

    public override bool DrawMenuItem()
    {
        for (var i = 0; i < UiConfig.NumQuickMoveFolders; ++i)
        {
            var value = drawer.Config.Ui.QuickMoveFolder(i);
            if (value.Length <= 0)
                continue;

            if (Im.Menu.Item($"清理快速移动折叠组 #{i + 1}"))
                drawer.Config.Ui.SetQuickMoveFolder(i, string.Empty);
            Im.Tooltip.OnHover($"清理当前快速移动分配的折叠组 {value}。");
        }

        return false;
    }
}
