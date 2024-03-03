using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using OtterGui;
using OtterGui.Custom;
using OtterGui.Raii;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.Tabs;
using Penumbra.Util;

namespace Penumbra.UI;

public sealed class ConfigWindow : Window
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly Configuration          _config;
    private readonly PerformanceTracker     _tracker;
    private readonly ValidityChecker        _validityChecker;
    private          Penumbra?              _penumbra;
    private          ConfigTabBar           _configTabs = null!;
    private          string?                _lastException;

    public ConfigWindow(PerformanceTracker tracker, DalamudPluginInterface pi, Configuration config, ValidityChecker checker,
        TutorialService tutorial)
        : base(GetLabel(checker))
    {
        _pluginInterface = pi;
        _config          = config;
        _tracker         = tracker;
        _validityChecker = checker;

        RespectCloseHotkey = true;
        tutorial.UpdateTutorialStep();
        IsOpen = _config.OpenWindowAtStart;
    }

    public void Setup(Penumbra penumbra, ConfigTabBar configTabs)
    {
        _penumbra             = penumbra;
        _configTabs           = configTabs;
        _configTabs.SelectTab = _config.Ephemeral.SelectedTab;
    }

    public override bool DrawConditions()
        => _penumbra != null;

    public override void PreDraw()
    {
        if (_config.Ephemeral.FixMainWindow)
            Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
        else
            Flags &= ~(ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);
        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = _config.MinimumSize,
            MaximumSize = new Vector2(4096, 2160),
        };
    }

    public override void Draw()
    {
        using var timer = _tracker.Measure(PerformanceType.UiMainWindow);
        UiHelpers.SetupCommonSizes();
        try
        {
            if (_validityChecker.ImcExceptions.Count > 0)
            {
                DrawProblemWindow(
                    $"在尝试从游戏数据加载IMC文件时发生了 {_validityChecker.ImcExceptions.Count} 个错误。\n"
                  + "这通常意味着你用TexTools装了mod，但没有初始化就更新游戏而对游戏文件产生了损坏。\n"
                  + "建议不要同时使用TexTools和Penumbra（或其他基于Lumina的工具)。\n\n"
                  + "请修复客户端。");
                DrawImcExceptions();
            }
            else if (!_validityChecker.IsValidSourceRepo)
            {
                DrawProblemWindow(
                    $"你正在从其他仓库 \"{_pluginInterface.SourceRepository}\" 而不是官方仓库加载Penumbra的发行版本。\n"
                  + $"请使用獭三方： {ValidityChecker.OtterCnRepositoryLower}。\n"
                  + $"或：{ValidityChecker.Repository}。\n"
                  + $"需要原版请使用原作者仓库：\"https://raw.githubusercontent.com/xivdev/Penumbra/master/repo.json\"（最新版可能不适用国服）\n\n"
                  + "如果你正在进行Penumbra的开发并看到这条信息，请在编译器切换到Debug模式避免出现这个情况。");
            }
            else if (_validityChecker.IsNotInstalledPenumbra)
            {
                DrawProblemWindow(
                    $"你正在从 \"{_pluginInterface.AssemblyLocation.Directory?.FullName ?? "未知"}\" 目录而不是 \"installedPlugins\" 目录加载Penumbra的发行版本。\n\n"
                  + "你不应该手动安装Penumbra，而应该在\"卫月设置-测试版-自定义插件仓库\"下添加仓库地址后，在\"插件中心\"进行安装。\n\n"
                  + "如果你不清楚怎么做，请在Penumbras的github仓库下查看readme或加入我们的Discord.\n"
                  + "如果你正在进行Penumbra的开发并看到这条信息，请在编译器切换到Debug模式避免出现这个情况。");
            }
            else if (_validityChecker.DevPenumbraExists)
            {
                DrawProblemWindow(
                    $"你正在使用来自 \"{_pluginInterface.AssemblyLocation.Directory?.FullName ?? "未知"}\"目录下的Penmubra。 "
                  + "但在你的\"DevPlugins\"文件夹中仍然有自定义安装留下的残余文件。\n\n"
                  + "这可能会导致一些问题，请前往 \"%%appdata%%\\XIVLauncher\\devPlugins\" 目录并删除其中的Penumbra文件夹。\n\n"
                  + "如果你正在开发Penumbra，请尽量避免混淆版本。在Debug模式下编译不会出现这个警告。");
            }
            else
            {
                var type = _configTabs.Draw();
                if (type != _config.Ephemeral.SelectedTab)
                {
                    _config.Ephemeral.SelectedTab = type;
                    _config.Ephemeral.Save();
                }
            }

            _lastException = null;
        }
        catch (Exception e)
        {
            if (_lastException != null)
            {
                var text = e.ToString();
                if (text == _lastException)
                    return;

                _lastException = text;
            }
            else
            {
                _lastException = e.ToString();
            }

            Penumbra.Log.Error($"渲染UI时出现异常：\n{_lastException}");
        }
    }

    private static string GetLabel(ValidityChecker checker)
        => checker.Version.Length == 0
            ? "Penumbra###PenumbraConfigWindow"
            : $"Penumbra v{checker.Version}###PenumbraConfigWindow";

    private void DrawProblemWindow(string text)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, Colors.RegexWarningBorder);
        ImGui.NewLine();
        ImGui.NewLine();
        ImGuiUtil.TextWrapped(text);
        color.Pop();

        ImGui.NewLine();
        ImGui.NewLine();
        CustomGui.DrawDiscordButton(Penumbra.Messager, 0);
        ImGui.SameLine();
        UiHelpers.DrawSupportButton(_penumbra!);
        ImGui.NewLine();
        ImGui.NewLine();
    }

    private void DrawImcExceptions()
    {
        ImGui.TextUnformatted("异常");
        ImGui.Separator();
        using var box = ImRaii.ListBox("##Exceptions", new Vector2(-1, -1));
        foreach (var exception in _validityChecker.ImcExceptions)
        {
            ImGuiUtil.TextWrapped(exception.ToString());
            ImGui.Separator();
            ImGui.NewLine();
        }
    }
}
