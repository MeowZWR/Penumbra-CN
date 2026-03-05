using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public class ModPanelCollectionsTab(CollectionManager manager, ModSelection selection) : ITab<ModPanelTab>
{
    private enum ModState
    {
        Enabled,
        Disabled,
        Unconfigured,
    }

    private readonly List<(ModCollection, ModCollection, uint, ModState)> _cache = [];

    public ReadOnlySpan<byte> Label
        => "模组合集"u8;

    public ModPanelTab Identifier
        => ModPanelTab.Collections;

    public void DrawContent()
    {
        var (direct, inherited) = CountUsage(selection.Mod!);
        Im.Line.New();
        switch (direct)
        {
            case 1:  Im.Text("此模组已在 1 个合集中直接配置。"u8); break;
            case 0:  Im.Text("此模组未在任何合集中使用。"u8, Colors.RegexWarningBorder); break;
            default: Im.Text($"此模组已在 {direct} 个合集中直接配置。"); break;
        }
        if (inherited > 0)
            Im.Text($"也通过继承关系在 {inherited} {(inherited == 1 ? "个合集" : "个合集")}中被使用。");

        Im.Line.New();
        Im.Separator();
        Im.Line.New();
        using var table = Im.Table.Begin("##modCollections"u8, 3, TableFlags.SizingFixedFit | TableFlags.RowBackground);
        if (!table)
            return;

        var size           = Im.Font.CalculateSize(ToText(ModState.Unconfigured)).X + 20 * Im.Style.GlobalScale;
        var collectionSize = 200 * Im.Style.GlobalScale;
        table.SetupColumn("合集"u8,     TableColumnFlags.WidthFixed, collectionSize);
        table.SetupColumn("状态"u8,          TableColumnFlags.WidthFixed, size);
        table.SetupColumn("继承自"u8, TableColumnFlags.WidthFixed, collectionSize);
        table.HeaderRow();

        foreach (var (idx, (collection, parent, color, state)) in _cache.Index())
        {
            using var id = Im.Id.Push(idx);
            table.DrawColumn(collection.Identity.Name);

            table.NextColumn();
            Im.Text(ToText(state), color);

            using (var context = Im.Popup.BeginContextItem("Context"u8))
            {
                if (context)
                {
                    Im.Text(collection.Identity.Name);
                    Im.Separator();
                    using (Im.Disabled(state is ModState.Enabled && parent == collection))
                    {
                        if (Im.Menu.Item("启用"u8))
                        {
                            if (parent != collection)
                                manager.Editor.SetModInheritance(collection, selection.Mod!, false);
                            manager.Editor.SetModState(collection, selection.Mod!, true);
                        }
                    }

                    using (Im.Disabled(state is ModState.Disabled && parent == collection))
                    {
                        if (Im.Menu.Item("禁用"u8))
                        {
                            if (parent != collection)
                                manager.Editor.SetModInheritance(collection, selection.Mod!, false);
                            manager.Editor.SetModState(collection, selection.Mod!, false);
                        }
                    }

                    using (Im.Disabled(parent != collection))
                    {
                        if (Im.Menu.Item("继承"u8))
                            manager.Editor.SetModInheritance(collection, selection.Mod!, true);
                    }
                }
            }

            table.DrawColumn(parent == collection ? StringU8.Empty : parent.Identity.Name);
        }
    }

    private static ReadOnlySpan<byte> ToText(ModState state)
        => state switch
        {
            ModState.Unconfigured => "未配置"u8,
            ModState.Enabled      => "已启用"u8,
            ModState.Disabled     => "已禁用"u8,
            _                     => "未知"u8,
        };

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
        foreach (var collection in manager.Storage)
        {
            var (settings, parent) = collection.GetInheritedSettings(mod.Index);
            var (color, text) = settings == null
                ? (undefined, ModState.Unconfigured)
                : settings.Enabled
                    ? (parent == collection ? enabled : inherited, ModState.Enabled)
                    : (parent == collection ? disabled : disInherited, ModState.Disabled);
            _cache.Add((collection, parent, color.Color, text));

            if (color == enabled)
                ++directCount;
            else if (color == inherited)
                ++inheritedCount;
        }

        return (directCount, inheritedCount);
    }
}
