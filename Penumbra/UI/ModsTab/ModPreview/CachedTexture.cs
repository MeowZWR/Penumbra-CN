using Dalamud.Interface.Textures.TextureWraps;

namespace Penumbra.UI.ModsTab.ModPreview;

public class CachedTexture
{
    public IDalamudTextureWrap? Texture { get; set; }
    public IDalamudTextureWrap? OriginalTexture { get; set; }
    public DateTime LastAccessTime { get; set; }
    public DateTime LastModifiedTime { get; set; }
    public long MemorySize { get; set; }
    public bool IsVisible { get; set; }
    public Vector2 ScaledSize { get; set; }
    public Vector2 HoveredSize { get; set; }
    public Vector2 OriginalSize { get; set; }
    public ImageCompressor.ResolutionType Resolution { get; set; } = ImageCompressor.ResolutionType.Thumbnail;
    
    public bool IsNew { get; set; } = false;
} 