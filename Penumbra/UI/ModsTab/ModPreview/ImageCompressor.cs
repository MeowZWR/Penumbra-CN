using System;
using System.IO;
using System.Threading.Tasks;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using System.Numerics;

namespace Penumbra.UI.ModsTab.ModPreview;

public class ImageCompressor : IDisposable
{
    private readonly ITextureProvider _textureProvider;
    private const int MaxCompressDimension = 1024;
    private const int MaxRetryCount = 3;
    private const int FileOperationDelay = 100;

    public ImageCompressor(ITextureProvider textureProvider)
    {
        _textureProvider = textureProvider;
    }

    public async Task<(IDalamudTextureWrap? PreviewTexture, IDalamudTextureWrap? OriginalTexture, Vector2 ScaledSize, Vector2 OriginalSize, long MemorySize)> CompressImageAsync(string imagePath)
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
            
            // 计算缩放尺寸
            var scaledSize = GetScaledSize(originalSize, MaxCompressDimension);

            // 创建预览图
            using var previewImage = image.Width <= MaxCompressDimension && image.Height <= MaxCompressDimension
                ? image.Clone()
                : image.Clone(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(MaxCompressDimension, MaxCompressDimension),
                    Mode = ResizeMode.Max,
                    Compand = false
                }));

            // 保存预览图到内存流
            using var memoryStream = new MemoryStream();
            await previewImage.SaveAsPngAsync(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);

            // 创建纹理
            var previewTexture = await _textureProvider.CreateFromImageAsync(memoryStream, leaveOpen: true);
            if (previewTexture == null)
            {
                throw new Exception("创建预览纹理失败");
            }

            // 创建原始图片纹理
            using var originalMs = new MemoryStream();
            await image.SaveAsPngAsync(originalMs, new PngEncoder
            {
                ColorType = PngColorType.RgbWithAlpha,
                BitDepth = PngBitDepth.Bit8,
                CompressionLevel = PngCompressionLevel.NoCompression
            });
            originalMs.Seek(0, SeekOrigin.Begin);
            var originalTexture = await _textureProvider.CreateFromImageAsync(originalMs, leaveOpen: true);

            if (originalTexture == null)
            {
                previewTexture.Dispose();
                throw new Exception("创建原始纹理失败");
            }

            return (previewTexture, originalTexture, scaledSize, originalSize, memoryStream.Length + originalMs.Length);
        }
        catch (Exception ex)
        {
            throw new Exception($"压缩图片失败: {ex.Message}", ex);
        }
    }

    private Vector2 GetScaledSize(Vector2 originalSize, float maxWidth)
    {
        var scale = maxWidth / originalSize.X;
        return new Vector2(originalSize.X * scale, originalSize.Y * scale);
    }

    public void Dispose()
    {
        // 清理资源
    }
} 