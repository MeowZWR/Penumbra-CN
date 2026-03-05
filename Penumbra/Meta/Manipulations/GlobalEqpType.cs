using Luna.Generators;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Penumbra.Meta.Manipulations;

[NamedEnum(Utf16: false)]
[TooltipEnum]
[JsonConverter(typeof(StringEnumConverter))]
public enum GlobalEqpType
{
    [Name("始终显示耳环")]
    [Tooltip("防止游戏在佩戴特定耳环时被其他模型隐藏耳环。")]
    DoNotHideEarrings,

    [Name("始终显示项链")]
    [Tooltip("防止游戏在佩戴特定项链时被其他模型隐藏项链。")]
    DoNotHideNecklace,

    [Name("始终显示手镯")]
    [Tooltip("防止游戏在佩戴特定手镯时被其他模型隐藏手镯。")]
    DoNotHideBracelets,

    [Name("始终显示戒指 (右指)")]
    [Tooltip(
        "防止游戏在佩戴右手特定戒指时被其他模型隐藏右手戒指。")]
    DoNotHideRingR,

    [Name("始终显示戒指 (左指)")]
    [Tooltip(
        "防止游戏在佩戴左手特定戒指时被其他模型隐藏左手戒指。")]
    DoNotHideRingL,

    [Name("始终显示帽子（硌狮族）")]
    [Tooltip("防止游戏隐藏为硌狮族准备的帽子，这些帽子通常被标记为不在他们身上显示。")]
    DoNotHideHrothgarHats,

    [Name("始终显示帽子（维埃拉族）")]
    [Tooltip("防止游戏隐藏为维埃拉族准备的帽子，这些帽子通常被标记为不在他们身上显示。")]
    DoNotHideVieraHats,

    [Name("始终隐藏角（敖龙族）")]
    [Tooltip("强制游戏隐藏敖龙族的角，无论是否佩戴头部装备。")]
    HideHorns,

    [Name("始终隐藏耳朵（维埃拉族）")]
    [Tooltip("强制游戏隐藏维埃拉族的耳朵，无论是否佩戴头部装备。")]
    HideVieraEars,

    [Name("始终隐藏耳朵（猫魅族）")]
    [Tooltip("强制游戏隐藏猫魅族的耳朵，无论是否佩戴头部装备。")]
    HideMiqoteEars,
}

public static partial class GlobalEqpExtensions
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
}
