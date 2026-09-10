using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

/// <summary> The menu items to set all descendants of a folder enabled or disabled.  </summary>
/// <param name="drawer"> The file system drawer. </param>
/// <param name="setTo"> Whether the drawer should enable or disable the descendants. </param>
/// <param name="inherit"> Whether the drawer should inherit all descendants instead of enabling or disabling them. </param>
public sealed class SetDescendantsButton(ModFileSystemDrawer drawer, bool setTo, bool? inherit) : BaseButton<IFileSystemFolder>
{
    private readonly StringU8 _label = new((inherit, setTo) switch
    {
        (true, _)     => "继承子折叠组"u8,
        (false, _)    => "停止继承子折叠组"u8,
        (null, true)  => "启用子折叠组"u8,
        (null, false) => "禁用子折叠组"u8,
    });

    /// <inheritdoc/>
    public override bool Enabled(in IFileSystemFolder data)
        => LunaStyle.Modifier.Misclick.Active;

    /// <inheritdoc/>
    public override bool HasTooltip
        => true;

    public override void DrawTooltip(in IFileSystemFolder data)
    {
        Im.Text("对当前组及其所有子组中的模组执行批量操作。"u8);
        if (!LunaStyle.Modifier.Misclick.Active)
            Im.Text($"按住 {LunaStyle.Modifier.Misclick} 点击以执行。");
    }

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> Label(in IFileSystemFolder folder)
        => _label;

    /// <inheritdoc/>
    public override void OnClick(in IFileSystemFolder folder)
        => drawer.SetDescendants(folder, setTo, inherit);
}
