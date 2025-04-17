#nullable enable

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

    // Windows API 常量
    private const uint CF_DIB = 8;
    private const uint CF_BITMAP = 2;
    private const uint CF_HDROP = 15;
    private const uint CF_UNICODETEXT = 13;
    private const uint CF_TIFF = 6;

    // 支持的图片扩展名
    private static readonly string[] SupportedImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tga", ".webp" };

    // Windows API 函数
    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll")]
    private static extern uint EnumClipboardFormats(uint format);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalSize(IntPtr hMem);

    // DIB结构
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint BiSize;
        public int BiWidth;
        public int BiHeight;
        public ushort BiPlanes;
        public ushort BiBitCount;
        public uint BiCompression;
        public uint BiSizeImage;
        public int BiXPelsPerMeter;
        public int BiYPelsPerMeter;
        public uint BiClrUsed;
        public uint BiClrImportant;
    }

    // HDROP结构
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, int cch);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr hDrop);

    public ClipboardImageImporter(
        INotificationManager notificationManager,
        IDalamudPluginInterface pluginInterface,
        ModManager modManager)
    {
        _notificationManager = notificationManager;
    }

    /// <summary>
    /// 从剪贴板获取图片数据列表
    /// </summary>
    /// <returns>图片数据列表，如果没有图片则返回空列表</returns>
    private List<byte[]> GetClipboardImageData()
    {
        var result = new List<byte[]>();
        try
        {
            if (!OpenClipboard(IntPtr.Zero))
                return result;

            try
            {
                // 1. 首先检查是否有HDROP格式（文件资源管理器复制的文件）
                IntPtr hDrop = GetClipboardData(CF_HDROP);
                if (hDrop != IntPtr.Zero)
                {
                    // 获取拖放的文件数量
                    int fileCount = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
                    if (fileCount > 0)
                    {
                        // 获取所有文件
                        StringBuilder filePath = new StringBuilder(260);
                        for (uint i = 0; i < fileCount; i++)
                        {
                            if (DragQueryFile(hDrop, i, filePath, filePath.Capacity) > 0)
                            {
                                string path = filePath.ToString();
                                // 检查是否是支持的图片文件
                                if (IsImageFile(path))
                                {
                                    // 读取文件内容
                                    result.Add(File.ReadAllBytes(path));
                                }
                            }
                        }
                    }
                    DragFinish(hDrop);
                }

                // 2. 检查剪贴板中是否有DIB格式的图片
                IntPtr hBitmap = GetClipboardData(CF_DIB);
                if (hBitmap != IntPtr.Zero)
                {
                    // 获取DIB数据
                    IntPtr pData = GlobalLock(hBitmap);
                    if (pData != IntPtr.Zero)
                    {
                        try
                        {
                            // 获取数据大小
                            IntPtr size = GlobalSize(hBitmap);
                            byte[] dibData = new byte[size.ToInt64()];
                            Marshal.Copy(pData, dibData, 0, dibData.Length);
                            
                            // 将DIB数据转换为PNG
                            var pngData = ConvertDibToPng(dibData);
                            if (pngData != null)
                            {
                                result.Add(pngData);
                            }
                        }
                        finally
                        {
                            GlobalUnlock(hBitmap);
                        }
                    }
                }

                // 3. 检查是否有文本格式（可能是文件路径）
                IntPtr hText = GetClipboardData(CF_UNICODETEXT);
                if (hText != IntPtr.Zero)
                {
                    IntPtr pText = GlobalLock(hText);
                    if (pText != IntPtr.Zero)
                    {
                        try
                        {
                            int length = 0;
                            while (Marshal.ReadByte(pText, length) != 0)
                                length++;

                            byte[] textBytes = new byte[length];
                            Marshal.Copy(pText, textBytes, 0, length);
                            string text = Encoding.Unicode.GetString(textBytes).TrimEnd('\0');

                            // 检查是否是支持的图片文件路径
                            if (IsImageFile(text))
                            {
                                // 读取文件内容
                                result.Add(File.ReadAllBytes(text));
                            }
                        }
                        finally
                        {
                            GlobalUnlock(hText);
                        }
                    }
                }

                // 4. 如果没有DIB格式，尝试其他格式
                uint format = 0;
                while ((format = EnumClipboardFormats(format)) != 0)
                {
                    // 检查是否是图片格式
                    if (IsImageFormat(format))
                    {
                        IntPtr hData = GetClipboardData(format);
                        if (hData != IntPtr.Zero)
                        {
                            IntPtr pData = GlobalLock(hData);
                            if (pData != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr size = GlobalSize(hData);
                                    byte[] data = new byte[size.ToInt64()];
                                    Marshal.Copy(pData, data, 0, data.Length);
                                    result.Add(data);
                                }
                                finally
                                {
                                    GlobalUnlock(hData);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                CloseClipboard();
            }
        }
        catch (Exception)
        {
            // 发生异常时返回空列表
        }

        return result;
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

            // 尝试从剪贴板获取图片数据列表
            var clipboardDataList = GetClipboardImageData();
            if (clipboardDataList.Count > 0)
            {
                foreach (var clipboardData in clipboardDataList)
                {
                    if (clipboardData != null && clipboardData.Length > 0)
                    {
                        // 生成唯一的文件名
                        var fileName = $"clipboard_{DateTime.Now:yyyyMMddHHmmss}_{importedCount}.png";
                        var filePath = Path.Combine(coverFolder, fileName);

                        // 将剪贴板数据保存为图片
                        File.WriteAllBytes(filePath, clipboardData);
                        importedCount++;
                    }
                }
                
                _notificationManager.AddNotification(new Notification
                {
                    Content = $"已导入{importedCount}张图片到 {mod.Name} 的CoverImage文件夹",
                    Title = "图片导入",
                    Type = NotificationType.Success,
                    Minimized = false,
                    InitialDuration = TimeSpan.FromSeconds(3)
                });
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

    /// <summary>
    /// 将DIB数据转换为PNG格式
    /// </summary>
    /// <param name="dibData">DIB数据</param>
    /// <returns>PNG格式的图片数据</returns>
    private byte[]? ConvertDibToPng(byte[] dibData)
    {
        try
        {
            // 解析BITMAPINFOHEADER
            var header = new BITMAPINFOHEADER();
            GCHandle handle = GCHandle.Alloc(dibData, GCHandleType.Pinned);
            try
            {
                IntPtr ptr = handle.AddrOfPinnedObject();
                var result = Marshal.PtrToStructure(ptr, typeof(BITMAPINFOHEADER));
                if (result == null)
                    return null;
                    
                header = (BITMAPINFOHEADER)result;
            }
            finally
            {
                handle.Free();
            }

            // 检查是否是有效的DIB
            if (header.BiSize < 40 || header.BiWidth <= 0 || header.BiHeight <= 0)
                return null;

            // 计算图像数据的大小
            int width = Math.Abs(header.BiWidth);
            int height = Math.Abs(header.BiHeight);
            int bitsPerPixel = header.BiBitCount;
            int bytesPerPixel = (bitsPerPixel + 7) / 8;
            int stride = ((width * bytesPerPixel + 3) / 4) * 4; // 4字节对齐

            // 创建ImageSharp图像
            using var image = new Image<Rgba32>(width, height);
            
            // 复制像素数据
            int headerSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            int colorTableSize = 0;
            
            // 如果是调色板格式，计算颜色表大小
            if (bitsPerPixel <= 8)
            {
                int maxColors = 1 << bitsPerPixel;
                colorTableSize = header.BiClrUsed > 0 ? (int)header.BiClrUsed : maxColors;
                colorTableSize *= 4; // 每个颜色表项是4字节
            }
            
            int pixelDataOffset = headerSize + colorTableSize;
            
            // 处理不同的位深度
            if (bitsPerPixel == 32)
            {
                // 32位真彩色
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int sourceIndex = pixelDataOffset + (height - 1 - y) * stride + x * 4;
                        if (sourceIndex + 3 < dibData.Length)
                        {
                            byte b = dibData[sourceIndex];
                            byte g = dibData[sourceIndex + 1];
                            byte r = dibData[sourceIndex + 2];
                            byte a = dibData[sourceIndex + 3];
                            image[x, y] = new Rgba32(r, g, b, a);
                        }
                    }
                }
            }
            else if (bitsPerPixel == 24)
            {
                // 24位真彩色
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int sourceIndex = pixelDataOffset + (height - 1 - y) * stride + x * 3;
                        if (sourceIndex + 2 < dibData.Length)
                        {
                            byte b = dibData[sourceIndex];
                            byte g = dibData[sourceIndex + 1];
                            byte r = dibData[sourceIndex + 2];
                            image[x, y] = new Rgba32(r, g, b, 255);
                        }
                    }
                }
            }
            else if (bitsPerPixel == 8)
            {
                // 8位索引色
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int sourceIndex = pixelDataOffset + (height - 1 - y) * stride + x;
                        if (sourceIndex < dibData.Length)
                        {
                            int colorIndex = dibData[sourceIndex];
                            if (colorIndex < colorTableSize / 4)
                            {
                                int colorTableOffset = headerSize + colorIndex * 4;
                                if (colorTableOffset + 2 < dibData.Length)
                                {
                                    byte b = dibData[colorTableOffset];
                                    byte g = dibData[colorTableOffset + 1];
                                    byte r = dibData[colorTableOffset + 2];
                                    image[x, y] = new Rgba32(r, g, b, 255);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // 不支持的位深度
                return null;
            }

            // 将图像保存为PNG
            using var ms = new MemoryStream();
            image.Save(ms, new PngEncoder());
            return ms.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 检查剪贴板格式是否是图片格式
    /// </summary>
    /// <param name="format">剪贴板格式</param>
    /// <returns>是否是图片格式</returns>
    private bool IsImageFormat(uint format)
    {
        // 常见的图片格式
        return format == CF_DIB || format == CF_BITMAP || format == CF_TIFF;
    }
} 