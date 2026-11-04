using ImSharp;
using Luna;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ModsTab;

public class MultiModPanel(ModFileSystem fileSystem, ModDataEditor editor, PredefinedTagManager tagManager, CollectionManager collections)
    : IUiService
{
    public void Draw()
    {
        if (fileSystem.Selection.OrderedNodes.Count is 0)
            return;

        Im.Line.New();
        var treeNodePos = Im.Cursor.Position;
        DrawModList();
        DrawCounts(treeNodePos);
        DrawMultiToggleOptions();
        DrawMultiTagger();
    }

    private void DrawCounts(Vector2 treeNodePos)
    {
        var startPos   = Im.Cursor.Position;
        var numLeaves  = fileSystem.Selection.DataNodes.Count;
        var numFolders = fileSystem.Selection.Folders.Count;
        Utf8StringHandler<TextStringHandlerBuffer> text = (numLeaves, numFolders) switch
        {
            (0, 0)   => StringU8.Empty, // should not happen
            (> 0, 0) => $"{numLeaves} 个模组",
            (0, > 0) => $"{numFolders} 个折叠组",
            _        => $"{numLeaves} 个模组，{numFolders} 个折叠组",
        };
        Im.Cursor.Position = treeNodePos;
        ImEx.TextRightAligned(ref text);
        Im.Cursor.Position = startPos;
    }

    private void DrawModList()
    {
        using var tree = Im.Tree.Node("当前选中对象###Selected"u8,
            TreeNodeFlags.DefaultOpen | TreeNodeFlags.NoTreePushOnOpen);
        Im.Separator();

        if (!tree)
            return;

        var sizeType             = new Vector2(Im.Style.FrameHeight);
        var availableSizePercent = (Im.ContentRegion.Available.X - sizeType.X - 4 * Im.Style.CellPadding.X) / 100;
        var sizeMods             = availableSizePercent * 35;
        var sizeFolders          = availableSizePercent * 65;

        using (var table = Im.Table.Begin("mods"u8, 3, TableFlags.RowBackground))
        {
            if (!table)
                return;

            table.SetupColumn("type"u8, TableColumnFlags.WidthFixed, sizeType.X);
            table.SetupColumn("mod"u8,  TableColumnFlags.WidthFixed, sizeMods);
            table.SetupColumn("path"u8, TableColumnFlags.WidthFixed, sizeFolders);

            using var id = Im.Id.Empty();
            foreach (var node in fileSystem.Selection.OrderedNodes.OrderBy(p => p.FullPath, StringComparer.OrdinalIgnoreCase))
            {
                id.PushNext();
                var (icon, text) = node is IFileSystemData<Mod> l
                    ? (LunaStyle.RemoveFileIcon, l.Value.Name)
                    : (LunaStyle.RemoveFolderIcon, string.Empty);
                table.NextColumn();
                if (ImEx.Icon.Button(icon, "从选择中移除。"u8, false, sizeType))
                    fileSystem.Selection.RemoveFromSelection(node);

                table.DrawFrameColumn(text);
                table.DrawFrameColumn(node.FullPath);
                id.Pop();
            }
        }

        Im.Separator();
    }

    private void DrawMultiToggleOptions()
    {
        var total = fileSystem.Selection.DataNodes.Count;
        if (total is 0)
            return;

        var buttonWidth = new Vector2((Im.ContentRegion.Available.X - 2 * Im.Style.ItemInnerSpacing.X) / 3, Im.Style.FrameHeight);
        var current     = collections.Active.Current;
        var enabled     = 0;
        var disabled    = 0;
        foreach (var mod in fileSystem.Selection.DataNodes)
        {
            var settings = current.GetOwnSettings(((Mod)mod.Value).Index);
            switch (settings)
            {
                case null:               break;
                case { Enabled: true }:  ++enabled; break;
                case { Enabled: false }: ++disabled; break;
            }
        }

        var inherited = total - enabled - disabled;

        if (ImEx.Button("Enable All"u8, buttonWidth,
                enabled == total
                    ? $"All selected mods are already enabled in {current.Identity.Name}."
                    : $"Enable {total - enabled} mods that are not currently enabled in {current.Identity.Name}.", enabled == total))
            collections.Editor.SetMultipleModStates(current, fileSystem.Selection.DataNodes.Select(n => (Mod)n.Value), true);

        Im.Line.SameInner();
        if (ImEx.Button("Disable All"u8, buttonWidth,
                disabled == total
                    ? $"All selected mods are already disabled in {current.Identity.Name}."
                    : $"Disable {total - disabled} mods that are not currently disabled in {current.Identity.Name}.",
                disabled == total))
            collections.Editor.SetMultipleModStates(current, fileSystem.Selection.DataNodes.Select(n => (Mod)n.Value), false);

        Im.Line.SameInner();
        if (ImEx.Button("Inherit All"u8, buttonWidth,
                inherited == total
                    ? $"All selected mods are already inherited in {current.Identity.Name}."
                    : $"Inherit {total - inherited} mods that are not currently enabled in {current.Identity.Name}.",
                inherited == total))
            collections.Editor.SetMultipleModInheritances(current, fileSystem.Selection.DataNodes.Select(n => (Mod)n.Value), true);
        Im.Separator();
    }

    private          string           _tag        = string.Empty;
    private readonly List<Mod>        _addMods    = [];
    private readonly List<(Mod, int)> _removeMods = [];

    private void DrawMultiTagger()
    {
        var width = ImEx.ScaledVector(150, 0);
        ImEx.TextFrameAligned("批量标签："u8);
        Im.Line.Same();

        var predefinedTagsEnabled = tagManager.Enabled;
        var inputWidth = predefinedTagsEnabled
            ? Im.ContentRegion.Available.X - 2 * width.X - 3 * Im.Style.ItemInnerSpacing.X - Im.Style.FrameHeight
            : Im.ContentRegion.Available.X - 2 * (width.X + Im.Style.ItemInnerSpacing.X);
        Im.Item.SetNextWidth(inputWidth);
        Im.Input.Text("##tag"u8, ref _tag, "本地标签名称..."u8);

        UpdateTagCache();
        Utf8StringHandler<LabelStringHandlerBuffer> label = _addMods.Count > 0
            ? $"添加到 {_addMods.Count} 个模组"
            : "添加";
        Utf8StringHandler<TextStringHandlerBuffer> tooltip = _addMods.Count is 0
            ? _tag.Length is 0
                ? "未指定标签。"
                : $"所有选中的模组已包含标签 \"{_tag}\"，无论是本地还是作为模组数据。"
            : $"Add the tag \"{_tag}\" to {_addMods.Count} mods as a local tag:\n\n\t{string.Join("\n\t", _addMods.Select(m => m.Name))}";
        Im.Line.SameInner();
        if (ImEx.Button(label, width, tooltip, _addMods.Count is 0))
            foreach (var mod in _addMods)
                editor.ChangeLocalTag(mod, mod.LocalTags.Count, _tag);

        label = _removeMods.Count > 0
            ? $"从 {_removeMods.Count} 个模组中移除"
            : "移除";
        tooltip = _removeMods.Count is 0
            ? _tag.Length is 0
                ? "未指定标签。"
                : $"选中的模组不包含本地标签 \"{_tag}\"。"
            : $"从 {_removeMods.Count} 个模组中移除本地标签 \"{_tag}\"：\n\n\t{string.Join("\n\t", _removeMods.Select(m => m.Item1.Name))}";
        Im.Line.SameInner();
        if (ImEx.Button(label, width, tooltip, _removeMods.Count is 0))
            foreach (var (mod, index) in _removeMods)
                editor.ChangeLocalTag(mod, index, string.Empty);

        if (predefinedTagsEnabled)
        {
            Im.Line.SameInner();
            tagManager.DrawToggleButton();
            tagManager.DrawListMulti(fileSystem.Selection.DataNodes.Select(l => (Mod)l.Value));
        }

        Im.Separator();
    }

    private void UpdateTagCache()
    {
        _addMods.Clear();
        _removeMods.Clear();
        if (_tag.Length is 0)
            return;

        foreach (var leaf in fileSystem.Selection.DataNodes)
        {
            var mod   = (Mod)leaf.Value;
            var index = mod.LocalTags.IndexOf(_tag);
            if (index >= 0)
                _removeMods.Add((mod, index));
            else if (!mod.ModTags.Contains(_tag))
                _addMods.Add(mod);
        }
    }
}
