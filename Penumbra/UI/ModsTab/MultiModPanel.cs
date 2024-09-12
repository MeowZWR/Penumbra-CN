using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ModsTab;

public class MultiModPanel(ModFileSystemSelector _selector, ModDataEditor _editor) : IUiService
{
    public void Draw()
    {
        if (_selector.SelectedPaths.Count == 0)
            return;

        ImGui.NewLine();
        DrawModList();
        DrawMultiTagger();
    }

    private void DrawModList()
    {
        using var tree = ImRaii.TreeNode("当前选中的对象", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.NoTreePushOnOpen);
        ImGui.Separator();
        if (!tree)
            return;

        var sizeType             = ImGui.GetFrameHeight();
        var availableSizePercent = (ImGui.GetContentRegionAvail().X - sizeType - 4 * ImGui.GetStyle().CellPadding.X) / 100;
        var sizeMods             = availableSizePercent * 35;
        var sizeFolders          = availableSizePercent * 65;

        using (var table = ImRaii.Table("mods", 3, ImGuiTableFlags.RowBg))
        {
            if (!table)
                return;

            ImGui.TableSetupColumn("type", ImGuiTableColumnFlags.WidthFixed, sizeType);
            ImGui.TableSetupColumn("mod",  ImGuiTableColumnFlags.WidthFixed, sizeMods);
            ImGui.TableSetupColumn("path", ImGuiTableColumnFlags.WidthFixed, sizeFolders);

            var i = 0;
            foreach (var (fullName, path) in _selector.SelectedPaths.Select(p => (p.FullName(), p))
                         .OrderBy(p => p.Item1, StringComparer.OrdinalIgnoreCase))
            {
                using var id = ImRaii.PushId(i++);
                ImGui.TableNextColumn();
                var icon = (path is ModFileSystem.Leaf ? FontAwesomeIcon.FileCircleMinus : FontAwesomeIcon.FolderMinus).ToIconString();
                if (ImGuiUtil.DrawDisabledButton(icon, new Vector2(sizeType), "从选择中移除。", false, true))
                    _selector.RemovePathFromMultiSelection(path);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(path is ModFileSystem.Leaf l ? l.Value.Name : string.Empty);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(fullName);
            }
        }

        ImGui.Separator();
    }

    private          string           _tag        = string.Empty;
    private readonly List<Mod>        _addMods    = [];
    private readonly List<(Mod, int)> _removeMods = [];

    private void DrawMultiTagger()
    {
        var width = ImGuiHelpers.ScaledVector2(150, 0);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("批量标签：");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 2 * (width.X + ImGui.GetStyle().ItemSpacing.X));
        ImGui.InputTextWithHint("##tag", "本地标签名称...", ref _tag, 128);

        UpdateTagCache();
        var label = _addMods.Count > 0
            ? $"添加到{_addMods.Count}个模组"
            : "添加";
        var tooltip = _addMods.Count == 0
            ? _tag.Length == 0
                ? "未指定标签。"
                : $"所有选中的模组已包含标签 \"{_tag}\"，无论是本地还是作为模组数据。"
            : $"将\"{_tag}\"作为本地标签添加到{_addMods.Count}个模组：\n\n\t{string.Join("\n\t", _addMods.Select(m => m.Name.Text))}";
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(label, width, tooltip, _addMods.Count == 0))
            foreach (var mod in _addMods)
                _editor.ChangeLocalTag(mod, mod.LocalTags.Count, _tag);

        label = _removeMods.Count > 0
            ? $"从 {_removeMods.Count} 个模组中移除"
            : "移除";
        tooltip = _removeMods.Count == 0
            ? _tag.Length == 0
                ? "未指定标签。"
                : $"选中的模组不包含\"{_tag}\"这个本地标签。"
            : $"从{_removeMods.Count}个模组移除本地标签\"{_tag}\" ：\n\n\t{string.Join("\n\t", _removeMods.Select(m => m.Item1.Name.Text))}";
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(label, width, tooltip, _removeMods.Count == 0))
            foreach (var (mod, index) in _removeMods)
                _editor.ChangeLocalTag(mod, index, string.Empty);
        ImGui.Separator();
    }

    private void UpdateTagCache()
    {
        _addMods.Clear();
        _removeMods.Clear();
        if (_tag.Length == 0)
            return;

        foreach (var leaf in _selector.SelectedPaths.OfType<ModFileSystem.Leaf>())
        {
            var index = leaf.Value.LocalTags.IndexOf(_tag);
            if (index >= 0)
                _removeMods.Add((leaf.Value, index));
            else if (!leaf.Value.ModTags.Contains(_tag))
                _addMods.Add(leaf.Value);
        }
    }
}
