using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Services;

namespace Penumbra.UI.Classes;

public class MigrationSectionDrawer(MigrationManager migrationManager, Configuration config) : IUiService
{
    private bool    _createBackups = true;
    private Vector2 _buttonSize;

    public void Draw()
    {
        using var header = ImUtf8.CollapsingHeaderId("迁移设置"u8);
        if (!header)
            return;

        _buttonSize = UiHelpers.InputTextWidth;
        DrawSettings();
        ImGui.Separator();
        DrawMdlMigration();
        DrawMdlRestore();
        DrawMdlCleanup();
        // TODO enable when this works
        ImGui.Separator();
        //DrawMtrlMigration();
        DrawMtrlRestore();
        DrawMtrlCleanup();
    }

    private void DrawSettings()
    {
        var value = config.MigrateImportedModelsToV6;
        if (ImUtf8.Checkbox("自动迁移V5模型到V6版本"u8, ref value))
        {
            config.MigrateImportedModelsToV6 = value;
            config.Save();
        }

        ImUtf8.HoverTooltip("这会增加版本标记并将骨骼表重构为新版本。"u8);

        // TODO enable when this works
        //value = config.MigrateImportedMaterialsToLegacy;
        //if (ImUtf8.Checkbox("Automatically Migrate Materials to Dawntrail on Import"u8, ref value))
        //{
        //    config.MigrateImportedMaterialsToLegacy = value;
        //    config.Save();
        //}
        //
        //ImUtf8.HoverTooltip(
        //    "This currently only increases the color-table size and switches the shader from 'character.shpk' to 'characterlegacy.shpk', if the former is used."u8);

        ImUtf8.Checkbox("手动迁移时创建备份", ref _createBackups);
    }

    private static ReadOnlySpan<byte> MigrationTooltip
        => "取消迁移。这不会恢复已经完成的迁移。"u8;

    private void DrawMdlMigration()
    {
        if (ImUtf8.ButtonEx("迁移V5模型文件到V6版本"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.MigrateMdlDirectory(config.ModDirectory, _createBackups);

        ImUtf8.SameLineInner();
        DrawCancelButton(MigrationManager.TaskType.MdlMigration, "取消迁移。这不会恢复已经完成的迁移。"u8);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlMigration, IsRunning: true });
        DrawData(migrationManager.MdlMigration, "未找到模型文件。"u8, "已迁移"u8);
    }

    private void DrawMtrlMigration()
    {
        if (ImUtf8.ButtonEx("将材质文件迁移到「金曦之遗辉」"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.MigrateMtrlDirectory(config.ModDirectory, _createBackups);

        ImUtf8.SameLineInner();
        DrawCancelButton(MigrationManager.TaskType.MtrlMigration, MigrationTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MtrlMigration, IsRunning: true });
        DrawData(migrationManager.MtrlMigration, "未找到材质文件。"u8, "已迁移"u8);
    }


    private static ReadOnlySpan<byte> CleanupTooltip
        => "取消清理。注意无法恢复。"u8;

    private void DrawMdlCleanup()
    {
        if (ImUtf8.ButtonEx("删除现有的模型备份文件"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.CleanMdlBackups(config.ModDirectory);

        ImUtf8.SameLineInner();
        DrawCancelButton(MigrationManager.TaskType.MdlCleanup, CleanupTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlCleanup, IsRunning: true });
        DrawData(migrationManager.MdlCleanup, "未找到模型备份文件。"u8, "已删除"u8);
    }

    private void DrawMtrlCleanup()
    {
        if (ImUtf8.ButtonEx("删除现有的材质备份文件"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.CleanMtrlBackups(config.ModDirectory);

        ImUtf8.SameLineInner();
        DrawCancelButton(MigrationManager.TaskType.MtrlCleanup, CleanupTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MtrlCleanup, IsRunning: true });
        DrawData(migrationManager.MtrlCleanup, "未找到材质备份文件。"u8, "已删除"u8);
    }

    private static ReadOnlySpan<byte> RestorationTooltip
        => "取消恢复。这不会恢复已完成的恢复。"u8;

    private void DrawMdlRestore()
    {
        if (ImUtf8.ButtonEx("恢复模型备份"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.RestoreMdlBackups(config.ModDirectory);

        ImUtf8.SameLineInner();
        DrawCancelButton(MigrationManager.TaskType.MdlRestoration, RestorationTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlRestoration, IsRunning: true });
        DrawData(migrationManager.MdlRestoration, "未找到模型备份文件。"u8, "已恢复"u8);
    }

    private void DrawMtrlRestore()
    {
        if (ImUtf8.ButtonEx("恢复材质备份"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.RestoreMtrlBackups(config.ModDirectory);

        ImUtf8.SameLineInner();
        DrawCancelButton(MigrationManager.TaskType.MtrlRestoration, RestorationTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MtrlRestoration, IsRunning: true });
        DrawData(migrationManager.MtrlRestoration, "未找到材质备份文件。"u8, "已恢复"u8);
    }

    private static void DrawSpinner(bool enabled)
    {
        if (!enabled)
            return;

        ImGui.SameLine();
        ImUtf8.Spinner("Spinner"u8, ImGui.GetTextLineHeight() / 2, 2, ImGui.GetColorU32(ImGuiCol.Text));
    }

    private void DrawCancelButton(MigrationManager.TaskType task, ReadOnlySpan<byte> tooltip)
    {
        using var _ = ImUtf8.PushId((int)task);
        if (ImUtf8.ButtonEx("取消"u8, tooltip, disabled: !migrationManager.IsRunning || task != migrationManager.CurrentTask))
            migrationManager.Cancel();
    }

    private static void DrawData(MigrationManager.MigrationData data, ReadOnlySpan<byte> empty, ReadOnlySpan<byte> action)
    {
        if (!data.HasData)
        {
            ImUtf8.IconDummy();
            return;
        }

        var total = data.Total;
        if (total == 0)
            ImUtf8.TextFrameAligned(empty);
        else
            ImUtf8.TextFrameAligned($"{data.Changed} 文件 {action}, {data.Failed} 文件失败, {total} 文件找到。");
    }
}
