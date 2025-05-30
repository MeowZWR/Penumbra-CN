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
    HideHorns,
    HideVieraEars,
    HideMiqoteEars,
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
            GlobalEqpType.HideHorns             => false,
            GlobalEqpType.HideVieraEars         => false,
            GlobalEqpType.HideMiqoteEars        => false,
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
            GlobalEqpType.DoNotHideHrothgarHats => "始终显示帽子（硌狮族）"u8,
            GlobalEqpType.DoNotHideVieraHats    => "始终显示帽子（维埃拉族）"u8,
            GlobalEqpType.HideHorns             => "始终隐藏角（敖龙族）"u8,
            GlobalEqpType.HideVieraEars         => "始终隐藏耳朵（维埃拉族）"u8,
            GlobalEqpType.HideMiqoteEars        => "始终隐藏耳朵（猫魅族）"u8,
            _                                   => "\0"u8,
        };

    public static ReadOnlySpan<byte> ToDescription(this GlobalEqpType type)
        => type switch
        {
            GlobalEqpType.DoNotHideEarrings => "防止游戏在佩戴特定耳环时被其他模型隐藏耳环。"u8,
            GlobalEqpType.DoNotHideNecklace => "防止游戏在佩戴特定项链时被其他模型隐藏项链。"u8,
            GlobalEqpType.DoNotHideBracelets => "防止游戏在佩戴特定手镯时被其他模型隐藏手镯。"u8,
            GlobalEqpType.DoNotHideRingR => "防止游戏在佩戴右手特定戒指时被其他模型隐藏右手戒指。"u8,
            GlobalEqpType.DoNotHideRingL => "防止游戏在佩戴左手特定戒指时被其他模型隐藏左手戒指。"u8,
            GlobalEqpType.DoNotHideHrothgarHats => "防止游戏隐藏为硌狮族准备的帽子，这些帽子通常被标记为不在他们身上显示。"u8,
            GlobalEqpType.DoNotHideVieraHats => "防止游戏隐藏为维埃拉族准备的帽子，这些帽子通常被标记为不在他们身上显示。"u8,
            GlobalEqpType.HideHorns => "强制游戏隐藏敖龙族的角，无论是否佩戴头部装备。"u8,
            GlobalEqpType.HideVieraEars => "强制游戏隐藏维埃拉族的耳朵，无论是否佩戴头部装备。"u8,
            GlobalEqpType.HideMiqoteEars => "强制游戏隐藏猫魅族的耳朵，无论是否佩戴头部装备。"u8,
            _ => "\0"u8,
        };
}
