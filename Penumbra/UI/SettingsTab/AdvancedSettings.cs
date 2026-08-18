using ImSharp;
using JetBrains.Annotations;
using Luna;
using Penumbra.Import.Textures;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Interop.Services;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.UI;

public sealed class AdvancedSettings(
    AdvancedConfig config,
    FileCompactor compactor,
    ModManager modManager,
    DalamudConfigService dalamudConfig,
    FontReloader fontReloader,
    ResidentResourceManager residentResources,
    CharacterUtility characterUtility) : IUiService
{
    [UsedImplicitly]
    private readonly bool _initializedCompactor = InitializeCompactor(config, compactor);

    /// <summary> Draw all advanced settings. </summary>
    public void Draw()
    {
        if (SettingsTab.Checkbox("启用Penumbra崩溃记录（实验性功能）"u8,
                "使Penumbra能够启动一个二级进程，记录一些游戏活动，这可能对诊断与Penumbra相关的游戏崩溃有帮助，也可能没帮助。"u8,
                config.UseCrashHandler ?? false))
            config.UseCrashHandler = !(config.UseCrashHandler ?? false);

        DrawMinimumDimensionConfig();
        DrawHdrRenderTargets();
        DrawAuxiliaryDeviceMode();
        if (SettingsTab.Checkbox("导入时自动清除重复文件"u8,
                "导入时自动清除模组中的重复文件。这将使模组文件的占用变小，但会删除（二进制完全相同的）文件。"u8,
                config.AutoDeduplicateOnImport))
            config.AutoDeduplicateOnImport ^= true;
        if (SettingsTab.Checkbox("PMP导入时自动重复复制UI文件"u8,
                "从PMP文件导入时自动重复复制并规范化与UI有关的文件。强烈建议启用此选项，因为UI文件导入时去重会导致游戏崩溃。"u8,
                config.AutoReduplicateUiOnImport))
            config.AutoDeduplicateOnImport ^= true;
        DrawCompressionBox();
        if (SettingsTab.Checkbox("导入时保持默认的元数据修改"u8,
                "通常情况下，元数据修改的值（有时是由TexTools导出的）与游戏默认的值相同时，将被抛弃。"u8
              + "切换此选项以保留它们 - 假如你认为某个模组中的某个选项在先前的选项中被禁用了元数据的修改。"u8,
                config.KeepDefaultMetaChanges))
            config.KeepDefaultMetaChanges ^= true;
        if (SettingsTab.Checkbox("启用自定义形状与属性支持"u8,
                "Penumbra将允许对模组模型的自定义形状键和属性进行识别与合并。"u8,
                config.EnableCustomShapes))
            config.EnableCustomShapes ^= true;
        DrawWaitForPluginsReflection();
        DrawEnableHttpApiBox();
        DrawEnableDebugModeBox();
        Im.Separator();
        DrawReloadResourceButton();
        DrawReloadFontsButton();
        Im.Line.Spacing();
    }

    private void DrawCompressionBox()
    {
        if (!compactor.CanCompact)
            return;

        if (SettingsTab.Checkbox("使用文件系统压缩"u8,
                "使用 Windows 功能（压缩驱动器）可以明显地减少计算机上模组文件的存储大小。\n会提高CPU负担减少硬盘负担，对硬盘负担大CPU负担小的电脑性能有益。对硬盘负担小CPU负担大的电脑则可能减少性能。"u8,
                config.UseFileSystemCompression))
        {
            config.UseFileSystemCompression ^= true;
            compactor.Enabled               =  config.UseFileSystemCompression;
        }

        Im.Line.Same();
        if (ImEx.Button("压缩现有文件"u8, Vector2.Zero,
                "尝试压缩根目录中的所有文件。这需要一段时间。"u8,
                compactor.MassCompactRunning || !modManager.Valid))
            compactor.StartMassCompact(modManager.BasePath.EnumerateFiles("*.*", SearchOption.AllDirectories),
                CompressionAlgorithm.Xpress8K,
                true);

        Im.Line.Same();
        if (ImEx.Button("解压缩现有文件"u8, Vector2.Zero,
                "尝试解压缩根目录中的所有文件。这需要一段时间。"u8,
                compactor.MassCompactRunning || !modManager.Valid))
            compactor.StartMassCompact(modManager.BasePath.EnumerateFiles("*.*", SearchOption.AllDirectories), CompressionAlgorithm.None,
                true);

        if (compactor.MassCompactRunning)
        {
            Im.ProgressBar((float)compactor.CurrentIndex / compactor.TotalFiles, new Vector2(
                    Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X - UiHelpers.IconButtonSize.X,
                    Im.Style.FrameHeight),
                compactor.CurrentFile?.FullName[(modManager.BasePath.FullName.Length + 1)..] ?? "正在收集文件...");
            Im.Line.Same();
            if (ImEx.Icon.Button(LunaStyle.CancelIcon, "取消此批量操作。"u8, !compactor.MassCompactRunning))
                compactor.CancelMassCompact();
        }
        else
        {
            Im.FrameDummy();
        }
    }

    /// <summary> Draw two integral inputs for minimum dimensions of this window. </summary>
    private void DrawMinimumDimensionConfig()
    {
        var warning = config.MinimumSize.X < AdvancedConfig.MinimumSizeX
            ? config.MinimumSize.Y < AdvancedConfig.MinimumSizeY
                ? "尺寸小于默认值：这可能看起来不理想。"u8
                : "宽度小于默认值：这可能看起来不理想。"u8
            : config.MinimumSize.Y < AdvancedConfig.MinimumSizeY
                ? "高度小于默认值：这可能看起来不理想。"u8
                : StringU8.Empty;
        var buttonWidth = UiHelpers.InputTextWidth.X / 2.5f;
        Im.Item.SetNextWidth(buttonWidth);
        if (ImEx.InputOnDeactivation.Drag("##xMinSize"u8, (int)config.MinimumSize.X, out var newX, 500, 1500, 0.1f))
            config.MinimumSize = config.MinimumSize with { X = newX };

        Im.Line.Same();
        Im.Item.SetNextWidth(buttonWidth);
        if (ImEx.InputOnDeactivation.Drag("##yMinSize"u8, (int)config.MinimumSize.Y, out var newY, 300, 1500, 0.1f))
            config.MinimumSize = config.MinimumSize with { Y = newY };

        Im.Line.Same();
        if (ImEx.Button("Reset##resetMinSize"u8, new Vector2(buttonWidth / 2 - Im.Style.ItemSpacing.X * 2, 0),
                $"将最小尺寸重置为({AdvancedConfig.MinimumSizeX}, {AdvancedConfig.MinimumSizeY})。",
                config.MinimumSize is { X: AdvancedConfig.MinimumSizeX, Y: AdvancedConfig.MinimumSizeY }))
            config.MinimumSize = new Vector2(AdvancedConfig.MinimumSizeX, AdvancedConfig.MinimumSizeY);

        LunaStyle.DrawAlignedHelpMarkerLabel("窗口最小尺寸"u8,
            "设置此窗口的最小尺寸。不建议将值设置地比默认最小尺寸更小，可能导致窗口看起来很糟很混乱。"u8);

        if (warning.Length > 0)
            ImEx.TextFramed(warning, UiHelpers.InputTextWidth, DalamudColor.AttentionBackground.Value);
        else
            Im.Line.New();
    }

    private void DrawHdrRenderTargets()
    {
        if (!RenderTargetHdrEnabler.HdrModeSupported)
            return;

#pragma warning disable CS0162 // Unreachable code detected
        Im.Item.SetNextWidth(Im.Font.CalculateSize("M"u8).X * 5.0f + Im.Style.FrameHeight);
        using (var combo = Im.Combo.Begin("##hdrRenderTarget"u8, config.HdrRenderTargets ? "HDR"u8 : "SDR"u8))
        {
            if (combo)
            {
                if (Im.Selectable("HDR"u8, config.HdrRenderTargets) && !config.HdrRenderTargets)
                {
                    config.HdrRenderTargets = true;
                    config.Save();
                }

                if (Im.Selectable("SDR"u8, !config.HdrRenderTargets) && config.HdrRenderTargets)
                {
                    config.HdrRenderTargets = false;
                    config.Save();
                }
            }
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("漫反射动态范围"u8,
            "设置材质中漫反射颜色可用的动态范围，以避免产生视觉伪影。\n"u8
          + "更改此设置需要重启游戏。此设置仅在启用[启动时等待插件]时有效。"u8);
#pragma warning restore CS0162 // Unreachable code detected
    }

    private void DrawAuxiliaryDeviceMode()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        using (var combo = Im.Combo.Begin("##auxiliaryDeviceMode"u8, config.AuxiliaryDeviceMode.ToNameU8()))
        {
            if (combo)
                foreach (var value in AuxiliaryDeviceMode.Values)
                {
                    if (Im.Selectable(value.ToNameU8(), config.AuxiliaryDeviceMode == value))
                        config.AuxiliaryDeviceMode = value;

                    Im.Tooltip.OnHover(value.Tooltip());
                }
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("纹理压缩硬件加速模式"u8,
            "如何管理纹理压缩的硬件加速。\n如果压缩纹理后遇到 ReShade 问题，请更改此项。"u8);
    }

    /// <summary> Draw a checkbox for the HTTP API that creates and destroys the web server when toggled. </summary>
    private void DrawEnableHttpApiBox()
    {
        if (SettingsTab.Checkbox("启用 HTTP API"u8,
                "允许其他程序（如Anamnesis）使用Penumbra的功能，比如请求重绘。"u8, config.EnableHttpApi))
            config.EnableHttpApi ^= true;
    }

    /// <summary> Draw a checkbox to toggle Debug mode. </summary>
    private void DrawEnableDebugModeBox()
    {
        if (SettingsTab.Checkbox("启用调试模式"u8,
                "[DEBUG] 启用调试和资源管理器选项卡，操作一些额外数据。在插件加载时也会自动打开设置窗口。"u8,
                config.DebugMode))
            config.DebugMode ^= true;
    }

    /// <summary> Draw a button that reloads resident resources. </summary>
    private void DrawReloadResourceButton()
    {
        if (ImEx.Button("重新加载常驻资源"u8, Vector2.Zero,
                "重新加载一些始终保留在内存中的游戏特定文件。\n通常不需要执行此操作。"u8,
                !characterUtility.Ready))
            residentResources.Reload();
    }

    /// <summary> Draw a button that reloads fonts. </summary>
    private void DrawReloadFontsButton()
    {
        if (ImEx.Button("重新加载字体"u8, Vector2.Zero, "强制游戏重新加载调用的字体文件。"u8, !fontReloader.Valid))
            fontReloader.Reload();
    }


    /// <summary> Draw a checkbox that toggles the dalamud setting to wait for plugins on open. </summary>
    private void DrawWaitForPluginsReflection()
    {
        if (!dalamudConfig.GetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, out bool value))
        {
            using var disabled = Im.Disabled();
            SettingsTab.Checkbox("在游戏加载之前等待插件加载 (已禁用，无法访问Dalamud设置。）"u8, StringU8.Empty,
                false);
        }
        else
        {
            if (SettingsTab.Checkbox("在游戏加载之前等待插件加载"u8,
                    "有些模组需要在游戏开始时加载一次，之后不再加载的文件。\n"u8
                  + "游戏文件加载后Penumbra才加载该文件可能会导致出现问题。\n"u8
                  + "这个设置将导致游戏等待，直到Penumbra里的某些模组完成加载，使这些模组（一般在基础合集中）能够正常生效。\n\n"u8
                  + "这将更改Dalamud设置(命令 /xlsettings) -> 基本配置中的设置。"u8, value))
                dalamudConfig.SetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, !value, "doWaitForPluginsOnStartup");
        }
    }

    private static bool InitializeCompactor(AdvancedConfig config, FileCompactor compactor)
    {
        if (compactor.CanCompact)
            compactor.Enabled = config.UseFileSystemCompression;
        return true;
    }
}
