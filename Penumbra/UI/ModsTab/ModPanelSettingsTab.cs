using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.UI.Classes;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.Mods.Settings;
using Penumbra.UI.ModsTab.Groups;
using OtterGui.Extensions;
using Dalamud.Plugin.Services;
using Dalamud.Interface;
using Dalamud.Interface.DragDrop;
using Dalamud.Plugin;
using Penumbra.UI.ModsTab.ModPreview;

namespace Penumbra.UI.ModsTab;

public class ModPanelSettingsTab(
    CollectionManager collectionManager,
    ModManager modManager,
    ModSelection selection,
    TutorialService tutorial,
    CommunicatorService communicator,
    ModGroupDrawer modGroupDrawer,
    Configuration config,
    ITextureProvider textureProvider,
    IDragDropManager dragDrop,
    IDalamudPluginInterface pluginInterface,
    INotificationManager notificationManager)
    : ITab, IUiService, IDisposable
{
    private bool _inherited;
    private bool _temporary;
    private bool _locked;
    private int? _currentPriority;
    private readonly ModPreviewImagePanel _imagePanel = new(modManager, pluginInterface, textureProvider, dragDrop, config, notificationManager);
    private bool _previewExpanded;
    private readonly ClipboardImageImporter _clipboardImporter = new(notificationManager, pluginInterface, modManager);
    private readonly ModPreviewDownloader _previewDownloader = new(notificationManager, config);

    public ReadOnlySpan<byte> Label
        => "模组设置"u8;

    public void DrawHeader()
        => tutorial.OpenTutorial(BasicTutorialSteps.ModOptions);

    public void Reset()
        => _currentPriority = null;

    public void DrawContent()
    {
        static bool IsEqual(bool a, bool b) => a == b;
        if (config.SavePreviewPanelState && !IsEqual(_previewExpanded, _imagePanel.Config.Expanded))
            _previewExpanded = _imagePanel.Config.Expanded;
            
        // 计算可用总宽度
        var totalAvailableWidth = ImGui.GetContentRegionAvail().X;
        var buttonWidth = ImGui.GetFrameHeight();

        // 计算最大允许宽度（基于比例）
        var maxAllowedWidth = (totalAvailableWidth - buttonWidth) * config.PreviewPanelRatio;
        
        // 确保最小宽度不超过最大允许宽度
        var safeMinWidth = Math.Min(config.PreviewPanelMinWidth, maxAllowedWidth);
        var safeMaxWidth = Math.Min(config.PreviewPanelMaxWidth, maxAllowedWidth);

        // 面板占用比例，最小、最大宽度限制
        var calculatedPreviewWidth = (totalAvailableWidth - buttonWidth) * config.PreviewPanelRatio;
        var previewWidth = _previewExpanded && config.ShowModPreviewPanel
            ? Math.Clamp(calculatedPreviewWidth, 
                safeMinWidth * UiHelpers.Scale, 
                safeMaxWidth * UiHelpers.Scale)
            : 0;

        // 主面板宽度计算
        var mainWidth = config.ShowModPreviewPanel 
            ? totalAvailableWidth - buttonWidth - (_previewExpanded ? previewWidth : 0)
            : totalAvailableWidth;

        // 1. 绘制主面板
        using (var mainPanel = ImRaii.Child("##MainSettingsPanel", new Vector2(mainWidth, -1), false, ImGuiWindowFlags.NoScrollbar))
        {
            if (mainPanel)
                DrawSettingsPanelContent();
        }

        // 2. 绘制折叠按钮
        if (config.ShowModPreviewPanel)
        {
            ImGui.SameLine(0, 0); // 确保按钮紧跟主面板
            DrawPreviewCollapseButton(previewWidth);

            // 3. 绘制预览面板（如果展开）
            if (_previewExpanded)
            {
                ImGui.SameLine(0, 0); // 确保预览面板紧跟按钮
                DrawPreviewPanel(previewWidth);
            }
        }
    }

    private void DrawPreviewCollapseButton(float previewWidth)
    {
        var icon = _previewExpanded ? ">" : "<";
        var tooltip = _previewExpanded ? "隐藏预览面板" : "显示预览面板";

        // 保存当前光标位置以便之后恢复
        var originalPos = ImGui.GetCursorPos();

        // 计算按钮X位置
        var buttonPosX = _previewExpanded
            ? ImGui.GetWindowWidth() - previewWidth - ImGui.GetFrameHeight() * 1f
            : ImGui.GetWindowWidth() - ImGui.GetFrameHeight() * 1f;

        // 设置按钮位置（使用当前Y坐标，不是窗口顶部）
        ImGui.SetCursorPos(new Vector2(buttonPosX, originalPos.Y));

        // 使用合理的按钮高度，而不是整个窗口高度
        var buttonHeight = ImGui.GetContentRegionAvail().Y;
        if (ImGui.Button(icon, new Vector2(ImGui.GetFrameHeight() * 0.8f, buttonHeight)))
        {
            _previewExpanded = !_previewExpanded;
            if (config.SavePreviewPanelState)
            {
                _imagePanel.Config.Expanded = _previewExpanded;
                _imagePanel.SaveConfig();
            }
        }

        // 恢复光标位置以便继续绘制其他内容
        ImGui.SetCursorPos(originalPos);

        ImGuiUtil.HoverTooltip(tooltip);
    }

    private void DrawPreviewPanel(float width)
    {
        using var previewPanel = ImRaii.Child("##PreviewPanel", new Vector2(width, -1), true);
        if (!previewPanel)
            return;

        // 使用表格结构来组织预览面板内容
        using var table = ImUtf8.Table("##previewTable", 1, ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoBordersInBody, -Vector2.UnitY);
        if (!table)
            return;

        // 冻结第一行，用于放置按钮
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableNextColumn();

        // 绘制预览面板按钮
        if (selection.Mod != null)
        {
            // 设置左侧间距
            var leftPadding = 0 * UiHelpers.Scale;
            ImGui.SetCursorPosX(leftPadding);

            var coverFolder = Path.Combine(selection.Mod!.ModPath.FullName, "CoverImage");
            var folderExists = Directory.Exists(coverFolder);
            var icon = folderExists ? FontAwesomeIcon.FolderOpen : FontAwesomeIcon.Plus;
            var tooltip = folderExists 
                ? "在文件资源管理器中打开 CoverImage 文件夹" 
                : "按住 Ctrl 点击创建 CoverImage 文件夹";

            if (ImGuiUtil.DrawDisabledButton($"{icon.ToIconString()}##openFolder", UiHelpers.IconButtonSize,
                tooltip, !folderExists && !ImGui.GetIO().KeyCtrl, true))
            {
                _imagePanel.OpenCoverImageFolder();
            }

            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton($"{FontAwesomeIcon.Clipboard.ToIconString()}##importFromClipboard", UiHelpers.IconButtonSize,
                "从剪贴板导入图片", false, true))
            {
                var staThread = new System.Threading.Thread(() =>
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
                staThread.SetApartmentState(System.Threading.ApartmentState.STA);
                
                staThread.Start();
            }
            
            ImGui.SameLine();
            // 绘制下载按钮
            var websiteUrl = selection.Mod != null ? ModPreviewDownloader.GetModWebsiteUrl(selection.Mod) : string.Empty;
            var isHeliosphere = !string.IsNullOrEmpty(websiteUrl) && websiteUrl.Contains("heliosphere.app");
            
            var buttonDisabled = string.IsNullOrEmpty(websiteUrl) || !isHeliosphere;
            
            var downloadTooltip = string.Empty;
            
            if (string.IsNullOrEmpty(websiteUrl))
                downloadTooltip = "模组中未找到网址相关字段，无法下载预览图";
            else if (isHeliosphere)
                downloadTooltip = "从Heliosphere下载预览图（最多3张）";
            else
                downloadTooltip = "只支持从Heliosphere下载预览图";
            
            if (ImGuiUtil.DrawDisabledButton($"{FontAwesomeIcon.Download.ToIconString()}##downloadImages", UiHelpers.IconButtonSize,
                downloadTooltip, buttonDisabled, true))
            {
                Task.Run(async () => await _previewDownloader.TryDownloadPreviewImage(selection.Mod!));
            }

            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton($"{FontAwesomeIcon.Repeat.ToIconString()}##reloadImages", UiHelpers.IconButtonSize,
                "重新加载预览图", false, true))
            {
                _imagePanel.ReloadImages();
            }

            ImGui.SameLine();
            var showHoverPreview = _imagePanel.Config.EnableImageInteraction;
            if (ImGui.Checkbox("图片交互", ref showHoverPreview))
            {
                _imagePanel.Config.EnableImageInteraction = showHoverPreview;
                _imagePanel.SaveConfig();
            }
            ImGuiUtil.HoverTooltip("启用/禁用图片交互功能（点击打开外部工具，右键放大图片等）");
        }

        // 绘制预览图片内容
        ImGui.TableNextColumn();
        
        // 计算滚动条宽度
        var scrollbarWidth = ImGui.GetStyle().ScrollbarSize;
        // 预留右侧间距，减少与滚动条的间距
        var rightPadding = 2 * UiHelpers.Scale;
        // 使用统一的图片间距参数
        var imageSpacing = config.PreviewPanelImageSpacing * UiHelpers.Scale;
        
        // 估算可能的最大列数（基于最小图片宽度）
        var minImageWidth = config.PreviewImageMinWidth * UiHelpers.Scale;
        var effectiveWidth = width - scrollbarWidth - rightPadding;
        var possibleColumns = Math.Max(1, (int)(effectiveWidth / minImageWidth));
        
        // 为每列预留右侧安全边距，防止被剪切
        var reservedSpace = scrollbarWidth + rightPadding + imageSpacing;
        
        // 最终可用宽度
        var availableWidth = Math.Max(0, width - reservedSpace);
        
        if (selection.Mod != null)
            _imagePanel.Draw(selection.Mod, availableWidth);
        else
            ImGui.TextDisabled("未选择模组。");
    }

    private void DrawSettingsPanelContent()
    {
        using var table = ImUtf8.Table("##settings", 1, ImGuiTableFlags.ScrollY, -Vector2.UnitY);
        if (!table)
            return;

        _inherited = selection.Collection != collectionManager.Active.Current;
        _temporary = selection.TemporarySettings != null;
        _locked    = (selection.TemporarySettings?.Lock ?? 0) > 0;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableNextColumn();
        DrawTemporaryWarning();
        DrawInheritedWarning();
        ImGui.Dummy(Vector2.Zero);
        communicator.PreSettingsPanelDraw.Invoke(selection.Mod!.Identifier);
        DrawEnabledInput();
        tutorial.OpenTutorial(BasicTutorialSteps.EnablingMods);
        ImGui.SameLine();
        DrawPriorityInput();
        tutorial.OpenTutorial(BasicTutorialSteps.Priority);
        DrawRemoveSettings();

        ImGui.TableNextColumn();
        communicator.PostEnabledDraw.Invoke(selection.Mod!.Identifier);

        modGroupDrawer.Draw(selection.Mod!, selection.Settings, selection.TemporarySettings);
        UiHelpers.DefaultLineSpace();
        communicator.PostSettingsPanelDraw.Invoke(selection.Mod!.Identifier);
    }

    /// <summary> Draw a big tinted bar if the current setting is temporary. </summary>
    private void DrawTemporaryWarning()
    {
        if (!_temporary)
            return;

        using var color = ImRaii.PushColor(ImGuiCol.Button, ImGuiCol.Button.Tinted(ColorId.TemporaryModSettingsTint));
        var       width = new Vector2(ImGui.GetContentRegionAvail().X, 0);
        if (ImUtf8.ButtonEx($"这些设置是由 {selection.TemporarySettings!.Source} 临时设置的{(_locked ? "，并且已锁定。" : "。")}",
                width,
                _locked))
            collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!, null);

        ImUtf8.HoverTooltip("更改临时设置中的设置不会在会话之间保存。\n"u8
          + "你可以点击此按钮来移除临时设置并返回到常规设置。"u8);
    }

    /// <summary> Draw a big red bar if the current setting is inherited. </summary>
    private void DrawInheritedWarning()
    {
        if (!_inherited)
            return;

        var inheritanceBorderColor = ImGui.GetColorU32(ImGuiCol.Border);
        using var color = ImRaii.PushColor(ImGuiCol.Border, inheritanceBorderColor);
        var       width = new Vector2(ImGui.GetContentRegionAvail().X, 0);
        if (ImUtf8.ButtonEx($"此模组设置继承自合集：{selection.Collection.Identity.Name}.", width, _locked))
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

        ImUtf8.HoverTooltip("你可以点击这个按钮将当前设置独立到此合集。\n"u8
          + "你也可以在下面随意修改设置，修改后此模组的设置也会独立到此合集。"u8);
    }

    /// <summary> Draw a checkbox for the enabled status of the mod. </summary>
    private void DrawEnabledInput()
    {
        var       enabled  = selection.Settings.Enabled;
        using var disabled = ImRaii.Disabled(_locked);
        if (!ImUtf8.Checkbox("启用"u8, ref enabled))
            return;

        modManager.SetKnown(selection.Mod!);
        if (_temporary || config.DefaultTemporaryMode)
        {
            var temporarySettings = selection.TemporarySettings ?? new TemporaryModSettings(selection.Mod!, selection.Settings);
            temporarySettings.ForceInherit = false;
            temporarySettings.Enabled      = enabled;
            collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!, temporarySettings);
        }
        else
        {
            collectionManager.Editor.SetModState(collectionManager.Active.Current, selection.Mod!, enabled);
        }
    }

    /// <summary>
    /// Draw a priority input.
    /// Priority is changed on deactivation of the input box.
    /// </summary>
    private void DrawPriorityInput()
    {
        using var group    = ImUtf8.Group();
        var       settings = selection.Settings;
        var       priority = _currentPriority ?? settings.Priority.Value;
        ImGui.SetNextItemWidth(50 * UiHelpers.Scale);
        using var disabled = ImRaii.Disabled(_locked);
        if (ImUtf8.InputScalar("##Priority"u8, ref priority))
            _currentPriority = priority;
        if (new ModPriority(priority).IsHidden)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled,
                $"此优先级特殊处理以在冲突标签中隐藏此模组（{ModPriority.HiddenMin}, {ModPriority.HiddenMax}）。");


        if (ImGui.IsItemDeactivatedAfterEdit() && _currentPriority.HasValue)
        {
            if (_currentPriority != settings.Priority.Value)
            {
                if (_temporary || config.DefaultTemporaryMode)
                {
                    var temporarySettings = selection.TemporarySettings ?? new TemporaryModSettings(selection.Mod!, selection.Settings);
                    temporarySettings.ForceInherit = false;
                    temporarySettings.Priority     = new ModPriority(_currentPriority.Value);
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

        ImUtf8.LabeledHelpMarker("优先级"u8, "优先级更高的模组文件将优先使用。\n"u8
          + "如果要用模组A覆盖模组B，则模组A的优先级应高于模组B。"u8);
    }

    /// <summary>
    /// Draw a button to remove the current settings and inherit them instead
    /// in the top-right corner of the window/tab.
    /// </summary>
    private void DrawRemoveSettings()
    {
        var drawInherited = !_inherited && selection.Settings.IsEmpty;
        var scroll        = ImGui.GetScrollMaxY() > 0 ? ImGui.GetStyle().ScrollbarSize + ImGui.GetStyle().ItemInnerSpacing.X : 0;
        var buttonSize    = ImUtf8.CalcTextSize("设为永久_"u8).X;
        var offset = drawInherited
            ? buttonSize + ImUtf8.CalcTextSize("继承设置"u8).X + ImGui.GetStyle().FramePadding.X * 4 + ImGui.GetStyle().ItemSpacing.X
            : buttonSize + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SameLine(ImGui.GetWindowWidth() - offset - scroll);
        var enabled = config.DeleteModModifier.IsActive();
        if (drawInherited)
        {
            var inherit = (enabled, _locked) switch
            {
                (true, false) => ImUtf8.ButtonEx("继承设置"u8,
                    "从此合集移除当前设置，以便它可以继承设置。\n"u8
                  + "如果没有继承的合集为此模组设置了设置，它将被禁用。"u8, default, false),
                (false, false) => ImUtf8.ButtonEx("继承设置"u8,
                    $"从此合集移除当前设置，以便它可以继承设置。\n按住 {config.DeleteModModifier} 以进行继承。",
                    default, true),
                (_, true) => ImUtf8.ButtonEx("继承设置"u8,
                    "从此合集移除当前设置，以便它可以继承设置。\n设置当前被锁定，无法更改。"u8,
                    default, true),
            };
            if (inherit)
            {
                if (_temporary || config.DefaultTemporaryMode)
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
        ImGui.SameLine();
        }

        if (_temporary)
        {
            var overwrite = enabled
                ? ImUtf8.ButtonEx("设为永久"u8,
                    "使用当前的临时设置覆盖此合集中的该模组的实际设置。"u8,
                    new Vector2(buttonSize, 0))
                : ImUtf8.ButtonEx("设为永久"u8,
                    $"使用当前的临时设置覆盖该模组在此合集中的实际设置。\n按住 {config.DeleteModModifier} 以覆盖。",
                    new Vector2(buttonSize, 0), true);
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
                    foreach (var (setting, index) in settings.Settings.WithIndex())
                        collectionManager.Editor.SetModSetting(collectionManager.Active.Current, selection.Mod, index, setting);
                }

                collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod, null);
            }
        }
        else
        {
            var actual = collectionManager.Active.Current.GetActualSettings(selection.Mod!.Index).Settings;
            if (ImUtf8.ButtonEx("设为临时"u8, "将当前设置复制到临时设置中以进行实验。"u8))
                collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!,
                    new TemporaryModSettings(selection.Mod!, actual));
        }
    }    

    public void Dispose()
    {
        _previewDownloader.Dispose();
    }
}
