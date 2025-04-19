using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace Penumbra.UI.ModsTab.ModPreview;

public class ImageCompressor : IDisposable
{
    private readonly ITextureProvider _textureProvider;
    
    // 多级压缩尺寸
    private const int ThumbnailDimension = 256; // 小缩略图
    private const int MediumDimension = 512;   // 中等尺寸
    private const int MaxCompressDimension = 1024; // 最大压缩尺寸
    private const int MaxRetryCount = 3;
    private const int FileOperationDelay = 100;

    public enum ResolutionType
    {
        Thumbnail,  // 小缩略图，用于列表显示
        Medium,     // 中等尺寸，用于常规预览
        Large,      // 大尺寸，用于高质量预览
        Original    // 原始尺寸，用于全屏查看
    }

    public ImageCompressor(ITextureProvider textureProvider)
    {
        _textureProvider = textureProvider;
    }

    /// <summary>
    /// 根据显示需求计算需要的图片尺寸
    /// </summary>
    /// <param name="displaySize">显示区域大小</param>
    /// <param name="originalSize">原始图片大小</param>
    /// <returns>适合的分辨率类型</returns>
    public ResolutionType DetermineRequiredResolution(Vector2 displaySize, Vector2 originalSize)
    {
        // 计算显示区域的对角线长度作为参考
        float displayArea = displaySize.X * displaySize.Y;
        
        // 根据显示区域大小决定使用哪种分辨率
        if (displayArea < ThumbnailDimension * ThumbnailDimension * 0.8f)
            return ResolutionType.Thumbnail;
        else if (displayArea < MediumDimension * MediumDimension * 0.8f)
            return ResolutionType.Medium;
        else if (Math.Max(originalSize.X, originalSize.Y) <= MaxCompressDimension)
            return ResolutionType.Original; // 如果原图较小，直接使用原图
        else
            return ResolutionType.Large;
    }

    /// <summary>
    /// 压缩图片并生成多种分辨率的预览图
    /// </summary>
    public async Task<(IDalamudTextureWrap? PreviewTexture, IDalamudTextureWrap? OriginalTexture, Vector2 ScaledSize, Vector2 OriginalSize, long MemorySize)> 
        CompressImageAsync(string imagePath, ResolutionType requestedResolution = ResolutionType.Medium)
    {
        try
        {
            // 读取原图
            byte[] imageData;
            using (var fs = File.OpenRead(imagePath))
            {
                using var ms = new MemoryStream();
                await fs.CopyToAsync(ms);
                imageData = ms.ToArray();
            }

            // 加载图片
            using var image = Image.Load<Rgba32>(imageData);
            var originalSize = new Vector2(image.Width, image.Height);
            
            // 根据请求的分辨率类型确定目标尺寸
            int targetSize = requestedResolution switch
            {
                ResolutionType.Thumbnail => ThumbnailDimension,
                ResolutionType.Medium => MediumDimension,
                ResolutionType.Large => MaxCompressDimension,
                ResolutionType.Original => Math.Max(image.Width, image.Height),
                _ => MediumDimension
            };
            
            // 计算缩放尺寸保持宽高比
            var scaleFactor = (float)targetSize / Math.Max(originalSize.X, originalSize.Y);
            var scaledSize = new Vector2(originalSize.X * scaleFactor, originalSize.Y * scaleFactor);
            
            // 仅当需要缩小图片时才进行缩放处理
            bool needsResize = image.Width > targetSize || image.Height > targetSize;
            using var previewImage = needsResize
                ? image.Clone(x => x.Resize(new ResizeOptions
                {
                    Size = new Size((int)scaledSize.X, (int)scaledSize.Y),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.Lanczos3,
                    Compand = false
                }))
                : image.Clone();

            // 保存预览图到内存流（使用压缩以减少内存使用）
            using var memoryStream = new MemoryStream();
            await previewImage.SaveAsPngAsync(memoryStream, new PngEncoder
            {
                ColorType = PngColorType.RgbWithAlpha,
                BitDepth = PngBitDepth.Bit8,
                CompressionLevel = PngCompressionLevel.BestSpeed, // 使用快速压缩
                FilterMethod = PngFilterMethod.Adaptive
            });
            memoryStream.Seek(0, SeekOrigin.Begin);

            // 创建预览纹理
            var previewTexture = await _textureProvider.CreateFromImageAsync(memoryStream, leaveOpen: true);
            if (previewTexture == null)
            {
                throw new Exception("创建预览纹理失败");
            }

            // 创建原始图片纹理（只在需要的情况下）
            IDalamudTextureWrap? originalTexture = null;
            long originalMemorySize = 0;
            
            // 只有在请求原始分辨率或者高分辨率预览时才加载原图
            if (requestedResolution == ResolutionType.Original)
            {
                using var originalMs = new MemoryStream();
                if (needsResize)
                {
                    // 使用原始图片
                    await image.SaveAsPngAsync(originalMs, new PngEncoder
                    {
                        ColorType = PngColorType.RgbWithAlpha,
                        BitDepth = PngBitDepth.Bit8,
                        CompressionLevel = PngCompressionLevel.BestSpeed
                    });
                }
                else
                {
                    // 如果预览图就是原图大小，直接复制
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await memoryStream.CopyToAsync(originalMs);
                }
                
                originalMs.Seek(0, SeekOrigin.Begin);
                originalTexture = await _textureProvider.CreateFromImageAsync(originalMs, leaveOpen: true);
                originalMemorySize = originalMs.Length;

                if (originalTexture == null)
                {
                    previewTexture.Dispose();
                    throw new Exception("创建原始纹理失败");
                }
            }
            else
            {
                // 如果不需要原始分辨率，使用预览纹理作为原始纹理
                originalTexture = previewTexture;
            }

            // 计算实际占用的内存大小
            long totalMemorySize = memoryStream.Length + originalMemorySize;
            
#if DEBUG
            Penumbra.Log.Debug($"[图片压缩] 加载图片 {Path.GetFileName(imagePath)}, 分辨率: {requestedResolution}, 原始: {originalSize.X}x{originalSize.Y}, 压缩后: {scaledSize.X}x{scaledSize.Y}, 内存: {totalMemorySize/1024}KB");
#endif
            
            return (previewTexture, originalTexture, scaledSize, originalSize, totalMemorySize);
        }
        catch (Exception ex)
        {
            throw new Exception($"压缩图片失败: {ex.Message}", ex);
        }
    }

    private Vector2 GetScaledSize(Vector2 originalSize, float maxWidth)
    {
        // 计算缩放比例，保持宽高比
        float scale = Math.Min(maxWidth / originalSize.X, maxWidth / originalSize.Y);
        if (scale >= 1) return originalSize; // 不放大，只缩小
        return new Vector2(originalSize.X * scale, originalSize.Y * scale);
    }

    public void Dispose()
    {
        // 清理资源
    }
} 