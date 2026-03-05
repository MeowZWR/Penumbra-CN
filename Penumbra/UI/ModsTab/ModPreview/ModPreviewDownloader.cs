using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using Newtonsoft.Json.Linq;
using Penumbra.Mods;
using System.Net.Http;

namespace Penumbra.UI.ModsTab.ModPreview;

public class ModPreviewDownloader : IDisposable
{
    private readonly INotificationManager _notificationManager;
    private HttpClient? _httpClient;
    private readonly Configuration _configuration;
    
    private const int MaxPreviewImageCount = 3;

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
    {
        try
        {
            var metaFile = Path.Combine(mod.ModPath.FullName, "meta.json");
            if (!File.Exists(metaFile))
                return string.Empty;

            var json = JObject.Parse(File.ReadAllText(metaFile));
            return json["Website"]?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"读取模组网站链接失败: {ex.Message}");
            return string.Empty;
        }
    }

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
        var websiteUrl = GetModWebsiteUrl(mod);
        if (string.IsNullOrEmpty(websiteUrl))
        {
            ShowNotification("模组没有网站链接，无法下载预览图", NotificationType.Warning);
            return false;
        }

        var site = GetSupportedSite(websiteUrl);
        
        if (site == null)
        {
            ShowNotification($"不支持从该网站下载预览图: {new Uri(websiteUrl).Host}", NotificationType.Warning);
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

            // 并行下载
            var results = await Task.WhenAll(
                previewUrls.Select(url => DownloadImage(url, coverFolder)));
            
            var successCount = results.Count(r => r);
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

    /// <summary>
    /// 下载单张图片
    /// </summary>
    private async Task<bool> DownloadImage(string url, string destinationFolder)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "PenumbraModManager/1.0");
            
            var response = await _httpClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return false;
            
            // 处理重定向
            var finalUrl = url;
            if (response.Headers.Location != null)
            {
                finalUrl = response.Headers.Location.ToString();
                if (!finalUrl.StartsWith("http"))
                    finalUrl = new Uri(new Uri(url), finalUrl).ToString();
                
                request = new HttpRequestMessage(HttpMethod.Get, finalUrl);
                request.Headers.Add("User-Agent", "PenumbraModManager/1.0");
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                
                if (!response.IsSuccessStatusCode)
                    return false;
            }
            
            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            if (!IsValidImageFile(imageBytes))
                return false;
            
            var fileName = GenerateFileName(response, finalUrl);
            await File.WriteAllBytesAsync(Path.Combine(destinationFolder, fileName), imageBytes);
            
            return true;
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"下载图片失败 {url}: {ex.Message}");
            return false;
        }
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
            fileName = fileName.Trim('"');
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
                       
        return $"{prefix}_{Math.Abs(url.GetHashCode())}_{DateTime.Now.Ticks % 10000}{extension}";
    }

    private void ShowNotification(string content, NotificationType type) =>
        _notificationManager.AddNotification(new Notification
        {
            Content = content,
            Title = "预览图下载",
            Type = type,
            InitialDuration = TimeSpan.FromSeconds(3)
        });
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
    private static readonly Regex ModIdRegex = new(@"heliosphere\.app/mod/([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);

    public bool IsSupported(string url) => ModIdRegex.IsMatch(url);

    public async Task<List<string>> GetPreviewImageUrls(string modUrl, HttpClient client)
    {
        var result = new List<string>();
        
        try
        {
            // 验证URL格式
            if (!ModIdRegex.IsMatch(modUrl))
                return result;

            // 获取页面内容
            var response = await client.GetStringAsync(modUrl);
            
            // 尝试查找API图片路径
            foreach (Match match in Regex.Matches(response, @"/api/web/package/([a-zA-Z0-9]+)/image/(\d+)"))
            {
                var packageId = match.Groups[1].Value;
                var imageId = match.Groups[2].Value;
                var apiImageUrl = $"https://heliosphere.app/api/web/package/{packageId}/image/{imageId}";
                
                if (!result.Contains(apiImageUrl))
                    result.Add(apiImageUrl);
            }
            
            if (result.Count == 0)
            {
                foreach (Match match in Regex.Matches(response, @"https://data\.heliosphere\.app/images/[a-zA-Z0-9_\-]+"))
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
} 
