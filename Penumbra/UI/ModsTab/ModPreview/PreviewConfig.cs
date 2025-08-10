using System.Text.Json.Serialization;
using Penumbra.UI;
using Dalamud.Bindings.ImGui;

namespace Penumbra.UI.ModsTab.ModPreview;

public class PreviewConfig
{
    public bool EnableImageInteraction { get; set; } = true;
    public int ThumbnailSize { get; set; } = 128;
    public int MaxPreviewWidth { get; set; } = 800;
    public int MaxPreviewHeight { get; set; } = 600;
    public float PreviewPanelLeftMargin { get; set; } = 0f;
    public bool EnableExternalViewer { get; set; } = false;
    public bool Expanded { get; set; } = false;

    // 用于在UI中使用的临时值存储，处理配置保存，只有在完成编辑后才更新配置
    [JsonIgnore] private float _tempRatio = float.NaN;
    [JsonIgnore] private float _tempMinWidth = float.NaN;
    [JsonIgnore] private float _tempMaxWidth = float.NaN;
    [JsonIgnore] private float _tempImageMinWidth = float.NaN;
    [JsonIgnore] private float _tempSpacing = float.NaN;

    // 处理预览面板比例的编辑与保存
    public bool DrawRatioSelector(Configuration config, float min = 0.1f, float max = 0.5f)
    {
        if (float.IsNaN(_tempRatio))
            _tempRatio = config.PreviewPanelRatio;

        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X);
        bool changed = ImGui.DragFloat("##预览面板比例", ref _tempRatio, 0.01f, min, max, "%.2f");
        
        if (ImGui.IsItemDeactivatedAfterEdit() && _tempRatio != config.PreviewPanelRatio)
        {
            config.PreviewPanelRatio = _tempRatio;
            config.Save();
            return true;
        }
        
        return false;
    }

    // 处理预览面板最小宽度的编辑与保存
    public bool DrawMinWidthSelector(Configuration config, float min = 100f, float max = 800f)
    {
        if (float.IsNaN(_tempMinWidth))
            _tempMinWidth = config.PreviewPanelMinWidth;
            
        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X);
        bool changed = ImGui.DragFloat("##预览面板最小宽度", ref _tempMinWidth, 1f, min, max, "%.0f");
        
        if (ImGui.IsItemDeactivatedAfterEdit() && _tempMinWidth != config.PreviewPanelMinWidth)
        {
            config.PreviewPanelMinWidth = _tempMinWidth;
            config.Save();
            return true;
        }
        
        return false;
    }

    // 处理预览面板最大宽度的编辑与保存
    public bool DrawMaxWidthSelector(Configuration config, float min = 200f, float max = 1000f)
    {
        if (float.IsNaN(_tempMaxWidth))
            _tempMaxWidth = config.PreviewPanelMaxWidth;
            
        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X);
        bool changed = ImGui.DragFloat("##预览面板最大宽度", ref _tempMaxWidth, 1f, min, max, "%.0f");
        
        if (ImGui.IsItemDeactivatedAfterEdit() && _tempMaxWidth != config.PreviewPanelMaxWidth)
        {
            config.PreviewPanelMaxWidth = _tempMaxWidth;
            config.Save();
            return true;
        }
        
        return false;
    }

    // 处理预览图片最小宽度的编辑与保存
    public bool DrawImageMinWidthSelector(Configuration config, float min = 100f, float max = 800f)
    {
        if (float.IsNaN(_tempImageMinWidth))
            _tempImageMinWidth = config.PreviewImageMinWidth;
            
        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X);
        bool changed = ImGui.DragFloat("##预览图片最小宽度", ref _tempImageMinWidth, 1f, min, max, "%.0f");
        
        if (ImGui.IsItemDeactivatedAfterEdit() && _tempImageMinWidth != config.PreviewImageMinWidth)
        {
            config.PreviewImageMinWidth = _tempImageMinWidth;
            config.Save();
            return true;
        }
        
        return false;
    }

    // 处理图片间距的编辑与保存
    public bool DrawSpacingSelector(Configuration config, float min = 0f, float max = 10f)
    {
        if (float.IsNaN(_tempSpacing))
            _tempSpacing = config.PreviewPanelImageSpacing;
            
        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X);
        bool changed = ImGui.DragFloat("##图片间距", ref _tempSpacing, 0.1f, min, max, "%.1f");
        
        if (ImGui.IsItemDeactivatedAfterEdit() && _tempSpacing != config.PreviewPanelImageSpacing)
        {
            config.PreviewPanelImageSpacing = _tempSpacing;
            config.Save();
            return true;
        }
        
        return false;
    }
}

public class PinnedImageConfig
{
    public string? PinnedImagePath { get; set; }
} 