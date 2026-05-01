using Dalamud.Plugin;
using ImSharp;
using Luna;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;

namespace Penumbra.Api.IpcTester;

public class PluginStateIpcTester : IUiService, IDisposable
{
    private readonly IDalamudPluginInterface       _pi;
    public readonly  EventSubscriber<string, bool> ModDirectoryChanged;
    public readonly  EventSubscriber               Initialized;
    public readonly  EventSubscriber               Disposed;
    public readonly  EventSubscriber<bool>         EnabledChange;

    private string         _currentConfiguration = string.Empty;
    private string         _lastModDirectory     = string.Empty;
    private bool           _lastModDirectoryValid;
    private DateTimeOffset _lastModDirectoryTime = DateTimeOffset.MinValue;

    private readonly List<DateTimeOffset> _initializedList = [];
    private readonly List<DateTimeOffset> _disposedList    = [];

    private string   _requiredFeatureString = string.Empty;
    private string[] _requiredFeatures      = [];

    private DateTimeOffset _lastEnabledChange = DateTimeOffset.UnixEpoch;
    private bool?          _lastEnabledValue;

    public PluginStateIpcTester(IDalamudPluginInterface pi)
    {
        _pi                 = pi;
        ModDirectoryChanged = IpcSubscribers.ModDirectoryChanged.Subscriber(pi, UpdateModDirectoryChanged);
        Initialized         = IpcSubscribers.Initialized.Subscriber(pi, AddInitialized);
        Disposed            = IpcSubscribers.Disposed.Subscriber(pi, AddDisposed);
        EnabledChange       = IpcSubscribers.EnabledChange.Subscriber(pi, SetLastEnabled);
        ModDirectoryChanged.Disable();
        EnabledChange.Disable();
    }

    public void Dispose()
    {
        ModDirectoryChanged.Dispose();
        Initialized.Dispose();
        Disposed.Dispose();
        EnabledChange.Dispose();
    }


    public void Draw()
    {
        using var tree = Im.Tree.Node("插件状态"u8);
        if (!tree)
            return;

        if (Im.Input.Text("所需特性"u8, ref _requiredFeatureString))
            _requiredFeatures = _requiredFeatureString.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        using var table = Im.Table.Begin(StringU8.Empty, 3, TableFlags.SizingFixedFit);
        if (!table)
            return;

        DrawList(IpcSubscribers.Initialized.Label, "上次初始化"u8, _initializedList);
        DrawList(IpcSubscribers.Disposed.Label,    "上次释放"u8,    _disposedList);

        using (IpcTester.DrawIntro(ApiVersion.LabelU8, "当前版本"u8))
        {
            var (breaking, features) = new ApiVersion(_pi).Invoke();
            table.DrawColumn($"{breaking}.{features:D4}");
        }

        using (IpcTester.DrawIntro(GetEnabledState.LabelU8, "当前状态"u8))
        {
            table.DrawColumn($"{new GetEnabledState(_pi).Invoke()}");
        }

        using (IpcTester.DrawIntro(IpcSubscribers.EnabledChange.LabelU8, "上次变更"u8))
        {
            table.DrawColumn(_lastEnabledValue is { } v ? $"{_lastEnabledChange} (变为 {v})" : "从未"u8);
        }

        using (IpcTester.DrawIntro(SupportedFeatures.LabelU8, "支持的特性"u8))
        {
            table.DrawColumn(StringU8.Join(", "u8, new SupportedFeatures(_pi).Invoke()));
        }

        using (IpcTester.DrawIntro(CheckSupportedFeatures.LabelU8, "缺失的特性"u8))
            table.DrawColumn(StringU8.Join(", "u8, new CheckSupportedFeatures(_pi).Invoke(_requiredFeatures)));

        using (IpcTester.DrawIntro(GetConfiguration.LabelU8, "配置信息"u8))
        {
            DrawConfigPopup();
            table.NextColumn();
            if (Im.SmallButton("Get"u8))
            {
                _currentConfiguration = new GetConfiguration(_pi).Invoke();
                Im.Popup.Open("配置弹窗"u8);
            }
        }

        using (IpcTester.DrawIntro(GetModDirectory.LabelU8, "当前模组目录"u8))
        {
            table.DrawColumn(new GetModDirectory(_pi).Invoke());
        }

        using (IpcTester.DrawIntro(IpcSubscribers.ModDirectoryChanged.LabelU8, "上次模组目录变更"u8))
        {
            table.DrawColumn(_lastModDirectoryTime > DateTimeOffset.MinValue
                ? $"{_lastModDirectory} ({(_lastModDirectoryValid ? "有效" : "无效")}) 于 {_lastModDirectoryTime}"
                : "无"u8);
        }

        return;

        static void DrawList(string label, ReadOnlySpan<byte> text, List<DateTimeOffset> list)
        {
            using var _ = IpcTester.DrawIntro(label, text);
            if (list.Count is 0)
            {
                Im.Table.DrawColumn("从未"u8);
            }
            else
            {
                Im.Table.DrawColumn(list[^1].LocalDateTime.ToString(CultureInfo.CurrentCulture));
                if (list.Count > 1 && Im.Item.Hovered())
                    Im.Tooltip.Set(
                        StringU8.Join((byte)'\n', list.SkipLast(1).Select(t => t.LocalDateTime.ToString(CultureInfo.CurrentCulture))));
            }
        }
    }

    private void DrawConfigPopup()
    {
        Im.Window.SetNextSize(ImEx.ScaledVector(500, 500));
        using var popup = Im.Popup.Begin("配置弹窗"u8);
        if (!popup)
            return;

        using (Im.Font.PushMono())
        {
            Im.TextWrapped(_currentConfiguration);
        }

        if (Im.Button("关闭"u8, -Vector2.UnitX) || !Im.Window.Focused())
            Im.Popup.CloseCurrent();
    }

    private void UpdateModDirectoryChanged(string path, bool valid)
        => (_lastModDirectory, _lastModDirectoryValid, _lastModDirectoryTime) = (path, valid, DateTimeOffset.Now);

    private void AddInitialized()
        => _initializedList.Add(DateTimeOffset.UtcNow);

    private void AddDisposed()
        => _disposedList.Add(DateTimeOffset.UtcNow);

    private void SetLastEnabled(bool val)
        => (_lastEnabledChange, _lastEnabledValue) = (DateTimeOffset.Now, val);
}
