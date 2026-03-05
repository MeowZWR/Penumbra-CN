using Dalamud.Game.ClientState.Objects.Enums;
using ImSharp;

namespace Penumbra.UI.CollectionTab;

public sealed class ObjectKindCombo(params IReadOnlyList<ObjectKind> kinds) : SimpleFilterCombo<ObjectKind>(SimpleFilterType.None)
{
    public override StringU8 DisplayString(in ObjectKind value)
        => value switch
        {
            ObjectKind.None      => new StringU8("未知"u8),
            ObjectKind.BattleNpc => new StringU8("战斗 NPC"u8),
            ObjectKind.EventNpc  => new StringU8("事件 NPC"u8),
            ObjectKind.MountType => new StringU8("坐骑"u8),
            ObjectKind.Companion => new StringU8("宠物"u8),
            ObjectKind.Ornament  => new StringU8("时尚配饰"u8),
            _                    => new StringU8($"{value}"),
        };

    public override string FilterString(in ObjectKind value)
        => string.Empty;

    public override IEnumerable<ObjectKind> GetBaseItems()
        => kinds;
}
