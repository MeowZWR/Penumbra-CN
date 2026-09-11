using Luna;
using Penumbra.Services;
using Penumbra.UI.ModsTab.Optimized;

namespace Penumbra.UI.ModsTab;

public sealed class ModPanelSettingsTabHost(ServiceManager services, Configuration config) : ITab<ModPanelTab>
{
    private ModPanelSettingsTab?          _original;
    private OptimizedModPanelSettingsTab? _optimized;

    private ITab<ModPanelTab> Current
        => config.Ui.UseOptimizedModSettingsUi
            ? _optimized ??= services.GetService<OptimizedModPanelSettingsTab>()
            : _original ??= services.GetService<ModPanelSettingsTab>();

    public ReadOnlySpan<byte> Label
        => Current.Label;

    public ModPanelTab Identifier
        => ModPanelTab.Settings;

    public void DrawContent()
        => Current.DrawContent();

    public void PostTabButton()
        => Current.PostTabButton();

    public void Reset()
    {
        _original?.Reset();
        _optimized?.Reset();
    }
}
