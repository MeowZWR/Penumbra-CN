using Dalamud.Interface;
using Dalamud.Interface.DragDrop;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using Penumbra.Api.Preset;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Gui;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab.ModPreview;
using Penumbra.UI.ModsTab.Settings;

namespace Penumbra.UI.ModsTab.Optimized;

public sealed class OptimizedModPanelSettingsTab(
    CollectionManager collectionManager,
    ModManager modManager,
    ModSelection selection,
    TutorialService tutorial,
    CommunicatorService communicator,
    OptimizedModGroupDrawer modGroupDrawer,
    Configuration config,
    PresetCombo presets,
    ITextureProvider textureProvider,
    IDragDropManager dragDrop,
    IDalamudPluginInterface pluginInterface,
    INotificationManager notificationManager)
    : ITab<ModPanelTab>, IDisposable
{
    private bool _temporary;
    private bool _locked;
    private int? _currentPriority;
    private bool _editPresetMode;
    private bool _actualEditPresetMode;
    private readonly ModPreviewImagePanel _imagePanel = new(modManager, pluginInterface, textureProvider, dragDrop, config, notificationManager);
    private bool _previewExpanded;
    private readonly ClipboardImageImporter _clipboardImporter = new(notificationManager, pluginInterface, modManager);
    private readonly ModPreviewDownloader _previewDownloader = new(notificationManager, config);

    public ReadOnlySpan<byte> Label
        => "模组设置"u8;

    public ModPanelTab Identifier
        => ModPanelTab.Settings;

    public void PostTabButton()
        => tutorial.OpenTutorial(BasicTutorialSteps.ModOptions);

    public void Reset()
        => _currentPriority = null;

    public void DrawContent()
    {
        using var optimizedId = Im.Id.Push("OptimizedSettings"u8);
        var ui = config.Ui;
        if (ui.SavePreviewPanelState && _previewExpanded != _imagePanel.Config.Expanded)
            _previewExpanded = _imagePanel.Config.Expanded;

        var totalAvailableWidth = Im.ContentRegion.Available.X;
        var gutterWidth = PreviewCollapseGutterWidth;
        var maxAllowedWidth = (totalAvailableWidth - gutterWidth) * ui.PreviewPanelRatio;
        var safeMinWidth = Math.Min(ui.PreviewPanelMinWidth, maxAllowedWidth);
        var safeMaxWidth = Math.Min(ui.PreviewPanelMaxWidth, maxAllowedWidth);
        var calculatedPreviewWidth = (totalAvailableWidth - gutterWidth) * ui.PreviewPanelRatio;
        var previewWidth = _previewExpanded && ui.ShowModPreviewPanel
            ? Math.Clamp(calculatedPreviewWidth, safeMinWidth * UiHelpers.Scale, safeMaxWidth * UiHelpers.Scale)
            : 0;
        var mainWidth = ui.ShowModPreviewPanel
            ? totalAvailableWidth - gutterWidth - (_previewExpanded ? previewWidth : 0)
            : totalAvailableWidth;

        using (var mainPanel = Im.Child.Begin("##MainSettingsPanel"u8, new Vector2(mainWidth, -1), false, WindowFlags.NoScrollbar))
        {
            if (mainPanel)
                DrawSettingsPanelContent();
        }

        if (!ui.ShowModPreviewPanel)
            return;

        Im.Line.Same(0, 0);
        DrawPreviewCollapseButton();
        if (!_previewExpanded)
            return;

        Im.Line.Same(0, 0);
        DrawPreviewPanel(previewWidth);
    }

    private void DrawSettingsPanelContent()
    {
        using var id = Im.Id.Push(selection.ModName);
        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current,
            () => new ModSettingsCache(selection, config.Ui, communicator, Im.State.Storage));

        _actualEditPresetMode = _editPresetMode && presets.Selected is not null;
        _temporary = selection.TemporarySettings is not null;
        _locked = (selection.TemporarySettings?.Lock ?? 0) > 0;

        if (cache.VisiblePages.Count > 1 && config.Ui.DisplayPages)
        {
            DrawPreamble();
            if (_actualEditPresetMode)
                DrawEditPresetMode();
            else
            {
                communicator.PostEnabledDraw.Invoke(new PostEnabledDraw.Arguments(selection.Mod!));
                modGroupDrawer.Draw(cache, selection.Mod!, selection.Settings, selection.TemporarySettings);
            }
        }
        else
        {
            using var style = ImStyleDouble.CellPadding.PushY(0);
            using var table = Im.Table.Begin("##settings"u8, 1, TableFlags.ScrollY, Im.ContentRegion.Available);
            if (!table)
                return;

            table.SetupScrollFreeze(0, 1);
            table.NextColumn();
            style.Pop();
            DrawPreamble();
            Im.Dummy(0);
            table.NextColumn();
            if (_actualEditPresetMode)
                DrawEditPresetMode();
            else
            {
                communicator.PostEnabledDraw.Invoke(new PostEnabledDraw.Arguments(selection.Mod!));
                modGroupDrawer.Draw(cache, selection.Mod!, selection.Settings, selection.TemporarySettings);
            }
        }
    }

    private void DrawPreamble()
    {
        if (!_actualEditPresetMode)
        {
            DrawTemporaryWarning();
            DrawInheritedWarning();
        }

        Im.Dummy(Vector2.Zero);
        DrawPresetRow();
        if (_actualEditPresetMode)
            return;

        communicator.PreSettingsPanelDraw.Invoke(new PreSettingsPanelDraw.Arguments(selection.Mod!));
        DrawEnabledInput();
        tutorial.OpenTutorial(BasicTutorialSteps.EnablingMods);
        Im.Line.Same();
        DrawPriorityInput();
        tutorial.OpenTutorial(BasicTutorialSteps.Priority);
        Im.Line.SameInner();
        DrawPresetVisibilityInput();
        Im.Line.SameInner();
        DrawPageTabsInput();
        DrawRemoveSettings();
    }

    private void DrawPresetVisibilityInput()
    {
        var visible = !config.Ui.HideTraditionalModSettingsPresetBar;
        if (Im.Checkbox("预设"u8, ref visible))
            config.Ui.HideTraditionalModSettingsPresetBar = !visible;
        Im.Tooltip.OnHover("显示或隐藏传统模组设置界面中的预设栏。"u8);
    }

    private void DrawPageTabsInput()
    {
        var showPages = !selection.Mod!.IgnorePages;
        if (Im.Checkbox("页签"u8, ref showPages))
            modManager.DataEditor.ChangeIgnorePages(selection.Mod, !showPages);
        Im.Tooltip.OnHover("如果此模组有选项页，启用后会以标签页的形式显示。"u8);
    }

    private void DrawTemporaryWarning()
    {
        if (!_temporary)
            return;

        using var color =
            ImGuiColor.Button.Push(Rgba32.TintColor(Im.Style[ImGuiColor.Button], ColorId.TemporaryModSettingsTint.Vector));
        var width = Im.ContentRegion.Available with { Y = 0 };
        if (ImEx.Button($"这些设置由 {selection.TemporarySettings!.Source} 临时设定{(_locked ? "，且已锁定。" : "。")}",
                width, _locked))
            collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!, null);

        Im.Tooltip.OnHover("更改临时设置中的设置不会在会话之间保存。\n"u8
          + "你可以点击这个按钮移除临时设置并返回到正常设置。"u8);
    }

    private void DrawInheritedWarning()
    {
        if (!selection.Inherited)
            return;

        using var color = ImGuiColor.Button.Push(Colors.PressEnterWarningBg);
        var width = Im.ContentRegion.Available with { Y = 0 };
        if (ImEx.Button($"这些设置继承自 {selection.Collection.Identity.Name}。", width, _locked))
        {
            if (_temporary)
            {
                selection.TemporarySettings!.ForceInherit = false;
                collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!, selection.TemporarySettings);
            }
            else
            {
                collectionManager.Editor.SetModInheritance(collectionManager.Active.Current, selection.Mod!, false);
            }
        }

        Im.Tooltip.OnHover("你可以点击这个按钮将当前设置独立到此合集。\n"u8
          + "你也可以直接更改任何设置，这会把当前设置连同该项改动一起复制到此合集。"u8);
    }

    private void DrawEnabledInput()
    {
        var enabled = selection.Settings.Enabled;
        using var disabled = Im.Disabled(_locked);
        if (!Im.Checkbox("启用"u8, ref enabled))
            return;

        modManager.SetKnown(selection.Mod!);
        if (_temporary || config.Main.DefaultTemporaryMode)
        {
            var temporarySettings = selection.TemporarySettings ?? new TemporaryModSettings(selection.Mod!, selection.Settings);
            temporarySettings.ForceInherit = false;
            temporarySettings.Enabled = enabled;
            collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!, temporarySettings);
        }
        else
        {
            collectionManager.Editor.SetModState(collectionManager.Active.Current, selection.Mod!, enabled);
        }
    }

    private static float LabelButtonWidth(ReadOnlySpan<byte> label)
        => Im.Font.CalculateButtonSize(label).X + 2 * Im.Style.FramePadding.X;

    private static float SettingsToggleButtonWidth()
    {
        var smallWidth = Math.Max(
            Math.Max(LabelButtonWidth("应用"u8), LabelButtonWidth("复制"u8)),
            Math.Max(LabelButtonWidth("继承"u8), LabelButtonWidth("临时"u8)));
        return smallWidth * 2 + Im.Style.ItemInnerSpacing.X;
    }

    private float SettingsButtonsRightPadding()
        => config.Ui.ShowModPreviewPanel && Im.Scroll.MaximumY is 0
            ? Im.Style.ScrollbarSize
            : 0;

    private float RemainingOnLine()
        => Im.ContentRegion.Maximum.X - Im.Cursor.PositionPreviousLine.X;

    private float FitPresetComboWidth(float rightExceptCombo)
    {
        var min = 100 * Im.Style.GlobalScale;
        var padding = SettingsButtonsRightPadding();
        var sameLine = RemainingOnLine() - Im.Style.ItemSpacing.X - padding - rightExceptCombo;
        return sameLine >= min
            ? sameLine
            : Math.Max(min, Im.ContentRegion.Available.X - padding - rightExceptCombo);
    }

    private void AlignRightOrWrap(float width, bool alignWrapped = true)
    {
        width += SettingsButtonsRightPadding();
        var spacing = RemainingOnLine() - width;
        if (spacing >= Im.Style.ItemSpacing.X)
        {
            Im.Line.Same(0, spacing);
            return;
        }

        if (alignWrapped)
        {
            var alignedX = Im.ContentRegion.Maximum.X - width;
            if (alignedX > Im.Cursor.X)
                Im.Cursor.X = alignedX;
        }
    }

    private void DrawPresetRow()
    {
        if (config.Ui.HideTraditionalModSettingsPresetBar)
            return;

        using var id = Im.Id.Push("presets"u8);
        if (ImEx.Icon.Button(LunaStyle.FromClipboardIcon, "尝试从剪贴板导入设置预设。"u8))
            if (SettingPresetData.FromClipboard(out var data))
                collectionManager.Editor.ApplyPreset(collectionManager.Active.Current, selection.Mod!, data,
                    config.Main.DefaultTemporaryMode || _temporary);
        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.ToClipboardIcon, "将当前设置复制到剪贴板作为可分享预设。"u8))
            SettingPresetData.FromMod(selection.Mod!, selection.Settings).ToClipboard();

        var buttonSize = SettingsToggleButtonWidth();
        var smallButtonSize = new Vector2((buttonSize - Im.Style.ItemInnerSpacing.X) / 2, 0);
        var rightExceptCombo = 2 * Im.Style.FrameHeight + 2 * Im.Style.ItemInnerSpacing.X + Im.Style.ItemSpacing.X + buttonSize;
        var comboWidth = FitPresetComboWidth(rightExceptCombo);
        AlignRightOrWrap(rightExceptCombo + comboWidth);
        if (ImEx.Icon.Button(LunaStyle.SaveIcon, "将当前设置保存为此模组的新预设。"u8))
            Im.Popup.Open("presetName"u8);

        Im.Line.SameInner();
        presets.Draw(StringU8.Empty, comboWidth);
        Im.Line.SameInner();
        using (ImGuiColor.Button.Push(ImGuiColor.ButtonActive.Vector, _editPresetMode && presets.Selected is not null))
        {
            if (ImEx.Icon.Button(LunaStyle.EditIcon, "编辑当前选中的预设。"u8, presets.Selected is null))
                _editPresetMode ^= true;
        }

        Im.Line.Same();
        if (ImEx.Button("应用"u8, smallButtonSize, "将此预设应用到当前合集。此操作会遵循临时设置模式。"u8,
                _locked || presets.Selected is null))
        {
            collectionManager.Editor.ApplyPreset(collectionManager.Active.Current, selection.Mod!, presets.Selected!.Data,
                config.Main.DefaultTemporaryMode || _temporary);
            presets.PresetManager.ChangeLastApply(presets.ModIdentifier, presets.Selected!, DateTimeOffset.UtcNow);
        }

        var hovered = Im.Item.Hovered();
        Im.Line.SameInner();
        if (ImEx.Button("复制"u8, smallButtonSize, "将此预设数据复制到剪贴板以便分享。"u8, presets.Selected is null))
        {
            presets.Selected!.Data.ToClipboard();
            presets.PresetManager.ChangeLastApply(presets.ModIdentifier, presets.Selected!, DateTimeOffset.UtcNow);
        }

        if (hovered || Im.Item.Hovered())
        {
            using var _ = Im.Style.PushDefault();
            using var tt = Im.Tooltip.Begin();
            LunaStyle.DrawSeparator();
            presets.DrawTooltip(presets.Selected!);
        }

        if (InputPopup.OpenName("presetName"u8, out var name))
            presets.PresetManager.AddPreset(selection.Mod!, selection.Settings, name);
    }

    private void DrawPriorityInput()
    {
        using var group = Im.Group();
        var settings = selection.Settings;
        var priority = _currentPriority ?? settings.Priority.Value;
        Im.Item.SetNextWidth(50 * Im.Style.GlobalScale);
        using var disabled = Im.Disabled(_locked);
        if (Im.Input.Scalar("##Priority"u8, ref priority))
            _currentPriority = priority;
        if (new ModPriority(priority).IsHidden)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled,
                $"此优先级为特殊值，会在冲突选项卡中隐藏此模组（{ModPriority.HiddenMin}–{ModPriority.HiddenMax}）。");

        if (Im.Item.DeactivatedAfterEdit && _currentPriority.HasValue)
        {
            if (_currentPriority != settings.Priority.Value)
            {
                if (_temporary || config.Main.DefaultTemporaryMode)
                {
                    var temporarySettings = selection.TemporarySettings ?? new TemporaryModSettings(selection.Mod!, selection.Settings);
                    temporarySettings.ForceInherit = false;
                    temporarySettings.Priority = new ModPriority(_currentPriority.Value);
                    collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!,
                        temporarySettings);
                }
                else
                {
                    collectionManager.Editor.SetModPriority(collectionManager.Active.Current, selection.Mod!,
                        new ModPriority(_currentPriority.Value));
                }
            }

            _currentPriority = null;
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("优先级"u8, "优先级更高的模组文件将优先使用。\n"u8
          + "如果要用模组A覆盖模组B，则模组A的优先级应高于模组B。"u8);
    }

    private void DrawRemoveSettings()
    {
        var drawInherited = selection is { Inherited: false, Settings.IsEmpty: false };
        var buttonSize = SettingsToggleButtonWidth();
        var smallButtonSize = new Vector2((buttonSize - Im.Style.ItemInnerSpacing.X) / 2, 0);
        AlignRightOrWrap(buttonSize, false);
        var enabled = LunaStyle.Modifier.Destructive.Active;
        if (drawInherited)
        {
            var inherit = (enabled, _locked) switch
            {
                (true, false) => ImEx.Button("继承"u8, smallButtonSize,
                    "从此合集移除当前设置，以便它可以继承设置。\n"u8
                  + "如果没有继承的合集为此模组设置了设置，它将被禁用。"u8),
                (false, false) => ImEx.Button("继承"u8, smallButtonSize,
                    $"从此合集移除当前设置，以便它可以继承设置。\n按住 {LunaStyle.Modifier.Destructive} 以进行继承。",
                    true),
                (_, true) => ImEx.Button("继承"u8, smallButtonSize,
                    "从此合集移除当前设置，以便它可以继承设置。\n设置当前被锁定，无法更改。"u8,
                    true),
            };
            if (inherit)
            {
                if (_temporary || config.Main.DefaultTemporaryMode)
                {
                    var temporarySettings = selection.TemporarySettings ?? new TemporaryModSettings(selection.Mod!, selection.Settings);
                    temporarySettings.ForceInherit = true;
                    collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!,
                        temporarySettings);
                }
                else
                {
                    collectionManager.Editor.SetModInheritance(collectionManager.Active.Current, selection.Mod!, true);
                }
            }

            Im.Line.SameInner();
        }
        else
        {
            Im.Cursor.X += smallButtonSize.X + Im.Style.ItemInnerSpacing.X;
        }

        if (_temporary)
        {
            var overwrite = enabled
                ? ImEx.Button("永久"u8, smallButtonSize,
                    "使用当前的临时设置覆盖此合集中的该模组的实际设置。"u8)
                : ImEx.Button("永久"u8, smallButtonSize,
                    $"使用当前的临时设置覆盖该模组在此合集中的实际设置。\n按住 {LunaStyle.Modifier.Destructive} 以覆盖。",
                    true);
            if (overwrite)
            {
                var settings = collectionManager.Active.Current.GetTempSettings(selection.Mod!.Index)!;
                if (settings.ForceInherit)
                {
                    collectionManager.Editor.SetModInheritance(collectionManager.Active.Current, selection.Mod, true);
                }
                else
                {
                    collectionManager.Editor.SetModState(collectionManager.Active.Current, selection.Mod, settings.Enabled);
                    collectionManager.Editor.SetModPriority(collectionManager.Active.Current, selection.Mod, settings.Priority);
                    foreach (var (index, setting) in settings.Settings.Index())
                        collectionManager.Editor.SetModSetting(collectionManager.Active.Current, selection.Mod, index, setting);
                }

                collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod, null);
            }
        }
        else
        {
            var actual = collectionManager.Active.Current.GetActualSettings(selection.Mod!.Index).Settings;
            if (ImEx.Button("临时"u8, smallButtonSize,
                    "将当前设置复制到临时设置中以进行实验。"u8))
                collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!,
                    new TemporaryModSettings(selection.Mod!, actual));
        }
    }

    private static float PreviewCollapseGutterWidth
        => MathF.Round(Im.Style.FrameHeight * 0.55f);

    private void DrawPreviewCollapseButton()
    {
        var gutterWidth = PreviewCollapseGutterWidth;
        using var style = Im.Style.Push(ImStyleDouble.WindowPadding, Vector2.Zero)
            .Push(ImStyleDouble.ItemSpacing, Vector2.Zero);
        using var gutter = Im.Child.Begin("##previewCollapseGutter"u8, new Vector2(gutterWidth, -1), false,
            WindowFlags.NoScrollbar | WindowFlags.NoBackground | WindowFlags.NoScrollWithMouse);
        if (!gutter)
            return;

        var gutterMin = Im.Cursor.ScreenPosition;
        var gutterHeight = Im.ContentRegion.Available.Y;
        var handleHeight = Im.Style.FrameHeight * 4;
        Im.Dummy(new Vector2(gutterWidth, MathF.Max(0, MathF.Round((gutterHeight - handleHeight) * 0.5f))));

        var clicked = Im.InvisibleButton("##handle"u8, new Vector2(gutterWidth, handleHeight));
        var hovered = Im.Item.Hovered();
        var active = Im.Item.Active;
        var handleRect = Im.Item.Bounds;
        Im.Tooltip.OnHover(_previewExpanded ? "隐藏预览面板"u8 : "显示预览面板"u8);
        if (clicked)
        {
            _previewExpanded ^= true;
            if (config.Ui.SavePreviewPanelState)
            {
                _imagePanel.Config.Expanded = _previewExpanded;
                _imagePanel.SaveConfig();
            }
        }

        var drawList = Im.Window.DrawList;
        var lineX = MathF.Round(gutterMin.X + gutterWidth * 0.5f);
        var lineColor = Im.Style[ImGuiColor.Separator];
        var lineThickness = MathF.Max(1, Im.Style.GlobalScale);
        var gutterMaxY = gutterMin.Y + gutterHeight;
        drawList.Shape.Line(new Vector2(lineX, gutterMin.Y), new Vector2(lineX, handleRect.Minimum.Y), lineColor, lineThickness);
        drawList.Shape.Line(new Vector2(lineX, handleRect.Maximum.Y), new Vector2(lineX, gutterMaxY), lineColor, lineThickness);

        var fill = active
            ? Im.Style[ImGuiColor.ButtonActive]
            : hovered
                ? Im.Style[ImGuiColor.ButtonHovered]
                : Im.Style[ImGuiColor.Button];
        var rounding = MathF.Min(gutterWidth, handleHeight) * 0.5f;
        drawList.Shape.RectangleFilled(handleRect, fill, rounding);
        drawList.Shape.Rectangle(handleRect, Im.Style[ImGuiColor.Border], rounding, thickness: lineThickness);

        var fontSize = Im.Style.TextHeight;
        var arrowScale = Math.Clamp(gutterWidth / fontSize * 0.75f, 0.5f, 1f);
        var arrowPos = new Vector2(
            handleRect.Minimum.X + (gutterWidth - fontSize) * 0.5f,
            handleRect.Minimum.Y + (handleHeight - fontSize * arrowScale) * 0.5f);
        var arrowColor = hovered || active ? Im.Style[ImGuiColor.Text] : Im.Style[ImGuiColor.TextDisabled];
        drawList.Render.Arrow(arrowPos, arrowColor, _previewExpanded ? Direction.Right : Direction.Left, arrowScale);
    }

    private void DrawPreviewPanel(float width)
    {
        using var previewPanel = Im.Child.Begin("##PreviewPanel"u8, new Vector2(width, -1), true);
        if (!previewPanel)
            return;

        using var table = Im.Table.Begin("##previewTable"u8, 1, TableFlags.ScrollY | TableFlags.NoBordersInBody, -Vector2.UnitY);
        if (!table)
            return;

        table.SetupScrollFreeze(0, 1);
        table.NextColumn();

        if (selection.Mod != null)
        {
            var coverFolder = Path.Combine(selection.Mod.ModPath.FullName, "CoverImage");
            var folderExists = Directory.Exists(coverFolder);
            var icon = folderExists ? FontAwesomeIcon.FolderOpen : FontAwesomeIcon.Plus;
            var tooltip = folderExists
                ? "在文件资源管理器中打开 CoverImage 文件夹"
                : "按住 Ctrl 点击创建 CoverImage 文件夹";

            if (ImEx.Icon.Button(icon.Icon(), tooltip, !folderExists && !Im.Io.KeyControl, UiHelpers.IconButtonSize))
                _imagePanel.OpenCoverImageFolder();

            Im.Line.Same();
            if (ImEx.Icon.Button(FontAwesomeIcon.Clipboard.Icon(), "从剪贴板导入图片"u8, false, UiHelpers.IconButtonSize))
            {
                var staThread = new Thread(() =>
                {
                    try
                    {
                        _clipboardImporter.ImportFromClipboard(selection.Mod);
                    }
                    catch (Exception ex)
                    {
                        Penumbra.Log.Warning($"从剪贴板导入图片失败: {ex.Message}");
                    }
                });
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
            }

            Im.Line.Same();
            var websiteUrl = ModPreviewDownloader.GetModWebsiteUrl(selection.Mod);
            var isHeliosphere = !string.IsNullOrEmpty(websiteUrl) && websiteUrl.Contains("heliosphere.app");
            var buttonDisabled = string.IsNullOrEmpty(websiteUrl) || !isHeliosphere;
            var downloadTooltip = string.IsNullOrEmpty(websiteUrl)
                ? "模组中未找到网址相关字段，无法下载预览图"
                : isHeliosphere
                    ? "从Heliosphere下载预览图（最多3张）"
                    : "只支持从Heliosphere下载预览图";

            if (ImEx.Icon.Button(FontAwesomeIcon.Download.Icon(), downloadTooltip, buttonDisabled, UiHelpers.IconButtonSize))
                Task.Run(async () => await _previewDownloader.TryDownloadPreviewImage(selection.Mod!));

            Im.Line.Same();
            if (ImEx.Icon.Button(FontAwesomeIcon.Repeat.Icon(), "重新加载预览图"u8, false, UiHelpers.IconButtonSize))
                _imagePanel.ReloadImages();

            Im.Line.Same();
            var showHoverPreview = _imagePanel.Config.EnableImageInteraction;
            if (Im.Checkbox("图片交互"u8, ref showHoverPreview))
            {
                _imagePanel.Config.EnableImageInteraction = showHoverPreview;
                _imagePanel.SaveConfig();
            }

            Im.Tooltip.OnHover("启用/禁用图片交互功能（点击打开外部工具，右键放大图片等）");
        }

        table.NextColumn();
        var scrollbarWidth = Im.Style.ScrollbarSize;
        var rightPadding = 2 * UiHelpers.Scale;
        var imageSpacing = config.Ui.PreviewPanelImageSpacing * UiHelpers.Scale;
        var reservedSpace = scrollbarWidth + rightPadding + imageSpacing;
        var availableWidth = Math.Max(0, width - reservedSpace);

        if (selection.Mod != null)
            _imagePanel.Draw(selection.Mod, availableWidth);
        else
            Im.TextDisabled("未选择模组。"u8);
    }

    public void Dispose()
    {
        _imagePanel.Dispose();
        _previewDownloader.Dispose();
    }

    private string _groupNameInput = string.Empty;
    private string _optionNameInput = string.Empty;
    private Guid _groupIdentifierInput = Guid.Empty;
    private Guid _optionIdentifierInput = Guid.Empty;

    private void DrawEditPresetMode()
    {
        if (presets.Selected is not { } preset || selection.Mod is not { } mod)
            return;

        Im.Line.New();
        ImEx.TextCentered($"正在编辑{(presets.ModIdentifier.Length > 0 ? "模组" : "通用")}预设 {preset.Name}");
        LunaStyle.DrawSeparator();

        var size = UiHelpers.InputTextWidth;
        preset.DrawIdentifier(size);
        if (preset.DrawName(size, out var newName))
            presets.PresetManager.ChangeName(presets.ModIdentifier, preset, newName);
        if (preset.DrawEditTime(size, out var newTime))
            presets.PresetManager.ChangeLastEdit(presets.ModIdentifier, preset, newTime);
        if (preset.DrawApplicationTime(size, out newTime))
            presets.PresetManager.ChangeLastApply(presets.ModIdentifier, preset, newTime);
        if (presets.ModIdentifier.Length > 0)
            if (ImEx.Button("将模组预设转为通用"u8, size,
                    "移除此预设中的全部 GUID，仅保留按名称引用，使其可应用于任意模组；同时从模组预设列表移至通用预设列表。"u8))
                presets.PresetManager.MakeGeneric(preset, selection.Mod);

        if (ImEx.Button("删除"u8, size, !LunaStyle.Modifier.Destructive))
        {
            if (presets.ModIdentifier.Length > 0)
                presets.PresetManager.DeletePreset(mod, preset);
            else
                presets.PresetManager.DeleteGeneric(preset);
        }

        LunaStyle.Modifier.Destructive.TooltipLineBreak("delete"u8);

        var halfSize = new Vector2((size.X - Im.Style.ItemSpacing.X) / 2, 0);
        if (ImEx.Button("从剪贴板更新"u8, halfSize, "尝试用剪贴板中的预设数据覆盖此预设。"u8,
                !LunaStyle.Modifier.Misclick)
         && SettingPresetData.FromClipboard(out var sharedData))
            presets.PresetManager.Update(presets.ModIdentifier, preset, sharedData);
        LunaStyle.Modifier.Misclick.TooltipLineBreak("update"u8);

        Im.Line.Same();
        if (ImEx.Button("从模组更新"u8, halfSize, "尝试用此模组的当前设置覆盖此预设。"u8,
                !LunaStyle.Modifier.Misclick))
            presets.PresetManager.Update(presets.ModIdentifier, preset, mod, selection.Settings);
        LunaStyle.Modifier.Misclick.TooltipLineBreak("update"u8);

        LunaStyle.DrawSeparator();
        if (preset.Data.DrawState(size, out var newState))
            presets.PresetManager.ChangeState(presets.ModIdentifier, preset, newState);
        if (preset.Data.DrawPriority(size, out var newPriority))
            presets.PresetManager.ChangePriority(presets.ModIdentifier, preset, newPriority);

        if (DrawGroups(size, preset, mod, out var changedGroupIdentifier, out var newGroupIdentifier, out var newDisableUnknown))
        {
            if (newDisableUnknown.HasValue)
                presets.PresetManager.ChangeDisableUnknownOptions(presets.ModIdentifier, preset, changedGroupIdentifier,
                    newDisableUnknown.Value);
            else if (newGroupIdentifier is null)
                presets.PresetManager.DeleteGroupReference(presets.ModIdentifier, preset, changedGroupIdentifier);
            else
                presets.PresetManager.ChangeGroupReference(presets.ModIdentifier, preset, changedGroupIdentifier, newGroupIdentifier.Value);
        }

        LunaStyle.DrawSeparator();
        var id = new ModObjectIdentifier(_groupIdentifierInput, _groupNameInput);
        if (preset.Data.DrawAddGroup(size, ref _groupIdentifierInput, ref _groupNameInput, out var newGroup,
                id.FindGroup(mod) is { } g ? ModObjectIdentifier.From(g) : null))
            presets.PresetManager.AddGroupReference(presets.ModIdentifier, preset, newGroup);
    }

    private bool DrawGroups(Vector2 size, SettingPreset preset, Mod mod, out ModObjectIdentifier changedGroupIdentifier,
        out ModObjectIdentifier? newGroupIdentifier, out bool? newDisableUnknown)
    {
        var ret = false;
        changedGroupIdentifier = default;
        newGroupIdentifier = null;
        newDisableUnknown = null;
        foreach (var (index, (groupIdentifier, groupData)) in preset.Data.Settings.Index())
        {
            using var groupId = Im.Id.Push(index);
            LunaStyle.DrawSeparator();
            var actualGroup = groupIdentifier.FindGroup(mod);
            if (SettingPresetData.DrawGroup(size, index, groupIdentifier,
                    actualGroup is not null ? ModObjectIdentifier.From(actualGroup) : null, groupData.DisableAllUnknown, out var newGroup,
                    out var disable))
            {
                ret = true;
                changedGroupIdentifier = groupIdentifier;
                newGroupIdentifier = newGroup;
                newDisableUnknown = disable;
            }

            using var indent = Im.Indent();
            if (DrawOptions(size.AddX(-Im.Style.IndentSpacing), groupData, actualGroup, out var changedOption, out var newOption,
                    out var newOptionState))
            {
                if (newOptionState.HasValue)
                    presets.PresetManager.ChangeOption(presets.ModIdentifier, preset, groupIdentifier, changedOption,
                        newOptionState.Value);
                else if (newOption is null)
                    presets.PresetManager.DeleteOptionReference(presets.ModIdentifier, preset, groupIdentifier, changedOption);
                else
                    presets.PresetManager.ChangeOptionReference(presets.ModIdentifier, preset, groupIdentifier, changedOption,
                        newOption.Value);
            }

            Im.Cursor.Y += Im.Style.ItemSpacing.Y;
            var id = new ModObjectIdentifier(_optionIdentifierInput, _optionNameInput);
            if (SettingPresetData.DrawAddOption(size.AddX(-Im.Style.IndentSpacing), groupData, ref _optionIdentifierInput,
                    ref _optionNameInput, out var option,
                    id.FindOption(actualGroup) is { } o ? ModObjectIdentifier.From(o) : null))
                presets.PresetManager.ChangeOption(presets.ModIdentifier, preset, groupIdentifier, option, OptionState.Ignored);
        }

        return ret;
    }

    private static bool DrawOptions(Vector2 size, in GroupSettingData data, IModGroup? actualGroup,
        out ModObjectIdentifier changedOptionIdentifier, out ModObjectIdentifier? newOptionIdentifier, out OptionState? newOptionState)
    {
        var ret = false;
        changedOptionIdentifier = default;
        newOptionIdentifier = null;
        newOptionState = null;
        foreach (var (optionIndex, (optionIdentifier, optionState)) in data.Options.Index())
        {
            using var id = Im.Id.Push(optionIndex);
            var resolvedOption = optionIdentifier.FindOption(actualGroup);
            Im.Cursor.Y += Im.Style.ItemSpacing.Y;
            if (SettingPresetData.DrawOption(size, optionIndex, optionIdentifier,
                    resolvedOption is not null ? ModObjectIdentifier.From(resolvedOption) : null,
                    (OptionState)optionState, out var newOption, out var state))
            {
                ret = true;
                changedOptionIdentifier = optionIdentifier;
                newOptionIdentifier = newOption;
                newOptionState = state;
            }
        }

        return ret;
    }
}
