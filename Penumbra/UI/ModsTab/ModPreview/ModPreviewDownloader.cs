using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using Penumbra.Mods;
using System.Net.Http;

namespace Penumbra.UI.ModsTab.ModPreview;

public class ModPreviewDownloader : IDisposable
{
    private readonly INotificationManager _notificationManager;
    private HttpClient? _httpClient;
    private readonly Configuration _configuration;
    private readonly Lock _progressLock = new();
    private readonly Dictionary<string, DownloadProgress> _downloadProgress = [];
    private bool _isDownloading;
    
    private const int MaxPreviewImageCount = 3;
    private const int MaxDownloadedImageSize = 50 * 1024 * 1024;

    private static readonly List<ISupportedModSite> SupportedSites =
    [
        new HeliosphereSite()
    ];

    public ModPreviewDownloader(INotificationManager notificationManager, Configuration configuration)
    {
        _notificationManager = notificationManager;
        _configuration = configuration;
        
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _httpClient = null;
    }

    public bool IsDownloading
    {
        get
        {
            lock (_progressLock)
                return _isDownloading;
        }
    }

    public bool TryGetDownloadProgress(out float progress, out string label)
    {
        lock (_progressLock)
        {
            if (!_isDownloading)
            {
                progress = 0;
                label = string.Empty;
                return false;
            }

            if (_downloadProgress.Count == 0)
            {
                progress = 0;
                label = "正在获取预览图...";
                return true;
            }

            var completed = 0;
            double totalProgress = 0;
            foreach (var item in _downloadProgress.Values)
            {
                if (item.Completed)
                {
                    ++completed;
                    totalProgress += 1;
                }
                else if (item.TotalBytes is > 0)
                {
                    totalProgress += Math.Clamp(item.DownloadedBytes / (double)item.TotalBytes.Value, 0, 1);
                }
            }

            progress = (float)(totalProgress / _downloadProgress.Count);
            label = $"正在下载预览图 {completed}/{_downloadProgress.Count}";
            return true;
        }
    }

    /// <summary>
    /// 检查模组是否有支持下载预览图的网站链接
    /// </summary>
    public bool CanDownloadPreview(Mod mod)
    {
        string websiteUrl = GetModWebsiteUrl(mod);
        return !string.IsNullOrEmpty(websiteUrl) && GetSupportedSite(websiteUrl) != null;
    }

    /// <summary>
    /// 获取模组网站链接
    /// </summary>
    public static string GetModWebsiteUrl(Mod mod)
        => mod.Website;

    /// <summary>
    /// 获取模组支持的网站处理器
    /// </summary>
    private static ISupportedModSite? GetSupportedSite(string websiteUrl)
    {
        if (string.IsNullOrEmpty(websiteUrl))
            return null;

        return SupportedSites.FirstOrDefault(site => site.IsSupported(websiteUrl));
    }

    /// <summary>
    /// 尝试下载模组预览图
    /// </summary>
    public async Task<bool> TryDownloadPreviewImage(Mod mod)
    {
        lock (_progressLock)
        {
            if (_isDownloading)
                return false;

            _isDownloading = true;
            _downloadProgress.Clear();
        }

        try
        {
            var websiteUrl = GetModWebsiteUrl(mod);
            if (string.IsNullOrEmpty(websiteUrl))
            {
                ShowNotification("模组没有网站链接，无法下载预览图", NotificationType.Warning);
                return false;
            }

            var site = GetSupportedSite(websiteUrl);
            if (site == null)
            {
                var source = Uri.TryCreate(websiteUrl, UriKind.Absolute, out var uri)
                    ? uri.Host
                    : websiteUrl;
                ShowNotification($"不支持从该网站下载预览图: {source}", NotificationType.Warning);
                return false;
            }

            try
            {
                var coverFolder = Path.Combine(mod.ModPath.FullName, "CoverImage");
                Directory.CreateDirectory(coverFolder);

                List<string> previewUrls;
                var originalCount = 0;
                try
                {
                    previewUrls = await site.GetPreviewImageUrls(websiteUrl, _httpClient!);

                    originalCount = previewUrls.Count;
                    if (previewUrls.Count > MaxPreviewImageCount)
                    {
                        Penumbra.Log.Information($"检测到 {previewUrls.Count} 张，限制为下载 {MaxPreviewImageCount} 张图片");
                        previewUrls = [.. previewUrls.Take(MaxPreviewImageCount)];
                    }
                }
                catch (Exception ex)
                {
                    ShowNotification($"获取预览图URL失败: {ex.Message}", NotificationType.Error);
                    return false;
                }

                if (previewUrls.Count == 0)
                {
                    ShowNotification("没有找到可下载的预览图", NotificationType.Warning);
                    return false;
                }

                lock (_progressLock)
                    foreach (var url in previewUrls)
                        _downloadProgress[url] = new DownloadProgress();

                // 并行下载
                var results = await Task.WhenAll(
                    previewUrls.Select(url => DownloadImage(url, coverFolder)));

                var successCount = results.Count(result => result);
                ShowNotification(
                    successCount > 0
                        ? $"成功下载 {successCount} / {originalCount} 张预览图"
                        : "所有预览图下载失败",
                    successCount > 0 ? NotificationType.Success : NotificationType.Error);

                return successCount > 0;
            }
            catch (Exception ex)
            {
                Penumbra.Log.Error($"下载预览图过程中出错: {ex.Message}");
                ShowNotification($"下载预览图失败: {ex.Message}", NotificationType.Error);
                return false;
            }
        }
        finally
        {
            lock (_progressLock)
            {
                _isDownloading = false;
                _downloadProgress.Clear();
            }
        }
    }

    /// <summary>
    /// 下载单张图片
    /// </summary>
    private async Task<bool> DownloadImage(string url, string destinationFolder)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "PenumbraModManager/1.0");
            
            using var response = await _httpClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return false;

            if (response.Content.Headers.ContentLength > MaxDownloadedImageSize)
                return false;

            SetDownloadTotal(url, response.Content.Headers.ContentLength);
            var imageBytes = await ReadWithLimitAsync(response.Content, MaxDownloadedImageSize,
                bytes => AddDownloadedBytes(url, bytes));
            if (!IsValidImageFile(imageBytes))
                return false;

            var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
            var fileName = GenerateFileName(response, finalUrl);
            await PreviewImageFile.WriteUniqueAsync(destinationFolder, fileName,
                output => output.WriteAsync(imageBytes, 0, imageBytes.Length));
            
            return true;
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"下载图片失败 {url}: {ex.Message}");
            return false;
        }
        finally
        {
            CompleteDownload(url);
        }
    }

    internal static async Task<byte[]> ReadWithLimitAsync(HttpContent content, int maxBytes,
        Action<int>? reportProgress = null)
    {
        await using var source = await content.ReadAsStreamAsync();
        using var destination = new MemoryStream(Math.Min((int)(content.Headers.ContentLength ?? 0), maxBytes));
        var buffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            if (destination.Length + read > maxBytes)
                throw new InvalidDataException($"下载内容超过 {maxBytes / 1024 / 1024} MB 限制");

            await destination.WriteAsync(buffer.AsMemory(0, read));
            reportProgress?.Invoke(read);
        }

        return destination.ToArray();
    }

    private void SetDownloadTotal(string url, long? totalBytes)
    {
        lock (_progressLock)
            if (_downloadProgress.TryGetValue(url, out var progress))
                progress.TotalBytes = totalBytes;
    }

    private void AddDownloadedBytes(string url, int bytes)
    {
        lock (_progressLock)
            if (_downloadProgress.TryGetValue(url, out var progress))
                progress.DownloadedBytes += bytes;
    }

    private void CompleteDownload(string url)
    {
        lock (_progressLock)
            if (_downloadProgress.TryGetValue(url, out var progress))
                progress.Completed = true;
    }
    
    /// <summary>
    /// 验证文件是否为有效的图片格式
    /// </summary>
    private static bool IsValidImageFile(byte[] data) => 
        data.Length >= 8 && (
            // JPEG
            data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF || 
            // PNG
            data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 || 
            // GIF
            data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38 || 
            // WebP
            data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && 
            data[2] == 0x46 && data[3] == 0x46 && data[8] == 0x57 && 
            data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50
        );
    
    /// <summary>
    /// 根据响应和URL生成文件名
    /// </summary>
    private static string GenerateFileName(HttpResponseMessage response, string url)
    {
        // 从Content-Type获取扩展名
        var contentType = response.Content.Headers.ContentType?.MediaType;
        string extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg" // 默认为jpg
        };
        
        // 特殊处理Heliosphere API链接
        if (url.Contains("heliosphere.app/api/web/package"))
        {
            var match = Regex.Match(url, @"/package/([a-zA-Z0-9]+)/image/(\d+)");
            if (match.Success)
                return $"heliosphere_{match.Groups[1].Value}_{match.Groups[2].Value}{extension}";
        }
        
        // 尝试从Content-Disposition获取文件名
        if (response.Content.Headers.ContentDisposition?.FileName is string fileName && !string.IsNullOrEmpty(fileName))
        {
            fileName = Path.GetFileName(fileName.Trim('"'));
            // 确保文件名不包含无效字符
            foreach (var c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');
            
            // 确保有正确的扩展名
            if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                fileName = Path.ChangeExtension(fileName, extension);
                
            return fileName;
        }
        
        // 从URL特征生成文件名
        var prefix = url.Contains("heliosphere") ? "heliosphere" : "preview";
                       
        return $"{prefix}_{Math.Abs((long)url.GetHashCode())}{extension}";
    }

    private void ShowNotification(string content, NotificationType type) =>
        _notificationManager.AddNotification(new Notification
        {
            Content = content,
            Title = "预览图下载",
            Type = type,
            InitialDuration = TimeSpan.FromSeconds(3)
        });

    private sealed class DownloadProgress
    {
        public long DownloadedBytes { get; set; }
        public long? TotalBytes { get; set; }
        public bool Completed { get; set; }
    }
}

/// <summary>
/// 支持的模组网站接口
/// </summary>
public interface ISupportedModSite
{
    public bool IsSupported(string url);
    public Task<List<string>> GetPreviewImageUrls(string modUrl, HttpClient client);
}

/// <summary>
/// Heliosphere网站支持
/// </summary>
public class HeliosphereSite : ISupportedModSite
{
    private const int MaxPageSize = 5 * 1024 * 1024;
    private static readonly Regex ModIdRegex = new(@"^/mod/([a-zA-Z0-9]+)(?:/|$)", RegexOptions.IgnoreCase);

    public bool IsSupported(string url)
        => TryGetModUri(url, out _);

    public async Task<List<string>> GetPreviewImageUrls(string modUrl, HttpClient client)
    {
        var result = new List<string>();
        
        try
        {
            if (!TryGetModUri(modUrl, out var modUri))
                return result;

            // 获取页面内容
            using var response = await client.GetAsync(modUri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxPageSize)
                return result;

            var responseBytes = await ModPreviewDownloader.ReadWithLimitAsync(response.Content, MaxPageSize);
            var page = System.Text.Encoding.UTF8.GetString(responseBytes);
            
            // 尝试查找API图片路径
            foreach (Match match in Regex.Matches(page, @"/api/web/package/([a-zA-Z0-9]+)/image/(\d+)"))
            {
                var packageId = match.Groups[1].Value;
                var imageId = match.Groups[2].Value;
                var apiImageUrl = $"https://heliosphere.app/api/web/package/{packageId}/image/{imageId}";
                
                if (!result.Contains(apiImageUrl))
                    result.Add(apiImageUrl);
            }
            
            if (result.Count == 0)
            {
                foreach (Match match in Regex.Matches(page, @"https://data\.heliosphere\.app/images/[a-zA-Z0-9_\-]+"))
                {
                    if (!result.Contains(match.Value))
                        result.Add(match.Value);
                }
            }
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"获取Heliosphere预览图失败: {ex.Message}");
        }
        
        return result;
    }

    private static bool TryGetModUri(string url, out Uri uri)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed)
         && parsed.Scheme == Uri.UriSchemeHttps
         && parsed.Host.Equals("heliosphere.app", StringComparison.OrdinalIgnoreCase)
         && ModIdRegex.IsMatch(parsed.AbsolutePath))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

} 
