using Luna;

namespace Penumbra.UI.ModsTab.Selector;

/// <summary> The menu item to set a given folder as default import folder. </summary>
/// <param name="drawer"> The file system drawer. </param>
public sealed class SetDefaultImportFolderButton(ModFileSystemDrawer drawer) : BaseButton<IFileSystemFolder>
{
    /// <inheritdoc/>
    public override ReadOnlySpan<byte> Label(in IFileSystemFolder _)
        => "★设置为默认导入折叠组"u8;

    /// <inheritdoc/>
    public override void OnClick(in IFileSystemFolder folder)
    {
        if (folder.FullPath == drawer.Config.DefaultImportFolder)
            return;

        drawer.Config.DefaultImportFolder = folder.FullPath;
        drawer.Config.Save();
    }
}
