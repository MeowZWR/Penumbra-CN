using ImSharp;
using Penumbra.Services;

namespace Penumbra.UI.Classes;

public class MigrationSectionDrawer(MigrationManager migrationManager, Configuration config) : Luna.IUiService
{
    private bool    _createBackups = true;
    private Vector2 _buttonSize;

    public void Draw()
    {
        using var header = Im.Tree.HeaderId("迁移设置"u8);
        if (!header)
            return;

        _buttonSize = UiHelpers.InputTextWidth;
        DrawSettings();
        Im.Separator();
        DrawMdlMigration();
        DrawMdlRestore();
        DrawMdlCleanup();
        // TODO enable when this works
        Im.Separator();
        //DrawMtrlMigration();
        DrawMtrlRestore();
        DrawMtrlCleanup();
    }

    private void DrawSettings()
    {
        var value = config.MigrateImportedModelsToV6;
        if (Im.Checkbox("自动迁移V5模型到V6版本"u8, ref value))
        {
            config.MigrateImportedModelsToV6 = value;
            config.Save();
        }

        Im.Tooltip.OnHover("这会增加版本标记并将骨骼表重构为新版本。"u8);

        // TODO enable when this works
        //value = config.MigrateImportedMaterialsToLegacy;
        //if (Im.Checkbox("Automatically Migrate Materials to Dawntrail on Import"u8, ref value))
        //{
        //    config.MigrateImportedMaterialsToLegacy = value;
        //    config.Save();
        //}
        //
        //Im.Tooltip.OnHover(
        //    "This currently only increases the color-table size and switches the shader from 'character.shpk' to 'characterlegacy.shpk', if the former is used."u8);

        Im.Checkbox("手动迁移时创建备份"u8, ref _createBackups);
    }

    private static ReadOnlySpan<byte> MigrationTooltip
        => "取消迁移。这不会恢复已经完成的迁移。"u8;

    private void DrawMdlMigration()
    {
        if (Im.Button("迁移V5模型文件到V6版本"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.MigrateMdlDirectory(config.ModDirectory, _createBackups);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MdlMigration, "取消迁移。这不会恢复已经完成的迁移。"u8);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlMigration, IsRunning: true });
        DrawData(migrationManager.MdlMigration, "未找到模型文件。"u8, "已迁移"u8);
    }

    private void DrawMtrlMigration()
    {
        if (ImEx.Button("将材质文件迁移到「金曦之遗辉」"u8, _buttonSize, StringU8.Empty, migrationManager.IsRunning))
            migrationManager.MigrateMtrlDirectory(config.ModDirectory, _createBackups);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MtrlMigration, MigrationTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MtrlMigration, IsRunning: true });
        DrawData(migrationManager.MtrlMigration, "未找到材质文件。"u8, "已迁移"u8);
    }


    private static ReadOnlySpan<byte> CleanupTooltip
        => "取消清理。注意无法恢复。"u8;

    private void DrawMdlCleanup()
    {
        if (Im.Button("删除现有的模型备份文件"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.CleanMdlBackups(config.ModDirectory);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MdlCleanup, CleanupTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlCleanup, IsRunning: true });
        DrawData(migrationManager.MdlCleanup, "未找到模型备份文件。"u8, "已删除"u8);
    }

    private void DrawMtrlCleanup()
    {
        if (Im.Button("删除现有的材质备份文件"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.CleanMtrlBackups(config.ModDirectory);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MtrlCleanup, CleanupTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MtrlCleanup, IsRunning: true });
        DrawData(migrationManager.MtrlCleanup, "未找到材质备份文件。"u8, "已删除"u8);
    }

    private static ReadOnlySpan<byte> RestorationTooltip
        => "取消恢复。这不会恢复已完成的恢复。"u8;

    private void DrawMdlRestore()
    {
        if (Im.Button("恢复模型备份"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.RestoreMdlBackups(config.ModDirectory);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MdlRestoration, RestorationTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlRestoration, IsRunning: true });
        DrawData(migrationManager.MdlRestoration, "未找到模型备份文件。"u8, "已恢复"u8);
    }

    private void DrawMtrlRestore()
    {
        if (Im.Button("恢复材质备份"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.RestoreMtrlBackups(config.ModDirectory);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MtrlRestoration, RestorationTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MtrlRestoration, IsRunning: true });
        DrawData(migrationManager.MtrlRestoration, "未找到材质备份文件。"u8, "已恢复"u8);
    }

    private static void DrawSpinner(bool enabled)
    {
        if (!enabled)
            return;

        Im.Line.Same();
        ImEx.Spinner("Spinner"u8, Im.Style.TextHeight / 2, 2, ImGuiColor.Text.Get());
    }

    private void DrawCancelButton(MigrationManager.TaskType task, ReadOnlySpan<byte> tooltip)
    {
        using var _ = Im.Id.Push((int)task);
        if (Im.Button("取消"u8, Vector2.Zero, tooltip, !migrationManager.IsRunning || task != migrationManager.CurrentTask))
            migrationManager.Cancel();
    }

    private static void DrawData(MigrationManager.MigrationData data, ReadOnlySpan<byte> empty, ReadOnlySpan<byte> action)
    {
        if (!data.HasData)
        {
            Im.FrameDummy();
            return;
        }

        var total = data.Total;
        if (total is 0)
            ImEx.TextFrameAligned(empty);
        else
            ImEx.TextFrameAligned($"{data.Changed} 文件 {action}, {data.Failed} 文件失败, {total} 文件找到。");
    }
}
