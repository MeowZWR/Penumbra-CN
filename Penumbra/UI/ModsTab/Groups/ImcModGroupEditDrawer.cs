using ImSharp;
using Luna;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.SubMods;
using Penumbra.UI.AdvancedWindow.Meta;

namespace Penumbra.UI.ModsTab.Groups;

public readonly struct ImcModGroupEditDrawer(ModGroupEditDrawer editor, ImcModGroup group) : IModGroupEditDrawer
{
    public void Draw()
    {
        var identifier   = group.Identifier;
        var defaultEntry = ImcChecker.GetDefaultEntry(identifier, true).Entry;
        var entry        = group.DefaultEntry;
        var changes      = false;

        var width = editor.AvailableWidth.X
          - 3 * Im.Style.ItemInnerSpacing.X
          - Im.Style.ItemSpacing.X
          - Im.Font.CalculateSize("所有变体"u8).X
          - Im.Font.CalculateSize("仅属性"u8).X
          - 2 * Im.Style.FrameHeight;
        ImEx.TextFramed($"{identifier}", new Vector2(width, 0), Rgba32.Transparent);

        Im.Line.SameInner();
        var allVariants = group.AllVariants;
        if (Im.Checkbox("所有变体"u8, ref allVariants))
            editor.ModManager.OptionEditor.ImcEditor.ChangeAllVariants(group, allVariants);
        Im.Tooltip.OnHover("使此组覆盖此标识符的所有对应变体，而不仅仅是指定的一个。"u8);

        Im.Line.Same();
        var onlyAttributes = group.OnlyAttributes;
        if (Im.Checkbox("仅属性"u8, ref onlyAttributes))
            editor.ModManager.OptionEditor.ImcEditor.ChangeOnlyAttributes(group, onlyAttributes);
        Im.Tooltip.OnHover(
            "仅覆盖属性标志，其他值则使用游戏的默认项，而不是此处配置的项。\n\n主要在与所有变体一起使用时有用，以保持每个变体的材质 ID。"u8);

        using (Im.Group())
        {
            ImEx.TextFrameAligned("材质 ID"u8);
            ImEx.TextFrameAligned("视效 ID"u8);
            ImEx.TextFrameAligned("贴花 ID"u8);
        }

        Im.Line.Same();
        using (Im.Group())
        {
            changes |= ImcMetaDrawer.DrawMaterialId(defaultEntry, ref entry, true);
            changes |= ImcMetaDrawer.DrawVfxId(defaultEntry, ref entry, true);
            changes |= ImcMetaDrawer.DrawDecalId(defaultEntry, ref entry, true);
        }

        Im.Line.Same(0, editor.PriorityWidth);
        using (Im.Group())
        {
            ImEx.TextFrameAligned("材质动画 ID"u8);
            ImEx.TextFrameAligned("声音 ID"u8);
            ImEx.TextFrameAligned("可被禁用"u8);
        }

        Im.Line.Same();

        using (Im.Group())
        {
            changes |= ImcMetaDrawer.DrawMaterialAnimationId(defaultEntry, ref entry, true);
            changes |= ImcMetaDrawer.DrawSoundId(defaultEntry, ref entry, true);
            var canBeDisabled = group.CanBeDisabled;
            if (Im.Checkbox("##disabled"u8, ref canBeDisabled))
                editor.ModManager.OptionEditor.ImcEditor.ChangeCanBeDisabled(group, canBeDisabled);
        }

        if (changes)
            editor.ModManager.OptionEditor.ImcEditor.ChangeDefaultEntry(group, entry);

        Im.Dummy(Vector2.Zero);
        DrawOptions();
        var attributeCache = new ImcAttributeCache(group);
        DrawNewOption(attributeCache);
        Im.Dummy(Vector2.Zero);


        using (Im.Group())
        {
            ImEx.TextFrameAligned("默认属性"u8);
            foreach (var option in group.OptionData.Where(o => !o.IsDisableSubMod))
                ImEx.TextFrameAligned(option.Name);
        }

        Im.Line.SameInner();
        using (Im.Group())
        {
            DrawAttributes(editor.ModManager.OptionEditor.ImcEditor, attributeCache, group.DefaultEntry.AttributeMask, group);
            foreach (var (idx, option) in group.OptionData.Index().Where(o => !o.Item.IsDisableSubMod))
            {
                using var id = Im.Id.Push(idx);
                DrawAttributes(editor.ModManager.OptionEditor.ImcEditor, attributeCache, option.AttributeMask, option,
                    group.DefaultEntry.AttributeMask);
            }
        }
    }

    private void DrawOptions()
    {
        foreach (var (optionIdx, option) in group.OptionData.Index())
        {
            using var id = Im.Id.Push(optionIdx);
            editor.DrawOptionPosition(group, option, optionIdx);

            Im.Line.SameInner();
            editor.DrawOptionDefaultMultiBehaviour(group, option, optionIdx);

            Im.Line.SameInner();
            editor.DrawOptionName(option);

            Im.Line.SameInner();
            editor.DrawOptionButtons(option);

            if (!option.IsDisableSubMod)
            {
                Im.Line.SameInner();
                editor.DrawOptionDelete(option);
            }
        }
    }

    private void DrawNewOption(in ImcAttributeCache cache)
    {
        var dis       = cache.LowestUnsetMask is 0;
        var name      = editor.DrawNewOptionBase(group, group.Options.Count);
        var validName = name.Length > 0;
        var tt = dis
            ? "新选项没有空闲属性插槽..."u8
            : validName
                ? "添加一个新的选项到此组。"u8
                : "请输入新选项的名称。"u8;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, !validName || dis))
        {
            editor.ModManager.OptionEditor.ImcEditor.AddOption(group, cache, name);
            editor.NewOptionName = null;
        }
    }

    private static void DrawAttributes(ImcModGroupEditor editor, in ImcAttributeCache cache, ushort mask, object data,
        ushort? defaultMask = null)
    {
        for (var i = 0; i < ImcEntry.NumAttributes; ++i)
        {
            using var id        = Im.Id.Push(i);
            var       flag      = 1 << i;
            var       value     = (mask & flag) is not 0;
            var       inDefault = defaultMask.HasValue && (defaultMask & flag) is not 0;
            using (Im.Disabled(defaultMask is not null && !cache.CanChange(i)))
            {
                if (inDefault ? ImEx.XCheckbox(""u8, ref value) : Im.Checkbox(""u8, ref value))
                {
                    if (data is ImcModGroup g)
                        editor.ChangeDefaultAttribute(g, cache, i, value);
                    else
                        editor.ChangeOptionAttribute((ImcSubMod)data, cache, i, value);
                }
            }

            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "ABCDEFGHIJ"u8.Slice(i, 1));
            if (i != 9)
                Im.Line.SameInner();
        }
    }
}
