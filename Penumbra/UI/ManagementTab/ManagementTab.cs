using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Services;

namespace Penumbra.UI.ManagementTab;

public enum ManagementTabType
{
    UnusedMods,
    DuplicateMods,
    Cleanup,
}

public sealed class CleanupTab(CleanupService cleanup, Configuration config) : ITab<ManagementTabType>
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
            Im.ProgressBar((float)cleanup.Progress, new Vector2(200 * Im.Style.GlobalScale, Im.Style.FrameHeight),
                $"{cleanup.Progress * 100}%");
            Im.Line.Same();
            if (Im.Button("取消##FileCleanup"u8))
                cleanup.Cancel();
        }
        else
        {
            Im.Line.New();
        }

        if (ImEx.Button("清除未使用的本地模组数据文件"u8, default,
                "删除所有与当前安装的模组不对应的本地模组数据文件。"u8,
                !enabled || cleanup.IsRunning))
            cleanup.CleanUnusedLocalData();
        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteModModifier} 键以删除文件。");

        if (ImEx.Button("清除备份文件"u8, default,
                "删除所有配置文件夹中的 .json 备份文件和模组目录中的模组组备份文件。"u8,
                !enabled || cleanup.IsRunning))
            cleanup.CleanBackupFiles();
        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteModModifier} 键以删除文件。");

        if (ImEx.Button("清除所有未使用的设置"u8, default,
                "删除所有与当前安装的模组不对应的合集中所有模组设置。"u8,
                !enabled || cleanup.IsRunning))
            cleanup.CleanupAllUnusedSettings();
        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteModModifier} 键以删除设置。");
    }
}

public sealed class ManagementTab : TabBar<ManagementTabType>, ITab<TabType>
{
    public new ReadOnlySpan<byte> Label
        => base.Label;

    public TabType Identifier
        => TabType.Management;

    public ManagementTab(Logger log,
        EphemeralConfig config,
        UnusedModsTab unusedMods,
        DuplicateModsTab duplicateMods,
        CleanupTab cleanup)
        : base("模组管理", log, unusedMods, duplicateMods, cleanup)
    {
        NextTab = config.SelectedManagementTab;
        TabSelected.Subscribe((in tab) => config.SelectedManagementTab = tab, 0);
    }

    public void DrawContent()
        => Draw();
}
