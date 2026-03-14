using ImSharp;
using Luna;
using Penumbra.Mods;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class DeleteSelectionButton(ModFileSystemDrawer drawer) : BaseIconButton<AwesomeIcon>
{
    /// <inheritdoc/>
    public override AwesomeIcon Icon
        => LunaStyle.DeleteIcon;

    /// <inheritdoc/>
    public override bool HasTooltip
        => true;

    /// <inheritdoc/>
    public override void DrawTooltip()
    {
        var anySelected = drawer.FileSystem.Selection.DataNodes.Count > 0;
        var modifier    = Enabled;

        Im.Text(anySelected ? "从你的驱动器中完全删除当前选中的模组\n操作不可撤销。"u8 : "未选中模组。"u8);
        if (!modifier)
            Im.Text($"按住 {drawer.Config.DeleteModModifier} 点击删除模组。");
    }

    /// <inheritdoc/>
    public override bool Enabled
        => drawer.Config.DeleteModModifier.IsActive() && drawer.FileSystem.Selection.DataNodes.Count > 0;

    /// <inheritdoc/>
    public override void OnClick()
    {
        var mods = drawer.FileSystem.Selection.DataNodes.Select(n => n.Value).OfType<Mod>().ToList();
        drawer.FileSystem.Selection.UnselectAll();
        foreach (var mod in mods)
            drawer.ModManager.DeleteMod(mod);
    }
}
