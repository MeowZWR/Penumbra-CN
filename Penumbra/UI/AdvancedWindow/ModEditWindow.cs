using System.Collections.Frozen;
using Dalamud.Interface.DragDrop;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Enums;
using Penumbra.Interop.ResourceTree;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.UI.AdvancedWindow.Meta;
using Penumbra.UI.Classes;
using Penumbra.UI.FileEditing;
using Penumbra.UI.FileEditing.Materials;
using Penumbra.UI.FileEditing.Models;
using Penumbra.UI.FileEditing.Shaders;
using Penumbra.UI.FileEditing.Skeletons;
using Penumbra.UI.FileEditing.Textures;
using MdlMaterialEditor = Penumbra.Mods.Editor.MdlMaterialEditor;

namespace Penumbra.UI.AdvancedWindow;

public sealed partial class ModEditWindow : IndexedWindow, IDisposable
{
    private const string WindowBaseLabel = "###SubModEdit";

    private readonly ModEditor           _editor;
    private readonly Configuration       _config;
    private readonly ItemSwapTab         _itemSwapTab;
    private readonly MetaFileManager     _metaFileManager;
    private readonly ActiveCollections   _activeCollections;
    private readonly ModMergeTab         _modMergeTab;
    private readonly CommunicatorService _communicator;
    private readonly IDragDropManager    _dragDropManager;
    private readonly OptionSelectCombo   _optionSelect;

    private readonly FileEditor _modelTab;
    private readonly FileEditor _materialTab;
    private readonly FileEditor _shaderPackageTab;
    private readonly FileEditor _pbdTab;
#if false
    private readonly FileEditor _newTextureTab;
#endif

    private readonly CombiningTextureEditor _textureEditor;

    private Vector2 _iconSize = Vector2.Zero;
    private bool    _allowReduplicate;

    public Mod? Mod { get; private set; }


    public bool IsLoading
    {
        get
        {
            lock (_lock)
            {
                return _editor.IsLoading || _loadingMod is { IsCompleted: false };
            }
        }
    }

    private readonly object _lock = new();
    private          Task?  _loadingMod;


    private void AppendTask(Action run)
    {
        lock (_lock)
        {
            if (_loadingMod == null || _loadingMod.IsCompleted)
                _loadingMod = Task.Run(run);
            else
                _loadingMod = _loadingMod.ContinueWith(_ => run());
        }
    }

    public void ChangeMod(Mod mod)
    {
        if (mod == Mod)
            return;

        WindowName = $"{mod.Name} (LOADING){WindowBaseLabel}{Index}";
        AppendTask(() =>
        {
            _editor.LoadMod(mod, -1, 0).Wait();
            Mod = mod;

            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(1240, 600),
                MaximumSize = 4000 * Vector2.One,
            };
            _selectedFiles.Clear();
            _modelTab.Reset();
            _materialTab.Reset();
            _shaderPackageTab.Reset();
            _modMergeTab.ModMerger.ResetMod();
            _pbdTab.Reset();
            _itemSwapTab.UpdateMod(mod, _activeCollections.Current.GetInheritedSettings(mod.Index).Settings);
            UpdateModels();
        });
    }

    public void ChangeOption(IModDataContainer? subMod)
    {
        AppendTask(() =>
        {
            var (groupIdx, dataIdx) = subMod?.GetDataIndices() ?? (-1, 0);
            _editor.LoadOption(groupIdx, dataIdx).Wait();
        });
    }

    public void UpdateModels()
    {
        if (Mod != null)
            _editor.MdlMaterialEditor.ScanModels(Mod);
    }

    public override bool DrawConditions()
        => Mod != null;

    public override void PreDraw()
    {
        if (IsLoading)
            return;

        var sb = new StringBuilder(256);

        var redirections = 0;
        var unused       = 0;
        var size = _editor.Files.Available.Sum(f =>
        {
            if (f.SubModUsage.Count > 0)
                redirections += f.SubModUsage.Count;
            else
                ++unused;

            return f.FileSize;
        });
        var manipulations = 0;
        var subMods       = 0;
        var swaps = Mod!.AllDataContainers.Sum(m =>
        {
            ++subMods;
            manipulations += m.Manipulations.Count;
            return m.FileSwaps.Count;
        });
        sb.Append(Mod!.Name);
        if (subMods > 1)
            sb.Append($"   |   {subMods} 选项");

        if (size > 0)
            sb.Append($"   |   {_editor.Files.Available.Count} 文件 ({FormattingFunctions.HumanReadableSize(size)})");

        if (unused > 0)
            sb.Append($"   |   {unused} 未使用的文件");

        if (_editor.Files.Missing.Count > 0)
            sb.Append($"   |   {_editor.Files.Missing.Count} 丢失的文件");

        if (redirections > 0)
            sb.Append($"   |   {redirections} 重定向");

        if (manipulations > 0)
            sb.Append($"   |   {manipulations} 元数据操作");

        if (swaps > 0)
            sb.Append($"   |   {swaps} 替换");

        _allowReduplicate = redirections != _editor.Files.Available.Count || _editor.Files.Missing.Count > 0 || unused > 0;
        sb.Append(WindowBaseLabel);
        sb.Append(Index);
        WindowName = sb.ToString();
    }

    public override void OnClose()
    {
        base.OnClose();
        if (Mod is not null && _config.Ephemeral.AdvancedEditingOpenForModPaths.Remove(Mod.Identifier))
            _config.Ephemeral.Save();
        AppendTask(() =>
        {
            _textureEditor.Dispose();
            _materialTab.Reset();
            _modelTab.Reset();
            _shaderPackageTab.Reset();
            _pbdTab.Reset();
#if false
            _newTextureTab.Reset();
#endif
        });
    }

    public override void Draw()
    {
        if (_config.Ephemeral.AdvancedEditingOpenForModPaths.Add(Mod!.Identifier))
            _config.Ephemeral.Save();

        if (IsLoading)
        {
            var radius    = 100 * Im.Style.GlobalScale;
            var thickness = (int)(20 * Im.Style.GlobalScale);
            var offsetX   = Im.ContentRegion.Available.X / 2 - radius;
            var offsetY   = Im.ContentRegion.Available.Y / 2 - radius;
            Im.Cursor.Position += new Vector2(offsetX, offsetY);
            ImEx.Spinner("##spinner"u8, radius, thickness, ImGuiColor.Text.Get());
            return;
        }

        using var id     = Im.Id.Push(Mod!.Identifier);
        using var tabBar = Im.TabBar.Begin("##tabs"u8);
        if (!tabBar)
            return;

        _iconSize = new Vector2(Im.Style.FrameHeight);
        DrawFileTab();
        DrawMetaTab();
        DrawSwapTab();
        _modMergeTab.Draw();
        DrawDuplicatesTab();
        DrawQuickImportTab();
        _modelTab.Draw();
        _materialTab.Draw();
        using (var tab = tabBar.Item("Textures"u8))
        {
            if (tab)
                _textureEditor.DrawPanel(false);
        }
#if false
        _newTextureTab.Draw();
#endif
        _shaderPackageTab.Draw();
        using (var tab = tabBar.Item("道具转换"u8))
        {
            if (tab)
                _itemSwapTab.DrawContent();
        }

        _pbdTab.Draw();

        DrawMissingFilesTab();
        DrawMaterialReassignmentTab();
    }

    private static readonly FrozenDictionary<GenderRace, StringU8> RaceCodeNames = GenderRace.Values.ToFrozenDictionary(v => v, v =>
    {
        if (v is GenderRace.Unknown)
            return new StringU8("所有种族和性别");

        var (gender, race) = v.Split();
        return new StringU8($"({v.ToRaceCode()}) {race.ToNameU8()} {gender.ToNameU8()} ");
    });

    /// <summary> A row of three buttonSizes and a help marker that can be used for material suffix changing. </summary>
    private static class MaterialSuffix
    {
        private static string     _materialSuffixFrom = string.Empty;
        private static string     _materialSuffixTo   = string.Empty;
        private static GenderRace _raceCode           = GenderRace.Unknown;

        private static void DrawRaceCodeCombo(Vector2 buttonSize)
        {
            Im.Item.SetNextWidth(buttonSize.X);
            using var combo = Im.Combo.Begin("##RaceCode"u8, RaceCodeNames[_raceCode]);
            if (!combo)
                return;

            foreach (var (raceCode, name) in RaceCodeNames)
            {
                if (Im.Selectable(name, _raceCode == raceCode))
                    _raceCode = raceCode;
            }
        }

        public static void Draw(ModEditor editor, Vector2 buttonSize)
        {
            DrawRaceCodeCombo(buttonSize);
            Im.Line.Same();
            Im.Item.SetNextWidth(buttonSize.X);
            Im.Input.Text("##suffixFrom"u8, ref _materialSuffixFrom, "此后缀..."u8);
            Im.Line.Same();
            Im.Item.SetNextWidth(buttonSize.X);
            Im.Input.Text("##suffixTo"u8, ref _materialSuffixTo, "改为..."u8);
            Im.Line.Same();
            var disabled = !MdlMaterialEditor.ValidString(_materialSuffixTo);
            Utf8StringHandler<TextStringHandlerBuffer> tt = _materialSuffixTo.Length is 0
                ? "请输入目标后缀。"
                : _materialSuffixFrom == _materialSuffixTo
                    ? "原后缀与新后缀不能相同。"
                    : disabled
                        ? "后缀无效。"
                        : _materialSuffixFrom.Length is 0
                            ? _raceCode is GenderRace.Unknown
                                ? "将所有皮肤材质后缀替换为目标后缀。"
                                : "将指定种族的皮肤材质后缀替换为目标后缀。"
                            : _raceCode is GenderRace.Unknown
                                ? $"将所有皮肤材质后缀从 '{_materialSuffixFrom}' 改为 '{_materialSuffixTo}'."
                                : $"将指定种族的皮肤材质后缀从 '{_materialSuffixFrom}' 改为 '{_materialSuffixTo}'.";
            if (ImEx.Button("修改材质后缀"u8, buttonSize, tt, disabled))
                editor.MdlMaterialEditor.ReplaceAllMaterials(_materialSuffixTo, _materialSuffixFrom, _raceCode);

            var anyChanges = editor.MdlMaterialEditor.ModelFiles.Any(m => m.Changed);
            if (ImEx.Button("保存所有修改"u8, buttonSize,
                    anyChanges ? "不可逆地重写当前应用于模型文件的所有修改。"u8 : "还未做任何修改。"u8,
                    !anyChanges))
                editor.MdlMaterialEditor.SaveAllModels(editor.Compactor);

            Im.Line.Same();
            if (ImEx.Button("撤销所有修改"u8, buttonSize,
                    anyChanges ? "撤销当前进行的和未保存的所有修改。"u8 : "还未做任何修改。"u8, !anyChanges))
                editor.MdlMaterialEditor.RestoreAllModels();

            Im.Line.SameInner();
            LunaStyle.DrawAlignedHelpMarker(
                "模型文件引用了它们应该使用的皮肤材质。这个皮肤材质一般都是同一种。不过mod作者们可能会采用不同的材质来区分体型。\n"u8
              + "此选项允许你将所有模型文件的一个后缀修改为另一个后缀，比如将所有的后缀b改为bibo。这会修改文件，因此请注意此操作有风险。\n"u8
              + "如果你不知道这个模组当前使用的后缀是什么，你可以将'将此后缀...'留空，它会将所有后缀替换为'改为'里面的内容，而不仅仅是匹配的后缀。\n"u8);
        }
    }

    private void DrawMissingFilesTab()
    {
        if (_editor.Files.Missing.Count is 0)
            return;

        using var tab = Im.TabBar.BeginItem("丢失的文件"u8);
        if (!tab)
            return;

        Im.Line.New();
        if (Im.Button("从模组中删除丢失的文件"u8))
            _editor.FileEditor.RemoveMissingPaths(Mod!, _editor.Option!);

        using var child = Im.Child.Begin("##unusedFiles"u8, Im.ContentRegion.Available, true);
        if (!child)
            return;

        using var table = Im.Table.Begin("##missingFiles"u8, 1, TableFlags.RowBackground, Im.ContentRegion.Available);
        if (!table)
            return;

        foreach (var path in _editor.Files.Missing)
            table.DrawColumn(path.FullName);
    }

    private void DrawDuplicatesTab()
    {
        using var tab = Im.TabBar.BeginItem("重复项"u8);
        if (!tab)
            return;

        if (_editor.Duplicates.Worker.IsCompleted)
        {
            if (ImEx.Button("查找重复项"u8, Vector2.Zero,
                    "在这个模组中搜索相同的文件，这可能需要花上一段时间。"u8))
                _editor.Duplicates.StartDuplicateCheck(_editor.Files.Available);
        }
        else
        {
            if (ImEx.Button("取消查找重复项"u8, Vector2.Zero, "取消当前查找操作..."u8))
                _editor.Duplicates.Clear();
        }

        var modifier = _config.DeleteModModifier.IsActive();

        if (_editor.ModNormalizer.Running)
        {
            Im.ProgressBar((float)_editor.ModNormalizer.Step / _editor.ModNormalizer.TotalSteps,
                new Vector2(300 * Im.Style.GlobalScale, Im.Style.FrameHeight),
                $"{_editor.ModNormalizer.Step} / {_editor.ModNormalizer.TotalSteps}");
        }
        else if (ImEx.Button("重新复制文件并将模组标准化"u8, Vector2.Zero,
                     "尝试为每个游戏路径操作创建一个唯一副本并将其按[Groupname]/[Optionname]/[GamePath]排列。\n"u8
                   + "如果成功，还将删除所有未使用的文件和目录。\n"u8
                   + "注意，失败后不会破坏模组，而是应该恢复到其原始状态，但无论如何，请注意此操作有风险。"u8,
                     !_allowReduplicate && !modifier))
        {
            _editor.ModNormalizer.Normalize(Mod!);
            _editor.ModNormalizer.Worker.ContinueWith(_ => _editor.LoadMod(Mod!, _editor.GroupIdx, _editor.DataIdx), TaskScheduler.Default);
        }

        if (_allowReduplicate && !modifier)
            Im.Tooltip.OnHover($"\n\nNo duplicates detected! Hold {_config.DeleteModModifier} to force normalization anyway.");

        if (!_editor.Duplicates.Worker.IsCompleted)
            return;

        if (_editor.Duplicates.Duplicates.Count is 0)
        {
            Im.Line.New();
            Im.Text("未找到重复项。"u8);
            return;
        }

        if (Im.Button("删除并重定向重复项"u8))
            _editor.Duplicates.DeleteDuplicates(_editor.Files, _editor.Mod!, _editor.Option!, true);

        if (_editor.Duplicates.SavedSpace > 0)
        {
            Im.Line.Same();
            Im.Text($"从你的硬盘释放 {FormattingFunctions.HumanReadableSize(_editor.Duplicates.SavedSpace)} 。");
        }

        using var child = Im.Child.Begin("##duptable"u8, Im.ContentRegion.Available, true);
        if (!child)
            return;

        using var table = Im.Table.Begin("##duplicates"u8, 3, TableFlags.RowBackground | TableFlags.SizingFixedFit, Im.ContentRegion.Available);
        if (!table)
            return;

        var width = Im.Font.CalculateSize("NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN "u8).X;
        table.SetupColumn("file"u8, TableColumnFlags.WidthStretch);
        table.SetupColumn("size"u8, TableColumnFlags.WidthFixed, Im.Font.CalculateSize("NNN.NNN  "u8).X);
        table.SetupColumn("hash"u8, TableColumnFlags.WidthFixed,
            Im.Window.Width > 2 * width ? width : Im.Font.CalculateSize("NNNNNNNN... "u8).X);
        foreach (var (set, size, hash) in _editor.Duplicates.Duplicates.Where(s => s.Paths.Length > 1))
        {
            table.NextColumn();
            using var tree = Im.Tree.Node(set[0].FullName[(Mod!.ModPath.FullName.Length + 1)..],
                TreeNodeFlags.NoTreePushOnOpen);
            table.NextColumn();
            ImEx.TextRightAligned(FormattingFunctions.HumanReadableSize(size));
            table.NextColumn();
            using (var _ = Im.Font.PushMono())
            {
                if (Im.Window.Width > 2 * width)
                    ImEx.TextRightAligned(FormattingFunctions.BytewiseHex(hash));
                else
                    ImEx.TextRightAligned($"{FormattingFunctions.BytewiseHex(hash.AsSpan(4))}...");
            }

            if (!tree)
                continue;

            using var indent = Im.Indent();
            foreach (var duplicate in set.Skip(1))
            {
                table.NextColumn();
                table.SetBackgroundColor(TableBackgroundTarget.Cell, Colors.RedTableBgTint);
                Im.Tree.Leaf(duplicate.FullName.AsSpan(Mod!.ModPath.FullName.Length + 1), TreeNodeFlags.Leaf);
                table.NextColumn();
                table.SetBackgroundColor(TableBackgroundTarget.Cell, Colors.RedTableBgTint);
                table.NextColumn();
                table.SetBackgroundColor(TableBackgroundTarget.Cell, Colors.RedTableBgTint);
            }
        }
    }

    private bool DrawOptionSelectHeader()
    {
        using var style = ImStyleDouble.ItemSpacing.Push(Vector2.Zero).Push(ImStyleSingle.FrameRounding, 0);
        var       width = new Vector2(Im.ContentRegion.Available.X / 3, 0);
        var       ret   = false;
        if (ImEx.Button("默认选项"u8, width, "切换到模组的默认选项。\n这将重置未保存的更改。"u8,
                _editor.Option is DefaultSubMod))
        {
            _editor.LoadOption(-1, 0).Wait();
            ret = true;
        }

        Im.Line.Same();
        if (ImEx.Button("刷新数据"u8, width, "刷新当前选项的数据。\n这将重置未保存的更改。"u8))
        {
            _editor.LoadMod(_editor.Mod!, _editor.GroupIdx, _editor.DataIdx).Wait();
            ret = true;
        }

        Im.Line.Same();
        if (_optionSelect.Draw("##option"u8, _editor.Option?.GetFullName() ?? string.Empty, default, width.X, out var option))
        {
            _editor.LoadOption(option.GroupIndex, option.DataIndex).Wait();
            ret = true;
        }

        return ret;
    }

    private string _newSwapKey   = string.Empty;
    private string _newSwapValue = string.Empty;

    private void DrawSwapTab()
    {
        using var tab = Im.TabBar.BeginItem("文件替换"u8);
        if (!tab)
            return;

        DrawOptionSelectHeader();

        var setsEqual = !_editor.SwapEditor.Changes;
        var tt        = setsEqual ? "未暂存任何修改" : "应用当前暂存的修改到此选项。";
        Im.Line.New();
        if (ImEx.Button("应用修改"u8, Vector2.Zero, tt, setsEqual))
            _editor.SwapEditor.Apply(_editor.Option!);

        Im.Line.Same();
        tt = setsEqual ? "未暂存任何修改" : "撤销当前暂存的所有修改。";
        if (ImEx.Button("撤销修改"u8, Vector2.Zero, tt, setsEqual))
            _editor.SwapEditor.Revert(_editor.Option!);

        var otherSwaps = _editor.Mod!.TotalSwapCount - _editor.Option!.FileSwaps.Count;
        if (otherSwaps > 0)
        {
            Im.Line.Same();
            ImEx.TextFramed($"{otherSwaps} 文件替换已经在其他选项中设置过了。", Vector2.Zero,
                ColorId.RedundantAssignment.Value().Color);
        }

        using var child = Im.Child.Begin("##swaps"u8, Im.ContentRegion.Available, true);
        if (!child)
            return;

        using var table = Im.Table.Begin("##table"u8, 3, TableFlags.RowBackground, Im.ContentRegion.Available);
        if (!table)
            return;

        var idx      = 0;
        var iconSize = Im.Style.FrameHeight * Vector2.One;
        var pathSize = Im.ContentRegion.Available.X / 2 - iconSize.X;
        table.SetupColumn("button"u8, TableColumnFlags.WidthFixed, iconSize.X);
        table.SetupColumn("source"u8, TableColumnFlags.WidthFixed, pathSize);
        table.SetupColumn("value"u8,  TableColumnFlags.WidthFixed, pathSize);

        foreach (var (gamePath, file) in _editor.SwapEditor.Swaps.ToList())
        {
            using var id = Im.Id.Push(idx++);
            table.NextColumn();
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "删除此替换。"u8))
                _editor.SwapEditor.Remove(gamePath);

            table.NextColumn();
            var tmp = file.FullName;
            Im.Item.SetNextWidth(-1);
            if (Im.Input.Text("##value"u8, ref tmp, maxLength: Utf8GamePath.MaxGamePathLength) && tmp.Length > 0)
                _editor.SwapEditor.Change(gamePath, new FullPath(tmp));

            table.NextColumn();
            tmp = gamePath.Path.ToString();
            Im.Item.SetNextWidth(-1);
            if (Im.Input.Text("##key"u8, ref tmp, maxLength: Utf8GamePath.MaxGamePathLength)
             && Utf8GamePath.FromString(tmp, out var path)
             && !_editor.SwapEditor.Swaps.ContainsKey(path))
                _editor.SwapEditor.Change(gamePath, path);
        }

        table.NextColumn();
        var addable = Utf8GamePath.FromString(_newSwapKey, out var newPath)
         && newPath.Length > 0
         && _newSwapValue.Length > 0
         && _newSwapValue != _newSwapKey
         && !_editor.SwapEditor.Swaps.ContainsKey(newPath);
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, "添加一个新的文件替换到此选项。"u8, !addable))
        {
            _editor.SwapEditor.Add(newPath, new FullPath(_newSwapValue));
            _newSwapKey   = string.Empty;
            _newSwapValue = string.Empty;
        }

        table.NextColumn();
        Im.Item.SetNextWidth(-1);
        Im.Input.Text("##swapKey"u8, ref _newSwapValue, "新替换来源..."u8, maxLength: Utf8GamePath.MaxGamePathLength);
        table.NextColumn();
        Im.Item.SetNextWidth(-1);
        Im.Input.Text("##swapValue"u8, ref _newSwapKey, "... 新替换目标。"u8, maxLength: Utf8GamePath.MaxGamePathLength);
    }

    public ModEditWindow(FileDialogService fileDialog, ItemSwapTab itemSwapTab, IDataManager gameData,
        Configuration config, ModEditor editor, ResourceTreeFactory resourceTreeFactory, MetaFileManager metaFileManager,
        ActiveCollections activeCollections, ModMergeTab modMergeTab,
        CommunicatorService communicator, IDragDropManager dragDropManager,
        ResourceTreeViewerFactory resourceTreeViewerFactory, IFramework framework,
        MetaDrawers metaDrawers, MaterialEditorFactory materialEditorFactory, ModelEditorFactory modelEditorFactory,
        ShaderPackageEditorFactory shaderPackageEditorFactory, DeformerEditorFactory deformerEditorFactory,
        CombiningTextureEditorFactory textureEditorFactory, int index)
        : base(WindowBaseLabel, index)
    {
        _itemSwapTab       = itemSwapTab;
        _config            = config;
        _editor            = editor;
        _metaFileManager   = metaFileManager;
        _activeCollections = activeCollections;
        _modMergeTab       = modMergeTab;
        _communicator      = communicator;
        _dragDropManager   = dragDropManager;
        _fileDialog        = fileDialog;
        _metaDrawers       = metaDrawers;
        _overviewTable     = new OverviewTable(_editor);
        _optionSelect      = new OptionSelectCombo(editor, this);

        var fileEditingContext = new ModEditFileEditingContext(activeCollections, editor);

        _materialTab      = CreateFileEditor("材质(颜色集)", ".mtrl", ResourceType.Mtrl, materialEditorFactory);
        _modelTab         = CreateFileEditor("模型",    ".mdl",  ResourceType.Mdl,  modelEditorFactory);
        _shaderPackageTab = CreateFileEditor("着色器",   ".shpk", ResourceType.Shpk, shaderPackageEditorFactory);
        _pbdTab           = CreateFileEditor("变形器", ".pbd",  ResourceType.Pbd,  deformerEditorFactory);
#if false
        _newTextureTab = CreateFileEditor("贴图", ".tex", ResourceType.Tex, textureEditorFactory);
#endif

        _textureEditor = textureEditorFactory.CreateForModEditWindow(fileEditingContext);

        _resourceTreeFactory = resourceTreeFactory;
        _quickImportViewer   = resourceTreeViewerFactory.Create(1, OnQuickImportRefresh, DrawQuickImportActions);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ModEditWindow);

        return;

        FileEditor CreateFileEditor(string tabName, string fileType, ResourceType type, IFileEditorFactory editorFactory)
        {
            return new FileEditor(this, communicator, config, editor.Compactor, fileDialog, framework, tabName, fileType,
                () => PopulateIsOnPlayer(_editor.Files.GetByType(type), type), () => Mod?.ModPath.FullName ?? string.Empty, editorFactory,
                fileEditingContext);
        }
    }

    public void Dispose()
    {
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
        _editor.Dispose();
        _materialTab.Dispose();
        _modelTab.Dispose();
        _shaderPackageTab.Dispose();
        _textureEditor.Dispose();
        _modMergeTab.ModMerger.Dispose();
    }

    private void OnModPathChange(in ModPathChanged.Arguments arguments)
    {
        if (arguments.Mod != Mod)
            return;

        switch (arguments.Type)
        {
            case ModPathChangeType.Reloaded or ModPathChangeType.Moved:
                Mod = null;
                ChangeMod(arguments.Mod);
                break;
            case ModPathChangeType.Deleted:
                IsOpen = false;
                Dispose();
                break;
        }
    }
}
