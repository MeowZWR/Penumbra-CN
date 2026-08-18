using ImSharp;
using Penumbra.Services;

namespace Penumbra.UI.Classes;

public class MigrationSectionDrawer(MigrationManager migrationManager, Configuration config) : Luna.IUiService
{
    private bool    _createBackups = true;
    private Vector2 _buttonSize;

    public void Draw()
    {
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
        if (Im.Checkbox("自动迁移V5模型到V6版本"u8, config.Io.MigrateImportedModelsToV6))
            config.Io.MigrateImportedModelsToV6 ^= true;

        Im.Tooltip.OnHover("这会增加版本标记并将骨骼表重构为新版本。"u8);

        // TODO enable when this works
        //value = config.MigrateImportedMaterialsToLegacy;
        //if (Im.Checkbox("导入时自动将材质迁移到「金曦之遗辉」"u8, ref value))
        //{
        //    config.MigrateImportedMaterialsToLegacy = value;
        //    config.Save();
        //}
        //
        //Im.Tooltip.OnHover(
        //    "目前仅会增大颜色表尺寸，并在使用 character.shpk 时将其切换为 characterlegacy.shpk。"u8);

        Im.Checkbox("手动迁移时创建备份"u8, ref _createBackups);
    }

    private static ReadOnlySpan<byte> MigrationTooltip
        => "取消迁移。这不会恢复已经完成的迁移。"u8;

    private void DrawMdlMigration()
    {
        if (ImEx.Button("迁移V5模型文件到V6版本"u8, _buttonSize, StringU8.Empty, migrationManager.IsRunning))
            migrationManager.MigrateMdlDirectory(config.Main.ModDirectory, _createBackups);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MdlMigration, "取消迁移。这不会恢复已经完成的迁移。"u8);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlMigration, IsRunning: true });
        DrawData(migrationManager.MdlMigration, "未找到模型文件。"u8, "已迁移"u8);
    }

    private void DrawMtrlMigration()
    {
        if (ImEx.Button("将材质文件迁移到「金曦之遗辉」"u8, _buttonSize, StringU8.Empty, migrationManager.IsRunning))
            migrationManager.MigrateMtrlDirectory(config.Main.ModDirectory, _createBackups);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MtrlMigration, MigrationTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MtrlMigration, IsRunning: true });
        DrawData(migrationManager.MtrlMigration, "未找到材质文件。"u8, "已迁移"u8);
    }


    private static ReadOnlySpan<byte> CleanupTooltip
        => "取消清理。注意无法恢复。"u8;

    private void DrawMdlCleanup()
    {
        if (ImEx.Button("删除现有的模型备份文件"u8, _buttonSize, StringU8.Empty, migrationManager.IsRunning))
            migrationManager.CleanMdlBackups(config.Main.ModDirectory);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MdlCleanup, CleanupTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlCleanup, IsRunning: true });
        DrawData(migrationManager.MdlCleanup, "未找到模型备份文件。"u8, "已删除"u8);
    }

    private void DrawMtrlCleanup()
    {
        if (ImEx.Button("删除现有的材质备份文件"u8, _buttonSize, StringU8.Empty, migrationManager.IsRunning))
            migrationManager.CleanMtrlBackups(config.Main.ModDirectory);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MtrlCleanup, CleanupTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MtrlCleanup, IsRunning: true });
        DrawData(migrationManager.MtrlCleanup, "未找到材质备份文件。"u8, "已删除"u8);
    }

    private static ReadOnlySpan<byte> RestorationTooltip
        => "取消恢复。这不会恢复已经完成的恢复。"u8;

    private void DrawMdlRestore()
    {
        if (ImEx.Button("恢复模型备份"u8, _buttonSize, StringU8.Empty, migrationManager.IsRunning))
            migrationManager.RestoreMdlBackups(config.Main.ModDirectory);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MdlRestoration, RestorationTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlRestoration, IsRunning: true });
        DrawData(migrationManager.MdlRestoration, "未找到模型备份文件。"u8, "已恢复"u8);
    }

    private void DrawMtrlRestore()
    {
        if (ImEx.Button("恢复材质备份"u8, _buttonSize, StringU8.Empty, migrationManager.IsRunning))
            migrationManager.RestoreMtrlBackups(config.Main.ModDirectory);

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
        if (ImEx.Button("取消"u8, Vector2.Zero, tooltip, !migrationManager.IsRunning || task != migrationManager.CurrentTask))
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
            ImEx.TextFrameAligned($"{data.Changed} 文件 {action}，{data.Failed} 文件失败，共找到 {total} 个文件。");
    }
}
