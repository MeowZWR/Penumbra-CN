using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public class ModPanelCollectionsTab : ITab
{
    private readonly ModFileSystemSelector _selector;
    private readonly CollectionStorage     _collections;

    private readonly List<(ModCollection, ModCollection, uint, string)> _cache = new();

    public ModPanelCollectionsTab(CollectionStorage storage, ModFileSystemSelector selector)
    {
        _collections = storage;
        _selector    = selector;
    }

    public ReadOnlySpan<byte> Label
        => "模组合集"u8;

    public void DrawContent()
    {
        var (direct, inherited) = CountUsage(_selector.Selected!);
        ImGui.NewLine();
        if (direct == 1)
            ImGui.TextUnformatted("此模组已在 1 个合集中直接配置。");
        else if (direct == 0)
            ImGuiUtil.TextColored(Colors.RegexWarningBorder, "此模组未在任何合集中使用。");
        else
            ImGui.TextUnformatted($"此模组已在 {direct} 个合集中直接配置。" );
        if (inherited > 0)
            ImGui.TextUnformatted(
                $"也通过继承关系在 {inherited} {(inherited == 1 ? "个合集" : "个合集")}中被使用。");

        ImGui.NewLine();
        ImGui.Separator();
        ImGui.NewLine();
        using var table = ImRaii.Table("##modCollections", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
        if (!table)
            return;

        var size           = ImGui.CalcTextSize("未配置").X + 20 * ImGuiHelpers.GlobalScale;
        var collectionSize = 200 * ImGuiHelpers.GlobalScale;
        ImGui.TableSetupColumn("合集",     ImGuiTableColumnFlags.WidthFixed, collectionSize);
        ImGui.TableSetupColumn("状态",          ImGuiTableColumnFlags.WidthFixed, size);
        ImGui.TableSetupColumn("继承自", ImGuiTableColumnFlags.WidthFixed, collectionSize);

        ImGui.TableHeadersRow();
        foreach (var (collection, parent, color, text) in _cache)
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(collection.Name);

            ImGui.TableNextColumn();
            using (var c = ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(text);
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(parent == collection ? string.Empty : parent.Name);
        }
    }

    private (int Direct, int Inherited) CountUsage(Mod mod)
    {
        _cache.Clear();
        var undefined      = ColorId.UndefinedMod.Value();
        var enabled        = ColorId.EnabledMod.Value();
        var inherited      = ColorId.InheritedMod.Value();
        var disabled       = ColorId.DisabledMod.Value();
        var disInherited   = ColorId.InheritedDisabledMod.Value();
        var directCount    = 0;
        var inheritedCount = 0;
        foreach (var collection in _collections)
        {
            var (settings, parent) = collection[mod.Index];
            var (color, text) = settings == null
                ? (undefined, "未配置")
                : settings.Enabled
                    ? (parent == collection ? enabled : inherited, "启用")
                    : (parent == collection ? disabled : disInherited, "禁用");
            _cache.Add((collection, parent, color, text));

            if (color == enabled)
                ++directCount;
            else if (color == inherited)
                ++inheritedCount;
        }

        return (directCount, inheritedCount);
    }
}
