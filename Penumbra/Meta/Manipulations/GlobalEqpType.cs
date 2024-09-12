using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Penumbra.Meta.Manipulations;

[JsonConverter(typeof(StringEnumConverter))]
public enum GlobalEqpType
{
    DoNotHideEarrings,
    DoNotHideNecklace,
    DoNotHideBracelets,
    DoNotHideRingR,
    DoNotHideRingL,
    DoNotHideHrothgarHats,
    DoNotHideVieraHats,
}

public static class GlobalEqpExtensions
{
    public static bool HasCondition(this GlobalEqpType type)
        => type switch
        {
            GlobalEqpType.DoNotHideEarrings     => true,
            GlobalEqpType.DoNotHideNecklace     => true,
            GlobalEqpType.DoNotHideBracelets    => true,
            GlobalEqpType.DoNotHideRingR        => true,
            GlobalEqpType.DoNotHideRingL        => true,
            GlobalEqpType.DoNotHideHrothgarHats => false,
            GlobalEqpType.DoNotHideVieraHats    => false,
            _                                   => false,
        };


    public static ReadOnlySpan<byte> ToName(this GlobalEqpType type)
        => type switch
        {
            GlobalEqpType.DoNotHideEarrings     => "始终显示耳环"u8,
            GlobalEqpType.DoNotHideNecklace     => "始终显示项链"u8,
            GlobalEqpType.DoNotHideBracelets    => "始终显示手镯"u8,
            GlobalEqpType.DoNotHideRingR        => "始终显示戒指 (右指)"u8,
            GlobalEqpType.DoNotHideRingL        => "始终显示戒指 (左指)"u8,
            GlobalEqpType.DoNotHideHrothgarHats => "始终为硌狮族显示帽子"u8,
            GlobalEqpType.DoNotHideVieraHats    => "始终为维埃拉族显示帽子"u8,
            _                                   => "\0"u8,
        };

    public static ReadOnlySpan<byte> ToDescription(this GlobalEqpType type)
        => type switch
        {
            GlobalEqpType.DoNotHideEarrings => "防止游戏在佩戴特定耳环时被其他模型隐藏耳环。"u8,
            GlobalEqpType.DoNotHideNecklace =>
                "防止游戏在佩戴特定项链时被其他模型隐藏项链。"u8,
            GlobalEqpType.DoNotHideBracelets =>
                "防止游戏在佩戴特定项链时被其他模型隐藏手镯。"u8,
            GlobalEqpType.DoNotHideRingR =>
                "防止游戏在佩戴右手指上的特定戒指时被其他模型隐藏右手指上的戒指。"u8,
            GlobalEqpType.DoNotHideRingL =>
                "防止游戏在佩戴左手指上的特定戒指时被其他模型隐藏左手指上的戒指。"u8,
            GlobalEqpType.DoNotHideHrothgarHats =>
                "防止游戏隐藏为硌狮族准备的帽子，这些帽子通常被标记为不在他们身上显示。"u8,
            GlobalEqpType.DoNotHideVieraHats =>
                "防止游戏隐藏为维埃拉族准备的帽子，这些帽子通常被标记为不在他们身上显示。"u8,
            _ => "\0"u8,
        };
}
