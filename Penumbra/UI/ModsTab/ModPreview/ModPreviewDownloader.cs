using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using Newtonsoft.Json.Linq;
using Penumbra.Mods;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Net;
using System.Reflection;

namespace Penumbra.UI.ModsTab.ModPreview;

public class ModPreviewDownloader
{
    private readonly INotificationManager _notificationManager;
    private HttpClient _httpClient = new();
    private readonly Configuration _configuration;
    
    // 添加静态引用以便全局访问
    private static readonly List<ModPreviewDownloader> Instances = new();

    // 支持的模组网站列表
    private static readonly List<ISupportedModSite> SupportedSites = new()
    {
        new XivModArchiveSite(),
        new HeliosphereSite()
    };

    /// <summary>
    /// 应用代理设置到所有使用此类的实例
    /// </summary>
    /// <param name="config">配置对象</param>
    public static void ApplyProxySettings(Configuration config)
    {
        try
        {
            if (Instances.Count > 0)
            {
                foreach (var downloader in Instances)
                {
                    downloader.RecreateHttpClient();
                }
                Penumbra.Log.Information($"成功应用代理设置到 {Instances.Count} 个预览图下载器");
            }
            else
            {
                Penumbra.Log.Warning("没有找到预览图下载器实例");
            }
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"应用代理设置时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 测试代理连接
    /// </summary>
    /// <param name="config">配置对象</param>
    /// <returns>测试结果和消息</returns>
    public static async Task<(bool success, string message)> TestProxyConnection(Configuration config)
    {
        try
        {
            var handler = new HttpClientHandler();
            if (config.UseManualProxy)
            {
                handler.UseProxy = true;
                handler.Proxy = new WebProxy($"{config.ProxyProtocol}://{config.ProxyHost}:{config.ProxyPort}", true);
            }
            else
            {
                handler.UseProxy = false;
            }
            handler.AllowAutoRedirect = true;

            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(10);

            var testUrl = "https://www.google.com";
            Penumbra.Log.Debug($"测试代理连接 {testUrl}");
            
            var response = await client.GetAsync(testUrl);
            if (response.IsSuccessStatusCode)
            {
                return (true, "连接测试成功。");
            }
            else
            {
                return (false, $"连接测试失败，状态码: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            Penumbra.Log.Error($"测试代理连接时出错: {ex.Message}");
            return (false, $"连接失败: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            Penumbra.Log.Error("测试代理连接超时");
            return (false, "连接测试超时");
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"测试代理连接时出现异常: {ex.Message}");
            return (false, $"连接出错: {ex.Message}");
        }
    }

    public ModPreviewDownloader(INotificationManager notificationManager, Configuration configuration)
    {
        _notificationManager = notificationManager;
        _configuration = configuration;
        
        Instances.Add(this);
    }

    /// <summary>
    /// 重新创建HttpClient，应用当前配置中的代理设置
    /// </summary>
    private void RecreateHttpClient()
    {
        // 如果已存在，则释放旧的HttpClient
        _httpClient?.Dispose();

        // 创建新的HttpHandler并应用代理设置
        var handler = new HttpClientHandler();
        if (_configuration.UseManualProxy)
        {
            handler.UseProxy = true;
            handler.Proxy = new WebProxy($"{_configuration.ProxyProtocol}://{_configuration.ProxyHost}:{_configuration.ProxyPort}", true);
            Penumbra.Log.Debug($"预览下载器使用代理: {_configuration.ProxyProtocol}://{_configuration.ProxyHost}:{_configuration.ProxyPort}");
        }
        else
        {
            handler.UseProxy = false;
            Penumbra.Log.Debug("预览下载器使用系统默认连接");
        }

        // 允许自动重定向
        handler.AllowAutoRedirect = true;

        // 创建新的HttpClient
        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        // 从实例列表中移除
        Instances.Remove(this);
    }

    /// <summary>
    /// 检查模组是否有支持下载预览图的网站链接
    /// </summary>
    /// <param name="mod">要检查的模组</param>
    /// <returns>是否支持下载预览图</returns>
    public bool CanDownloadPreview(Mod mod)
    {
        string websiteUrl = GetModWebsiteUrl(mod);
        return !string.IsNullOrEmpty(websiteUrl) && GetSupportedSite(websiteUrl) != null;
    }

    /// <summary>
    /// 获取模组网站链接
    /// </summary>
    /// <param name="mod">模组</param>
    /// <returns>网站链接，如果没有则返回空字符串</returns>
    public string GetModWebsiteUrl(Mod mod)
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
    /// <param name="websiteUrl">网站链接</param>
    /// <returns>网站处理器，不支持则返回null</returns>
    private ISupportedModSite? GetSupportedSite(string websiteUrl)
    {
        if (string.IsNullOrEmpty(websiteUrl))
            return null;

        return SupportedSites.FirstOrDefault(site => site.IsSupported(websiteUrl));
    }

    /// <summary>
    /// 尝试下载模组预览图
    /// </summary>
    /// <param name="mod">要下载预览图的模组</param>
    /// <param name="forceDownload">强制下载，忽略站点支持检测</param>
    /// <returns>是否下载成功</returns>
    public async Task<bool> TryDownloadPreviewImage(Mod mod, bool forceDownload = false)
    {
        try
        {
            string websiteUrl = GetModWebsiteUrl(mod);
            if (string.IsNullOrEmpty(websiteUrl))
            {
                ShowNotification("模组没有网站链接，无法下载预览图", NotificationType.Warning);
                return false;
            }

            var site = GetSupportedSite(websiteUrl);
            // 处理xivmodarchive特殊情况
            var isXivModArchive = websiteUrl.Contains("xivmodarchive.com");
            
            if (site == null && !(isXivModArchive && forceDownload))
            {
                ShowNotification($"不支持从该网站下载预览图: {new Uri(websiteUrl).Host}", NotificationType.Warning);
                return false;
            }
            
            if (site == null && isXivModArchive && forceDownload)
            {
                site = SupportedSites.FirstOrDefault(s => s is XivModArchiveSite) as XivModArchiveSite;
                if (site == null)
                {
                    ShowNotification("无法获取XIV Mod Archive站点处理器", NotificationType.Error);
                    return false;
                }
            }

            // 确保CoverImage文件夹存在
            var coverFolder = Path.Combine(mod.ModPath.FullName, "CoverImage");
            if (!Directory.Exists(coverFolder))
            {
                Directory.CreateDirectory(coverFolder);
            }

            // 获取预览图URL列表
            List<string> previewUrls;
            try
            {
                previewUrls = await site!.GetPreviewImageUrls(websiteUrl, _httpClient);
            }
            catch (CloudflareBlockedException ex)
            {
                // 如果是在强制模式下，给出更明确的提示
                if (forceDownload && isXivModArchive)
                {
                    ShowNotification($"即使在强制模式下也无法访问XIV Mod Archive: {ex.Message}\n请考虑在浏览器中登录后使用代理。", NotificationType.Error);
                }
                else
                {
                    ShowNotification(ex.Message, NotificationType.Error);
                }
                return false;
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

            // 下载预览图
            int successCount = 0;
            foreach (var url in previewUrls)
            {
                try
                {
                    if (await DownloadImage(url, coverFolder))
                        successCount++;
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"下载预览图失败 {url}: {ex.Message}");
                }
            }

            if (successCount > 0)
            {
                ShowNotification($"成功下载 {successCount} 张预览图到CoverImage文件夹", NotificationType.Success);
                return true;
            }
            else
            {
                ShowNotification("所有预览图下载失败", NotificationType.Error);
                return false;
            }
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
    /// <param name="url">图片URL</param>
    /// <param name="destinationFolder">保存目录</param>
    /// <returns>是否下载成功</returns>
    private async Task<bool> DownloadImage(string url, string destinationFolder)
    {
        try
        {
            // 使用特殊处理跟随重定向链接
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "PenumbraModManager/1.0");
            
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            
            if (!response.IsSuccessStatusCode)
            {
                Penumbra.Log.Warning($"下载图片失败，HTTP状态码: {response.StatusCode}, URL: {url}");
                return false;
            }
            
            // 处理重定向链接
            string finalUrl = url;
            string fileName = "";
            string filePath = "";
            
            if (response.Headers.Location != null)
            {
                finalUrl = response.Headers.Location.ToString();
                // 如果是相对URL，转换为绝对URL
                if (!finalUrl.StartsWith("http"))
                {
                    finalUrl = new Uri(new Uri(url), finalUrl).ToString();
                }
                
                Penumbra.Log.Debug($"图片URL重定向: {url} -> {finalUrl}");
                
                // 对新URL再次请求
                request = new HttpRequestMessage(HttpMethod.Get, finalUrl);
                request.Headers.Add("User-Agent", "PenumbraModManager/1.0");
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                
                if (!response.IsSuccessStatusCode)
                {
                    Penumbra.Log.Warning($"请求重定向后URL失败: {finalUrl}, 状态码: {response.StatusCode}");
                    return false;
                }
            }
            // 特殊处理 Heliosphere API 链接
            else if (url.Contains("heliosphere.app/api/web/package"))
            {
                // API 响应没有重定向，但我们需要手动处理API响应
                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                
                // 根据内容类型确定文件扩展名
                var contentType = response.Content.Headers.ContentType?.MediaType;
                string extension = contentType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png", 
                    "image/webp" => ".webp",
                    "image/gif" => ".gif",
                    _ => ".jpg" // 默认为jpg
                };
                
                // 从URL中提取唯一标识部分作为文件名
                var match = new Regex(@"/package/([a-zA-Z0-9]+)/image/(\d+)").Match(url);
                if (match.Success)
                {
                    fileName = $"heliosphere_{match.Groups[1].Value}_{match.Groups[2].Value}{extension}";
                }
                else
                {
                    fileName = $"heliosphere_{Guid.NewGuid().ToString("N")}{extension}";
                }
                
                filePath = Path.Combine(destinationFolder, fileName);
                await File.WriteAllBytesAsync(filePath, imageBytes);
                
                Penumbra.Log.Debug($"成功下载Heliosphere API图片: {fileName}");
                return true;
            }

            // 如果尚未处理图片下载，使用常规方法
            
            // 1. 尝试从Content-Disposition头获取文件名
            if (response.Content.Headers.ContentDisposition != null && 
                !string.IsNullOrEmpty(response.Content.Headers.ContentDisposition.FileName))
            {
                fileName = response.Content.Headers.ContentDisposition.FileName.Trim('"');
                Penumbra.Log.Debug($"从Content-Disposition获取文件名: {fileName}");
            }
            // 2. 尝试从URL获取文件名
            else
            {
                fileName = Path.GetFileName(finalUrl);
                
                // 如果从URL获取的文件名没有有效扩展名或者包含无效字符
                bool hasValidExtension = !string.IsNullOrEmpty(Path.GetExtension(fileName));
                bool hasInvalidChars = fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
                
                if (!hasValidExtension || hasInvalidChars || string.IsNullOrEmpty(fileName))
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
                    
                    // 如果URL中有关键标识，使用它作为文件名的一部分
                    if (finalUrl.Contains("heliosphere"))
                    {
                        var urlHash = Math.Abs(finalUrl.GetHashCode()).ToString();
                        fileName = $"heliosphere_{urlHash}{extension}";
                    }
                    else if (finalUrl.Contains("xivmodarchive"))
                    {
                        var urlHash = Math.Abs(finalUrl.GetHashCode()).ToString();
                        fileName = $"xivmodarchive_{urlHash}{extension}";
                    }
                    else
                    {
                        // 生成通用唯一文件名
                        fileName = $"preview_{Guid.NewGuid().ToString("N")}{extension}";
                    }
                    
                    Penumbra.Log.Debug($"为URL生成文件名: {finalUrl} -> {fileName}");
                }
            }

            filePath = Path.Combine(destinationFolder, fileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(fileStream);
            }

            Penumbra.Log.Debug($"成功下载图片: {fileName}，源URL: {url}");
            return true;
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"下载图片失败 {url}: {ex.Message}");
            return false;
        }
    }

    private void ShowNotification(string content, NotificationType type)
    {
        _notificationManager.AddNotification(new Notification
        {
            Content = content,
            Title = "预览图下载",
            Type = type,
            Minimized = false,
            InitialDuration = TimeSpan.FromSeconds(3)
        });
    }
}

/// <summary>
/// 支持的模组网站接口
/// </summary>
public interface ISupportedModSite
{
    /// <summary>
    /// 检查URL是否是该网站
    /// </summary>
    bool IsSupported(string url);

    /// <summary>
    /// 获取预览图URL列表
    /// </summary>
    Task<List<string>> GetPreviewImageUrls(string modUrl, HttpClient client);
}

/// <summary>
/// XIV Mod Archive网站支持
/// </summary>
public class XivModArchiveSite : ISupportedModSite
{
    private static readonly Regex ModIdRegex = new Regex(@"xivmodarchive\.com/modid/(\d+)", RegexOptions.IgnoreCase);
    private static bool _isCloudflareBlocked = false;
    private static DateTime _lastCheckTime = DateTime.MinValue;
    // 检测cloudflare阻止的间隔，避免频繁检查
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    public bool IsSupported(string url)
    {
        return ModIdRegex.IsMatch(url);
    }

    public async Task<List<string>> GetPreviewImageUrls(string modUrl, HttpClient client)
    {
        var result = new List<string>();
        
        try
        {
            // 如果之前检测到被Cloudflare阻止，并且还在冷却时间内，直接返回特定错误
            if (_isCloudflareBlocked && DateTime.Now - _lastCheckTime < CheckInterval)
            {
                throw new CloudflareBlockedException("XIV Mod Archive启用了Cloudflare保护，需要在浏览器中访问并登录后才能下载预览图。");
            }

            var match = ModIdRegex.Match(modUrl);
            if (!match.Success)
                return result;

            string modId = match.Groups[1].Value;
            
            // 获取模组页面内容
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, modUrl);
            requestMessage.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            requestMessage.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            requestMessage.Headers.Add("Accept-Language", "en-US,en;q=0.5");
            
            var response = await client.SendAsync(requestMessage);
            
            // 检测是否被Cloudflare拦截
            var content = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || 
                content.Contains("Cloudflare") && content.Contains("challenge") ||
                content.Contains("security check"))
            {
                _isCloudflareBlocked = true;
                _lastCheckTime = DateTime.Now;
                Penumbra.Log.Warning("XIV Mod Archive网站启用了Cloudflare保护，无法直接访问。");
                throw new CloudflareBlockedException("XIV Mod Archive启用了Cloudflare保护，需要在浏览器中访问并登录后才能下载预览图。");
            }
            
            if (!response.IsSuccessStatusCode)
            {
                Penumbra.Log.Warning($"访问XIV Mod Archive失败，HTTP状态码: {response.StatusCode}");
                throw new HttpRequestException($"访问XIV Mod Archive失败，HTTP状态码: {response.StatusCode}");
            }
            
            // 重置阻止状态（如果之前被阻止但现在可以访问了）
            _isCloudflareBlocked = false;
            
            // 查找预览图URL
            var imageRegex = new Regex(@"https://static\.xivmodarchive\.com/mod-images/[a-zA-Z0-9-]+\.(jpg|png|webp|jpeg)", RegexOptions.IgnoreCase);
            var matches = imageRegex.Matches(content);
            
            foreach (Match imageMatch in matches)
            {
                string imageUrl = imageMatch.Value;
                if (!result.Contains(imageUrl))
                    result.Add(imageUrl);
            }
        }
        catch (CloudflareBlockedException)
        {
            // 重新抛出这个特定的异常以便外部处理
            throw;
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"获取XIV Mod Archive预览图失败: {ex.Message}");
            throw new Exception($"获取XIV Mod Archive预览图失败: {ex.Message}", ex);
        }
        
        return result;
    }
}

/// <summary>
/// Cloudflare阻止访问异常
/// </summary>
public class CloudflareBlockedException : Exception
{
    public CloudflareBlockedException(string message) : base(message) { }
}

/// <summary>
/// Heliosphere网站支持
/// </summary>
public class HeliosphereSite : ISupportedModSite
{
    private static readonly Regex ModIdRegex = new Regex(@"heliosphere\.app/mod/([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
    // 匹配 API 图片路径的正则表达式
    private static readonly Regex ApiImageRegex = new Regex(@"/api/web/package/([a-zA-Z0-9]+)/image/(\d+)", RegexOptions.IgnoreCase);

    public bool IsSupported(string url)
    {
        return ModIdRegex.IsMatch(url);
    }

    public async Task<List<string>> GetPreviewImageUrls(string modUrl, HttpClient client)
    {
        var result = new List<string>();
        
        try
        {
            var match = ModIdRegex.Match(modUrl);
            if (!match.Success)
                return result;

            string modId = match.Groups[1].Value;
            
            // 获取模组页面内容
            var response = await client.GetStringAsync(modUrl);
            
            // 先尝试查找 API 图片路径
            var apiImageMatches = ApiImageRegex.Matches(response);
            if (apiImageMatches.Count > 0)
            {
                foreach (Match apiMatch in apiImageMatches)
                {
                    var packageId = apiMatch.Groups[1].Value;
                    var imageId = apiMatch.Groups[2].Value;
                    var apiImageUrl = $"https://heliosphere.app/api/web/package/{packageId}/image/{imageId}";
                    
                    if (!result.Contains(apiImageUrl))
                        result.Add(apiImageUrl);
                }
                
                Penumbra.Log.Debug($"从 Heliosphere 找到 {result.Count} 张预览图");
                return result;
            }
            
            // 作为备用方案，尝试查找已经渲染的图片URL
            var imageRegex = new Regex(@"https://data\.heliosphere\.app/images/[a-zA-Z0-9_\-]+", RegexOptions.IgnoreCase);
            var matches = imageRegex.Matches(response);
            
            foreach (Match imageMatch in matches)
            {
                string imageUrl = imageMatch.Value;
                if (!result.Contains(imageUrl))
                    result.Add(imageUrl);
            }
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"获取Heliosphere预览图失败: {ex.Message}");
        }
        
        return result;
    }
} 
