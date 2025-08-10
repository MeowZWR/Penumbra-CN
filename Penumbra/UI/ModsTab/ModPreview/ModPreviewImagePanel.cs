using Dalamud.Interface.DragDrop;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using Dalamud.Bindings.ImGui;
using OtterGui.Raii;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using SixLabors.ImageSharp;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;

namespace Penumbra.UI.ModsTab.ModPreview;

public class ModPreviewImagePanel : IDisposable
{
    private readonly ITextureProvider _textureProvider;
    private readonly ImageCompressor _imageCompressor;
    private readonly Dictionary<string, CachedTexture> _textureCache = new();
    private readonly Dictionary<string, Vector2> _originalSizes = new();
    private readonly Dictionary<string, Vector2> _scaledSizes = new();
    private readonly HashSet<string> _loadingImages = new();
    private readonly LinkedList<string> _lruList = new();
    private readonly object _cacheLock = new();
    private readonly Timer _cacheCleanupTimer;
    private readonly ObjectPool<MemoryStream> _memoryStreamPool;
    private readonly object _fileLock = new();
    private readonly HashSet<string> _processingFiles = new();
    private readonly IDragDropManager _dragDrop;
    private readonly ModManager _modManager;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly Configuration _configuration;
    private readonly INotificationManager _notificationManager;
    private bool _disposed;
    private const int MaxRetryCount = 3;
    private const int FileOperationDelay = 500;
    private const int MaxCacheSize = 30;
    private const int CacheCleanupInterval = 300000;
    private const int MemoryStreamPoolSize = 10;
    private float _maxPreviewWidth = 800f;
    
    // 日志控制变量
    private DateTime _lastLogTime = DateTime.MinValue;
    private const int LogIntervalMs = 1000;
    private int _lastTotalImageCount = 0;
    private int _lastDrawnImageCount = 0;

    private static ModPreviewImagePanel? _instance;

    public float MaxPreviewWidth
    {
        get => _maxPreviewWidth;
        set => _maxPreviewWidth = Math.Max(200f, value);
    }

    public string? CurrentModPath { get; private set; }

    public long CurrentMemoryUsage
    {
        get
        {
            lock (_cacheLock)
            {
                return _textureCache.Values.Sum(t => t.MemorySize);
            }
        }
    }

    public PreviewConfig Config => _config;
    public PinnedImageConfig PinnedConfig => _pinnedConfig;

    private readonly PreviewConfig _config = new();
    private readonly PinnedImageConfig _pinnedConfig = new();
    private readonly List<string> _imagePaths = new();

    private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".webp", ".gif", ".tiff" };
    private static readonly string ConfigFileName = "preview_config.json";
    private static readonly string PinnedConfigFileName = "pinned_images.json";

    public ModPreviewImagePanel(ModManager modManager, IDalamudPluginInterface pluginInterface, ITextureProvider textureProvider, IDragDropManager dragDrop, Configuration config, INotificationManager notificationManager)
    {
        _modManager = modManager;
        _pluginInterface = pluginInterface;
        _textureProvider = textureProvider;
        _imageCompressor = new ImageCompressor(textureProvider);
        _dragDrop = dragDrop;
        _configuration = config;
        _notificationManager = notificationManager;
        _cacheCleanupTimer = new Timer(CleanupCache, null, CacheCleanupInterval, CacheCleanupInterval);
        _memoryStreamPool = new ObjectPool<MemoryStream>(
            () => new MemoryStream(),
            stream => stream.SetLength(0),
            MemoryStreamPoolSize
        );
        _instance = this;

        // 从配置文件中加载悬浮显示设置
        LoadConfig();
        LoadPinnedConfig();
    }

    public static ModPreviewImagePanel? GetInstance() => _instance;

    public static long GetCurrentMemoryUsage()
    {
        if (_instance == null)
            return 0;

        lock (_instance._cacheLock)
        {
            return _instance._textureCache.Values.Sum(t => t.MemorySize);
        }
    }

    private void LoadConfig()
    {
        try
        {
            var configPath = Path.Combine(_pluginInterface.ConfigDirectory.FullName, ConfigFileName);
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<PreviewConfig>(json);
                if (config != null)
                {
                    _config.EnableImageInteraction = config.EnableImageInteraction;
                    _config.ThumbnailSize = config.ThumbnailSize;
                    _config.MaxPreviewWidth = config.MaxPreviewWidth;
                    _config.MaxPreviewHeight = config.MaxPreviewHeight;
                    _config.PreviewPanelLeftMargin = config.PreviewPanelLeftMargin;
                    _config.EnableExternalViewer = config.EnableExternalViewer;
                    _config.Expanded = config.Expanded;
                }
            }
        }
        catch (Exception ex)
        {
            Penumbra.Log.Warning($"加载预览配置失败: {ex.Message}");
        }
    }

    public void SaveConfig()
    {
        try
        {
            var configPath = Path.Combine(_pluginInterface.ConfigDirectory.FullName, ConfigFileName);
            var secureWrite = File.Exists(configPath);
            var tempPath = secureWrite ? configPath + ".tmp" : configPath;
            
            Penumbra.Log.Debug($"正在保存预览配置 {Path.GetFileName(configPath)} {(secureWrite ? "，使用安全写入" : "，首次创建")}...");
            
            var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(tempPath, json);
            
            if (secureWrite)
                File.Move(tempPath, configPath, true);
        }
        catch (Exception ex)
        {
            Penumbra.Log.Warning($"保存预览配置失败: {ex.Message}");
        }
    }

    private void LoadPinnedConfig()
    {
        try
        {
            if (string.IsNullOrEmpty(CurrentModPath))
                return;

            var coverFolder = Path.Combine(CurrentModPath, "CoverImage");
            if (!Directory.Exists(coverFolder))
                return;

            var configPath = Path.Combine(coverFolder, PinnedConfigFileName);
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<PinnedImageConfig>(json);
                if (config != null)
                {
                    _pinnedConfig.PinnedImagePath = config.PinnedImagePath;
                }
            }
        }
        catch (Exception ex)
        {
            Penumbra.Log.Warning($"加载置顶图片配置失败: {ex.Message}");
        }
    }

    private void SavePinnedConfig()
    {
        try
        {
            if (string.IsNullOrEmpty(CurrentModPath))
                return;

            var coverFolder = Path.Combine(CurrentModPath, "CoverImage");
            if (!Directory.Exists(coverFolder))
                return;

            var configPath = Path.Combine(coverFolder, PinnedConfigFileName);
            var secureWrite = File.Exists(configPath);
            var tempPath = secureWrite ? configPath + ".tmp" : configPath;
            
            Penumbra.Log.Debug($"正在保存置顶图片配置 {Path.GetFileName(configPath)} {(secureWrite ? "，使用安全写入" : "，首次创建")}...");
            
            var json = JsonConvert.SerializeObject(_pinnedConfig, Formatting.Indented);
            File.WriteAllText(tempPath, json);
            
            if (secureWrite)
                File.Move(tempPath, configPath, true);
        }
        catch (Exception ex)
        {
            Penumbra.Log.Warning($"保存置顶图片配置失败: {ex.Message}");
        }
    }

    private void PinImage(string imagePath)
    {
        if (_pinnedConfig.PinnedImagePath == imagePath)
        {
            _pinnedConfig.PinnedImagePath = null;
        }
        else
        {
            _pinnedConfig.PinnedImagePath = imagePath;
        }
        SavePinnedConfig();
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _cacheCleanupTimer.Dispose();
        _memoryStreamPool.Dispose();
        _imageCompressor.Dispose();
        ClearCache();
    }

    private void CleanupCache(object? state)
    {
        if (_disposed) return;

        lock (_cacheLock)
        {
            var now = DateTime.Now;
            var itemsToRemove = _textureCache
                .Where(kvp => 
                    !kvp.Value.IsVisible && 
                    (now - kvp.Value.LastAccessTime).TotalMinutes > 5 &&
                    !_loadingImages.Contains(kvp.Key))
                .ToList();

            if (itemsToRemove.Count > 0)
            {
#if DEBUG
                Penumbra.Log.Debug($"[缓存清理] 定时清理 {itemsToRemove.Count} 张图片，当前缓存总数: {_textureCache.Count}，内存使用: {CurrentMemoryUsage / 1024 / 1024}MB");
                foreach (var item in itemsToRemove)
                {
                    Penumbra.Log.Debug($"[缓存清理] 清理图片: {Path.GetFileName(item.Key)}，上次访问: {(now - item.Value.LastAccessTime).TotalMinutes:F1}分钟前，是否可见: {item.Value.IsVisible}");
                }
#endif
                foreach (var item in itemsToRemove)
                {
                    _textureCache.Remove(item.Key);
                    _lruList.Remove(item.Key);
                    item.Value.Texture?.Dispose();
                    item.Value.OriginalTexture?.Dispose();
                }
            }
        }
    }

    public void ClearCache()
    {
        lock (_cacheLock)
        {
#if DEBUG
            Penumbra.Log.Debug($"[缓存管理] 执行完全清理，当前缓存: {_textureCache.Count}张，内存使用: {CurrentMemoryUsage / 1024 / 1024}MB");
#endif
            foreach (var texture in _textureCache.Values)
            {
                texture.Texture?.Dispose();
                texture.OriginalTexture?.Dispose();
            }
            _textureCache.Clear();
            _lruList.Clear();
            _originalSizes.Clear();
            _scaledSizes.Clear();
            
#if DEBUG
            Penumbra.Log.Debug("[缓存管理] 完全清理完成");
#endif
        }
    }

    private void UpdateLRU(string path, bool isVisible = false)
    {
        lock (_cacheLock)
        {
            if (_textureCache.TryGetValue(path, out var texture))
            {
                bool visibilityChanged = texture.IsVisible != isVisible;
                _lruList.Remove(path);
                _lruList.AddFirst(path);
                texture.LastAccessTime = DateTime.Now;
                
                // 更新可见状态 - 确保不会保留过多可见图片
                if (isVisible)
                {
                    // 统计当前可见图片数量，如果过多，将此图片标记为不可见
                    int visibleCount = _textureCache.Values.Count(t => t.IsVisible);
                    if (visibleCount > MaxCacheSize && !texture.IsVisible)
                    {
#if DEBUG
                        if (path.Contains("screenshot"))
                        {
                            Penumbra.Log.Debug($"[LRU更新] 图片:{Path.GetFileName(path)} 不标记为可见，当前可见图片数过多: {visibleCount}/{MaxCacheSize}");
                        }
#endif
                        return;
                    }
                }
                
                texture.IsVisible = isVisible;
                
#if DEBUG
                if (visibilityChanged && path.Contains("screenshot"))
                {
                    Penumbra.Log.Debug($"[LRU更新] 图片:{Path.GetFileName(path)} 可见状态变更: {isVisible}");
                }
#endif
            }
        }
    }

    private void EnsureCacheSize()
    {
        lock (_cacheLock)
        {
            bool isOverCount = _textureCache.Count > MaxCacheSize * 1.5;
            bool isOverMemory = CurrentMemoryUsage > _configuration.PreviewPanelMaxMemory * 0.9;
            
            if (isOverCount || isOverMemory)
            {
#if DEBUG
                Penumbra.Log.Debug($"[缓存管理] 准备清理缓存，当前数量: {_textureCache.Count}，最大允许: {MaxCacheSize}，当前内存: {CurrentMemoryUsage / 1024 / 1024}MB，最大允许: {_configuration.PreviewPanelMaxMemory / 1024 / 1024}MB");
#endif
            }
            else
            {
                return;
            }

            // 首先尝试清理非可见图片且不是新加载的图片
            var nonVisibleCandidates = _lruList
                .Where(path => 
                    _textureCache.TryGetValue(path, out var tex) && 
                    !tex.IsVisible && 
                    !tex.IsNew &&
                    !_loadingImages.Contains(path))
                .ToList();

            // 循环直到大小符合要求或没有可清理的非可见图片
            while ((isOverCount || isOverMemory) && nonVisibleCandidates.Count > 0)
            {
                var lastPath = nonVisibleCandidates.Last();
                nonVisibleCandidates.RemoveAt(nonVisibleCandidates.Count - 1);

                if (_textureCache.TryGetValue(lastPath, out var texture))
                {
#if DEBUG
                    Penumbra.Log.Debug($"[缓存管理] 清理非可见图片: {Path.GetFileName(lastPath)}，上次访问: {(DateTime.Now - texture.LastAccessTime).TotalSeconds:F1}秒前，内存占用: {texture.MemorySize / 1024}KB");
#endif
                    _textureCache.Remove(lastPath);
                    _lruList.Remove(lastPath);
                    texture.Texture?.Dispose();
                    texture.OriginalTexture?.Dispose();
                }

                isOverCount = _textureCache.Count > MaxCacheSize * 1.5;
                isOverMemory = CurrentMemoryUsage > _configuration.PreviewPanelMaxMemory * 0.9;
            }

            if (isOverMemory && CurrentMemoryUsage > _configuration.PreviewPanelMaxMemory * 0.95)
            {
#if DEBUG
                Penumbra.Log.Warning($"[缓存管理] 非可见图片已全部清理，但内存仍严重超出限制。开始清理可见图片。当前: {_textureCache.Count}张，{CurrentMemoryUsage / 1024 / 1024}MB");
#endif
                
                // 对内存超限，采取更激进的清理策略
                var visibleCandidates = _lruList
                    .Where(path => 
                        _textureCache.TryGetValue(path, out var tex) && 
                        tex.IsVisible && 
                        !_loadingImages.Contains(path))
                    .ToList();
                
                // 清理最不常用的可见图片，直到内存使用符合要求或没有更多图片可清理
                while (isOverMemory && visibleCandidates.Count > 0)
                {
                    var lastPath = visibleCandidates.Last();
                    visibleCandidates.RemoveAt(visibleCandidates.Count - 1);

                    if (_textureCache.TryGetValue(lastPath, out var texture))
                    {
#if DEBUG
                        Penumbra.Log.Warning($"[缓存管理] 强制清理可见图片: {Path.GetFileName(lastPath)}，上次访问: {(DateTime.Now - texture.LastAccessTime).TotalSeconds:F1}秒前，内存占用: {texture.MemorySize / 1024}KB");
#endif
                        _textureCache.Remove(lastPath);
                        _lruList.Remove(lastPath);
                        texture.Texture?.Dispose();
                        texture.OriginalTexture?.Dispose();
                    }

                    isOverMemory = CurrentMemoryUsage > _configuration.PreviewPanelMaxMemory * 0.95;
                }
            }

            if (isOverMemory && CurrentMemoryUsage > _configuration.PreviewPanelMaxMemory * 0.95)
            {
                Penumbra.Log.Error($"[缓存管理] 严重警告：清理后仍超出限制。当前: {_textureCache.Count}张，{CurrentMemoryUsage / 1024 / 1024}MB，加载中: {_loadingImages.Count}张");
            }
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

    public void Draw(Mod mod, float panelWidth)
    {
        var newModPath = mod.ModPath.FullName;
        var coverFolder = Path.Combine(newModPath, "CoverImage");
        
        if (CurrentModPath != newModPath)
        {
            CurrentModPath = newModPath;
            LoadPinnedConfig(); // 切换模组时加载新的置顶配置
#if DEBUG
            Penumbra.Log.Debug($"当前Mod路径: {CurrentModPath}");
            Penumbra.Log.Debug($"CoverImage文件夹路径: {coverFolder}");
#endif
        }

        // 创建拖拽源
        _dragDrop.CreateImGuiSource("PreviewImageDrop", m => m.Extensions.Any(e => SupportedExtensions.Contains(e.ToLowerInvariant())), m =>
        {
            ImGui.TextUnformatted($"拖拽图片到预览面板进行导入：\n\t{string.Join("\n\t", m.Files.Select(Path.GetFileName))}");
            return true;
        });
        var y = ImGui.GetCursorPos().Y;
        
        using (var _ = ImRaii.Child("##DragDropTarget", new Vector2(panelWidth, ImGui.GetContentRegionAvail().Y - 4f), false, ImGuiWindowFlags.NoMouseInputs)){}

        ImGui.SetCursorPos(new Vector2(ImGui.GetCursorPos().X, y));

        // 设置拖拽目标
        if (_dragDrop.CreateImGuiTarget("PreviewImageDrop", out var files, out _))
        {
            // 临时禁用图片交互
            var originalInteractionState = _config.EnableImageInteraction;
            _config.EnableImageInteraction = false;
            
            // 处理拖放的文件
            if (files != null && files.Count > 0)
            {
                Task.Run(async () =>
                {
                    int successCount = 0;
                    int failCount = 0;
                    foreach (var file in files)
                    {
                        if (!SupportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                            continue;

                        try
                        {
                            var fileName = Path.GetFileName(file);
                            var targetPath = Path.Combine(coverFolder, fileName);
                            
                            // 确保CoverImage文件夹存在
                            if (!Directory.Exists(coverFolder))
                            {
                                Directory.CreateDirectory(coverFolder);
                            }

                            File.Copy(file, targetPath, true);
                            await LoadImage(targetPath);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            Penumbra.Log.Warning($"导入图片失败: {file} - {ex.Message}");
                            failCount++;
                        }
                    }

                    if (successCount > 0)
                    {
                        ShowNotification($"成功导入 {successCount} 张图片", NotificationType.Success);
                    }
                    if (failCount > 0)
                    {
                        ShowNotification($"导入失败 {failCount} 张图片", NotificationType.Error);
                    }
                });
            }
            
            // 恢复原始状态
            _config.EnableImageInteraction = originalInteractionState;
        }

        if (!Directory.Exists(coverFolder))
        {
            ImGui.TextDisabled("未找到 CoverImage 文件夹。");
            return;
        }

        // 获取所有图片文件
        var allImageFiles = Directory.GetFiles(coverFolder)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        // 根据创建时间排序（最新的排在前面）
        var sortedByDate = allImageFiles
            .Select(path => new { Path = path, CreationTime = File.GetCreationTime(path) })
            .OrderByDescending(item => item.CreationTime)
            .ToList();

        // 准备实际要显示的图片列表（最多10张）
        const int maxImagesPerMod = 10;
        var imageFilesToDisplay = new List<string>(maxImagesPerMod);
        int totalImageCount = allImageFiles.Count;
        
        // 首先添加置顶图片（如果有）
        if (_pinnedConfig.PinnedImagePath != null && allImageFiles.Contains(_pinnedConfig.PinnedImagePath))
        {
            imageFilesToDisplay.Add(_pinnedConfig.PinnedImagePath);
        }
        
        // 然后添加最新的图片，直到达到上限
        foreach (var item in sortedByDate)
        {
            // 跳过已经添加的置顶图片
            if (imageFilesToDisplay.Contains(item.Path))
                continue;
                
            imageFilesToDisplay.Add(item.Path);
            
            // 达到10张上限后停止
            if (imageFilesToDisplay.Count >= maxImagesPerMod)
                break;
        }

        if (imageFilesToDisplay.Count == 0)
        {
            ImGui.TextDisabled("没有支持格式的图片。");
            return;
        }

        // 使用最终选择的图片列表
        var imageFiles = imageFilesToDisplay;

        // 减少日志输出频率: 只在数量变化或者超过日志间隔时才记录
        var now = DateTime.Now;
        var shouldLog = (totalImageCount != _lastTotalImageCount || 
                         (now - _lastLogTime).TotalMilliseconds > LogIntervalMs);
        
        if (shouldLog)
        {
#if DEBUG
            Penumbra.Log.Debug($"[预览面板] 开始加载，筛选后图片数: {imageFiles.Count}/{totalImageCount}，当前缓存: {_textureCache.Count}，加载队列: {_loadingImages.Count}，内存使用: {CurrentMemoryUsage / 1024 / 1024}MB");
#endif
            _lastTotalImageCount = totalImageCount;
            _lastLogTime = now;
        }

        // 确保缓存不超过限制
        EnsureCacheSize();

        // 计算允许的平均每张图片内存
        long averageMemoryPerImage = Math.Max(
            _configuration.PreviewPanelMaxMemory / Math.Max(imageFiles.Count, 1), 
            1024 * 1024 * 2); // 最小2MB每张图

        // 记录未缓存图片数量
        int uncachedCount = 0;
        
        // 线程安全操作：创建加载图片的列表副本
        var loadingImagesSnapshot = new HashSet<string>(_loadingImages);
        
        foreach (var path in imageFiles)
        {
            if (!_textureCache.ContainsKey(path) && !loadingImagesSnapshot.Contains(path))
            {
                uncachedCount++;
                
                lock (_cacheLock)
                {
                    if (!_loadingImages.Contains(path))
                    {
                        _loadingImages.Add(path);
                        Task.Run(async () => await LoadImage(path));
                    }
                }
            }
        }
        
        if (uncachedCount > 0 && shouldLog)
        {
#if DEBUG
            Penumbra.Log.Debug($"[预览面板] 发起加载 {uncachedCount} 张新图片，每张图允许平均内存: {averageMemoryPerImage / 1024}KB");
#endif
        }

        // 计算当前滑动窗口中实际可见的图片数量（现在我们每次都重置所有图片的可见状态）
        int actualVisibleCount = 0;
        
        // 创建一个子窗口来包含所有图片，并设置滚动条位置
        using (var child = ImRaii.Child("##PreviewContent", new Vector2(panelWidth, -1), false, ImGuiWindowFlags.NoScrollbar))
        {
            if (child)
            {
                ImGui.SetCursorPosY(0);
                
                // 重置所有图片的可见状态为false
                lock (_cacheLock)
                {
                    foreach (var texture in _textureCache.Values)
                    {
                        texture.IsVisible = false;
                    }
                }
                
                var scrollbarWidth = ImGui.GetStyle().ScrollbarSize;
                var leftPadding = 0 * UiHelpers.Scale; // 减小左侧间距
                var availableWidth = panelWidth - leftPadding - scrollbarWidth;
                var spacing = _configuration.PreviewPanelImageSpacing * UiHelpers.Scale;
                
                // 计算每行可以显示的图片数量，确保至少为1
                var imagesPerRow = 1;
                if (availableWidth >= _configuration.PreviewPanelMinWidth)
                {
                    var effectiveWidth = availableWidth - spacing * (imagesPerRow - 1);
                    imagesPerRow = Math.Max(1, (int)(effectiveWidth / _configuration.PreviewImageMinWidth));
                }

                // 如果图片数量少，调整布局以更好地利用空间
                if (imageFiles.Count <= 3 && imagesPerRow > 1)
                {
                    // 对于少量图片，使用更少的列以获得更大的图片尺寸
                    imagesPerRow = Math.Min(imageFiles.Count, 2);
                }

                imagesPerRow = Math.Max(1, imagesPerRow);

                // 计算每张图片的基础宽度（只考虑间距）
                var baseImageWidth = (availableWidth - spacing * (imagesPerRow - 1)) / imagesPerRow;
                
                // 准备瀑布流布局
                var columnHeights = new float[imagesPerRow];
                var columnWidths = new float[imagesPerRow];
                var columnPositions = new float[imagesPerRow];
                
                // 初始化列的位置和宽度
                for (var i = 0; i < imagesPerRow; i++)
                {
                    columnPositions[i] = leftPadding + i * (baseImageWidth + spacing);
                    columnWidths[i] = baseImageWidth;
                }

                // 瀑布流布局算法
                foreach (var path in imageFiles)
                {
                    if (!_textureCache.TryGetValue(path, out var cachedTexture))
                    {
                        if (_loadingImages.Contains(path))
                        {
                            ImGui.TextDisabled("加载中...");
                            ImGui.NewLine();
                            continue;
                        }
                        continue;
                    }

                    if (cachedTexture.Texture == null)
                        continue;

                    // 找到当前最短的列
                    var shortestColumn = 0;
                    var minHeight = columnHeights[0];
                    for (var i = 1; i < imagesPerRow; i++)
                    {
                        if (columnHeights[i] < minHeight)
                        {
                            minHeight = columnHeights[i];
                            shortestColumn = i;
                        }
                    }

                    // 计算图片的缩放尺寸
                    var newScaledSize = GetScaledSize(_originalSizes[path], columnWidths[shortestColumn]);
                    
                    // 检查图片是否需要更高分辨率
                    var displaySize = new Vector2(columnWidths[shortestColumn], newScaledSize.Y);
                    var neededResolution = _imageCompressor.DetermineRequiredResolution(displaySize, _originalSizes[path]);
                    
                    if (neededResolution > cachedTexture.Resolution && 
                        !_loadingImages.Contains(path) && 
                        CurrentMemoryUsage < _configuration.PreviewPanelMaxMemory * 0.9)
                    {
                        Task.Run(async () => 
                        {
                            try
                            {
#if DEBUG
                                Penumbra.Log.Debug($"[分辨率提升] 为图片 {Path.GetFileName(path)} 加载更高分辨率: {cachedTexture.Resolution} -> {neededResolution}");
                                _loadingImages.Add(path);
#endif
                                
                                var (newTexture, newOriginalTexture, newScaledSize, originalSize, newMemorySize) = 
                                    await _imageCompressor.CompressImageAsync(path, neededResolution);
                                
                                if (newTexture != null && newOriginalTexture != null)
                                {
                                    lock (_cacheLock)
                                    {
                                        var oldTexture = cachedTexture.Texture;
                                        var oldOriginalTexture = cachedTexture.OriginalTexture;
                                        
                                        cachedTexture.Texture = newTexture;
                                        cachedTexture.OriginalTexture = newOriginalTexture;
                                        cachedTexture.MemorySize = newMemorySize;
                                        cachedTexture.ScaledSize = newScaledSize;
                                        cachedTexture.Resolution = neededResolution;
                                        
                                        if (oldTexture != null && oldTexture != oldOriginalTexture)
                                            oldTexture.Dispose();
                                        if (oldOriginalTexture != null && oldOriginalTexture != newOriginalTexture)
                                            oldOriginalTexture.Dispose();
#if DEBUG
                                        Penumbra.Log.Debug($"[分辨率提升] 图片 {Path.GetFileName(path)} 已提升到 {neededResolution}，内存: {newMemorySize/1024}KB");
#endif
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Penumbra.Log.Warning($"[分辨率提升] 提升图片分辨率失败: {Path.GetFileName(path)} - {ex.Message}");
                            }
                            finally
                            {
                                _loadingImages.Remove(path);
                            }
                        });
                    }
                    
                    cachedTexture.ScaledSize = newScaledSize;

                    var posX = columnPositions[shortestColumn];
                    var posY = 0f;
                    if (columnHeights[shortestColumn] > 0)
                        posY = columnHeights[shortestColumn] + spacing;
                    
                    ImGui.SetCursorPos(new Vector2(posX, posY));
                    ImGui.Image(cachedTexture.Texture.Handle, cachedTexture.ScaledSize);

                    UpdateLRU(path, true);
                    actualVisibleCount++;

#if DEBUG
                    if (imageFiles.IndexOf(path) == 0 && shouldLog)
                    {
                        Penumbra.Log.Debug($"[可见状态] 图片:{Path.GetFileName(path)} 可见状态已更新，总可见图片数: {_textureCache.Values.Count(t => t.IsVisible)}，当前实际绘制: {actualVisibleCount}");
                    }
#endif

                    if (ImGui.IsItemHovered() && _config.EnableImageInteraction)
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text("图片操作说明：");
                        ImGui.BulletText("放大图片：按住右键");
                        ImGui.BulletText("删除图片：Shift + Ctrl + 左键");
                        ImGui.BulletText("置顶/取消置顶：Shift + 右键");
                        ImGui.BulletText("使用外部工具打开：Ctrl + 左键");

                        ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "注意："); 
                        ImGui.SameLine(0, 0);
                        ImGui.TextWrapped("点击后若未及时松开Ctrl键，外部工具会在后台打开");
                        ImGui.EndTooltip();

                        // Shift + 右键点击置顶
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && ImGui.GetIO().KeyShift)
                        {
                            PinImage(path);
                        }
                        else if (ImGui.IsMouseDown(ImGuiMouseButton.Right) && !ImGui.GetIO().KeyShift)
                        {
                            var winSize = ImGui.GetIO().DisplaySize;
                            
                            if (cachedTexture.Resolution < ImageCompressor.ResolutionType.Original && 
                                !_loadingImages.Contains(path))
                            {
                                Task.Run(async () => 
                                {
                                    try
                                    {
                                        _loadingImages.Add(path);
#if DEBUG
                                        Penumbra.Log.Debug($"[全屏预览] 为图片 {Path.GetFileName(path)} 提升分辨率至原始分辨率");
#endif
                                        var (newTexture, newOriginalTexture, newScaledSize, originalSize, newMemorySize) = 
                                            await _imageCompressor.CompressImageAsync(path, ImageCompressor.ResolutionType.Original);
                                        
                                        if (newTexture != null && newOriginalTexture != null)
                                        {
                                            lock (_cacheLock)
                                            {
                                                var oldTexture = cachedTexture.Texture;
                                                var oldOriginalTexture = cachedTexture.OriginalTexture;
                                                
                                                cachedTexture.Texture = newTexture;
                                                cachedTexture.OriginalTexture = newOriginalTexture;
                                                cachedTexture.MemorySize = newMemorySize;
                                                cachedTexture.Resolution = ImageCompressor.ResolutionType.Original;
                                                
                                                if (oldTexture != null && oldTexture != oldOriginalTexture)
                                                    oldTexture.Dispose();
                                                if (oldOriginalTexture != null && oldOriginalTexture != newOriginalTexture)
                                                    oldOriginalTexture.Dispose();
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Penumbra.Log.Warning($"[全屏预览] 提升图片分辨率失败: {Path.GetFileName(path)} - {ex.Message}");
                                    }
                                    finally
                                    {
                                        _loadingImages.Remove(path);
                                    }
                                });
                            }
                            
                            var imgSize = new Vector2(cachedTexture.OriginalTexture?.Width ?? cachedTexture.Texture.Width, 
                                                    cachedTexture.OriginalTexture?.Height ?? cachedTexture.Texture.Height);

                            var scale = Math.Min(
                                (winSize.X * 0.95f) / imgSize.X,
                                (winSize.Y * 0.95f) / imgSize.Y
                            );

                            if (scale < 1)
                            {
                                imgSize *= scale;
                            }

                            var min = new Vector2(winSize.X / 2 - imgSize.X / 2, winSize.Y / 2 - imgSize.Y / 2);
                            var max = min + imgSize;

                            var texture = cachedTexture.OriginalTexture ?? cachedTexture.Texture;
                            var foregroundDrawList = ImGui.GetForegroundDrawList();
                            
                            foregroundDrawList.AddRectFilled(
                                Vector2.Zero,
                                winSize,
                                ImGui.GetColorU32(new Vector4(0, 0, 0, 0.7f))
                            );
                            
                            foregroundDrawList.AddImage(texture.Handle, min, max);
                            
                            var sizeText = $"{texture.Width} x {texture.Height} - {cachedTexture.Resolution}";
                            if (cachedTexture.Resolution != ImageCompressor.ResolutionType.Original)
                                sizeText += " (加载中...)";
                                
                            var textSize = ImGui.CalcTextSize(sizeText);
                            var textPos = min + new Vector2(10, 10);
                            foregroundDrawList.AddText(
                                textPos,
                                ImGui.GetColorU32(new Vector4(1, 1, 1, 1)),
                                sizeText
                            );
                        }

                        // Ctrl + 左键点击打开外部工具
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyShift)
                        {
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = path,
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                Penumbra.Log.Warning($"无法使用外部工具打开图片: {ex.Message}");
                            }
                        }

                        // Shift + Ctrl + 左键删除
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.GetIO().KeyCtrl && ImGui.GetIO().KeyShift)
                        {
                            try
                            {
                                File.Delete(path);
                            }
                            catch (Exception ex)
                            {
                                Penumbra.Log.Warning($"无法删除图片: {ex.Message}");
                            }
                        }
                    }

                    columnHeights[shortestColumn] = posY + newScaledSize.Y;
                }

                if (totalImageCount > maxImagesPerMod)
                {
                    var shortestColumn = 0;
                    var minHeight = columnHeights[0];
                    for (var i = 1; i < imagesPerRow; i++)
                    {
                        if (columnHeights[i] < minHeight)
                        {
                            minHeight = columnHeights[i];
                            shortestColumn = i;
                        }
                    }
                    
                    var posX = columnPositions[shortestColumn];
                    var posY = 0f;
                    if (columnHeights[shortestColumn] > 0)
                        posY = columnHeights[shortestColumn] + spacing;
                    
                    ImGui.SetCursorPos(new Vector2(posX, posY));
                    
                    var hiddenCount = totalImageCount - imageFilesToDisplay.Count;
                    var infoText = $"只支持 10 张预览图\n还有 {hiddenCount} 张图片未显示...";
                    var textSize = ImGui.CalcTextSize(infoText);
                    
                    var padding = 10 * UiHelpers.Scale;
                    var rectMin = ImGui.GetCursorScreenPos();
                    var rectMax = new Vector2(
                        rectMin.X + columnWidths[shortestColumn],
                        rectMin.Y + textSize.Y + padding * 2
                    );
                    
                    ImGui.GetWindowDrawList().AddRectFilled(
                        rectMin,
                        rectMax,
                        ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, 0.7f)),
                        4.0f
                    );
                    
                    ImGui.SetCursorPos(new Vector2(
                        posX + (columnWidths[shortestColumn] - textSize.X) / 2,
                        posY + padding
                    ));
                    ImGui.TextColored(new Vector4(1, 1, 1, 0.9f), infoText);
                    
                    columnHeights[shortestColumn] = posY + textSize.Y + padding * 2;
                }

                var maxHeight = columnHeights.Max();
                if (maxHeight > 0)
                {
                    var contentHeight = maxHeight;
                    var availableHeight = ImGui.GetContentRegionAvail().Y;
                    var finalHeight = Math.Min(contentHeight, availableHeight);
                    ImGui.Dummy(new Vector2(0, finalHeight));
                }
            }
        }
        
        if (shouldLog || actualVisibleCount != _lastDrawnImageCount)
        {
#if DEBUG
            Penumbra.Log.Debug($"[预览面板] 绘制完成，实际绘制图片数: {actualVisibleCount}/{totalImageCount}，缓存中被标记为可见的图片: {_textureCache.Values.Count(t => t.IsVisible)}");
#endif
            _lastDrawnImageCount = actualVisibleCount;
        }
    }

    public async Task<bool> ProcessFileWithLock(string filePath, Func<Task> action, bool isCompress = false)
    {
        var retryCount = 0;
        while (retryCount < MaxRetryCount)
        {
            try
            {
                lock (_fileLock)
                {
                    if (_processingFiles.Contains(filePath))
                    {
#if DEBUG
                        // 避免对临时文件产生太多日志
                        if (!filePath.Contains("temp_"))
                        {
                            Penumbra.Log.Debug($"文件正在被处理中，等待重试: {filePath}");
                        }
#endif
                        return false;
                    }
                    _processingFiles.Add(filePath);
                }

                try
                {
                    await action();
                    return true;
                }
                finally
                {
                    lock (_fileLock)
                    {
                        _processingFiles.Remove(filePath);
                    }
                }
            }
            catch (IOException ex) when (ex.Message.Contains("being used by another process"))
            {
                retryCount++;
                if (retryCount < MaxRetryCount)
                {
                    if (retryCount == MaxRetryCount - 1)
                    {
#if DEBUG
                        Penumbra.Log.Debug($"文件被占用，等待重试 ({retryCount}/{MaxRetryCount}): {filePath}");
#endif
                    }
                    await Task.Delay(FileOperationDelay * retryCount);
                    continue;
                }
                Penumbra.Log.Warning($"文件处理失败，已达到最大重试次数: {filePath}");
                return false;
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("being used by another process"))
                {
                    Penumbra.Log.Warning($"文件处理失败: {filePath} - {ex.Message}");
                }
                return false;
            }
        }
        return false;
    }

    internal async Task LoadImage(string imagePath)
    {
#if DEBUG
        Penumbra.Log.Debug($"[图片加载] 开始加载图片: {Path.GetFileName(imagePath)}");
#endif
        
        if (!await ProcessFileWithLock(imagePath, async () =>
        {
            try
            {
                var resolutionType = ImageCompressor.ResolutionType.Thumbnail;
                
                var (previewTexture, originalTexture, scaledSize, originalSize, memorySize) = 
                    await _imageCompressor.CompressImageAsync(imagePath, resolutionType);

                if (previewTexture == null || originalTexture == null)
                {
                    throw new Exception("创建纹理失败");
                }

                CachedTexture cachedTexture;
                lock (_cacheLock)
                {
                    EnsureCacheSize();
                    
                    cachedTexture = new CachedTexture
                    {
                        Texture = previewTexture,
                        OriginalTexture = originalTexture,
                        LastAccessTime = DateTime.Now,
                        LastModifiedTime = File.GetLastWriteTime(imagePath),
                        MemorySize = memorySize,
                        IsVisible = false,
                        ScaledSize = scaledSize,
                        HoveredSize = scaledSize,
                        OriginalSize = originalSize,
                        Resolution = resolutionType,
                        IsNew = true
                    };
                    
                    _textureCache[imagePath] = cachedTexture;
                    _originalSizes[imagePath] = originalSize;
                    _lruList.AddFirst(imagePath);
                    
#if DEBUG
                    Penumbra.Log.Debug($"[图片加载] 成功加载图片: {Path.GetFileName(imagePath)}，大小: {originalSize.X}x{originalSize.Y}，内存占用: {memorySize / 1024}KB，分辨率级别: {resolutionType}");
#endif
                }
                
                await Task.Delay(2000);
                
                lock (_cacheLock)
                {
                    if (_textureCache.TryGetValue(imagePath, out var existingTexture))
                    {
                        existingTexture.IsNew = false;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("being used by another process"))
                {
                    Penumbra.Log.Warning($"加载预览图失败: {imagePath} - {ex.Message}");
                }
                lock (_cacheLock)
                {
                    if (_textureCache.TryGetValue(imagePath, out var cachedTexture))
                    {
                        cachedTexture.Texture?.Dispose();
                        cachedTexture.OriginalTexture?.Dispose();
                        _textureCache.Remove(imagePath);
                    }
                }
            }
            finally
            {
                _loadingImages.Remove(imagePath);
            }
        }))
        {
#if DEBUG
            if (!imagePath.Contains("temp_"))
            {
                Penumbra.Log.Debug($"文件正在被处理中，跳过加载: {imagePath}");
            }
#endif
        }
    }

    private Vector2 GetScaledSize(Vector2 originalSize, float maxWidth)
    {
        // 计算缩放比例，保持宽高比
        float scale = Math.Min(1.0f, maxWidth / originalSize.X);
        return new Vector2(originalSize.X * scale, originalSize.Y * scale);
    }

    public void OpenCoverImageFolder()
    {
        if (CurrentModPath == null)
            return;

        var coverFolder = Path.Combine(CurrentModPath, "CoverImage");
        var shouldCreate = !Directory.Exists(coverFolder) && ImGui.GetIO().KeyCtrl;

        if (shouldCreate)
        {
            try
            {
                Directory.CreateDirectory(coverFolder);
                ShowNotification("已创建 CoverImage 文件夹", NotificationType.Success);
            }
            catch (Exception ex)
            {
                Penumbra.Log.Warning($"创建 CoverImage 文件夹失败: {ex.Message}");
                ShowNotification($"创建 CoverImage 文件夹失败: {ex.Message}", NotificationType.Error);
                return;
            }
        }

        if (Directory.Exists(coverFolder))
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", coverFolder);
            }
            catch (Exception ex)
            {
                Penumbra.Log.Warning($"无法打开文件夹: {ex.Message}");
                ShowNotification($"无法打开文件夹: {ex.Message}", NotificationType.Error);
            }
        }
    }

    public void ReloadImages()
    {
        ClearCache();
    }     
} 
