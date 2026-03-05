using ImSharp;
using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class GlobalEqpMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<GlobalEqpManipulation, byte>(editor, metaFiles)
{
    public override ReadOnlySpan<byte> Label
        => "全局装备参数编辑(Global EQP)###GEQP"u8;

    public override int NumColumns
        => 4;

    protected override void Initialize()
    {
        Identifier = new GlobalEqpManipulation
        {
            Condition = 1,
            Type      = GlobalEqpType.DoNotHideEarrings,
        };
    }

    protected override void DrawNew()
    {
        Im.Table.NextColumn();
        CopyToClipboardButton("将当前所有全局EQP操作复制到剪贴板。"u8,
            new Lazy<JToken?>(() => MetaDictionary.SerializeTo([], Editor.GlobalEqp)));

        Im.Table.NextColumn();
        var canAdd = !Editor.Contains(Identifier);
        var tt     = canAdd ? "编辑此项。"u8 : "此项已被编辑。"u8;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, !canAdd))
            Editor.Changes |= Editor.TryAdd(Identifier);

        DrawIdentifierInput(ref Identifier);
    }

    protected override void DrawEntry(GlobalEqpManipulation identifier, byte _)
    {
        DrawMetaButtons(identifier, 0);
        DrawIdentifier(identifier);
    }

    protected override IEnumerable<(GlobalEqpManipulation, byte)> Enumerate()
        => Editor.GlobalEqp
            .OrderBy(identifier => identifier.Type)
            .ThenBy(identifier => identifier.Condition.Id)
            .Select(identifier => (identifier, (byte)0));

    protected override int Count
        => Editor.GlobalEqp.Count;

    private static void DrawIdentifierInput(ref GlobalEqpManipulation identifier)
    {
        Im.Table.NextColumn();
        DrawType(ref identifier);

        Im.Table.NextColumn();
        if (identifier.Type.HasCondition())
            DrawCondition(ref identifier);
        else
            Im.ScaledDummy(100);
    }

    private static void DrawIdentifier(GlobalEqpManipulation identifier)
    {
        Im.Table.NextColumn();
        ImEx.TextFramed(identifier.Type.ToNameU8(), default, FrameColor);
        Im.Tooltip.OnHover("全局EQP类型"u8);

        Im.Table.NextColumn();
        if (identifier.Type.HasCondition())
        {
            ImEx.TextFramed($"{identifier.Condition.Id}", default, FrameColor);
            Im.Tooltip.OnHover("条件模型ID"u8);
        }
    }

    public static bool DrawType(ref GlobalEqpManipulation identifier, float unscaledWidth = 250)
    {
        Im.Item.SetNextWidthScaled(unscaledWidth);
        using var combo = Im.Combo.Begin("##geqpType"u8, identifier.Type.ToNameU8());
        if (!combo)
            return false;

        var ret = false;
        foreach (var type in GlobalEqpType.Values)
        {
            if (Im.Selectable(type.ToNameU8(), type == identifier.Type))
            {
                identifier = new GlobalEqpManipulation
                {
                    Type      = type,
                    Condition = type.HasCondition() ? identifier.Type.HasCondition() ? identifier.Condition : new PrimaryId(1) : PrimaryId.Zero,
                };
                ret = true;
            }

            Im.Tooltip.OnHover(type.Tooltip());
        }

        return ret;
    }

    public static void DrawCondition(ref GlobalEqpManipulation identifier, float unscaledWidth = 100)
    {
        if (IdInput("##geqpCond"u8, unscaledWidth, identifier.Condition.Id, out var newId, 1, ushort.MaxValue,
                identifier.Condition.Id <= 1))
            identifier = identifier with { Condition = newId };
        Im.Tooltip.OnHover("不应被隐藏的物品的模型ID。"u8);
    }
}
