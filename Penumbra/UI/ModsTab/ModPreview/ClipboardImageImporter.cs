#nullable enable

using System.Windows.Forms;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using SixLabors.ImageSharp.Formats.Webp;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using System.Drawing.Imaging;

namespace Penumbra.UI.ModsTab.ModPreview;

/// <summary>
/// 负责从系统剪贴板导入图片到Mod的CoverImage文件夹
/// </summary>
public class ClipboardImageImporter
{
    private readonly INotificationManager _notificationManager;

    // 支持的图片扩展名
    private static readonly string[] SupportedImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tga", ".webp" };

    public ClipboardImageImporter(
        INotificationManager notificationManager,
        IDalamudPluginInterface pluginInterface,
        ModManager modManager)
    {
        _notificationManager = notificationManager;
    }


    /// <summary>
    /// 从剪贴板导入图片到指定Mod的CoverImage文件夹
    /// </summary>
    /// <param name="mod">目标Mod</param>
    /// <returns>导入的图片数量</returns>
    public int ImportFromClipboard(Mod mod)
    {
        if (mod == null)
        {
            ShowNotification("无法导入图片：未选择模组", NotificationType.Error);
            return 0;
        }

        try
        {
            // 确保CoverImage文件夹存在
            var coverFolder = Path.Combine(mod.ModPath.FullName, "CoverImage");
            if (!Directory.Exists(coverFolder))
            {
                Directory.CreateDirectory(coverFolder);
            }

            int importedCount = 0;
            int failedCount = 0;
            if (Clipboard.ContainsFileDropList())
            {
                var clipboardDataList = Clipboard.GetFileDropList();
                foreach (var image in clipboardDataList)
                {
                    if (image is null) continue;

                    if (!IsImageFile(image))
                        continue;

                    try
                    {
                        using var sourceImage = ImageSharpImage.Load(image);
                        var fileName = Path.ChangeExtension(Path.GetFileName(image), ".webp");
                        PreviewImageFile.WriteUnique(coverFolder, fileName,
                            output => sourceImage.Save(output, new WebpEncoder { Quality = 85 }));
                        importedCount++;
                    }
                    catch (Exception ex)
                    {
                        Penumbra.Log.Warning($"转换剪贴板图片失败: {image} - {ex.Message}");
                        failedCount++;
                    }
                }
            }
            else if (Clipboard.ContainsImage())
            {
                using var clipboardImage = Clipboard.GetImage();
                if (clipboardImage != null)
                {
                    using var sourceStream = new MemoryStream();
                    clipboardImage.Save(sourceStream, ImageFormat.Png);
                    sourceStream.Position = 0;
                    using var sourceImage = ImageSharpImage.Load(sourceStream);
                    var fileName = $"clipboard_{DateTime.Now:yyyyMMddHHmmssfff}.webp";
                    PreviewImageFile.WriteUnique(coverFolder, fileName,
                        output => sourceImage.Save(output, new WebpEncoder { Quality = 85 }));
                    importedCount++;
                }
            }

            if (importedCount > 0)
            {
                ShowNotification($"成功导入 {importedCount} 张图片", NotificationType.Success);
            }
            else if (failedCount == 0)
            {
                ShowNotification("剪贴板中没有图片或图片格式不支持", NotificationType.Warning);
            }

            if (failedCount > 0)
                ShowNotification($"{failedCount} 张图片转换失败", NotificationType.Error);

            return importedCount;
        }
        catch (Exception ex)
        {
            ShowNotification($"导入图片失败: {ex.Message}", NotificationType.Error);
            return 0;
        }
    }

    private void ShowNotification(string content, NotificationType type)
    {
        _notificationManager.AddNotification(new Notification
        {
            Content = content,
            Title = "图片预览",
            Type = type,
            Minimized = false,
            InitialDuration = TimeSpan.FromSeconds(3)
        });
    }

    /// <summary>
    /// 检查文件是否是支持的图片文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否是支持的图片文件</returns>
    private bool IsImageFile(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            string extension = Path.GetExtension(filePath).ToLower();
            return Array.Exists(SupportedImageExtensions, ext => ext == extension);
        }
        catch
        {
            return false;
        }
    }
} 
