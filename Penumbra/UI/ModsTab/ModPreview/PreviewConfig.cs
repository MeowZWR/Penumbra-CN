using System.Text.Json.Serialization;

namespace Penumbra.UI.ModsTab.ModPreview;

public class PreviewConfig
{
    public bool EnableImageInteraction { get; set; } = true;
    public int ThumbnailSize { get; set; } = 128;
    public int MaxPreviewWidth { get; set; } = 800;
    public int MaxPreviewHeight { get; set; } = 600;
    public float PreviewPanelImageSpacing { get; set; } = 4f;
    public float PreviewPanelLeftMargin { get; set; } = 0f;
    public bool EnableExternalViewer { get; set; } = false;
}

public class PinnedImageConfig
{
    [JsonIgnore]
    public string? CurrentModPath { get; set; }
    
    public string? PinnedImagePath { get; set; }
} 