using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using Lumina.Data;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Compression;
using OtterGui.Extensions;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.Api.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Structs;
using Penumbra.Interop.ResourceTree;
using Penumbra.Services;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public class ResourceTreeViewer(
    Configuration config,
    ResourceTreeFactory treeFactory,
    ChangedItemDrawer changedItemDrawer,
    IncognitoService incognito,
    int actionCapacity,
    Action onRefresh,
    Action<ResourceNode, IWritable?, Vector2> drawActions,
    CommunicatorService communicator,
    PcpService pcpService,
    IDataManager gameData,
    FileDialogService fileDialog,
    FileCompactor compactor)
{
    private const ResourceTreeFactory.Flags ResourceTreeFactoryFlags =
        ResourceTreeFactory.Flags.WithUiData | ResourceTreeFactory.Flags.WithOwnership;

    private readonly HashSet<nint> _unfolded = [];

    private readonly Dictionary<nint, NodeVisibility> _filterCache   = [];
    private readonly Dictionary<FullPath, IWritable?> _writableCache = [];

    private TreeCategory        _categoryFilter = AllCategories;
    private ChangedItemIconFlag _typeFilter     = ChangedItemFlagExtensions.AllFlags;
    private string              _nameFilter     = string.Empty;
    private string              _nodeFilter     = string.Empty;
    private string              _note           = string.Empty;

    private Task<ResourceTree[]>? _task;

    public void Draw()
    {
        DrawModifiedGameFilesWarning();
        DrawControls();
        _task ??= RefreshCharacterList();

        using var child = ImRaii.Child("##Data");
        if (!child)
            return;

        if (!_task.IsCompleted)
        {
            ImGui.NewLine();
            ImGui.TextUnformatted("正在计算角色列表...");
        }
        else if (_task.Exception != null)
        {
            ImGui.NewLine();
            using var color = ImRaii.PushColor(ImGuiCol.Text, Colors.RegexWarningBorder);
            ImGui.TextUnformatted($"Error during calculation of character list:\n\n{_task.Exception}");
        }
        else if (_task.IsCompletedSuccessfully)
        {
            var debugMode = config.DebugMode;
            foreach (var (tree, index) in _task.Result.WithIndex())
            {
                var category = Classify(tree);
                if (!_categoryFilter.HasFlag(category) || !tree.Name.Contains(_nameFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                using (var c = ImRaii.PushColor(ImGuiCol.Text, CategoryColor(category).Value()))
                {
                    var isOpen = ImGui.CollapsingHeader($"{(incognito.IncognitoMode ? tree.AnonymizedName : tree.Name)}###{index}",
                        index == 0 ? ImGuiTreeNodeFlags.DefaultOpen : 0);
                    if (debugMode)
                    {
                        using var _ = ImRaii.PushFont(UiBuilder.MonoFont);
                        ImGuiUtil.HoverTooltip(
                            $"Object Index:        {tree.GameObjectIndex}\nObject Address:      0x{tree.GameObjectAddress:X16}\nDraw Object Address: 0x{tree.DrawObjectAddress:X16}");
                    }

                    if (!isOpen)
                        continue;
                }

                using var id = ImRaii.PushId(index);

                ImUtf8.TextFrameAligned($"合集：{(incognito.IncognitoMode ? tree.AnonymizedCollectionName : tree.CollectionName)}");
                
                var isOtherPlayer = tree.PlayerRelated && !tree.LocalPlayerRelated;
                if (!isOtherPlayer)
                {
                    ImGui.SameLine();
                    if (ImUtf8.ButtonEx("导出角色包"u8,
                            "注意：如果角色仍然存在，这将重新计算角色的当前数据，而不会使用缓存数据。"u8))
                    {
                        pcpService.CreatePcp((ObjectIndex)tree.GameObjectIndex, _note).ContinueWith(t =>
                        {

                            var (success, text) = t.Result;

                            if (success)
                                Penumbra.Messager.NotificationMessage($"已创建 {text}。", NotificationType.Success, false);
                            else
                                Penumbra.Messager.NotificationMessage(text, NotificationType.Error, false);
                        });
                        _note = string.Empty;
                    }

                    ImUtf8.SameLineInner();
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    ImUtf8.InputText("##note"u8, ref _note, "导出备注..."u8);
                }


                using var table = ImRaii.Table("##ResourceTree", 4,
                    ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
                if (!table)
                    continue;

                ImGui.TableSetupColumn(string.Empty,  ImGuiTableColumnFlags.WidthStretch, 0.2f);
                ImGui.TableSetupColumn("游戏路径",   ImGuiTableColumnFlags.WidthStretch, 0.3f);
                ImGui.TableSetupColumn("实际路径", ImGuiTableColumnFlags.WidthStretch, 0.5f);
                ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed,
                    actionCapacity * 3 * ImGuiHelpers.GlobalScale + (actionCapacity + 1) * ImGui.GetFrameHeight());
                ImGui.TableHeadersRow();

                DrawNodes(tree.Nodes, 0, unchecked(tree.DrawObjectAddress * 31), 0);
            }
        }
    }

    private void DrawModifiedGameFilesWarning()
    {
        if (!gameData.HasModifiedGameDataFiles)
            return;

        using var style = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange);

        ImUtf8.TextWrapped(
            "Dalamud 检测到您的 FFXIV 安装目录存在被修改的游戏文件。任何通过 TexTools 安装的模组都会导致此提示。"u8);
        ImUtf8.TextWrapped("Penumbra 及部分其他插件假定您的 FFXIV 安装目录为未修改状态以正常工作。"u8);
        ImUtf8.TextWrapped(
            "由于该情况，当前显示的数据可能不准确，这可能会影响依赖这些数据的功能，例如角色包的导入/导出，或其他插件提供的模组同步功能。"u8);
        ImUtf8.TextWrapped(
            "请退出游戏，打开 XIVLauncher，点击登录旁的箭头并选择“修复游戏文件”以解决此问题。修复后，请勿再使用 TexTools 安装模组。您的插件配置和 Penumbra 启用的模组不会丢失。"u8);

        ImGui.Separator();
    }

    private void DrawControls()
    {
        var yOffset = (ChangedItemDrawer.TypeFilterIconSize.Y - ImGui.GetFrameHeight()) / 2f;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + yOffset);

        if (ImGui.Button("刷新角色列表"))
            _task = RefreshCharacterList();

        var checkSpacing = ImGui.GetStyle().ItemInnerSpacing.X;
        var checkPadding = 10 * ImGuiHelpers.GlobalScale + ImGui.GetStyle().ItemSpacing.X;
        ImGui.SameLine(0, checkPadding);

        using (var id = ImRaii.PushId("TreeCategoryFilter"))
        {
            var categoryFilter = (uint)_categoryFilter;
            foreach (var category in Enum.GetValues<TreeCategory>())
            {
                using var c = ImRaii.PushColor(ImGuiCol.CheckMark, CategoryColor(category).Value());
                ImGui.CheckboxFlags($"##{category}", ref categoryFilter, (uint)category);
                ImGuiUtil.HoverTooltip(CategoryFilterDescription(category));
                ImGui.SameLine(0.0f, checkSpacing);
            }

            _categoryFilter = (TreeCategory)categoryFilter;
        }

        ImGui.SameLine(0, checkPadding);

        var filterChanged = false;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - yOffset);
        using (ImRaii.Child("##typeFilter", new Vector2(ImGui.GetContentRegionAvail().X, ChangedItemDrawer.TypeFilterIconSize.Y)))
        {
            filterChanged |= changedItemDrawer.DrawTypeFilter(ref _typeFilter);
        }

        var fieldWidth = (ImGui.GetContentRegionAvail().X - checkSpacing * 2.0f - ImGui.GetFrameHeightWithSpacing()) / 2.0f;
        ImGui.SetNextItemWidth(fieldWidth);
        filterChanged |= ImGui.InputTextWithHint("##TreeNameFilter", "按角色/实体名称筛选...", ref _nameFilter, 128);
        ImGui.SameLine(0, checkSpacing);
        ImGui.SetNextItemWidth(fieldWidth);
        filterChanged |= ImGui.InputTextWithHint("##NodeFilter", "按物品/部件名称或路径筛选...", ref _nodeFilter, 128);
        ImGui.SameLine(0, checkSpacing);
        incognito.DrawToggle(ImGui.GetFrameHeightWithSpacing());

        if (filterChanged)
            _filterCache.Clear();
    }

    private Task<ResourceTree[]> RefreshCharacterList()
        => Task.Run(() =>
        {
            try
            {
                return treeFactory.FromObjectTable(ResourceTreeFactoryFlags)
                    .Select(entry => entry.ResourceTree)
                    .ToArray();
            }
            finally
            {
                _filterCache.Clear();
                _writableCache.Clear();
                _unfolded.Clear();
                onRefresh();
            }
        });

    private void DrawNodes(IEnumerable<ResourceNode> resourceNodes, int level, nint pathHash,
        ChangedItemIconFlag parentFilterIconFlag)
    {
        var debugMode   = config.DebugMode;
        var frameHeight = ImGui.GetFrameHeight();

        foreach (var (resourceNode, index) in resourceNodes.WithIndex())
        {
            var nodePathHash = unchecked(pathHash + resourceNode.ResourceHandle);

            var visibility = GetNodeVisibility(nodePathHash, resourceNode, parentFilterIconFlag);
            if (visibility == NodeVisibility.Hidden)
                continue;

            using var mutedColor = ImRaii.PushColor(ImGuiCol.Text, ImGuiUtil.HalfTransparentText(), resourceNode.Internal);

            var filterIcon = resourceNode.IconFlag != 0 ? resourceNode.IconFlag : parentFilterIconFlag;

            using var id = ImRaii.PushId(index);
            ImGui.TableNextColumn();
            var unfolded = _unfolded.Contains(nodePathHash);
            using (var indent = ImRaii.PushIndent(level))
            {
                var hasVisibleChildren = resourceNode.Children.Any(child
                    => GetNodeVisibility(unchecked(nodePathHash * 31 + child.ResourceHandle), child, filterIcon) != NodeVisibility.Hidden);
                var unfoldable = hasVisibleChildren && visibility != NodeVisibility.DescendentsOnly;
                if (unfoldable)
                {
                    using var font   = ImRaii.PushFont(UiBuilder.IconFont);
                    var       icon   = (unfolded ? FontAwesomeIcon.CaretDown : FontAwesomeIcon.CaretRight).ToIconString();
                    var       offset = (ImGui.GetFrameHeight() - ImGui.CalcTextSize(icon).X) / 2;
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
                    ImGui.TextUnformatted(icon);
                    ImGui.SameLine(0f, offset + ImGui.GetStyle().ItemInnerSpacing.X);
                }
                else
                {
                    if (hasVisibleChildren && !unfolded)
                    {
                        _unfolded.Add(nodePathHash);
                        unfolded = true;
                    }

                    ImGui.Dummy(new Vector2(ImGui.GetFrameHeight()));
                    ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
                }

                changedItemDrawer.DrawCategoryIcon(resourceNode.IconFlag);
                ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
                ImGui.TableHeader(resourceNode.Name);
                if (ImGui.IsItemClicked() && unfoldable)
                {
                    if (unfolded)
                        _unfolded.Remove(nodePathHash);
                    else
                        _unfolded.Add(nodePathHash);
                    unfolded = !unfolded;
                }

                if (debugMode)
                {
                    using var _ = ImRaii.PushFont(UiBuilder.MonoFont);
                    ImGuiUtil.HoverTooltip(
                        $"Resource Type:   {resourceNode.Type}\nObject Address:  0x{resourceNode.ObjectAddress:X16}\nResource Handle: 0x{resourceNode.ResourceHandle:X16}\nLength:          0x{resourceNode.Length:X16}");
                }
            }

            ImGui.TableNextColumn();
            var hasGamePaths = resourceNode.PossibleGamePaths.Length > 0;
            ImGui.Selectable(resourceNode.PossibleGamePaths.Length switch
            {
                0 => "(none)",
                1 => resourceNode.GamePath.ToString(),
                _ => "(multiple)",
            }, false, hasGamePaths ? 0 : ImGuiSelectableFlags.Disabled, new Vector2(ImGui.GetContentRegionAvail().X, frameHeight));
            if (hasGamePaths)
            {
                var allPaths = string.Join('\n', resourceNode.PossibleGamePaths);
                if (ImGui.IsItemClicked())
                    ImGui.SetClipboardText(allPaths);
                ImGuiUtil.HoverTooltip($"{allPaths}\n\n点击复制到剪贴板。");
            }

            ImGui.TableNextColumn();
            if (resourceNode.FullPath.FullName.Length > 0)
            {
                var hasMod = resourceNode.Mod.TryGetTarget(out var mod);
                if (resourceNode is { ModName: not null, ModRelativePath: not null })
                {
                    var       modName = $"[{(hasMod ? mod!.Name : resourceNode.ModName)}]";
                    var       textPos = ImGui.GetCursorPosX() + ImUtf8.CalcTextSize(modName).X + ImGui.GetStyle().ItemInnerSpacing.X;
                    using var group   = ImUtf8.Group();
                    using (var color = ImRaii.PushColor(ImGuiCol.Text, (hasMod ? ColorId.NewMod : ColorId.DisabledMod).Value()))
                    {
                        ImUtf8.Selectable(modName, false, ImGuiSelectableFlags.AllowItemOverlap,
                            new Vector2(ImGui.GetContentRegionAvail().X, frameHeight));
                    }

                    ImGui.SameLine();
                    ImGui.SetCursorPosX(textPos);
                    ImUtf8.Text(resourceNode.ModRelativePath);
                }
                else if (resourceNode.FullPath.IsRooted)
                {
                    var path                   = resourceNode.FullPath.FullName;
                    var lastDirectorySeparator = path.LastIndexOf('\\');
                    var secondLastDirectorySeparator = lastDirectorySeparator > 0
                        ? path.LastIndexOf('\\', lastDirectorySeparator - 1)
                        : -1;
                    if (secondLastDirectorySeparator >= 0)
                        path = $"…{path.AsSpan(secondLastDirectorySeparator)}";
                    ImGui.Selectable(path.AsSpan(), false, ImGuiSelectableFlags.AllowItemOverlap,
                        new Vector2(ImGui.GetContentRegionAvail().X, frameHeight));
                }
                else
                {
                    ImGui.Selectable(resourceNode.FullPath.ToPath(), false, ImGuiSelectableFlags.AllowItemOverlap,
                        new Vector2(ImGui.GetContentRegionAvail().X, frameHeight));
                }

                if (ImGui.IsItemClicked())
                    ImGui.SetClipboardText(resourceNode.FullPath.ToPath());
                if (hasMod && ImGui.IsItemClicked(ImGuiMouseButton.Right) && ImGui.GetIO().KeyCtrl)
                    communicator.SelectTab.Invoke(TabType.Mods, mod);

                ImGuiUtil.HoverTooltip(
                    $"{resourceNode.FullPath.ToPath()}\n\n点击复制到剪贴板。{(hasMod ? "\nCtrl + 右键点击跳转到模组。" : string.Empty)}{GetAdditionalDataSuffix(resourceNode.AdditionalData)}");
            }
            else
            {
                ImUtf8.Selectable(GetPathStatusLabel(resourceNode.FullPathStatus), false, ImGuiSelectableFlags.Disabled,
                    new Vector2(ImGui.GetContentRegionAvail().X, frameHeight));
                ImGuiUtil.HoverTooltip(
                    $"{GetPathStatusDescription(resourceNode.FullPathStatus)}{GetAdditionalDataSuffix(resourceNode.AdditionalData)}");
            }

            mutedColor.Dispose();

            ImGui.TableNextColumn();
            using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
                ImGui.GetStyle().ItemSpacing with { X = 3 * ImGuiHelpers.GlobalScale });
            DrawActions(resourceNode, new Vector2(frameHeight));

            if (unfolded)
                DrawNodes(resourceNode.Children, level + 1, unchecked(nodePathHash * 31), filterIcon);
        }

        return;

        string GetAdditionalDataSuffix(CiByteString data)
            => !debugMode || data.IsEmpty ? string.Empty : $"\n\nAdditional Data: {data}";

        NodeVisibility GetNodeVisibility(nint nodePathHash, ResourceNode node, ChangedItemIconFlag parentFilterIcon)
        {
            if (!_filterCache.TryGetValue(nodePathHash, out var visibility))
            {
                visibility = CalculateNodeVisibility(nodePathHash, node, parentFilterIcon);
                _filterCache.Add(nodePathHash, visibility);
            }

            return visibility;
        }

        NodeVisibility CalculateNodeVisibility(nint nodePathHash, ResourceNode node, ChangedItemIconFlag parentFilterIcon)
        {
            if (node.Internal && !debugMode)
                return NodeVisibility.Hidden;

            var filterIcon = node.IconFlag != 0 ? node.IconFlag : parentFilterIcon;
            if (MatchesFilter(node, filterIcon))
                return NodeVisibility.Visible;

            foreach (var child in node.Children)
            {
                if (GetNodeVisibility(unchecked(nodePathHash * 31 + child.ResourceHandle), child, filterIcon) != NodeVisibility.Hidden)
                    return NodeVisibility.DescendentsOnly;
            }

            return NodeVisibility.Hidden;
        }

        bool MatchesFilter(ResourceNode node, ChangedItemIconFlag filterIcon)
        {
            if (!_typeFilter.HasFlag(filterIcon))
                return false;

            if (_nodeFilter.Length == 0)
                return true;

            return node.Name != null && node.Name.Contains(_nodeFilter, StringComparison.OrdinalIgnoreCase)
             || node.FullPath.FullName.Contains(_nodeFilter, StringComparison.OrdinalIgnoreCase)
             || node.FullPath.InternalName.ToString().Contains(_nodeFilter, StringComparison.OrdinalIgnoreCase)
             || Array.Exists(node.PossibleGamePaths, path => path.Path.ToString().Contains(_nodeFilter, StringComparison.OrdinalIgnoreCase));
        }

        void DrawActions(ResourceNode resourceNode, Vector2 buttonSize)
        {
            if (!_writableCache!.TryGetValue(resourceNode.FullPath, out var writable))
            {
                var path = resourceNode.FullPath.ToPath();
                if (resourceNode.FullPath.IsRooted)
                {
                    writable = new RawFileWritable(path);
                }
                else
                {
                    var file = gameData.GetFile(path);
                    writable = file is null ? null : new RawGameFileWritable(file);
                }

                _writableCache.Add(resourceNode.FullPath, writable);
            }
            
            if (ImUtf8.IconButton(FontAwesomeIcon.Save, "Export this file."u8, buttonSize,
                    resourceNode.FullPath.FullName.Length is 0 || writable is null))
            {
                var fullPathStr = resourceNode.FullPath.FullName;
                var ext = resourceNode.PossibleGamePaths.Length == 1
                    ? Path.GetExtension(resourceNode.GamePath.ToString())
                    : Path.GetExtension(fullPathStr);
                fileDialog.OpenSavePicker($"Export {Path.GetFileName(fullPathStr)} to...", ext, Path.GetFileNameWithoutExtension(fullPathStr), ext,
                    (success, name) =>
                    {
                        if (!success)
                            return;

                        try
                        {
                            compactor.WriteAllBytes(name, writable!.Write());
                        }
                        catch (Exception e)
                        {
                            Penumbra.Log.Error($"Could not export {fullPathStr}:\n{e}");
                        }
                    }, null, false);
            }
            
            drawActions(resourceNode, writable, new Vector2(frameHeight));
        }
    }

    private static ReadOnlySpan<byte> GetPathStatusLabel(ResourceNode.PathStatus status)
        => status switch
        {
            ResourceNode.PathStatus.External    => "(由外部工具管理)"u8,
            ResourceNode.PathStatus.NonExistent => "(未找到)"u8,
            _                                   => "(不可用)"u8,
        };

    private static string GetPathStatusDescription(ResourceNode.PathStatus status)
        => status switch
        {
            ResourceNode.PathStatus.External => "该文件的实际路径不可用，因为它由外部工具管理。",
            ResourceNode.PathStatus.NonExistent =>
                "该文件的实际路径不可用，因为它在加载后可能已被移动或删除。",
            _ => "该文件的实际路径不可用。",
        };

    [Flags]
    private enum TreeCategory : uint
    {
        LocalPlayer  = 1,
        Player       = 2,
        Networked    = 4,
        NonNetworked = 8,
    }

    private const TreeCategory AllCategories = (TreeCategory)(((uint)TreeCategory.NonNetworked << 1) - 1);

    private static TreeCategory Classify(ResourceTree tree)
        => tree.LocalPlayerRelated ? TreeCategory.LocalPlayer :
            tree.PlayerRelated     ? TreeCategory.Player :
            tree.Networked         ? TreeCategory.Networked :
                                     TreeCategory.NonNetworked;

    private static ColorId CategoryColor(TreeCategory category)
        => category switch
        {
            TreeCategory.LocalPlayer  => ColorId.ResTreeLocalPlayer,
            TreeCategory.Player       => ColorId.ResTreePlayer,
            TreeCategory.Networked    => ColorId.ResTreeNetworked,
            TreeCategory.NonNetworked => ColorId.ResTreeNonNetworked,
            _                         => throw new ArgumentException(),
        };

    private static string CategoryFilterDescription(TreeCategory category)
        => category switch
        {
            TreeCategory.LocalPlayer  => "显示你和从属于你的对象（坐骑、宠物、时尚配饰、战斗伙伴等等）。",
            TreeCategory.Player       => "显示其他玩家和从属于他们的对象",
            TreeCategory.Networked    => "显示由游戏服务器处理的NPC对象。",
            TreeCategory.NonNetworked => "显示由本地处理的NPC对象。",
            _                         => throw new ArgumentException(),
        };

    [Flags]
    private enum NodeVisibility : uint
    {
        Hidden          = 0,
        Visible         = 1,
        DescendentsOnly = 2,
    }
    
    private record RawFileWritable(string Path) : IWritable
    {
        public bool Valid
            => true;

        public byte[] Write()
            => File.ReadAllBytes(Path);
    }

    private record RawGameFileWritable(FileResource FileResource) : IWritable
    {
        public bool Valid
            => true;

        public byte[] Write()
            => FileResource.Data;
    }
}
