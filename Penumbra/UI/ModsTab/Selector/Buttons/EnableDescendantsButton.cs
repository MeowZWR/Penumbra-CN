using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

/// <summary> The menu items to set all descendants of a folder enabled or disabled.  </summary>
/// <param name="drawer"> The file system drawer. </param>
/// <param name="setTo"> Whether the drawer should enable or disable the descendants. </param>
/// <param name="inherit"> Whether the drawer should inherit all descendants instead of enabling or disabling them. </param>
public sealed class SetDescendantsButton(ModFileSystemDrawer drawer, bool setTo, bool inherit = false) : BaseButton<IFileSystemFolder>
{
    private readonly StringU8 _label = new((inherit, setTo) switch
    {
        (true, true)  => "继承子折叠组"u8,
        (true, false) => "停止继承子折叠组"u8,
        (_, true)     => "启用子折叠组"u8,
        (_, false)    => "禁用子折叠组"u8,
    });

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> Label(in IFileSystemFolder folder)
        => _label;

    /// <inheritdoc/>
    public override void OnClick(in IFileSystemFolder folder)
        => drawer.SetDescendants(folder, setTo, inherit);
}
