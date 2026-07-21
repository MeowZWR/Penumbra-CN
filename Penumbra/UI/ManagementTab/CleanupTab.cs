using ImSharp;
using Luna;
using Penumbra.Services;

namespace Penumbra.UI.ManagementTab;

public sealed class CleanupTab(CleanupService cleanup, FileWatcher fileWatcher, Configuration config) : ITab<ManagementTabType>
{
    public ReadOnlySpan<byte> Label
        => "通用清理"u8;

    public ManagementTabType Identifier
        => ManagementTabType.Cleanup;

    public void DrawContent()
    {
        using var child = Im.Child.Begin("c"u8, Im.ContentRegion.Available);
        if (!child)
            return;

        var enabled = config.DeleteModModifier.IsActive();
        if (cleanup.Progress is not 0.0 and not 1.0)
        {
            Im.ProgressBar((float)cleanup.Progress, ImEx.ScaledVectorX(200, Im.Style.FrameHeight),
                $"{cleanup.Progress * 100}%");
            Im.Line.Same();
            if (Im.Button("Cancel##FileCleanup"u8))
                cleanup.Cancel();
        }
        else
        {
            Im.Line.New();
        }

        if (ImEx.Button("清理未使用的本地模组数据文件"u8, default,
                "删除所有与当前安装的模组不对应的本地模组数据文件。"u8,
                !enabled || cleanup.IsRunning))
            cleanup.CleanUnusedLocalData();
        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteModModifier} 键以删除文件。");

        if (ImEx.Button("清理备份文件"u8, default,
                "删除所有配置文件夹中的 .json 配置文件备份和模组文件夹中的模组组文件备份。"u8,
                !enabled || cleanup.IsRunning))
            cleanup.CleanBackupFiles();
        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteModModifier} 键以删除文件。");

        if (ImEx.Button("清理所有未使用的设置"u8, default,
                "删除所有与当前安装的模组不对应的模组设置。"u8,
                !enabled || cleanup.IsRunning))
            cleanup.CleanupAllUnusedSettings();
        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteModModifier} 键以删除设置。");

        if (ImEx.Button("清理提取的压缩包文件"u8, default,
                "删除所有由文件监视器从压缩包中提取的临时文件。尚未导入的提取文件将丢失。"u8,
                !enabled || cleanup.IsRunning))
            fileWatcher.CleanExtracted();
        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteModModifier} 键以删除设置。");
    }
}
