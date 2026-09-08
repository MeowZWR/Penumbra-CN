using ImSharp;
using Lumina.Data.Files;
using Lumina.Extensions;
using Luna;
using OtterTex;
using Penumbra.Api.Enums;
using Penumbra.Import.Textures;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;
using Penumbra.UI.ManagementTab;

namespace Penumbra.UI.AdvancedWindow;

/// <summary> Advanced editor tab: scan and batch-restrict texture dimensions for the current mod. </summary>
public sealed class ModEditTextureOptimizationTab(ModEditor editor, TextureOptimization optimization)
{
    private readonly List<Candidate> _candidates = [];
    private readonly HashSet<string> _selected   = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string>    _log        = [];
    private readonly Lock            _logLock    = new();

    private string                   _pathFilter     = string.Empty;
    private int                      _dimensionLimit = 2048;
    private bool                     _createBackups  = false;
    private Task?                    _scanTask;
    private CancellationTokenSource? _scanCts;
    private Task?                    _processTask;
    private CancellationTokenSource? _processCts;
    private int                      _processDone;
    private int                      _processTotal;
    private volatile bool            _busy;

    private sealed record Candidate(string FullPath, string RelPath, int Width, int Height, DXGIFormat Format, long Size);

    public void Reset()
    {
        CancelScan();
        CancelProcess();
        _candidates.Clear();
        _selected.Clear();
        lock (_logLock)
            _log.Clear();
        _processDone  = 0;
        _processTotal = 0;
        _busy         = false;
    }

    public void Draw()
    {
        using var tab = Im.TabBar.BeginItem("纹理优化"u8);
        if (!tab)
            return;

        DrawToolbar();
        LunaStyle.DrawSeparator();
        DrawCandidateList();
        LunaStyle.DrawSeparator();
        DrawProgressAndLog();
    }

    private void DrawToolbar()
    {
        var scanning  = _scanTask is { IsCompleted: false };
        var processing = _processTask is { IsCompleted: false };
        var busy      = scanning || processing;

        Im.Item.SetNextWidthScaled(100);
        ImEx.LogarithmicInput("纹理分辨率限制"u8, ref _dimensionLimit, 4);
        Im.Tooltip.OnHover("宽或高超过此值的纹理会作为候选。处理时将反复减半直到两边都不超过此上限。"u8);

        Im.Line.Same();
        Im.Checkbox("创建备份"u8, ref _createBackups);
        Im.Tooltip.OnHover("处理前将原文件重命名为 .bak。"u8);

        Im.Line.Same();
        if (ImEx.Button("扫描当前模组"u8, Vector2.Zero, "扫描当前模组中宽或高超过分辨率限制的 .tex / .atex。"u8, busy || editor.Mod is null))
            StartScan();

        Im.Line.Same();
        if (ImEx.Button("取消扫描"u8, Vector2.Zero, "取消正在进行的扫描。"u8, !scanning))
            CancelScan();

        Im.Item.SetNextWidthScaled(200);
        Im.Input.Text("##pathFilter"u8, ref _pathFilter, "路径筛选（如 _d）"u8);
        Im.Tooltip.OnHover("不区分大小写，按相对路径包含匹配过滤列表；「全选当前筛选」只作用于可见项。"u8);

        Im.Line.Same();
        if (ImEx.Button("全选当前筛选"u8, Vector2.Zero, "选中当前筛选条件下的全部候选。"u8, busy || _candidates.Count is 0))
        {
            foreach (var c in FilteredCandidates())
                _selected.Add(c.FullPath);
        }

        Im.Line.Same();
        if (ImEx.Button("清空选择"u8, Vector2.Zero, "取消全部选中。"u8, busy || _selected.Count is 0))
            _selected.Clear();

        Im.Line.Same();
        var modifierOk = LunaStyle.Modifier.Destructive.Active;
        var canProcess = !busy && _selected.Count > 0 && editor.Mod is not null;
        Utf8StringHandler<TextStringHandlerBuffer> processTt = !canProcess
            ? "请先扫描并选中要处理的纹理。"
            : !modifierOk
                ? $"按住 {LunaStyle.Modifier.Destructive} 键以处理选中纹理。"
                : $"将选中纹理分辨率限制为 {_dimensionLimit}×{_dimensionLimit}（保持原压缩格式）。";
        if (ImEx.Button($"处理选中 ({_selected.Count})", Vector2.Zero, processTt, !canProcess || !modifierOk))
            StartProcess();
    }

    private void DrawCandidateList()
    {
        var filtered = FilteredCandidates().ToList();
        Im.Text($"候选 {_candidates.Count}，显示 {filtered.Count}，已选 {_selected.Count}");

        using var child = Im.Child.Begin("##texOptList"u8, new Vector2(-1, -180 * Im.Style.GlobalScale), true);
        if (!child)
            return;

        using var table = Im.Table.Begin("##texOptTable"u8, 6, TableFlags.SizingFixedFit | TableFlags.RowBackground | TableFlags.ScrollY);
        if (!table)
            return;

        table.SetupColumn("选"u8, TableColumnFlags.WidthFixed, Im.Style.FrameHeight);
        table.SetupColumn("路径"u8, TableColumnFlags.WidthStretch);
        table.SetupColumn("宽"u8,   TableColumnFlags.WidthFixed, 60 * Im.Style.GlobalScale);
        table.SetupColumn("高"u8,   TableColumnFlags.WidthFixed, 60 * Im.Style.GlobalScale);
        table.SetupColumn("格式"u8, TableColumnFlags.WidthFixed, 100 * Im.Style.GlobalScale);
        table.SetupColumn("大小"u8, TableColumnFlags.WidthFixed, 80 * Im.Style.GlobalScale);
        table.HeaderRow();

        var disabled = _busy || _processTask is { IsCompleted: false };
        using var dis = Im.Disabled(disabled);

        var row = 0;
        foreach (var item in filtered)
        {
            using var id = Im.Id.Push(row++);
            table.NextColumn();
            var selected = _selected.Contains(item.FullPath);
            if (Im.Checkbox("##sel"u8, ref selected))
            {
                if (selected)
                    _selected.Add(item.FullPath);
                else
                    _selected.Remove(item.FullPath);
            }

            table.NextColumn();
            Im.Text(item.RelPath);
            table.NextColumn();
            Im.Text($"{item.Width}");
            table.NextColumn();
            Im.Text($"{item.Height}");
            table.NextColumn();
            Im.Text($"{item.Format}");
            table.NextColumn();
            Im.Text(FormattingFunctions.HumanReadableSize(item.Size));
        }
    }

    private void DrawProgressAndLog()
    {
        if (_scanTask is { IsCompleted: false })
            Im.Text("扫描中..."u8);
        else if (_processTask is { IsCompleted: false })
        {
            var fraction = _processTotal <= 0 ? 0f : (float)_processDone / _processTotal;
            Im.ProgressBar(fraction, new Vector2(-1, 0), $"{_processDone} / {_processTotal}");
        }

        Im.Text("日志"u8);
        using var child = Im.Child.Begin("##texOptLog"u8, new Vector2(-1, -1), true);
        if (!child)
            return;

        lock (_logLock)
        {
            foreach (var line in _log)
                Im.TextWrapped(line);
        }
    }

    private IEnumerable<Candidate> FilteredCandidates()
    {
        if (_pathFilter.Length is 0)
            return _candidates;

        return _candidates.Where(c => c.RelPath.Contains(_pathFilter, StringComparison.OrdinalIgnoreCase));
    }

    private void StartScan()
    {
        if (editor.Mod is null || _scanTask is { IsCompleted: false })
            return;

        CancelScan();
        _candidates.Clear();
        _selected.Clear();
        AppendLog($"开始扫描模组「{editor.Mod.Name}」，分辨率限制 {_dimensionLimit}…");

        var files = editor.Files.GetByType(ResourceType.Tex)
            .Concat(editor.Files.GetByType(ResourceType.Atex))
            .Select(f => (f.File.FullName, f.RelPath.ToString(), f.FileSize))
            .ToList();
        var limit = _dimensionLimit;
        _scanCts  = new CancellationTokenSource();
        var token = _scanCts.Token;

        _scanTask = Task.Run(() =>
        {
            var found = new List<Candidate>();
            foreach (var (fullPath, relPath, size) in files)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (!TryReadMeta(fullPath, out var width, out var height, out var format))
                        continue;

                    if (width <= limit && height <= limit)
                        continue;

                    found.Add(new Candidate(fullPath, relPath, width, height, format, size));
                }
                catch (Exception ex)
                {
                    AppendLog($"跳过 {relPath}: {ex.Message}");
                }
            }

            return found;
        }, token).ContinueWith(t =>
        {
            if (t.IsCanceled)
            {
                AppendLog("扫描已取消。");
                return;
            }

            if (t.IsFaulted)
            {
                AppendLog($"扫描失败: {t.Exception?.GetBaseException().Message}");
                return;
            }

            _candidates.Clear();
            _candidates.AddRange(t.Result);
            _candidates.Sort((a, b) => string.Compare(a.RelPath, b.RelPath, StringComparison.OrdinalIgnoreCase));
            AppendLog($"扫描完成：找到 {_candidates.Count} 个超限纹理。");
        }, TaskScheduler.Default);
    }

    private void StartProcess()
    {
        if (editor.Mod is null || _processTask is { IsCompleted: false })
            return;

        var mod      = editor.Mod;
        var selected = _candidates.Where(c => _selected.Contains(c.FullPath)).ToList();
        if (selected.Count is 0)
            return;

        CancelProcess();
        _processDone  = 0;
        _processTotal = selected.Count;
        _busy         = true;
        var limit   = _dimensionLimit;
        var backup  = _createBackups;
        _processCts = new CancellationTokenSource();
        var token   = _processCts.Token;

        AppendLog($"开始处理 {selected.Count} 个纹理，限制最大宽高为 {limit}…");

        _processTask = Task.Run(async () =>
        {
            var succeeded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in selected)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var saveType = item.Format.ToTexFormat() is TexFile.TextureFormat.B8G8R8A8
                        ? optimization.GetTargetFormat(item.FullPath, mod)
                        : CombinedTexture.TextureSaveType.AsIs;
                    var (newWidth, newHeight) = ComputeRestrictedSize(item.Width, item.Height, limit, limit);
                    await optimization.RestrictDimensions(item.FullPath, limit, limit, backup, saveType).ConfigureAwait(false);
                    succeeded.Add(item.FullPath);
                    AppendLog($"成功：{item.RelPath}（{item.Width}x{item.Height} -> {newWidth}x{newHeight}）");
                }
                catch (Exception ex)
                {
                    AppendLog($"失败：{item.RelPath} — {ex.Message}");
                }
                finally
                {
                    Interlocked.Increment(ref _processDone);
                }
            }

            return succeeded;
        }, token).ContinueWith(t =>
        {
            _busy = false;
            if (t.IsCanceled)
            {
                AppendLog("处理已取消。");
                return;
            }

            if (t.IsFaulted)
            {
                AppendLog($"处理失败: {t.Exception?.GetBaseException().Message}");
                return;
            }

            var ok = t.Result;
            _candidates.RemoveAll(c => ok.Contains(c.FullPath));
            foreach (var path in ok)
                _selected.Remove(path);

            AppendLog($"处理结束：成功 {ok.Count} / {_processTotal}。");
        }, TaskScheduler.Default);
    }

    private static (int Width, int Height) ComputeRestrictedSize(int width, int height, int maxWidth, int maxHeight)
    {
        var targetWidth  = width;
        var targetHeight = height;
        while (targetWidth > maxWidth || targetHeight > maxHeight)
        {
            targetWidth  /= 2;
            targetHeight /= 2;
        }

        return (targetWidth, targetHeight);
    }

    private static unsafe bool TryReadMeta(string fullPath, out int width, out int height, out DXGIFormat format)
    {
        width  = 0;
        height = 0;
        format = DXGIFormat.Unknown;

        var info = new FileInfo(fullPath);
        if (!info.Exists || info.Length <= sizeof(TexFile.TexHeader))
            return false;

        using var stream = info.OpenRead();
        using var reader = new BinaryReader(stream);
        var       header = reader.ReadStructure<TexFile.TexHeader>();
        var       meta   = header.ToTexMeta();
        if (meta.Format is DXGIFormat.Unknown)
            return false;

        width  = meta.Width;
        height = meta.Height;
        format = meta.Format;
        return true;
    }

    private void AppendLog(string line)
    {
        var stamped = $"[{DateTime.Now:HH:mm:ss}] {line}";
        lock (_logLock)
            _log.Add(stamped);
    }

    private void CancelScan()
    {
        try
        {
            _scanCts?.Cancel();
        }
        catch
        {
            // ignored
        }

        _scanCts?.Dispose();
        _scanCts  = null;
        _scanTask = null;
    }

    private void CancelProcess()
    {
        try
        {
            _processCts?.Cancel();
        }
        catch
        {
            // ignored
        }

        _processCts?.Dispose();
        _processCts  = null;
        _processTask = null;
        _busy        = false;
    }
}
