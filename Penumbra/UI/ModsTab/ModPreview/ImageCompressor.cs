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
    private const long MaxSourceFileSize = 100L * 1024 * 1024;
    private const long MaxSourcePixelCount = 100_000_000;

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
        CompressImageAsync(string imagePath, ResolutionType requestedResolution = ResolutionType.Medium,
            CancellationToken cancellationToken = default)
    {
        try
        {
            var fileLength = new FileInfo(imagePath).Length;
            if (fileLength > MaxSourceFileSize)
                throw new InvalidDataException($"图片文件超过 {MaxSourceFileSize / 1024 / 1024} MB 限制");

            // 读取原图
            byte[] imageData;
            using (var fs = File.OpenRead(imagePath))
            {
                using var ms = new MemoryStream();
                await fs.CopyToAsync(ms, cancellationToken);
                imageData = ms.ToArray();
            }

            cancellationToken.ThrowIfCancellationRequested();
            var imageInfo = Image.Identify(imageData)
             ?? throw new InvalidDataException("无法识别图片格式");
            if ((long)imageInfo.Width * imageInfo.Height > MaxSourcePixelCount)
                throw new InvalidDataException($"图片像素数超过 {MaxSourcePixelCount:N0} 限制");

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
            var scaleFactor = Math.Min(1f, targetSize / Math.Max(originalSize.X, originalSize.Y));
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
            }, cancellationToken);
            memoryStream.Seek(0, SeekOrigin.Begin);

            // 创建预览纹理
            var previewTexture = await _textureProvider.CreateFromImageAsync(memoryStream, leaveOpen: true);
            if (previewTexture == null)
            {
                throw new Exception("创建预览纹理失败");
            }

            // GPU纹理按解码后的RGBA像素占用显存，而不是按压缩后的PNG流大小计算。
            var originalTexture = previewTexture;
            long totalMemorySize = (long)previewTexture.Width * previewTexture.Height * 4;
            
#if DEBUG
            Penumbra.Log.Debug($"[图片压缩] 加载图片 {Path.GetFileName(imagePath)}, 分辨率: {requestedResolution}, 原始: {originalSize.X}x{originalSize.Y}, 压缩后: {scaledSize.X}x{scaledSize.Y}, 内存: {totalMemorySize/1024}KB");
#endif
            
            return (previewTexture, originalTexture, scaledSize, originalSize, totalMemorySize);
        }
        catch (OperationCanceledException)
        {
            throw;
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