﻿using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.DragDrop;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Api.Enums;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.Import.Models;
using Penumbra.Import.Textures;
using Penumbra.Interop.Hooks.Objects;
using Penumbra.Interop.ResourceTree;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Subclasses;
using Penumbra.Services;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;
using Penumbra.Util;
using MdlMaterialEditor = Penumbra.Mods.Editor.MdlMaterialEditor;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow : Window, IDisposable
{
    private const string WindowBaseLabel = "###SubModEdit";

    private readonly PerformanceTracker      _performance;
    private readonly ModEditor               _editor;
    private readonly Configuration           _config;
    private readonly ItemSwapTab             _itemSwapTab;
    private readonly MetaFileManager         _metaFileManager;
    private readonly ActiveCollections       _activeCollections;
    private readonly StainService            _stainService;
    private readonly ModMergeTab             _modMergeTab;
    private readonly CommunicatorService     _communicator;
    private readonly IDragDropManager        _dragDropManager;
    private readonly IDataManager            _gameData;
    private readonly IFramework              _framework;
    private readonly IObjectTable            _objects;
    private readonly CharacterBaseDestructor _characterBaseDestructor;

    private Vector2 _iconSize = Vector2.Zero;
    private bool    _allowReduplicate;

    public Mod? Mod { get; private set; }

    public void ChangeMod(Mod mod)
    {
        if (mod == Mod)
            return;

        _editor.LoadMod(mod, -1, 0);
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
        _itemSwapTab.UpdateMod(mod, _activeCollections.Current[mod.Index].Settings);
        UpdateModels();
        _forceTextureStartPath = true;
    }

    public void ChangeOption(SubMod? subMod)
        => _editor.LoadOption(subMod?.GroupIdx ?? -1, subMod?.OptionIdx ?? 0);

    public void UpdateModels()
    {
        if (Mod != null)
            _editor.MdlMaterialEditor.ScanModels(Mod);
    }

    public override bool DrawConditions()
        => Mod != null;

    public override void PreDraw()
    {
        using var performance = _performance.Measure(PerformanceType.UiAdvancedWindow);

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
        var swaps = Mod!.AllSubMods.Sum(m =>
        {
            ++subMods;
            manipulations += m.Manipulations.Count;
            return m.FileSwaps.Count;
        });
        sb.Append(Mod!.Name);
        if (subMods > 1)
            sb.Append($"   |   {subMods} 选项");

        if (size > 0)
            sb.Append($"   |   {_editor.Files.Available.Count} 文件 ({Functions.HumanReadableSize(size)})");

        if (unused > 0)
            sb.Append($"   |   {unused} 未使用的文件");

        if (_editor.Files.Missing.Count > 0)
            sb.Append($"   |   {_editor.Files.Available.Count} 丢失的文件");

        if (redirections > 0)
            sb.Append($"   |   {redirections} 重定向");

        if (manipulations > 0)
            sb.Append($"   |   {manipulations} 元数据操作");

        if (swaps > 0)
            sb.Append($"   |   {swaps} 替换");

        _allowReduplicate = redirections != _editor.Files.Available.Count || _editor.Files.Missing.Count > 0 || unused > 0;
        sb.Append(WindowBaseLabel);
        WindowName = sb.ToString();
    }

    public override void OnClose()
    {
        _left.Dispose();
        _right.Dispose();
        _materialTab.Reset();
        _modelTab.Reset();
        _shaderPackageTab.Reset();
        _config.Ephemeral.AdvancedEditingOpen = false;
        _config.Ephemeral.Save();
    }

    public override void Draw()
    {
        using var performance = _performance.Measure(PerformanceType.UiAdvancedWindow);

        if (!_config.Ephemeral.AdvancedEditingOpen)
        {
            _config.Ephemeral.AdvancedEditingOpen = true;
            _config.Ephemeral.Save();
        }

        using var tabBar = ImRaii.TabBar("##tabs");
        if (!tabBar)
            return;

        _iconSize = new Vector2(ImGui.GetFrameHeight());
        DrawFileTab();
        DrawMetaTab();
        DrawSwapTab();
        _modMergeTab.Draw();
        DrawDuplicatesTab();
        DrawMaterialReassignmentTab();
        DrawQuickImportTab();
        _modelTab.Draw();
        _materialTab.Draw();
        DrawTextureTab();
        _shaderPackageTab.Draw();
        using (var tab = ImRaii.TabItem("道具转换"))
        {
            if (tab)
                _itemSwapTab.DrawContent();
        }

        DrawMissingFilesTab();
    }

    /// <summary> A row of three buttonSizes and a help marker that can be used for material suffix changing. </summary>
    private static class MaterialSuffix
    {
        private static string     _materialSuffixFrom = string.Empty;
        private static string     _materialSuffixTo   = string.Empty;
        private static GenderRace _raceCode           = GenderRace.Unknown;

        private static string RaceCodeName(GenderRace raceCode)
        {
            if (raceCode == GenderRace.Unknown)
                return "所有种族和性别";

            var (gender, race) = raceCode.Split();
            return $"({raceCode.ToRaceCode()}) {race.ToName()} {gender.ToName()} ";
        }

        private static void DrawRaceCodeCombo(Vector2 buttonSize)
        {
            ImGui.SetNextItemWidth(buttonSize.X);
            using var combo = ImRaii.Combo("##RaceCode", RaceCodeName(_raceCode));
            if (!combo)
                return;

            foreach (var raceCode in Enum.GetValues<GenderRace>())
            {
                if (ImGui.Selectable(RaceCodeName(raceCode), _raceCode == raceCode))
                    _raceCode = raceCode;
            }
        }

        public static void Draw(ModEditor editor, Vector2 buttonSize)
        {
            DrawRaceCodeCombo(buttonSize);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(buttonSize.X);
            ImGui.InputTextWithHint("##suffixFrom", "将此后缀...", ref _materialSuffixFrom, 32);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(buttonSize.X);
            ImGui.InputTextWithHint("##suffixTo", "改为...", ref _materialSuffixTo, 32);
            ImGui.SameLine();
            var disabled = !MdlMaterialEditor.ValidString(_materialSuffixTo);
            var tt = _materialSuffixTo.Length == 0
                ? "请输入目标后缀。"
                : _materialSuffixFrom == _materialSuffixTo
                    ? "原后缀与新后缀不能相同。"
                    : disabled
                        ? "此后缀无效。"
                        : _materialSuffixFrom.Length == 0
                            ? _raceCode == GenderRace.Unknown
                                ? "将所有皮肤材质替换为目标材质。"
                                : "将指定种族的皮肤材质替换为目标材质。"
                            : _raceCode == GenderRace.Unknown
                                ? $"将所有皮肤材质的后缀从 '{_materialSuffixFrom}' 改为 '{_materialSuffixTo}'."
                                : $"将指定种族的皮肤材质的后缀从 '{_materialSuffixFrom}' 改为 '{_materialSuffixTo}'.";
            if( ImGuiUtil.DrawDisabledButton( "修改材质后缀", buttonSize, tt, disabled ) )
                editor.MdlMaterialEditor.ReplaceAllMaterials(_materialSuffixTo, _materialSuffixFrom, _raceCode);

            var anyChanges = editor.MdlMaterialEditor.ModelFiles.Any(m => m.Changed);
            if( ImGuiUtil.DrawDisabledButton( "保存所有修改", buttonSize,
                   anyChanges ? "不可逆地重写当前应用于模型文件的所有修改。" : "还未做任何修改。", !anyChanges ) )
                editor.MdlMaterialEditor.SaveAllModels(editor.Compactor);

            ImGui.SameLine();
            if( ImGuiUtil.DrawDisabledButton( "撤销所有修改", buttonSize,
                   anyChanges ? "撤销当前进行的和未保存的所有修改。" : "你还未做任何修改。", !anyChanges ) )
                editor.MdlMaterialEditor.RestoreAllModels();

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "模型文件已经调用了它们应该使用的皮肤材质。皮肤材质一般都是同一种。不过mod作者们可能会采用不同的材质来区分体型。\n"
              + "此选项允许你将所有模型文件的一个后缀修改为另一个后缀，比如将所有的后缀b改为bibo。这会修改文件，因此请注意此操作有风险。\n"
              + "如果你不知道这个模组当前使用的后缀是什么，你可以将'将此后缀...'留空，它会将所有后缀替换为'改为'里面的内容，而不仅仅是匹配的后缀。\n" );
        }
    }

    private void DrawMissingFilesTab()
    {
        if (_editor.Files.Missing.Count == 0)
            return;

        using var tab = ImRaii.TabItem( "丢失的文件" );
        if (!tab)
            return;

        ImGui.NewLine();
        if( ImGui.Button( "从模组中删除丢失的文件" ) )
            _editor.FileEditor.RemoveMissingPaths(Mod!, _editor.Option!);

        using var child = ImRaii.Child("##unusedFiles", -Vector2.One, true);
        if (!child)
            return;

        using var table = ImRaii.Table("##missingFiles", 1, ImGuiTableFlags.RowBg, -Vector2.One);
        if (!table)
            return;

        foreach (var path in _editor.Files.Missing)
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(path.FullName);
        }
    }

    private void DrawDuplicatesTab()
    {
        using var tab = ImRaii.TabItem( "去重" );
        if (!tab)
            return;

        if (_editor.Duplicates.Worker.IsCompleted)
        {
            if (ImGuiUtil.DrawDisabledButton("查找重复项", Vector2.Zero,
                    "在这个模组中搜索相同的文件，这可能需要花上一段时间。", false))
                _editor.Duplicates.StartDuplicateCheck(_editor.Files.Available);
        }
        else
        {
            if (ImGuiUtil.DrawDisabledButton("取消查找重复项", Vector2.Zero, "取消当前查找操作...", false))
                _editor.Duplicates.Clear();
        }

        const string desc =
            "尝试为每个游戏路径操作创建一个唯一副本并将其按[Groupname]/[Optionname]/[GamePath]排列。\n"
          + "如果成功，还将删除所有未使用的文件和目录。\n"
          + "注意，失败后不会破坏模组，而是应该恢复到其原始状态，但无论如何，请注意此操作有风险。";

        var modifier = _config.DeleteModModifier.IsActive();

        var tt = _allowReduplicate ? desc :
            modifier ? desc : desc + $"\n\n没有检查到重复项！按住{_config.DeleteModModifier}来强制标准化。";

        if (_editor.ModNormalizer.Running)
        {
            ImGui.ProgressBar((float)_editor.ModNormalizer.Step / _editor.ModNormalizer.TotalSteps,
                new Vector2(300 * UiHelpers.Scale, ImGui.GetFrameHeight()),
                $"{_editor.ModNormalizer.Step} / {_editor.ModNormalizer.TotalSteps}");
        }
        else if (ImGuiUtil.DrawDisabledButton("重新复制文件并将模组标准化", Vector2.Zero, tt, !_allowReduplicate && !modifier))
        {
            _editor.ModNormalizer.Normalize(Mod!);
            _editor.ModNormalizer.Worker.ContinueWith(_ => _editor.LoadMod(Mod!, _editor.GroupIdx, _editor.OptionIdx));
        }

        if (!_editor.Duplicates.Worker.IsCompleted)
            return;

        if (_editor.Duplicates.Duplicates.Count == 0)
        {
            ImGui.NewLine();
            ImGui.TextUnformatted( "未找到重复项。" );
            return;
        }

        if (ImGui.Button("删除并重定向重复项"))
            _editor.Duplicates.DeleteDuplicates(_editor.Files, _editor.Mod!, _editor.Option!, true);

        if (_editor.Duplicates.SavedSpace > 0)
        {
            ImGui.SameLine();
            ImGui.TextUnformatted($"从你的硬盘释放 {Functions.HumanReadableSize(_editor.Duplicates.SavedSpace)} 。");
        }

        using var child = ImRaii.Child("##duptable", -Vector2.One, true);
        if (!child)
            return;

        using var table = ImRaii.Table("##duplicates", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit, -Vector2.One);
        if (!table)
            return;

        var width = ImGui.CalcTextSize("NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN ").X;
        ImGui.TableSetupColumn("file", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("size", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("NNN.NNN  ").X);
        ImGui.TableSetupColumn("hash", ImGuiTableColumnFlags.WidthFixed,
            ImGui.GetWindowWidth() > 2 * width ? width : ImGui.CalcTextSize("NNNNNNNN... ").X);
        foreach (var (set, size, hash) in _editor.Duplicates.Duplicates.Where(s => s.Paths.Length > 1))
        {
            ImGui.TableNextColumn();
            using var tree = ImRaii.TreeNode(set[0].FullName[(Mod!.ModPath.FullName.Length + 1)..],
                ImGuiTreeNodeFlags.NoTreePushOnOpen);
            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(Functions.HumanReadableSize(size));
            ImGui.TableNextColumn();
            using (var _ = ImRaii.PushFont(UiBuilder.MonoFont))
            {
                if (ImGui.GetWindowWidth() > 2 * width)
                    ImGuiUtil.RightAlign(string.Concat(hash.Select(b => b.ToString("X2"))));
                else
                    ImGuiUtil.RightAlign(string.Concat(hash.Take(4).Select(b => b.ToString("X2"))) + "...");
            }

            if (!tree)
                continue;

            using var indent = ImRaii.PushIndent();
            foreach (var duplicate in set.Skip(1))
            {
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, Colors.RedTableBgTint);
                using var node = ImRaii.TreeNode(duplicate.FullName[(Mod!.ModPath.FullName.Length + 1)..], ImGuiTreeNodeFlags.Leaf);
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, Colors.RedTableBgTint);
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, Colors.RedTableBgTint);
            }
        }
    }

    private bool DrawOptionSelectHeader()
    {
        const string defaultOption = "默认选项";
        using var    style         = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero).Push(ImGuiStyleVar.FrameRounding, 0);
        var          width         = new Vector2(ImGui.GetContentRegionAvail().X / 3, 0);
        var          ret           = false;
        if (ImGuiUtil.DrawDisabledButton(defaultOption, width, "切换到模组的默认选项。\n这将重置未保存的更改。",
                _editor.Option!.IsDefault))
        {
            _editor.LoadOption(-1, 0);
            ret = true;
        }

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton( "刷新数据", width, "刷新当前选项的数据。\n这将重置未保存的更改。", false))
        {
            _editor.LoadMod(_editor.Mod!, _editor.GroupIdx, _editor.OptionIdx);
            ret = true;
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(width.X);
        style.Push(ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale);
        using var color = ImRaii.PushColor(ImGuiCol.Border, ColorId.FolderLine.Value());
        using var combo = ImRaii.Combo("##optionSelector", _editor.Option.FullName);
        if (!combo)
            return ret;

        foreach (var (option, idx) in Mod!.AllSubMods.WithIndex())
        {
            using var id = ImRaii.PushId(idx);
            if (ImGui.Selectable(option.FullName, option == _editor.Option))
            {
                _editor.LoadOption(option.GroupIdx, option.OptionIdx);
                ret = true;
            }
        }

        return ret;
    }

    private string _newSwapKey   = string.Empty;
    private string _newSwapValue = string.Empty;

    private void DrawSwapTab()
    {
        using var tab = ImRaii.TabItem( "文件替换" );
        if (!tab)
            return;

        DrawOptionSelectHeader();

        var setsEqual = !_editor!.SwapEditor.Changes;
        var tt        = setsEqual ? "未暂存任何修改" : "应用当前暂存的修改到此选项。";
        ImGui.NewLine();
        if( ImGuiUtil.DrawDisabledButton( "应用修改", Vector2.Zero, tt, setsEqual ) )
            _editor.SwapEditor.Apply(_editor.Mod!, _editor.GroupIdx, _editor.OptionIdx);

        ImGui.SameLine();
        tt = setsEqual ? "未暂存任何修改" : "撤销当前暂存的所有修改。";
        if( ImGuiUtil.DrawDisabledButton( "撤销修改", Vector2.Zero, tt, setsEqual ) )
            _editor.SwapEditor.Revert(_editor.Option!);

        var otherSwaps = _editor.Mod!.TotalSwapCount - _editor.Option!.FileSwaps.Count;
        if (otherSwaps > 0)
        {
            ImGui.SameLine();
            ImGuiUtil.DrawTextButton($"{otherSwaps} 文件替换已经在其他选项中设置过了。", Vector2.Zero,
                ColorId.RedundantAssignment.Value());
        }

        using var child = ImRaii.Child("##swaps", -Vector2.One, true);
        if (!child)
            return;

        using var list = ImRaii.Table("##table", 3, ImGuiTableFlags.RowBg, -Vector2.One);
        if (!list)
            return;

        var idx      = 0;
        var iconSize = ImGui.GetFrameHeight() * Vector2.One;
        var pathSize = ImGui.GetContentRegionAvail().X / 2 - iconSize.X;
        ImGui.TableSetupColumn("button", ImGuiTableColumnFlags.WidthFixed, iconSize.X);
        ImGui.TableSetupColumn("source", ImGuiTableColumnFlags.WidthFixed, pathSize);
        ImGui.TableSetupColumn("value",  ImGuiTableColumnFlags.WidthFixed, pathSize);

        foreach (var (gamePath, file) in _editor.SwapEditor.Swaps.ToList())
        {
            using var id = ImRaii.PushId(idx++);
            ImGui.TableNextColumn();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), iconSize, "Delete this swap.", false, true))
                _editor.SwapEditor.Remove(gamePath);

            ImGui.TableNextColumn();
            var tmp = file.FullName;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##value", ref tmp, Utf8GamePath.MaxGamePathLength) && tmp.Length > 0)
                _editor.SwapEditor.Change(gamePath, new FullPath(tmp));

            ImGui.TableNextColumn();
            tmp = gamePath.Path.ToString();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##key", ref tmp, Utf8GamePath.MaxGamePathLength)
             && Utf8GamePath.FromString(tmp, out var path)
             && !_editor.SwapEditor.Swaps.ContainsKey(path))
                _editor.SwapEditor.Change(gamePath, path);
        }

        ImGui.TableNextColumn();
        var addable = Utf8GamePath.FromString(_newSwapKey, out var newPath)
         && newPath.Length > 0
         && _newSwapValue.Length > 0
         && _newSwapValue != _newSwapKey
         && !_editor.SwapEditor.Swaps.ContainsKey(newPath);
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, "Add a new file swap to this option.", !addable,
                true))
        {
            _editor.SwapEditor.Add(newPath, new FullPath(_newSwapValue));
            _newSwapKey   = string.Empty;
            _newSwapValue = string.Empty;
        }

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##swapKey", "新替换来源...", ref _newSwapValue, Utf8GamePath.MaxGamePathLength);
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##swapValue", "... 新替换目标。", ref _newSwapKey, Utf8GamePath.MaxGamePathLength);
    }

    /// <summary>
    /// Find the best matching associated file for a given path.
    /// </summary>
    /// <remarks>
    /// Tries to resolve from the current collection first and chooses the currently resolved file if any exists.
    /// If none exists, goes through all options in the currently selected mod (if any) in order of priority and resolves in them. 
    /// If no redirection is found in either of those options, returns the original path.
    /// </remarks>
    private FullPath FindBestMatch(Utf8GamePath path)
    {
        var currentFile = _activeCollections.Current.ResolvePath(path);
        if (currentFile != null)
            return currentFile.Value;

        if (Mod != null)
            foreach (var option in Mod.Groups.OrderByDescending(g => g.Priority)
                         .SelectMany(g => g.WithIndex().OrderByDescending(o => g.OptionPriority(o.Index)).Select(g => g.Value))
                         .Append(Mod.Default))
            {
                if (option.Files.TryGetValue(path, out var value) || option.FileSwaps.TryGetValue(path, out value))
                    return value;
            }

        return new FullPath(path);
    }

    private HashSet<Utf8GamePath> FindPathsStartingWith(ByteString prefix)
    {
        var ret = new HashSet<Utf8GamePath>();

        foreach (var path in _activeCollections.Current.ResolvedFiles.Keys)
        {
            if (path.Path.StartsWith(prefix))
                ret.Add(path);
        }

        if (Mod != null)
            foreach (var option in Mod.Groups.SelectMany(g => g).Append(Mod.Default))
            {
                foreach (var path in option.Files.Keys)
                {
                    if (path.Path.StartsWith(prefix))
                        ret.Add(path);
                }
            }

        return ret;
    }

    public ModEditWindow(PerformanceTracker performance, FileDialogService fileDialog, ItemSwapTab itemSwapTab, IDataManager gameData,
        Configuration config, ModEditor editor, ResourceTreeFactory resourceTreeFactory, MetaFileManager metaFileManager,
        StainService stainService, ActiveCollections activeCollections, ModMergeTab modMergeTab,
        CommunicatorService communicator, TextureManager textures, ModelManager models, IDragDropManager dragDropManager,
        ChangedItemDrawer changedItemDrawer, IObjectTable objects, IFramework framework, CharacterBaseDestructor characterBaseDestructor)
        : base(WindowBaseLabel)
    {
        _performance             = performance;
        _itemSwapTab             = itemSwapTab;
        _gameData                = gameData;
        _config                  = config;
        _editor                  = editor;
        _metaFileManager         = metaFileManager;
        _stainService            = stainService;
        _activeCollections       = activeCollections;
        _modMergeTab             = modMergeTab;
        _communicator            = communicator;
        _dragDropManager         = dragDropManager;
        _textures                = textures;
        _models                  = models;
        _fileDialog              = fileDialog;
        _objects                 = objects;
        _framework               = framework;
        _characterBaseDestructor = characterBaseDestructor;
        _materialTab = new FileEditor<MtrlTab>(this, _communicator, gameData, config, _editor.Compactor, _fileDialog, "材质(颜色集)", ".mtrl",
            () => PopulateIsOnPlayer(_editor.Files.Mtrl, ResourceType.Mtrl), DrawMaterialPanel, () => Mod?.ModPath.FullName ?? string.Empty,
            (bytes, path, writable) => new MtrlTab(this, new MtrlFile(bytes), path, writable));
        _modelTab = new FileEditor<MdlTab>(this, _communicator, gameData, config, _editor.Compactor, _fileDialog, "模型", ".mdl",
            () => PopulateIsOnPlayer(_editor.Files.Mdl, ResourceType.Mdl), DrawModelPanel, () => Mod?.ModPath.FullName ?? string.Empty,
            (bytes, path, _) => new MdlTab(this, bytes, path));
        _shaderPackageTab = new FileEditor<ShpkTab>(this, _communicator, gameData, config, _editor.Compactor, _fileDialog, "着色器", ".shpk",
            () => PopulateIsOnPlayer(_editor.Files.Shpk, ResourceType.Shpk), DrawShaderPackagePanel,
            () => Mod?.ModPath.FullName ?? string.Empty,
            (bytes, _, _) => new ShpkTab(_fileDialog, bytes));
        _center              = new CombinedTexture(_left, _right);
        _textureSelectCombo  = new TextureDrawer.PathSelectCombo(textures, editor, () => GetPlayerResourcesOfType(ResourceType.Tex));
        _resourceTreeFactory = resourceTreeFactory;
        _quickImportViewer =
            new ResourceTreeViewer(_config, resourceTreeFactory, changedItemDrawer, 2, OnQuickImportRefresh, DrawQuickImportActions);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ModEditWindow);
        IsOpen = _config is { OpenWindowAtStart: true, Ephemeral.AdvancedEditingOpen: true };
    }

    public void Dispose()
    {
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
        _editor?.Dispose();
        _materialTab.Dispose();
        _modelTab.Dispose();
        _shaderPackageTab.Dispose();
        _left.Dispose();
        _right.Dispose();
        _center.Dispose();
    }

    private void OnModPathChange(ModPathChangeType type, Mod mod, DirectoryInfo? _1, DirectoryInfo? _2)
    {
        if (type is not (ModPathChangeType.Reloaded or ModPathChangeType.Moved) || mod != Mod)
            return;

        Mod = null;
        ChangeMod(mod);
    }
}
