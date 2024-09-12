using Penumbra.Api.Enums;
using Penumbra.GameData.Enums;

namespace Penumbra.UI;

[Flags]
public enum ChangedItemIconFlag : uint
{
    Head          = 0x00_00_01,
    Body          = 0x00_00_02,
    Hands         = 0x00_00_04,
    Legs          = 0x00_00_08,
    Feet          = 0x00_00_10,
    Ears          = 0x00_00_20,
    Neck          = 0x00_00_40,
    Wrists        = 0x00_00_80,
    Finger        = 0x00_01_00,
    Monster       = 0x00_02_00,
    Demihuman     = 0x00_04_00,
    Customization = 0x00_08_00,
    Action        = 0x00_10_00,
    Mainhand      = 0x00_20_00,
    Offhand       = 0x00_40_00,
    Unknown       = 0x00_80_00,
    Emote         = 0x01_00_00,
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
    public const           ChangedItemIconFlag DefaultFlags  = AllFlags & ~ChangedItemIconFlag.Offhand;

    public static string ToDescription(this ChangedItemIconFlag iconFlag)
        => iconFlag switch
        {
            ChangedItemIconFlag.Head          => EquipSlot.Head.ToName(),
            ChangedItemIconFlag.Body          => EquipSlot.Body.ToName(),
            ChangedItemIconFlag.Hands         => EquipSlot.Hands.ToName(),
            ChangedItemIconFlag.Legs          => EquipSlot.Legs.ToName(),
            ChangedItemIconFlag.Feet          => EquipSlot.Feet.ToName(),
            ChangedItemIconFlag.Ears          => EquipSlot.Ears.ToName(),
            ChangedItemIconFlag.Neck          => EquipSlot.Neck.ToName(),
            ChangedItemIconFlag.Wrists        => EquipSlot.Wrists.ToName(),
            ChangedItemIconFlag.Finger        => "戒指",
            ChangedItemIconFlag.Monster       => "怪物",
            ChangedItemIconFlag.Demihuman     => "蛮族",
            ChangedItemIconFlag.Customization => "外貌",
            ChangedItemIconFlag.Action        => "技能",
            ChangedItemIconFlag.Emote         => "情感动作",
            ChangedItemIconFlag.Mainhand      => "武器（主手）",
            ChangedItemIconFlag.Offhand       => "武器（副手）",
            _                                 => "其他",
        };

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
