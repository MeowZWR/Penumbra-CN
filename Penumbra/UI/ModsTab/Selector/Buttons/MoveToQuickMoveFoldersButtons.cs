using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class MoveToQuickMoveFoldersButtons(ModFileSystemDrawer drawer) : BaseButton<IFileSystemData>
{
    public override ReadOnlySpan<byte> Label(in IFileSystemData data)
        => throw new NotImplementedException();

    public override bool DrawMenuItem(in IFileSystemData data)
    {
        var       currentName = data.Name;
        var       currentPath = data.FullPath;
        using var id          = new Im.IdDisposable();
        if (drawer.Config.QuickMoveFolder1.Length > 0)
        {
            id.Push(0);
            var targetPath = $"{drawer.Config.QuickMoveFolder1}/{currentName}";
            if (!drawer.FileSystem.Equal(currentPath, targetPath))
            {
                if (Im.Menu.Item($"移动到 {drawer.Config.QuickMoveFolder1}"))
                {
                    foreach (var path in drawer.FileSystem.Selection.OrderedNodes)
                    {
                        if (path != data)
                            drawer.FileSystem.RenameAndMoveWithDuplicates(path, $"{drawer.Config.QuickMoveFolder1}/{path.Name}");
                    }

                    drawer.FileSystem.RenameAndMoveWithDuplicates(data, targetPath);
                }
                Im.Tooltip.OnHover("将选定的对象移动到之前设置的快速移动位置（如果有的话）。"u8);
            }

            id.Pop();
        }

        if (drawer.Config.QuickMoveFolder2.Length > 0)
        {
            id.Push(1);
            var targetPath = $"{drawer.Config.QuickMoveFolder2}/{currentName}";
            if (!drawer.FileSystem.Equal(currentPath, targetPath))
            {
                if (Im.Menu.Item($"移动到 {drawer.Config.QuickMoveFolder2}"))
                {
                    foreach (var path in drawer.FileSystem.Selection.OrderedNodes)
                    {
                        if (path != data)
                            drawer.FileSystem.RenameAndMoveWithDuplicates(path, $"{drawer.Config.QuickMoveFolder2}/{path.Name}");
                    }

                    drawer.FileSystem.RenameAndMoveWithDuplicates(data, targetPath);
                }
                Im.Tooltip.OnHover("将选定的对象移动到之前设置的快速移动位置（如果有的话）。"u8);
            }

            id.Pop();
        }

        if (drawer.Config.QuickMoveFolder3.Length > 0)
        {
            id.Push(2);
            var targetPath = $"{drawer.Config.QuickMoveFolder3}/{currentName}";
            if (!drawer.FileSystem.Equal(currentPath, targetPath))
            {
                if (Im.Menu.Item($"移动到 {drawer.Config.QuickMoveFolder3}"))
                {
                    foreach (var path in drawer.FileSystem.Selection.OrderedNodes)
                    {
                        if (path != data)
                            drawer.FileSystem.RenameAndMoveWithDuplicates(path, $"{drawer.Config.QuickMoveFolder3}/{path.Name}");
                    }

                    drawer.FileSystem.RenameAndMoveWithDuplicates(data, targetPath);
                }
                Im.Tooltip.OnHover("将选定的对象移动到之前设置的快速移动位置（如果有的话）。"u8);
            }

            id.Pop();
        }

        return false;
    }
}
