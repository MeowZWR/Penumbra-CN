using System.Text.Json.Serialization;
using ImSharp;
using Luna;
using Penumbra.UI;

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

        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        _ = Im.Drag("##预览面板比例"u8, ref _tempRatio, "%.2f"u8, min, max, 0.01f);
        
        if (Im.Item.DeactivatedAfterEdit && _tempRatio != config.PreviewPanelRatio)
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
            
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        _ = Im.Drag("##预览面板最小宽度"u8, ref _tempMinWidth, "%.0f"u8, min, max, 1f);
        
        if (Im.Item.DeactivatedAfterEdit && _tempMinWidth != config.PreviewPanelMinWidth)
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
            
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        _ = Im.Drag("##预览面板最大宽度"u8, ref _tempMaxWidth, "%.0f"u8, min, max, 1f);
        
        if (Im.Item.DeactivatedAfterEdit && _tempMaxWidth != config.PreviewPanelMaxWidth)
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
            
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        _ = Im.Drag("##预览图片最小宽度"u8, ref _tempImageMinWidth, "%.0f"u8, min, max, 1f);

        if (Im.Item.DeactivatedAfterEdit && _tempImageMinWidth != config.PreviewImageMinWidth)
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
            
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        _ = Im.Drag("##图片间距"u8, ref _tempSpacing, "%.1f"u8, min, max, 0.1f);
        
        if (Im.Item.DeactivatedAfterEdit && _tempSpacing != config.PreviewPanelImageSpacing)
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