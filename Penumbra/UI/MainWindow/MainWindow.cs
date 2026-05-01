using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImSharp;
using Luna;
using Penumbra.Communication;
using Penumbra.Services;
using Penumbra.UI.Classes;
using TabType = Penumbra.Api.Enums.TabType;
using Window = Luna.Window;

namespace Penumbra.UI.MainWindow;

public sealed class MainWindow : Window
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly Configuration           _config;
    private readonly ValidityChecker         _validityChecker;
    private readonly GlobalModImporter       _globalModImporter;
    private readonly UiNavigator             _navigator;
    private          Penumbra?               _penumbra;
    private          MainTabBar              _configTabs = null!;
    private          string?                 _lastException;

    public MainWindow(IDalamudPluginInterface pi, Configuration config, ValidityChecker checker,
        TutorialService tutorial, GlobalModImporter globalModImporter, UiNavigator navigator)
        : base(checker.GetMainWindowLabel())
    {
        _pluginInterface   = pi;
        _config            = config;
        _validityChecker   = checker;
        _globalModImporter = globalModImporter;
        _navigator         = navigator;

        _navigator.ToggleMainWindow += OnToggleMainWindow;
        RespectCloseHotkey          =  true;
        tutorial.UpdateTutorialStep();
        IsOpen = _config.OpenWindowAtStart;
    }

    public void OpenSettings()
    {
        _configTabs.NextTab = TabType.Settings;
        IsOpen              = true;
    }

    public void Setup(Penumbra penumbra, MainTabBar configTabs)
    {
        _penumbra           = penumbra;
        _configTabs         = configTabs;
        _configTabs.NextTab = _config.Ephemeral.SelectedTab;
    }

    public override bool DrawConditions()
        => _penumbra != null;

    public override void PreDraw()
    {
        if (_config.Ephemeral.FixMainWindow)
            Flags |= WindowFlags.NoResize | WindowFlags.NoMove;
        else
            Flags &= ~(WindowFlags.NoResize | WindowFlags.NoMove);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = _config.MinimumSize,
            MaximumSize = new Vector2(4096, 2160),
        };
    }

    public override void Draw()
    {
        UiHelpers.SetupCommonSizes();
        _globalModImporter.DrawWindowTarget();
        try
        {
            if (!_validityChecker.IsValidSourceRepo)
            {
                DrawProblemWindow(
                    $"你正在从其他仓库 \"{_pluginInterface.SourceRepository}\" 而不是官方仓库加载Penumbra的发行版本。\n"
                  + $"请检查仓库链接是否正确，注意区分\"http\"和\"https\"。\n\n"
                  + $"国服汉化请使用獭三方：\"{ValidityChecker.RepositoryOtter3rd}\"。\n"
                  + $"或使用meowrs国服仓库：\"{ValidityChecker.Repository}\"。\n\n"
                  + $"国际服汉化请使用meowrs国际服仓库：\"{ValidityChecker.RepositoryGlobal}\"。\n"
                  + $"国际服英文原版请使用官方星海库：\"{ValidityChecker.RepositoryOfficial}\"。\n\n"
                  + "如果你正在进行Penumbra的开发并看到这条信息，请在编译器切换到Debug模式避免出现这个情况。");
            }
            else if (_validityChecker.IsNotInstalledPenumbra)
            {
                DrawProblemWindow(
                    $"你正在从 \"{_pluginInterface.AssemblyLocation.Directory?.FullName ?? "未知"}\" 目录而不是 \"installedPlugins\" 目录加载Penumbra的发行版本。\n\n"
                  + "你不应该手动从本地安装Penumbra，而应该在 \"卫月设置-测试版-自定义插件仓库\" 下添加仓库地址后，在\"插件中心\"进行安装。\n\n"
                  + "如果你不清楚怎么做，请在Penumbras的github仓库下查看readme或加入我们的Discord.\n"
                  + "如果你正在进行Penumbra的开发并看到这条信息，请在编译器切换到Debug模式避免出现这个情况。");
            }
            else if (_validityChecker.DevPenumbraExists)
            {
                DrawProblemWindow(
                    $"你正在使用来自 \"{_pluginInterface.AssemblyLocation.Directory?.FullName ?? "未知"}\" 目录下的Penmubra。 "
                  + "但在你的 \"DevPlugins\" 文件夹中仍然有手动安装留下的残余文件。\n\n"
                  + "这可能会导致一些问题，请前往 \"%%appdata%%\\XIVLauncher\\devPlugins\" 目录并删除其中的Penumbra文件夹。\n\n"
                  + "如果你正在开发Penumbra，请尽量避免混淆版本。在Debug模式下编译不会出现这个警告。");
            }
            else
            {
                _configTabs.Draw();
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

   

    private void DrawProblemWindow(Utf8StringHandler<TextStringHandlerBuffer> text)
    {
        using var color = ImGuiColor.Text.Push(Colors.RegexWarningBorder);
        Im.Line.New();
        Im.Line.New();
        Im.TextWrapped(ref text);
        color.Pop();

        Im.Line.New();
        Im.Line.New();
        SupportButton.DiscordSplit(Penumbra.Messager, new Vector2(0, 0));
        Im.Line.Same();
        UiHelpers.DrawSupportButton(_penumbra!);
        Im.Line.New();
        Im.Line.New();
    }

    private void OnToggleMainWindow(bool open)
        => IsOpen = open;
}
