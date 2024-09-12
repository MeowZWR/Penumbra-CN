using OtterGui.Services;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

/// <summary> List of currently available tutorials. </summary>
public enum BasicTutorialSteps
{
    GeneralTooltips,
    ModDirectory,
    EnableMods,
    Deprecated1,
    GeneralSettings,
    Collections,
    EditingCollections,
    CurrentCollection,
    SimpleAssignments,
    IndividualAssignments,
    GroupAssignments,
    CollectionDetails,
    Incognito,
    Deprecated2,
    Mods,
    ModImport,
    AdvancedHelp,
    ModFilters,
    CollectionSelectors,
    Redrawing,
    EnablingMods,
    Priority,
    ModOptions,
    Fin,
    Deprecated3,
    Faq1,
    Faq2,
    Favorites,
    Tags,
}

/// <summary> Service for the in-game tutorial. </summary>
public class TutorialService : IUiService
{
    public const string SelectedCollection  = "选中的合集";
    public const string DefaultCollection   = "基础合集";
    public const string InterfaceCollection = "界面合集";
    public const string AssignedCollections = "指定的合集";

    public const string SupportedRedrawModifiers = "    - 留空, 用于重绘所有角色\n"
      + "    - 'self' or '<me>': 重绘自己\n"
      + "    - 'target' or '<t>': 重绘目标\n"
      + "    - 'focus' or '<f>: 重绘焦点目标\n"
      + "    - 'mouseover' or '<mo>': 重绘当前鼠标悬停指向的角色\n"
      + "    - 'furniture': 重绘大部分室内家具，目前不能在户外工作。\n"
      + "    - 输入任意指定的角色名称来重绘带有该名称的角色";

    private readonly EphemeralConfig _config;
    private readonly Tutorial        _tutorial;

    public TutorialService(EphemeralConfig config)
    {
        _config = config;
        _tutorial = new Tutorial()
            {
                BorderColor    = Colors.TutorialBorder,
                HighlightColor = Colors.TutorialMarker,
                PopupLabel     = "设置教程",
            }
            .Register( "小贴士", "此符号会为你提供旁边选项更多的信息。\n\n"
              + "当你不确定这个选项的功能，或者不知道怎么做时，将鼠标悬停到这个符号上方。" )
            .Register( "初始设置，步骤1：模组目录",
                "首先设置你的模组目录，这是你的模组文件存放的地方。\n\n"
              + "模组目录的路径越短越好，比如'C:\\FFXIVMods'；并且最好位于读写速度最快的驱动器上以提高性能。\n\n"
              + "该文件夹应该是没有其他应用程序在写入数据的空文件夹。" )
            .Register( "初始设置，步骤2：启用模组", "不要忘记勾选启用模组，不然模组不会生效。" )
            .Deprecated()
            .Register( "常规设置", "在开始使用之前查看里面的设置，或许会对你有很大的帮助。\n\n"
              + "如果你不知道其中一些选项有什么用处，可以以后再看。" )
            .Register( "初始设置，步骤3：合集", "合集是记录已安装模组的设置的列表。\n\n"
	          + "这是我们的下一站\n\n"
	          + "设置根目录后，点击此选项卡继续教程！" )
            .Register("初始设置，步骤4: 管理合集",
                "在左边，我们有合集选择器。在这里我们可以创建新的合集 - 创建空白合集或者复制现有的合集 - 并删除任何不再需要的合集。\n"
              + $"名为'{ModCollection.DefaultCollectionName}'的合集不能被删除。")
            .Register($"初始设置，步骤5: {SelectedCollection}",
                $"在选择器中高亮显示的合集是'{SelectedCollection}'。这是我们正在查看和操作的合集。\n我们稍后在下一个选项卡中对模组做的任何修改都将应用到此合集。\n"
              + $"我们应该已经选中了合集'{ModCollection.DefaultCollectionName}'，我们现在只是做基础设置，还不需要对它做任何操作。\n\n")
            .Register("初始设置，步骤6: 简单分配",
                "除了用合集来管理不同的模组设置，我们还可以使用分配功能来分配合集，可以让不同的模组设置生效于不同的角色。\n"
              + "简单分配面板提供对大部分人来说按照说明就足够使用的分配功能。\n"
              + $"如果你是初次使用，你可以看到合集'{ModCollection.DefaultCollectionName}'已经分配给了'{CollectionType.Default.ToName()}'和'{CollectionType.Interface.ToName()}'。\n"
              + "你也可以单击下面的功能单元，为该单元分配'不使用模组'使其不使用任何合集。")
            .Register("独立分配",
                "在'独立分配'面板，你可以手动为特定角色或者NPC分配合集，不仅仅是你自己或者你当前可以选中的目标。")
            .Register("组分配",
                "在'组分配'面板，你可以按种族甚至年龄为更特定的角色组创建分配。")
            .Register("合集详情",
                "在'合集详情'面板，你可以查看当前合集的使用情况，除此之外，你还可以移除过时的模组设置、对继承进行配置。\n"
              + "继承可以让一个合集同步另一个合集的设置，只要它本身没有出现问题的模组设置。")
            .Register("匿名模式",
                "此按钮可以切换匿名模式，匿名模式下所有合集名称缩短为两个字母和一个数字，\n"
              + "所有角色名称显示为首字母缩写和世界名称，方便你分享截图。\n"
              + "强烈建议你在使用Penumbra时不要在分享的截图上显示你的角色名称。")
            .Deprecated()
            .Register( "初始设置，步骤7: 模组", "最后一站是'模组'选项卡，在这里你可以导入和设置你的模组。\n\n"
              + $"请在按你的喜好确认好{SelectedCollection}和{DefaultCollection}设置后前往。")
            .Register( "初始设置，步骤8: 导入模组",
                "单击此按钮打开文件选择器，选择TTMP模组文件。 你可以同时选中多个进行批量导入。\n\n"
              + "不建议导入包含大量TexTool模组的大型模组包（比如从TT备份的那些），而是导入单个的模组包，否则你会失去很多的Penumbra独特功能！\n\n"
              + "高级编辑下提供了为纹身等导入原始纹理模型的功能，可以使用，但目前仍正在开发中。" ) // TODO
            .Register( "进阶帮助", "单击此按钮可以获取在模组选择器中一些操作的详细信息。\n\n"
              + "导入并选中一个模组来进行下一步。" )
            .Register( "模组筛选器", "你可以在此处按名称、作者、更改项目、或各种选项筛选模组。" )
            .Register( "合集选择器", $"此行提供了设置{SelectedCollection}的快捷方式。\n\n"
              + $"第一个选项设置为你的{DefaultCollection}（如果有）。n\n"
              + "第二个选项设置为当前所选模组的设置继承来自什么合集（如果有）。\n\n"
              + "第三个选项是常规合集选择菜单，你可以在这里选择所有存在的合集。" )
            .Register( "重绘",
                "当你修改模组设置，修改不会马上生效，你需要强制游戏重新加载相关文件。(如果重新加载不成功，应当重启游戏)。\n\n"
              + "为此，Penumbra添加了重绘按钮，功能与命令'/penumbra redraw'相同，会将所有角色重绘一次。你也可以使用帮助图标中描述的几个修饰符来代替。\n\n"
              + "你也可以将指令添加到宏(比如 '/penumbra redraw self' 重绘自己)。" )
            .Register( "初始设置，步骤9: 启用模组",
                "选中并启用一个模组，禁用的模组不会在当前合集中生效。\n\n"
              + "模组可以在合集中启用或禁用，也可以点击右边的继承设置，在这种情况下它们将按设定继承其他合集。" )
            .Register( "初始设置，步骤10: 优先级",
                "如果两个启用的模组更改了相同的文件，则会发生冲突。\n\n"
              + "可以通过设置不同的优先级来解决冲突，具有更高数字的模组其冲突的文件将优先使用。\n\n"
              + "只要设置正确的优先级，冲突就不是问题。优先级可以设置为负数。" )
            .Register( "模组选项", "许多模组自带选项。你可以在这里进行选择。\n\n"
              + "下拉选项只能选择其中一个，有复选框的选项则每个都可以单独启用。" )
            .Register( "初始设置 - 结束语", "现在，你已经知道可以让Penumbra运行和工作起来的所有信息了！\n\n"
              + "如果还有其他问题，或你需要有关高级功能的更多帮助，请查看设置页链接中的新手指引。" )
            .Deprecated()
            .Register("FAQ 1",
                "不建议同时使用TexTools和Penumbra。如果TexTools损坏了你的游戏索引，Penumbra可能会停止工作。")
            .Register("FAQ 2", "Penumbra可以重新分配模组使用的皮肤材质，在模组编辑选项卡中有个'更新Bibo材质'按钮会自动分配，如果不起作用，请使用高级编辑中的材质指定手动修改。")
            .Register( "收藏",
                "现在你可以通过此按钮来使用收藏功能了。你可以在模组选择器中筛选你喜欢的模组。收藏信息基于合集单独存储在本地，而不是在模组文件中。" )
            .Register("标签",
                "模组现在可以使用两种标签类型：\n\n- 本地标签，由你自行设置。他们单独存储在本地而不是模组目录。\n- 模组标签，存储在模组元数据中，该标签通常由模组作者设置，并随模组创建。只能在编辑选项卡中进行修改。\n\n模组设置的标签会覆盖本地设置的相同标签。\n\n你可以在模组筛选器中输入't:文本'进行筛选。")
            .EnsureSize(Enum.GetValues<BasicTutorialSteps>().Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OpenTutorial(BasicTutorialSteps step)
        => _tutorial.Open((int)step, _config.TutorialStep, v =>
        {
            _config.TutorialStep = v;
            _config.Save();
        });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SkipTutorial(BasicTutorialSteps step)
        => _tutorial.Skip((int)step, _config.TutorialStep, v =>
        {
            _config.TutorialStep = v;
            _config.Save();
        });

    /// <summary> Update the current tutorial step if tutorials have changed since last update. </summary>
    public void UpdateTutorialStep()
    {
        var tutorial = _tutorial.CurrentEnabledId(_config.TutorialStep);
        if (tutorial != _config.TutorialStep)
        {
            _config.TutorialStep = tutorial;
            _config.Save();
        }
    }
}
