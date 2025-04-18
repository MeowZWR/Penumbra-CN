#nullable enable

using System.Windows.Forms;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;

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
            _notificationManager.AddNotification(new Notification
            {
                Content = "无法导入图片：未选择模组",
                Title = "图片导入",
                Type = NotificationType.Error,
                Minimized = false,
                InitialDuration = TimeSpan.FromSeconds(3)
            });
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
            if (Clipboard.ContainsFileDropList())
            {
                var clipboardDataList = Clipboard.GetFileDropList();
                foreach (var image in clipboardDataList)
                {
                    if (image is null) continue;
                    // 生成唯一的文件名
                    var fileName = $"clipboard_{DateTime.Now:yyyyMMddHHmmss}_{importedCount}.png";
                    var filePath = Path.Combine(coverFolder, fileName);

                    if (IsImageFile(image))
                    {
                        File.Copy(image, filePath, true);
                        importedCount++;
                    }
                }

            }
            else if (Clipboard.ContainsImage())
            {
                var fileName = $"clipboard_{DateTime.Now:yyyyMMddHHmmss}_{importedCount}.png";
                var filePath = Path.Combine(coverFolder, fileName);
                var image    = Clipboard.GetImage();
                if (image != null)
                {
                    image.Save(filePath);
                }
            }
            else
            {
                _notificationManager.AddNotification(new Notification
                {
                    Content = "剪贴板中没有图片",
                    Title = "图片导入",
                    Type = NotificationType.Warning,
                    Minimized = false,
                    InitialDuration = TimeSpan.FromSeconds(3)
                });
            }

            return importedCount;
        }
        catch (Exception ex)
        {
            _notificationManager.AddNotification(new Notification
            {
                Content = $"导入图片失败: {ex.Message}",
                Title = "图片导入",
                Type = NotificationType.Error,
                Minimized = false,
                InitialDuration = TimeSpan.FromSeconds(3)
            });
            return 0;
        }
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
