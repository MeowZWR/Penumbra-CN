using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using ImSharp;
using Lumina.Data;
using Luna;
using Penumbra.Communication;
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
    PcpService pcpService,
    IDataManager gameData,
    FileDialogService fileDialog,
    FileCompactor compactor,
    UiNavigator navigator)
{
    private const ResourceTreeFactory.Flags ResourceTreeFactoryFlags =
        ResourceTreeFactory.Flags.WithUiData | ResourceTreeFactory.Flags.WithOwnership;

    private readonly HashSet<nint> _unfolded = [];

    private readonly Dictionary<nint, NodeVisibility> _filterCache   = [];
    private readonly Dictionary<FullPath, IWritable?> _writableCache = [];

    private TreeCategory _categoryFilter = AllCategories;

    private string _note = string.Empty;

    private Task<ResourceTree[]>? _task;

    public void Draw()
    {
        DrawModifiedGameFilesWarning();
        DrawControls();
        _task ??= RefreshCharacterList();

        using var child = Im.Child.Begin("##Data"u8);
        if (!child)
            return;

        if (!_task.IsCompleted)
        {
            Im.Line.New();
            Im.Text("正在计算角色列表..."u8);
        }
        else if (_task.Exception != null)
        {
            Im.Line.New();
            Im.Text($"计算角色列表时出错：\n\n{_task.Exception}", Colors.RegexWarningBorder);
        }
        else if (_task.IsCompletedSuccessfully)
        {
            var debugMode = config.DebugMode;
            foreach (var (index, tree) in _task.Result.Index())
            {
                var category = Classify(tree);
                if (!_categoryFilter.HasFlag(category) || !tree.Name.Contains(config.Filters.OnScreenCharacterFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                using (ImGuiColor.Text.Push(CategoryColor(category).Value()))
                {
                    var isOpen = Im.Tree.Header($"{(incognito.IncognitoMode ? tree.AnonymizedName : tree.Name)}###{index}",
                        index is 0 ? TreeNodeFlags.DefaultOpen : 0);
                    if (debugMode)
                        HeaderInteraction(tree);

                    if (!isOpen)
                        continue;
                }

                using var id = Im.Id.Push(index);

                ImEx.TextFrameAligned($"合集：{(incognito.IncognitoMode ? tree.AnonymizedCollectionName : tree.CollectionName)}");

                var isOtherPlayer = tree.PlayerRelated && !tree.LocalPlayerRelated;
                if (!isOtherPlayer)
                {
                    Im.Line.Same();
                    if (ImEx.Button("导出角色包"u8,
                            "注意：如果角色仍然存在，这将重新计算角色的当前数据，而不会使用缓存数据。"u8))
                    {
                        pcpService.CreatePcp((ObjectIndex)tree.GameObjectIndex, null, _note).ContinueWith(t =>
                        {
                            var (success, text) = t.Result;

                            if (success)
                                Penumbra.Messager.NotificationMessage($"已创建 {text}。", NotificationType.Success, false);
                            else
                                Penumbra.Messager.NotificationMessage(text, NotificationType.Error, false);
                        });
                        _note = string.Empty;
                    }

                    Im.Line.SameInner();
                    if (ImEx.Button("导出到..."u8,
                            "注意：如果角色仍然存在，这将重新计算角色的当前数据，而不会使用缓存数据。"u8))
                        fileDialog.OpenSavePicker("导出角色包...",
                            $"Penumbra Mod Packs{{.pcp,.pmp}},{config.PcpSettings.PcpExtension},Any File{{.*}}",
                            PcpService.ModName(tree.Name, _note, DateTime.Now),
                            config.PcpSettings.PcpExtension,
                            (selected, path) =>
                            {
                                if (!selected)
                                    return;

                                pcpService.CreatePcp((ObjectIndex)tree.GameObjectIndex, path, _note).ContinueWith(t =>
                                {
                                    var (success, text) = t.Result;

                                    if (success)
                                        Penumbra.Messager.NotificationMessage($"已创建 {text}。", NotificationType.Success, false);
                                    else
                                        Penumbra.Messager.NotificationMessage(text, NotificationType.Error, false);
                                });
                                _note = string.Empty;
                            }, config.ExportDirectory, false);
                    Im.Line.SameInner();
                    Im.Item.SetNextWidth(Im.ContentRegion.Available.X);
                    Im.Input.Text("##note"u8, ref _note, "导出备注..."u8);
                }

                using var table = Im.Table.Begin("##ResourceTree"u8, 4,
                    TableFlags.SizingFixedFit | TableFlags.RowBackground);
                if (!table)
                    continue;

                table.SetupColumn(StringU8.Empty,  TableColumnFlags.WidthStretch, 0.2f);
                table.SetupColumn("游戏路径"u8,   TableColumnFlags.WidthStretch, 0.3f);
                table.SetupColumn("实际路径"u8, TableColumnFlags.WidthStretch, 0.5f);
                table.SetupColumn(StringU8.Empty, TableColumnFlags.WidthFixed,
                    actionCapacity * 3 * Im.Style.GlobalScale + (actionCapacity + 1) * Im.Style.FrameHeight);
                table.HeaderRow();

                DrawNodes(table, tree.Nodes, 0, unchecked(tree.DrawObjectAddress * 31), 0);
            }
        }
    }

    private void DrawModifiedGameFilesWarning()
    {
        if (!gameData.HasModifiedGameDataFiles)
            return;

        using var style = ImGuiColor.Text.Push(ImGuiColors.DalamudOrange);

        Im.TextWrapped(
            "Dalamud 检测到您的 FFXIV 安装目录存在被修改的游戏文件。任何通过 TexTools 安装的模组都会导致此提示。"u8);
        Im.TextWrapped("Penumbra 及部分其他插件假定您的 FFXIV 安装目录为未修改状态以正常工作。"u8);
        Im.TextWrapped(
            "当前显示的数据可能不准确，这可能会影响依赖这些数据的功能，例如角色包的导入/导出，或其他插件提供的模组同步功能。"u8);
        Im.TextWrapped(
            "请退出游戏，打开 XIVLauncher，点击登录旁的箭头并选择“修复游戏文件”以解决此问题。修复后，请勿再使用 TexTools 安装模组。您的插件配置和 Penumbra 启用的模组不会丢失。"u8);

        Im.Separator();
    }

    private void DrawControls()
    {
        var yOffset = (ChangedItemDrawer.TypeFilterIconSize.Y - Im.Style.FrameHeight) / 2f;
        Im.Cursor.Y += yOffset;

        if (Im.Button("刷新角色列表"u8))
            _task = RefreshCharacterList();

        var checkSpacing = Im.Style.ItemInnerSpacing.X;
        var checkPadding = 10 * Im.Style.GlobalScale + Im.Style.ItemSpacing.X;
        Im.Line.Same(0, checkPadding);

        using (Im.Id.Push("TreeCategoryFilter"u8))
        {
            foreach (var category in TreeCategory.Values)
            {
                using var id = Im.Id.Push((int)category);
                using var c  = ImGuiColor.CheckMark.Push(CategoryColor(category).Value());
                Im.Checkbox(StringU8.Empty, ref _categoryFilter, category);
                Im.Tooltip.OnHover(CategoryFilterDescription(category));
                Im.Line.Same(0.0f, checkSpacing);
            }
        }

        Im.Line.Same(0, checkPadding);

        var filterChanged = false;
        Im.Cursor.Y -= yOffset;
        using (Im.Child.Begin("##typeFilter"u8, new Vector2(Im.ContentRegion.Available.X, ChangedItemDrawer.TypeFilterIconSize.Y)))
        {
            if (changedItemDrawer.DrawTypeFilter(config.Filters.OnScreenTypeFilter, out var newTypeFilter))
            {
                filterChanged                     = true;
                config.Filters.OnScreenTypeFilter = newTypeFilter;
            }
        }

        using (ImStyleSingle.FrameRounding.Push(0))
        {
            var fieldWidth = (Im.ContentRegion.Available.X - checkSpacing * 2.0f - Im.Style.FrameHeightWithSpacing) / 2.0f;
            Im.Item.SetNextWidth(fieldWidth);
            var filter = config.Filters.OnScreenCharacterFilter;
            if (Im.Input.Text("##TreeNameFilter"u8, ref filter, "按角色/实体名称筛选..."u8))
            {
                filterChanged                          = true;
                config.Filters.OnScreenCharacterFilter = filter;
            }

            Im.Line.Same(0, checkSpacing);
            Im.Item.SetNextWidth(fieldWidth);
            filter = config.Filters.OnScreenItemFilter;
            if (Im.Input.Text("##NodeFilter"u8, ref filter, "按物品/部件名称或路径筛选..."u8))
            {
                filterChanged                     = true;
                config.Filters.OnScreenItemFilter = filter;
            }
        }

        Im.Line.Same(0, checkSpacing);
        incognito.DrawToggle(Im.Style.FrameHeightWithSpacing);

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

    private void DrawNodes(in Im.TableDisposable table, IEnumerable<ResourceNode> resourceNodes, int level, nint pathHash,
        ChangedItemIconFlag parentFilterIconFlag)
    {
        var debugMode   = config.DebugMode;
        var frameHeight = Im.Style.FrameHeight;

        foreach (var (index, resourceNode) in resourceNodes.Index())
        {
            var nodePathHash = unchecked(pathHash + resourceNode.ResourceHandle);

            var visibility = GetNodeVisibility(nodePathHash, resourceNode, parentFilterIconFlag);
            if (visibility == NodeVisibility.Hidden)
                continue;

            using var mutedColor = ImGuiColor.Text.Push(Im.Style[ImGuiColor.Text].WithAlpha(0.5f), resourceNode.Internal);

            var filterIcon = resourceNode.IconFlag != 0 ? resourceNode.IconFlag : parentFilterIconFlag;

            using var id = Im.Id.Push(index);
            table.NextColumn();
            var unfolded = _unfolded.Contains(nodePathHash);
            using (Im.Indent(level))
            {
                var hasVisibleChildren = resourceNode.Children.Any(child
                    => GetNodeVisibility(unchecked(nodePathHash * 31 + child.ResourceHandle), child, filterIcon) != NodeVisibility.Hidden);
                var unfoldable = hasVisibleChildren && visibility != NodeVisibility.DescendentsOnly;
                if (unfoldable)
                {
                    var icon   = unfolded ? LunaStyle.TreeCollapseIcon : LunaStyle.TreeExpandIcon;
                    var offset = (Im.Style.FrameHeight - ImEx.Icon.CalculateSize(icon).X) / 2;
                    Im.Cursor.X += offset;
                    ImEx.Icon.Draw(icon);
                    Im.Line.Same(0f, offset + Im.Style.ItemInnerSpacing.X);
                }
                else
                {
                    if (hasVisibleChildren && !unfolded)
                    {
                        _unfolded.Add(nodePathHash);
                        unfolded = true;
                    }

                    Im.FrameDummy();
                    Im.Line.SameInner();
                }

                changedItemDrawer.DrawCategoryIcon(resourceNode.IconFlag);
                Im.Line.SameInner();
                table.Header(resourceNode.Name!);
                if (unfoldable && Im.Item.Clicked())
                {
                    if (unfolded)
                        _unfolded.Remove(nodePathHash);
                    else
                        _unfolded.Add(nodePathHash);
                    unfolded = !unfolded;
                }

                if (debugMode)
                    ResourceInteraction(resourceNode);
            }

            table.NextColumn();
            var hasGamePaths = resourceNode.PossibleGamePaths.Length > 0;
            Im.Selectable(resourceNode.PossibleGamePaths.Length switch
            {
                0 => "(none)"u8,
                1 => $"{resourceNode.GamePath}",
                _ => "(multiple)"u8,
            }, false, hasGamePaths ? 0 : SelectableFlags.Disabled, Im.ContentRegion.Available with { Y = frameHeight });
            if (hasGamePaths && Im.Item.Hovered())
            {
                var allPaths = StringU8.Join((byte)'\n', resourceNode.PossibleGamePaths.AsEnumerable());
                if (Im.Item.Clicked())
                    Im.Clipboard.Set(allPaths);
                using var tt = Im.Tooltip.Begin();
                using var c  = Im.Color.PushDefault(ImGuiColor.Text);
                Im.Text(allPaths);
                Im.Text("\n点击复制到剪贴板。"u8);
            }

            table.NextColumn();
            if (resourceNode.FullPath.FullName.Length > 0)
            {
                var hasMod = resourceNode.Mod.TryGetTarget(out var mod);
                if (resourceNode is { ModName: not null, ModRelativePath: not null })
                {
                    var       modName = $"[{(hasMod ? mod!.Name : resourceNode.ModName)}]";
                    var       textPos = Im.Cursor.X + Im.Font.CalculateSize(modName).X + Im.Style.ItemInnerSpacing.X;
                    using var group   = Im.Group();
                    using (ImGuiColor.Text.Push((hasMod ? ColorId.NewMod : ColorId.DisabledMod).Value()))
                    {
                        Im.Selectable(modName, false, SelectableFlags.AllowOverlap, Im.ContentRegion.Available with { Y = frameHeight });
                    }

                    Im.Line.Same();
                    Im.Cursor.X = textPos;
                    Im.Text(resourceNode.ModRelativePath);
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
                    Im.Selectable(path, false, SelectableFlags.AllowOverlap, Im.ContentRegion.Available with { Y = frameHeight });
                }
                else
                {
                    Im.Selectable(resourceNode.FullPath.ToPath(), false, SelectableFlags.AllowOverlap,
                        Im.ContentRegion.Available with { Y = frameHeight });
                }

                if (Im.Item.Clicked())
                    Im.Clipboard.Set(resourceNode.FullPath.ToPath());
                if (hasMod && Im.Item.RightClicked() && Im.Io.KeyControl)
                    navigator.OpenTo(mod);

                Im.Tooltip.OnHover(default,
                    $"{resourceNode.FullPath.ToPath()}\n\n点击复制到剪贴板。{(hasMod ? "\nCtrl + 右键点击跳转到模组。" : string.Empty)}{GetAdditionalDataSuffix(resourceNode.AdditionalData)}",
                    true);
            }
            else
            {
                Im.Selectable(GetPathStatusLabel(resourceNode.FullPathStatus), false, SelectableFlags.Disabled,
                    Im.ContentRegion.Available with { Y = frameHeight });
                Im.Tooltip.OnHover(default,
                    $"{GetPathStatusDescription(resourceNode.FullPathStatus)}{GetAdditionalDataSuffix(resourceNode.AdditionalData)}");
            }

            mutedColor.Pop();

            table.NextColumn();
            using var spacing = ImStyleDouble.ItemSpacing.PushX(3 * Im.Style.GlobalScale);
            DrawActions(resourceNode, new Vector2(frameHeight));

            if (unfolded)
                DrawNodes(table, resourceNode.Children, level + 1, unchecked(nodePathHash * 31), filterIcon);
        }

        return;

        string GetAdditionalDataSuffix(CiByteString data)
        {
            return !debugMode || data.IsEmpty ? string.Empty : $"\n\n附加数据：{data}";
        }

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

            var filterIcon = node.IconFlag is not 0 ? node.IconFlag : parentFilterIcon;
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
            if (!config.Filters.OnScreenTypeFilter.HasFlag(filterIcon))
                return false;

            if (config.Filters.OnScreenItemFilter.Length is 0)
                return true;

            return node.Name != null && node.Name.Contains(config.Filters.OnScreenItemFilter, StringComparison.OrdinalIgnoreCase)
             || node.FullPath.FullName.Contains(config.Filters.OnScreenItemFilter, StringComparison.OrdinalIgnoreCase)
             || node.FullPath.InternalName.ToString().Contains(config.Filters.OnScreenItemFilter, StringComparison.OrdinalIgnoreCase)
             || Array.Exists(node.PossibleGamePaths,
                    path => path.Path.ToString().Contains(config.Filters.OnScreenItemFilter, StringComparison.OrdinalIgnoreCase));
        }

        void DrawActions(ResourceNode resourceNode, Vector2 buttonSize)
        {
            if (!_writableCache.TryGetValue(resourceNode.FullPath, out var writable))
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

            if (ImEx.Icon.Button(LunaStyle.SaveIcon, "导出此文件。"u8, resourceNode.FullPath.FullName.Length is 0 || writable is null,
                    buttonSize))
            {
                var fullPathStr = resourceNode.FullPath.FullName;
                var ext = resourceNode.PossibleGamePaths.Length == 1
                    ? Path.GetExtension(resourceNode.GamePath.ToString())
                    : Path.GetExtension(fullPathStr);
                fileDialog.OpenSavePicker($"导出 {Path.GetFileName(fullPathStr)} 到...", ext, Path.GetFileNameWithoutExtension(fullPathStr),
                    ext,
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

    private static ReadOnlySpan<byte> GetPathStatusDescription(ResourceNode.PathStatus status)
        => status switch
        {
            ResourceNode.PathStatus.External => "该文件的实际路径不可用，因为它由外部工具管理。"u8,
            ResourceNode.PathStatus.NonExistent =>
                "该文件的实际路径不可用，因为它在加载后可能已被移动或删除。"u8,
            _ => "该文件的实际路径不可用。"u8,
        };

    private static void HeaderInteraction(ResourceTree tree)
    {
        Im.Tooltip.OnHover(default,
            $"对象索引：        {tree.GameObjectIndex}\n对象地址：      0x{tree.GameObjectAddress:X16}\n绘制对象地址： 0x{tree.DrawObjectAddress:X16}",
            true, Im.Font.Mono);
        if (tree.GameObjectAddress == nint.Zero)
            return;

        using var context = Im.Popup.BeginContextItem();
        if (context)
        {
            using var text = Im.Color.PushDefault(ImGuiColor.Text);
            if (Im.Menu.Item("复制游戏对象地址"u8))
                Im.Clipboard.Set($"0x{tree.GameObjectAddress:X}");
            if (Penumbra.Dynamis.IsSubscribed && Im.Menu.Item("检查游戏对象"u8))
                Penumbra.Dynamis.InspectObject(tree.GameObjectAddress, $"{tree.Name} Game Object");
            if (tree.DrawObjectAddress != nint.Zero)
            {
                if (Im.Menu.Item("复制绘制对象地址"u8))
                    Im.Clipboard.Set($"0x{tree.DrawObjectAddress:X}");
                if (Penumbra.Dynamis.IsSubscribed && Im.Menu.Item("检查绘制对象"u8))
                    Penumbra.Dynamis.InspectObject(tree.DrawObjectAddress, $"{tree.Name} 绘制对象");
            }
        }
    }

    private static void ResourceInteraction(ResourceNode node)
    {
        Im.Tooltip.OnHover(default,
            $"资源类型：   {node.Type}\n对象地址：  0x{node.ObjectAddress:X16}\n资源句柄： 0x{node.ResourceHandle:X16}\n长度：          0x{node.Length:X16}",
            true, Im.Font.Mono);

        if (node.ResourceHandle == nint.Zero)
            return;

        using var context = Im.Popup.BeginContextItem();
        if (context)
        {
            using var text = Im.Color.PushDefault(ImGuiColor.Text);
            if (Im.Menu.Item("复制资源句柄地址"u8))
                Im.Clipboard.Set($"0x{node.ResourceHandle:X}");
            if (Penumbra.Dynamis.IsSubscribed && Im.Menu.Item("检查资源句柄"u8))
                Penumbra.Dynamis.InspectObject(node.ResourceHandle, $"{node.Name} 资源句柄");
            if (node.ObjectAddress != nint.Zero)
            {
                if (Im.Menu.Item("复制对象地址"u8))
                    Im.Clipboard.Set($"0x{node.ObjectAddress:X}");
                if (Penumbra.Dynamis.IsSubscribed && Im.Menu.Item("Inspect Object"u8))
                    Penumbra.Dynamis.InspectObject(node.ObjectAddress, $"{node.Name} 对象");
            }
        }
    }

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

    private static ReadOnlySpan<byte> CategoryFilterDescription(TreeCategory category)
        => category switch
        {
            TreeCategory.LocalPlayer  => "显示你和从属于你的对象（坐骑、宠物、时尚配饰、战斗伙伴等等）。"u8,
            TreeCategory.Player       => "显示其他玩家和从属于他们的对象"u8,
            TreeCategory.Networked    => "显示由游戏服务器处理的NPC对象。"u8,
            TreeCategory.NonNetworked => "显示由本地处理的NPC对象。"u8,
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
