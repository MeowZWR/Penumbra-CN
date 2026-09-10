using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.UI.Classes;
using Penumbra.UI.Integration;

namespace Penumbra.UI;

public sealed class SettingsTab(
    MainSettings main,
    BehaviorSettings behavior,
    UiSettings ui,
    IoSettings io,
    EditingSettings editing,
    AdvancedSettings advanced,
    Configuration config,
    TutorialService tutorial,
    PredefinedTagManager predefinedTagManager,
    MigrationSectionDrawer migrationDrawer,
    IntegrationSettingsRegistry integrationSettings,
    Penumbra penumbra)
    : ITab<TabType>
{
    public TabType Identifier
        => TabType.Settings;

    public ReadOnlySpan<byte> Label
        => "插件设置"u8;

    public void PostTabButton()
    {
        tutorial.OpenTutorial(BasicTutorialSteps.Fin);
        tutorial.OpenTutorial(BasicTutorialSteps.Faq1);
        tutorial.OpenTutorial(BasicTutorialSteps.Faq2);
    }

    public void DrawContent()
    {
        using var child = Im.Child.Begin("##SettingsTab"u8, -Vector2.One);
        if (!child)
            return;

        main.DrawHeader();

        using (var header = Im.Tree.HeaderId("常规"u8))
        {
            if (header)
            {
                main.DrawGeneralSettings();
                Im.Line.Spacing();
            }
        }

        using (var header = Im.Tree.HeaderId("Penumbra 行为"u8))
        {
            if (header)
            {
                behavior.Draw();
                Im.Line.Spacing();
            }
        }

        using (var header = Im.Tree.HeaderId("用户界面"u8))
        {
            if (header)
            {
                ui.Draw();
                DrawColorSettings();
                DrawPredefinedTagsSection();
                Im.Line.Spacing();
            }
        }


        using (var header = Im.Tree.HeaderId("模组导入/导出"u8))
        {
            if (header)
            {
                io.Draw();
                using (var node = Im.Tree.Node("模组迁移"u8))
                {
                    if (node)
                        migrationDrawer.Draw();
                }

                Im.Line.Spacing();
            }
        }

        using (var header = Im.Tree.HeaderId("文件编辑"u8))
        {
            if (header)
            {
                editing.Draw();
                Im.Line.Spacing();
            }
        }

        using (var header = Im.Tree.HeaderId("高级"u8))
        {
            if (header)
            {
                advanced.Draw();
                Im.Line.Spacing();
            }
        }

        integrationSettings.Draw();
        DrawSupportButtons();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool Checkbox(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, bool value)
    {
        using var id  = Im.Id.Push(label);
        var       ret = Im.Checkbox(StringU8.Empty, value);
        LunaStyle.DrawAlignedHelpMarkerLabel(label, tooltip);
        return ret;
    }

    /// <summary> Draw the entire Color subsection. </summary>
    private void DrawColorSettings()
    {
        using var header = Im.Tree.Node("配色"u8);
        if (!header)
            return;

        if (!ColorSettingsDrawer.Draw(Penumbra.Messager, config.Ui.Colors, config.Ui.ColorCache))
            return;

        CacheManager.Instance.SetColorsDirty();
        config.Ui.Save();
    }

    /// <summary> Draw the support button group on the right-hand side of the window. </summary>
    private void DrawSupportButtons()
    {
        var width = Im.Font.CalculateSize(UiHelpers.SupportInfoButtonText).X + Im.Style.FramePadding.X * 2;
        var xPos  = Im.Window.Width - width;
        // Respect the scroll bar width.
        if (Im.Scroll.MaximumY > 0)
            xPos -= Im.Style.ScrollbarSize + Im.Style.FramePadding.X;

        Im.Cursor.Position = new Vector2(xPos, Im.Style.FrameHeightWithSpacing);
        UiHelpers.DrawSupportButton(penumbra);

        Im.Cursor.Position = new Vector2(xPos, 0);
        SupportButton.Discord(Penumbra.Messager, width);

        Im.Cursor.Position = new Vector2(xPos, 2 * Im.Style.FrameHeightWithSpacing);
        SupportButton.ReniGuide(Penumbra.Messager, width);

        Im.Cursor.Position = new Vector2(xPos, 3 * Im.Style.FrameHeightWithSpacing);
        if (Im.Button("重新开始教程"u8, new Vector2(width, 0)))
        {
            config.Ephemeral.TutorialStep = 0;
            config.Ephemeral.Save();
        }

        Im.Cursor.Position = new Vector2(xPos, 4 * Im.Style.FrameHeightWithSpacing);
        if (Im.Button("显示更新日志"u8, new Vector2(width, 0)))
            penumbra.ForceChangelogOpen();

        Im.Cursor.Position = new Vector2(xPos, 5 * Im.Style.FrameHeightWithSpacing);
        SupportButton.KoFiPatreon(Penumbra.Messager, new Vector2(width, 0));
    }

    private void DrawPredefinedTagsSection()
    {
        using var node = Im.Tree.Node("标签设置"u8, TreeNodeFlags.DefaultOpen);
        if (!node)
            return;

        var tagIdx = TagButtons.Draw("预定义标签："u8,
            "可以一键添加到模组或从模组中移除的预定义标签。"u8, predefinedTagManager,
            out var editedTag);

        if (tagIdx >= 0)
            predefinedTagManager.ChangeSharedTag(tagIdx, editedTag);
    }
}
