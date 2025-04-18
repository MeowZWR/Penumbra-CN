using Dalamud.Interface.DragDrop;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using ImGuiNET;
using OtterGui.Raii;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using SixLabors.ImageSharp;
using Dalamud.Plugin.Services;
using System.Text.Json;

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
    private const int FileOperationDelay = 500; // 毫秒
    private const int MaxCacheSize = 10;
    private const int CacheCleanupInterval = 300000;
    private const int MemoryStreamPoolSize = 10;
    private float _maxPreviewWidth = 800f;

    // 添加一个静态实例引用
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

    // 添加静态方法获取当前内存使用量
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
            var pluginPath = _pluginInterface.ConfigDirectory.Parent!.Parent!.FullName;
            if (string.IsNullOrEmpty(pluginPath))
            {
                Penumbra.Log.Warning("无法获取插件目录路径");
                return;
            }

            var configPath = Path.Combine(pluginPath, ConfigFileName);
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<PreviewConfig>(json);
                if (config != null)
                {
                    _config.EnableImageInteraction = config.EnableImageInteraction;
                    _config.ThumbnailSize = config.ThumbnailSize;
                    _config.MaxPreviewWidth = config.MaxPreviewWidth;
                    _config.MaxPreviewHeight = config.MaxPreviewHeight;
                    _config.PreviewPanelImageSpacing = config.PreviewPanelImageSpacing;
                    _config.PreviewPanelLeftMargin = config.PreviewPanelLeftMargin;
                    _config.EnableExternalViewer = config.EnableExternalViewer;
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
            var pluginPath = _pluginInterface.ConfigDirectory.Parent!.Parent!.FullName;
            if (string.IsNullOrEmpty(pluginPath))
            {
                Penumbra.Log.Warning("无法获取插件目录路径");
                return;
            }

            var configPath = Path.Combine(pluginPath, ConfigFileName);
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
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
                var config = JsonSerializer.Deserialize<PinnedImageConfig>(json);
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
            var json = JsonSerializer.Serialize(_pinnedConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
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
                    (now - kvp.Value.LastAccessTime).TotalMinutes > 5)
                .ToList();

            foreach (var item in itemsToRemove)
            {
                _textureCache.Remove(item.Key);
                _lruList.Remove(item.Key);
                item.Value.Texture?.Dispose();
                item.Value.OriginalTexture?.Dispose();
            }
        }
    }

    public void ClearCache()
    {
        lock (_cacheLock)
        {
            foreach (var texture in _textureCache.Values)
            {
                texture.Texture?.Dispose();
                texture.OriginalTexture?.Dispose();
            }
            _textureCache.Clear();
            _lruList.Clear();
            _originalSizes.Clear();
            _scaledSizes.Clear();
        }
    }

    private void UpdateLRU(string path, bool isVisible = false)
    {
        lock (_cacheLock)
        {
            if (_textureCache.TryGetValue(path, out var texture))
            {
                _lruList.Remove(path);
                _lruList.AddFirst(path);
                texture.LastAccessTime = DateTime.Now;
                texture.IsVisible = isVisible;
            }
        }
    }

    private void EnsureCacheSize()
    {
        lock (_cacheLock)
        {
            while (_textureCache.Count > MaxCacheSize || 
                   CurrentMemoryUsage > _configuration.PreviewPanelMaxMemory)
            {
                if (_lruList.Last == null) break;

                var lastPath = _lruList.Last.Value;
                if (_textureCache.TryGetValue(lastPath, out var texture))
                {
                    _textureCache.Remove(lastPath);
                    _lruList.RemoveLast();
                    texture.Texture?.Dispose();
                    texture.OriginalTexture?.Dispose();
                }
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
            _pinnedConfig.CurrentModPath = newModPath;
            LoadPinnedConfig(); // 切换模组时加载新的置顶配置
            Penumbra.Log.Debug($"当前Mod路径: {CurrentModPath}");
            Penumbra.Log.Debug($"CoverImage文件夹路径: {coverFolder}");
        }

        // 创建拖拽源
        _dragDrop.CreateImGuiSource("PreviewImageDrop", m => m.Extensions.Any(e => SupportedExtensions.Contains(e.ToLowerInvariant())), m =>
        {
            ImGui.TextUnformatted($"拖拽图片到预览面板进行导入：\n\t{string.Join("\n\t", m.Files.Select(Path.GetFileName))}");
            return true;
        });
        var y = ImGui.GetCursorPos().Y;
        ImGui.InvisibleButton("##InvisibleButton", new Vector2(panelWidth, ImGui.GetContentRegionAvail().Y- 4f));
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

        var imageFiles = Directory.GetFiles(coverFolder)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        // 如果有置顶图片，将其移到列表最前面
        if (_pinnedConfig.PinnedImagePath != null && imageFiles.Contains(_pinnedConfig.PinnedImagePath))
        {
            imageFiles.Remove(_pinnedConfig.PinnedImagePath);
            imageFiles.Insert(0, _pinnedConfig.PinnedImagePath);
        }

        if (imageFiles.Count == 0)
        {
            ImGui.TextDisabled("没有支持格式的图片。");
            return;
        }

        // 检查并启动图片加载，减少日志输出
        foreach (var path in imageFiles)
        {
            if (!_textureCache.ContainsKey(path) && !_loadingImages.Contains(path))
            {
                _loadingImages.Add(path);
                Task.Run(async () => await LoadImage(path));
            }
        }

        // 创建一个子窗口来包含所有图片，并设置滚动条位置
        using (var child = ImRaii.Child("##PreviewContent", new Vector2(panelWidth, -1), false, ImGuiWindowFlags.NoScrollbar))
        {
            if (child)
            {
                ImGui.SetCursorPosY(0);
                
                var scrollbarWidth = ImGui.GetStyle().ScrollbarSize;
                var leftPadding = 0 * UiHelpers.Scale; // 减小左侧间距
                var availableWidth = panelWidth - leftPadding - scrollbarWidth;
                var spacing = _configuration.PreviewPanelImageSpacing * UiHelpers.Scale;
                var imageMargin = 2 * UiHelpers.Scale; // 图片边距
                
                // 计算每行可以显示的图片数量
                var imagesPerRow = 1;
                if (availableWidth >= _configuration.PreviewPanelMinWidth)
                {
                    var effectiveWidth = availableWidth - spacing * (imagesPerRow - 1) - imageMargin * 2;
                    imagesPerRow = (int)(effectiveWidth / _configuration.PreviewImageMinWidth);
                    if (imagesPerRow < 1) imagesPerRow = 1;
                }

                // 如果图片数量少，调整布局以更好地利用空间
                if (imageFiles.Count <= 3 && imagesPerRow > 1)
                {
                    // 对于少量图片，使用更少的列以获得更大的图片尺寸
                    imagesPerRow = Math.Min(imageFiles.Count, 2);
                }

                // 计算每张图片的基础宽度（考虑间距和边距）
                var baseImageWidth = (availableWidth - spacing * (imagesPerRow - 1) - imageMargin * 2) / imagesPerRow;
                
                // 准备瀑布流布局
                var columnHeights = new float[imagesPerRow];
                var columnWidths = new float[imagesPerRow];
                var columnPositions = new float[imagesPerRow];
                
                // 初始化列的位置和宽度
                for (var i = 0; i < imagesPerRow; i++)
                {
                    columnPositions[i] = leftPadding + imageMargin + i * (baseImageWidth + spacing + imageMargin * 2);
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

                    UpdateLRU(path, true);

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
                    
                    // 平滑过渡到新的缩放尺寸
                    if (cachedTexture.ScaledSize.X > 0 && cachedTexture.ScaledSize.Y > 0)
                    {
                        // 使用插值平滑过渡
                        var lerpFactor = 0.3f; // 调整此值以控制过渡速度
                        cachedTexture.ScaledSize = new Vector2(
                            cachedTexture.ScaledSize.X + (newScaledSize.X - cachedTexture.ScaledSize.X) * lerpFactor,
                            cachedTexture.ScaledSize.Y + (newScaledSize.Y - cachedTexture.ScaledSize.Y) * lerpFactor
                        );
                    }
                    else
                    {
                        cachedTexture.ScaledSize = newScaledSize;
                    }

                    // 设置图片位置（添加边距）
                    var posX = columnPositions[shortestColumn];
                    var posY = 0f;
                    if (columnHeights[shortestColumn] > 0) // 如果不是第一行，添加间距
                        posY = columnHeights[shortestColumn] + spacing;
                    
                    ImGui.SetCursorPos(new Vector2(posX, posY));
                    
                    // 直接绘制图片
                    ImGui.Image(cachedTexture.Texture.ImGuiHandle, cachedTexture.ScaledSize);

                    if (ImGui.IsItemHovered() && _config.EnableImageInteraction)
                    {
                        // 显示提示信息
                        ImGui.BeginTooltip();
                        ImGui.Text("打开图片：Ctrl+左键\n删除图片：Shift+Ctrl+左键\n放大图片：按住右键\n置顶/取消置顶：Shift+右键");
                        ImGui.EndTooltip();

                        // 处理Shift+右键点击置顶
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && ImGui.GetIO().KeyShift)
                        {
                            PinImage(path);
                        }
                        // 处理按住右键放大
                        else if (ImGui.IsMouseDown(ImGuiMouseButton.Right) && !ImGui.GetIO().KeyShift)
                        {
                            var winSize = ImGui.GetIO().DisplaySize;
                            var imgSize = new Vector2(cachedTexture.OriginalTexture?.Width ?? cachedTexture.Texture.Width, 
                                                    cachedTexture.OriginalTexture?.Height ?? cachedTexture.Texture.Height);

                            // 如果图片尺寸大于窗口尺寸，则按比例缩放以适应屏幕
                            var scale = Math.Min(
                                (winSize.X * 0.95f) / imgSize.X,  // 留出一些边距
                                (winSize.Y * 0.95f) / imgSize.Y
                            );

                            if (scale < 1)
                            {
                                imgSize *= scale;
                            }

                            // 计算图片位置，使其居中显示
                            var min = new Vector2(winSize.X / 2 - imgSize.X / 2, winSize.Y / 2 - imgSize.Y / 2);
                            var max = min + imgSize;

                            // 使用原始尺寸的纹理（如果可用）
                            var texture = cachedTexture.OriginalTexture ?? cachedTexture.Texture;
                            
                            // 使用前景绘制列表，确保图片显示在所有窗口之上
                            var foregroundDrawList = ImGui.GetForegroundDrawList();
                            
                            // 先绘制半透明背景
                            foregroundDrawList.AddRectFilled(
                                Vector2.Zero,
                                winSize,
                                ImGui.GetColorU32(new Vector4(0, 0, 0, 0.7f))
                            );
                            
                            // 再绘制图片（不受半透明影响）
                            foregroundDrawList.AddImage(texture.ImGuiHandle, min, max);
                            
                            // 显示图片尺寸信息
                            var sizeText = $"{texture.Width} x {texture.Height}";
                            var textSize = ImGui.CalcTextSize(sizeText);
                            var textPos = min + new Vector2(10, 10);
                            foregroundDrawList.AddText(
                                textPos,
                                ImGui.GetColorU32(new Vector4(1, 1, 1, 1)),
                                sizeText
                            );
                        }

                        // 处理左键点击打开外部工具
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

                        //Shift + Ctrl + 左键删除
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

                    // 更新列的高度（考虑边距）
                    columnHeights[shortestColumn] = posY + newScaledSize.Y + imageMargin * 2;
                }

                // 计算实际内容高度（考虑所有列的最大高度）
                var maxHeight = columnHeights.Max();
                if (maxHeight > 0)
                {
                    // 添加底部边距
                    maxHeight += imageMargin;
                    
                    // 设置内容区域大小，确保没有多余空间
                    var contentHeight = maxHeight;
                    var availableHeight = ImGui.GetContentRegionAvail().Y;
                    
                    // 如果内容高度小于可用高度，则使用内容高度
                    // 如果内容高度大于可用高度，则使用可用高度（允许滚动）
                    var finalHeight = Math.Min(contentHeight, availableHeight);
                    
                    // 设置内容区域大小
                    ImGui.Dummy(new Vector2(0, finalHeight));
                }
            }
        }
    }

    internal async Task<bool> ProcessFileWithLock(string filePath, Func<Task> action, bool isCompress = false)
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
                        Penumbra.Log.Debug($"文件正在被处理中，等待重试: {filePath}");
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
                        Penumbra.Log.Debug($"文件被占用，等待重试 ({retryCount}/{MaxRetryCount}): {filePath}");
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
        if (!await ProcessFileWithLock(imagePath, async () =>
        {
            try
            {
                var (previewTexture, originalTexture, scaledSize, originalSize, memorySize) = 
                    await _imageCompressor.CompressImageAsync(imagePath);

                if (previewTexture == null || originalTexture == null)
                {
                    throw new Exception("创建纹理失败");
                }

                // 更新缓存
                lock (_cacheLock)
                {
                    EnsureCacheSize();
                    _textureCache[imagePath] = new CachedTexture
                    {
                        Texture = previewTexture,
                        OriginalTexture = originalTexture,
                        LastAccessTime = DateTime.Now,
                        LastModifiedTime = File.GetLastWriteTime(imagePath),
                        MemorySize = memorySize,
                        IsVisible = false,
                        ScaledSize = scaledSize,
                        HoveredSize = scaledSize,
                        OriginalSize = originalSize
                    };
                    _originalSizes[imagePath] = originalSize;
                    _lruList.AddFirst(imagePath);
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
            if (!imagePath.Contains("temp_"))
            {
                Penumbra.Log.Debug($"文件正在被处理中，跳过加载: {imagePath}");
            }
        }
    }

    public async Task CompressImages()
    {
        if (CurrentModPath == null)
            return;

        var coverFolder = Path.Combine(CurrentModPath, "CoverImage");
        if (!Directory.Exists(coverFolder))
            return;

        var imageFiles = Directory.GetFiles(coverFolder)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        // 先清除缓存
        ClearCache();

        // 逐个压缩图片
        foreach (var imagePath in imageFiles)
        {
            try
            {
                // 使用ImageCompressor加载图片（它会自动处理压缩）
                await LoadImage(imagePath);
            }
            catch (Exception ex)
            {
                Penumbra.Log.Warning($"压缩图片失败: {imagePath} - {ex.Message}");
            }
        }
    }

    private Vector2 GetScaledSize(Vector2 originalSize, float maxWidth)
    {
        // 计算缩放比例
        var scale = maxWidth / originalSize.X;
        
        // 如果缩放后的宽度小于最小宽度，则使用最小宽度
        if (originalSize.X * scale < _configuration.PreviewImageMinWidth)
        {
            scale = _configuration.PreviewImageMinWidth / originalSize.X;
        }
        
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
