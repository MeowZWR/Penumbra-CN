using ImSharp;
using Luna;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public sealed class IoSettings(IoConfig config, MainConfig main, FileDialogService fileDialog, PcpService pcpService)
    : IUiService
{
    public void Draw()
    {
        DrawImportSettings();
        DrawWatcherSettings();
        DrawExportSettings();
        DrawPcpSettings();
    }

    private void DrawWatcherSettings()
    {
        using var tree = Im.Tree.Node("自动导入"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        DrawFileWatcherPath();
        if (SettingsTab.Checkbox("启用目录监听器"u8,
                "启用文件监听器后，Penumbra会自动监听指定目录中新出现的模组文件，并在检测到新模组时弹出导入这些模组的询问弹窗。"u8,
                config.EnableDirectoryWatch))
            config.EnableDirectoryWatch ^= true;
        if (SettingsTab.Checkbox("启用压缩包预览"u8,
                "启用文件监听器后，自动预览压缩包（.rar、.zip、.7z）内的模组，并在检测到新模组时弹出导入这些模组的询问弹窗。"u8,
                config.EnableContainerPeeking))
            config.EnableContainerPeeking ^= true;
        if (SettingsTab.Checkbox("启用全自动导入"u8,
                "配合文件监听器，自动跳过询问弹窗并导入检测到的所有新模组。"u8,
                config.EnableAutomaticModImport))
            config.EnableAutomaticModImport ^= true;
        if (SettingsTab.Checkbox("防止导出的模组被自动重新导入"u8,
                "如果自动导入目录与默认模组导出目录相同，则防止导出的模组和角色包被自动重新导入或显示询问弹窗。"u8,
                config.PreventExportLoopback))
            config.PreventExportLoopback ^= true;
        LunaStyle.DrawSeparator();
    }

    private void DrawImportSettings()
    {
        using var tree = Im.Tree.Node("模组导入"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("导入时替换非标准符号"u8,
                "导入模组时，将模组和选项名称中的所有非ASCII符号替换为下划线。"u8,
                config.ReplaceNonAsciiOnImport))
            config.ReplaceNonAsciiOnImport ^= true;

        if (SettingsTab.Checkbox("打开导入窗口时始终使用默认目录"u8,
                "每次都在此处指定的目录位置打开导入窗口，不使用上一次的路径。"u8,
                config.AlwaysOpenDefaultImport))
            config.AlwaysOpenDefaultImport ^= true;
        DrawDefaultModImportFolder();
        DrawDefaultModImportPath();

        if (SettingsTab.Checkbox("始终显示导入详情窗口"u8,
                "在屏幕中央显示包含最新导入信息的详情窗口，而非使用 Dalamud 通知。"u8,
                config.AlwaysShowDetailedModImport))
            config.AlwaysShowDetailedModImport ^= true;
        if (SettingsTab.Checkbox("自动关闭导入成功的报告"u8,
                "如果所有模组都导入成功，导入报告通知将在几秒后自动消失。\n包含错误的报告仍需手动关闭。"u8,
                config.AutoDismissModImportSuccessReports))
            config.AutoDismissModImportSuccessReports ^= true;
        LunaStyle.DrawSeparator();
    }

    private void DrawExportSettings()
    {
        using var tree = Im.Tree.Node("模组导出"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        DrawDefaultModAuthor();
        DrawDefaultModExportPath();
        LunaStyle.DrawSeparator();
    }

    private void DrawPcpSettings()
    {
        using var tree = Im.Tree.Node("Penumbra 角色包 (PCP)"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("处理PCP文件"u8,
                "当检测到特定模组（通常以.pcp结尾，但不一定）时，Penumbra会自动尝试为该模组包创建对应角色的合集，并分配给特定角色。如果不需要自动处理可关闭此功能。"u8,
                !config.DisablePcpHandling))
            config.DisablePcpHandling ^= true;

        var active = LunaStyle.Modifier.Destructive.Active;
        Im.Line.Same();
        if (ImEx.Button("删除所有PCP模组"u8, default, "从模组列表中删除所有带有'PCP'标签的模组。"u8, !active))
            pcpService.CleanPcpMods();
        LunaStyle.Modifier.Destructive.TooltipLineBreak("delete"u8);

        Im.Line.Same();
        if (ImEx.Button("删除所有PCP合集"u8, default,
                "从合集列表中删除所有名称以'PCP/'开头的合集。"u8, !active))
            pcpService.CleanPcpCollections();
        LunaStyle.Modifier.Destructive.TooltipLineBreak("delete"u8);

        if (SettingsTab.Checkbox("允许其他插件访问PCP处理功能"u8,
                "在创建或导入PCP文件时，其他插件可以添加和解释它们自己的数据到character.json文件。"u8,
                config.PcpAllowIpc))
            config.PcpAllowIpc ^= true;

        if (SettingsTab.Checkbox("导入PCP文件时创建合集"u8,
                "导入PCP文件时自动创建对应的合集。"u8,
                config.PcpCreateCollection))
            config.PcpCreateCollection ^= true;

        if (SettingsTab.Checkbox("导入PCP文件时分配合集"u8,
                "导入PCP文件并创建合集时，自动将该合集分配给关联角色。"u8,
                config.PcpAssignCollection))
            config.PcpAssignCollection ^= true;
        DrawPcpFolder();
        DrawPcpExtension();
        LunaStyle.DrawSeparator();
    }

    /// <summary> Draw input for the default folder to sort put newly imported mods into. </summary>
    private void DrawDefaultModImportFolder()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##importFolder"u8, config.DefaultImportFolder, out string newFolder))
            config.DefaultImportFolder = newFolder;

        LunaStyle.DrawAlignedHelpMarkerLabel("默认模组导入折叠组"u8,
            "导入新模组后，模组默认进入以此名称命名的折叠组。\n留空则导入到根目录。"u8);
    }

    /// <summary> Draw input for the default import path for a mod. </summary>
    private void DrawDefaultModImportPath()
    {
        using var id = Im.Id.Push("##dmi"u8);
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButtonInner);
        if (ImEx.InputOnDeactivation.Text(StringU8.Empty, config.DefaultModImportPath, out string newDirectory))
            config.DefaultModImportPath = newDirectory;

        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon, "通过对话框选择一个目录。"u8))
        {
            var startDir = config.DefaultModImportPath.Length > 0 && Directory.Exists(config.DefaultModImportPath)
                ? config.DefaultModImportPath
                : Directory.Exists(main.ModDirectory)
                    ? main.ModDirectory
                    : null;

            fileDialog.OpenFolderPicker("选择默认导入目录", (b, s) =>
            {
                if (!b)
                    return;

                config.DefaultModImportPath = s;
                config.Save();
            }, startDir, false);
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("默认模组导入目录"u8,
            "设置首次使用文件选择器导入模组时打开的目录。"u8);
    }

    /// <summary> Draw input for the Automatic Mod import path. </summary>
    private void DrawFileWatcherPath()
    {
        using var id = Im.Id.Push("fw"u8);
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButtonInner);
        if (ImEx.InputOnDeactivation.Text(StringU8.Empty, config.WatchDirectory, out string newDirectory, maxLength: 256))
            config.WatchDirectory = newDirectory;

        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon, "通过对话框选择一个目录。"u8))
        {
            var startDir = config.WatchDirectory.Length > 0 && Directory.Exists(config.WatchDirectory)
                ? config.WatchDirectory
                : Directory.Exists(main.ModDirectory)
                    ? main.ModDirectory
                    : null;
            fileDialog.OpenFolderPicker("选择自动导入目录", (b, s) =>
            {
                if (b)
                    config.WatchDirectory = s;
            }, startDir, false);
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("自动导入目录"u8,
            "选择文件监听器监控的目录。"u8);
    }

    /// <summary> Draw input for the default export/backup path for mods. </summary>
    private void DrawDefaultModExportPath()
    {
        using var id = Im.Id.Push("##dme"u8);
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButtonInner);
        if (ImEx.InputOnDeactivation.Text(StringU8.Empty, config.ExportDirectory, out string newDirectory))
            config.ExportDirectory = newDirectory;

        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon, "通过对话框选择一个目录。"u8))
        {
            var startDir = config.ExportDirectory.Length > 0 && Directory.Exists(config.ExportDirectory)
                ? config.ExportDirectory
                : Directory.Exists(main.ModDirectory)
                    ? main.ModDirectory
                    : null;
            fileDialog.OpenFolderPicker("选择默认导出目录", (b, s) =>
            {
                if (b)
                    config.ExportDirectory = s;
            }, startDir, false);
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("默认模组导出目录"u8,
            "设置用于备份模组与恢复备份的路径。\n"u8
          + "留空则使用根目录。"u8);
    }

    /// <summary> Draw input for the default name to input as author into newly generated mods. </summary>
    private void DrawDefaultModAuthor()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##author"u8, config.DefaultModAuthor, out string newAuthor))
        {
            config.DefaultModAuthor = newAuthor;
            config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("默认模组作者"u8, "为新创建的模组设置一个默认的作者名字。"u8);
    }

    /// <summary> Draw input for the default folder to sort put newly imported mods into. </summary>
    private void DrawPcpFolder()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##pcpFolder"u8, config.PcpFolderName, out string newFolder))
            config.PcpFolderName = newFolder;

        LunaStyle.DrawAlignedHelpMarkerLabel("默认PCP折叠组"u8,
            "导入PCP角色包时，会将其移动到该折叠组中。\n留空则导入到根目录。"u8);
    }

    private void DrawPcpExtension()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##pcpExtension"u8, config.PcpExtension, out string newExtension))
            config.PcpExtension = newExtension;

        Im.Line.SameInner();
        if (ImEx.Button("Reset##pcpExtension"u8, Vector2.Zero, "重置扩展名为其默认值 \".pcp\"."u8,
                config.PcpExtension is ".pcp"))
            config.PcpExtension = ".pcp";

        LunaStyle.DrawAlignedHelpMarkerLabel("PCP扩展名"u8,
            "导出PCP文件时使用的扩展名。通常应该是 \".pcp\" 或 \".pmp\"。"u8);
    }
}
