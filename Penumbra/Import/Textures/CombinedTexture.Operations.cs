using Luna.Generators;

namespace Penumbra.Import.Textures;

public partial class CombinedTexture
{
    [NamedEnum("ToLabel")]
    [TooltipEnum]
    public enum CombineOp
    {
        LeftMultiply = -4,
        LeftCopy     = -3,
        RightCopy    = -2,
        Invalid      = -1,

        [Name("覆盖层在输入之上")]
        [Tooltip("标准合成。\n将覆盖层应用于输入。")]
        Over = 0,

        [Name("输入在覆盖层之上")]
        [Tooltip("标准合成，反向。\n将输入层应用于覆盖层；可用于修正某些错误的导入。")]
        Under = 1,

        [Name("替换输入")]
        [Tooltip("完全用覆盖层替换输入。\n可用于将目标文件选择为输入，将源文件选择为覆盖。")]
        RightMultiply = 2,

        [Name("复制通道")]
        [Tooltip("用叠加层的一些通道替换输入的通道。\n对于多通道地图非常有用。")]
        CopyChannels = 3,

        [Name("Blend: Multiply - Result over Input")]
        [Tooltip("Multiplies the RGB channel values of the input and the overlay.\nApplies the result over the input.")]
        BlendMultiplyOver = 4,

        [Name("Blend: Multiply - Input over Result")]
        [Tooltip("Multiplies the RGB channel values of the input and the overlay.\nApplies the input over the result.")]
        BlendMultiplyUnder = 5,

        [Name("Blend: Multiply - RGBA")]
        [Tooltip("Multiplies the RGBA channel values of the input and the overlay.\nThis mode is commutative.")]
        BlendMultiplyRgba = 6,

        [Name("Blend: Screen - Result over Input")]
        [Tooltip("Inverts the RGB channel values of the input and the overlay, multiplies them, and inverts them back.\nApplies the result over the input.")]
        BlendScreenOver = 7,

        [Name("Blend: Screen - Input over Result")]
        [Tooltip("Inverts the RGB channel values of the input and the overlay, multiplies them, and inverts them back.\nApplies the result over the input.")]
        BlendScreenUnder = 8,

        [Name("Blend: Screen - RGBA")]
        [Tooltip("Inverts the RGBA channel values of the input and the overlay, multiplies them, and inverts them back.\nThis mode is commutative.")]
        BlendScreenRgba = 9,

        [Name("Blend: Overlay - Result over Input")]
        [Tooltip("Darkens the overlay where the input is darker, lightens the overlay where the input is lighter.\nApplies the result over the input.")]
        BlendOverlayOver = 10,

        [Name("Blend: Overlay - Input over Result")]
        [Tooltip("Darkens the overlay where the input is darker, lightens the overlay where the input is lighter.\nApplies the input over the result.")]
        BlendOverlayUnder = 11,

        [Name("Blend: Overlay - RGBA")]
        [Tooltip("Darkens the overlay where the input is darker, lightens the overlay where the input is lighter, including the Alpha channel.\nThis mode is commutative.")]
        BlendOverlayRgba = 12,

        [Name("Blend: Hard Light - Result over Input")]
        [Tooltip("Darkens the input where the overlay is darker, lightens the input where the overlay is lighter.\nApplies the result over the input.")]
        BlendHardLightOver = 13,

        [Name("Blend: Hard Light - Input over Result")]
        [Tooltip("Darkens the input where the overlay is darker, lightens the input where the overlay is lighter.\nApplies the input over the result.")]
        BlendHardLightUnder = 14,

        [Name("Blend: Hard Light - RGBA")]
        [Tooltip("Darkens the input where the overlay is darker, lightens the input where the overlay is lighter, including the Alpha channel.\nThis mode is commutative.")]
        BlendHardLightRgba = 15,
    }

    [NamedEnum("ToLabel")]
    public enum ResizeOp
    {
        LeftOnly  = -2,
        RightOnly = -1,

        [Name("不调整大小")]
        None = 0,

        [Name("调整覆盖层到输入")]
        ToLeft = 1,

        [Name("调整输入到覆盖层")]
        ToRight = 2,
    }

    [Flags]
    [NamedEnum]
    public enum Channels : byte
    {
        Red   = 1,
        Green = 2,
        Blue  = 4,
        Alpha = 8,
    }

    private static ResizeOp GetActualResizeOp(ResizeOp resizeOp, CombineOp combineOp)
        => combineOp switch
        {
            CombineOp.LeftCopy or CombineOp.LeftMultiply   => ResizeOp.LeftOnly,
            CombineOp.RightCopy or CombineOp.RightMultiply => ResizeOp.RightOnly,
            >= 0                                           => resizeOp,
            _                                              => throw new ArgumentException($"Invalid combine operation {combineOp}"),
        };

    private CombineOp GetActualCombineOp()
    {
        var combineOp = (_left.IsLoaded, _right.IsLoaded) switch
        {
            (true, true)   => _combineOp,
            (true, false)  => CombineOp.LeftMultiply,
            (false, true)  => CombineOp.RightMultiply,
            (false, false) => CombineOp.Invalid,
        };

        if (combineOp == CombineOp.CopyChannels)
        {
            if (_copyChannels == 0)
                combineOp = CombineOp.LeftMultiply;
            else if (_copyChannels == (Channels.Red | Channels.Green | Channels.Blue | Channels.Alpha))
                combineOp = CombineOp.RightMultiply;
        }

        return combineOp switch
        {
            CombineOp.LeftMultiply when _multiplierLeft.IsIdentity && _constantLeft == Vector4.Zero    => CombineOp.LeftCopy,
            CombineOp.RightMultiply when _multiplierRight.IsIdentity && _constantRight == Vector4.Zero => CombineOp.RightCopy,
            _                                                                                          => combineOp,
        };
    }


    private static bool InvertChannels(Channels channels, ref Matrix4x4 multiplier, ref Vector4 constant)
    {
        if (channels.HasFlag(Channels.Red))
            InvertRed(ref multiplier, ref constant);
        if (channels.HasFlag(Channels.Green))
            InvertGreen(ref multiplier, ref constant);
        if (channels.HasFlag(Channels.Blue))
            InvertBlue(ref multiplier, ref constant);
        if (channels.HasFlag(Channels.Alpha))
            InvertAlpha(ref multiplier, ref constant);
        return channels != 0;
    }

    private static void InvertRed(ref Matrix4x4 multiplier, ref Vector4 constant)
    {
        multiplier.M11 = -multiplier.M11;
        multiplier.M21 = -multiplier.M21;
        multiplier.M31 = -multiplier.M31;
        multiplier.M41 = -multiplier.M41;
        constant.X     = 1.0f - constant.X;
    }

    private static void InvertGreen(ref Matrix4x4 multiplier, ref Vector4 constant)
    {
        multiplier.M12 = -multiplier.M12;
        multiplier.M22 = -multiplier.M22;
        multiplier.M32 = -multiplier.M32;
        multiplier.M42 = -multiplier.M42;
        constant.Y     = 1.0f - constant.Y;
    }

    private static void InvertBlue(ref Matrix4x4 multiplier, ref Vector4 constant)
    {
        multiplier.M13 = -multiplier.M13;
        multiplier.M23 = -multiplier.M23;
        multiplier.M33 = -multiplier.M33;
        multiplier.M43 = -multiplier.M43;
        constant.Z     = 1.0f - constant.Z;
    }

    private static void InvertAlpha(ref Matrix4x4 multiplier, ref Vector4 constant)
    {
        multiplier.M14 = -multiplier.M14;
        multiplier.M24 = -multiplier.M24;
        multiplier.M34 = -multiplier.M34;
        multiplier.M44 = -multiplier.M44;
        constant.W     = 1.0f - constant.W;
    }
}
