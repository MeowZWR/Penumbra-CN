using ImSharp;
using Luna;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Selector;

/// <summary> The button to import a mod. </summary>
public sealed class ImportModButton(ModFileSystemDrawer drawer) : BaseIconButton<AwesomeIcon>
{
    /// <inheritdoc/>
    public override AwesomeIcon Icon
        => LunaStyle.ImportIcon;

    /// <inheritdoc/>
    public override bool HasTooltip
        => true;

    /// <inheritdoc/>
    public override void DrawTooltip()
        => Im.Text("从TexTools模组包或Penumbra模组包导入一个或多个模组。"u8);

    /// <inheritdoc/>
    public override void OnClick()
    {
        var modPath = drawer.Config.DefaultModImportPath.Length > 0
            ? drawer.Config.DefaultModImportPath
            : drawer.Config.ModDirectory.Length > 0
                ? drawer.Config.ModDirectory
                : null;

        drawer.FileService.OpenFilePicker("导入模组包",
            "模组包{.ttmp,.ttmp2,.pmp,.pcp},TexTools模组包{.ttmp,.ttmp2},Penumbra模组包{.pmp,.pcp},压缩文件{.zip,.7z,.rar},Penumbra角色包{.pcp}",
            (s, f) =>
            {
                if (!s)
                    return;

                drawer.ModImport.AddUnpack(f);
            }, 0, modPath, drawer.Config.AlwaysOpenDefaultImport);
    }

    /// <inheritdoc/>
    protected override void PostDraw()
        => drawer.Tutorial.OpenTutorial(BasicTutorialSteps.ModImport);
}
