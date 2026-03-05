using Luna.Generators;
using Penumbra.Api.Enums;

namespace Penumbra.UI.Classes;

[Flags]
[NamedEnum(Utf16: false)]
public enum ChangedItemIconFlag : uint
{
    [Name("头部")]
    Head = 0x00_00_01,

    [Name("身体")]
    Body = 0x00_00_02,

    [Name("手臂")]
    Hands = 0x00_00_04,

    [Name("腿部")]
    Legs = 0x00_00_08,

    [Name("脚部")]
    Feet = 0x00_00_10,

    [Name("耳部")]
    Ears = 0x00_00_20,

    [Name("颈部")]
    Neck = 0x00_00_40,

    [Name("腕部")]
    Wrists = 0x00_00_80,

    [Name("戒指")]
    Finger = 0x00_01_00,

    [Name("怪物")]
    Monster = 0x00_02_00,

    [Name("亚人")]
    Demihuman = 0x00_04_00,

    [Name("外貌")]
    Customization = 0x00_08_00,

    [Name("技能")]
    Action = 0x00_10_00,

    [Name("主手武器")]
    Mainhand = 0x00_20_00,

    [Name("副手武器")]
    Offhand = 0x00_40_00,

    [Name("其他")]
    Unknown = 0x00_80_00,

    [Name("情感动作")]
    Emote = 0x01_00_00,
}

public static class ChangedItemFlagExtensions
{
    public static readonly IReadOnlyList<ChangedItemIconFlag> Order =
    [
        ChangedItemIconFlag.Head,
        ChangedItemIconFlag.Body,
        ChangedItemIconFlag.Hands,
        ChangedItemIconFlag.Legs,
        ChangedItemIconFlag.Feet,
        ChangedItemIconFlag.Ears,
        ChangedItemIconFlag.Neck,
        ChangedItemIconFlag.Wrists,
        ChangedItemIconFlag.Finger,
        ChangedItemIconFlag.Mainhand,
        ChangedItemIconFlag.Offhand,
        ChangedItemIconFlag.Customization,
        ChangedItemIconFlag.Action,
        ChangedItemIconFlag.Emote,
        ChangedItemIconFlag.Monster,
        ChangedItemIconFlag.Demihuman,
        ChangedItemIconFlag.Unknown,
    ];

    public const           ChangedItemIconFlag AllFlags      = (ChangedItemIconFlag)0x01FFFF;
    public static readonly int                 NumCategories = Order.Count;
    public const           ChangedItemIconFlag DefaultFlags  = AllFlags;

    public static ChangedItemIcon ToApiIcon(this ChangedItemIconFlag iconFlag)
        => iconFlag switch
        {
            ChangedItemIconFlag.Head          => ChangedItemIcon.Head,
            ChangedItemIconFlag.Body          => ChangedItemIcon.Body,
            ChangedItemIconFlag.Hands         => ChangedItemIcon.Hands,
            ChangedItemIconFlag.Legs          => ChangedItemIcon.Legs,
            ChangedItemIconFlag.Feet          => ChangedItemIcon.Feet,
            ChangedItemIconFlag.Ears          => ChangedItemIcon.Ears,
            ChangedItemIconFlag.Neck          => ChangedItemIcon.Neck,
            ChangedItemIconFlag.Wrists        => ChangedItemIcon.Wrists,
            ChangedItemIconFlag.Finger        => ChangedItemIcon.Finger,
            ChangedItemIconFlag.Monster       => ChangedItemIcon.Monster,
            ChangedItemIconFlag.Demihuman     => ChangedItemIcon.Demihuman,
            ChangedItemIconFlag.Customization => ChangedItemIcon.Customization,
            ChangedItemIconFlag.Action        => ChangedItemIcon.Action,
            ChangedItemIconFlag.Emote         => ChangedItemIcon.Emote,
            ChangedItemIconFlag.Mainhand      => ChangedItemIcon.Mainhand,
            ChangedItemIconFlag.Offhand       => ChangedItemIcon.Offhand,
            ChangedItemIconFlag.Unknown       => ChangedItemIcon.Unknown,
            _                                 => ChangedItemIcon.None,
        };

    public static ChangedItemIconFlag ToFlag(this ChangedItemIcon icon)
        => icon switch
        {
            ChangedItemIcon.Unknown       => ChangedItemIconFlag.Unknown,
            ChangedItemIcon.Head          => ChangedItemIconFlag.Head,
            ChangedItemIcon.Body          => ChangedItemIconFlag.Body,
            ChangedItemIcon.Hands         => ChangedItemIconFlag.Hands,
            ChangedItemIcon.Legs          => ChangedItemIconFlag.Legs,
            ChangedItemIcon.Feet          => ChangedItemIconFlag.Feet,
            ChangedItemIcon.Ears          => ChangedItemIconFlag.Ears,
            ChangedItemIcon.Neck          => ChangedItemIconFlag.Neck,
            ChangedItemIcon.Wrists        => ChangedItemIconFlag.Wrists,
            ChangedItemIcon.Finger        => ChangedItemIconFlag.Finger,
            ChangedItemIcon.Mainhand      => ChangedItemIconFlag.Mainhand,
            ChangedItemIcon.Offhand       => ChangedItemIconFlag.Offhand,
            ChangedItemIcon.Customization => ChangedItemIconFlag.Customization,
            ChangedItemIcon.Monster       => ChangedItemIconFlag.Monster,
            ChangedItemIcon.Demihuman     => ChangedItemIconFlag.Demihuman,
            ChangedItemIcon.Action        => ChangedItemIconFlag.Action,
            ChangedItemIcon.Emote         => ChangedItemIconFlag.Emote,
            _                             => ChangedItemIconFlag.Unknown,
        };
}
