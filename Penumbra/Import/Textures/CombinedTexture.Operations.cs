namespace Penumbra.Import.Textures;

public partial class CombinedTexture
{
    private enum CombineOp
    {
        LeftMultiply  = -4,
        LeftCopy      = -3,
        RightCopy     = -2,
        Invalid       = -1,
        Over          = 0,
        Under         = 1,
        RightMultiply = 2,
        CopyChannels  = 3,
    }

    private enum ResizeOp
    {
        LeftOnly  = -2,
        RightOnly = -1,
        None      = 0,
        ToLeft    = 1,
        ToRight   = 2,
    }

    [Flags]
    private enum Channels : byte
    {
        Red   = 1,
        Green = 2,
        Blue  = 4,
        Alpha = 8,
    }

    private static readonly IReadOnlyList<string> CombineOpLabels = new[]
    {
        "覆盖层在输入之上",
        "输入在覆盖层之上",
        "替换输入",
        "复制通道",
    };

    private static readonly IReadOnlyList<string> CombineOpTooltips = new[]
    {
        "标准合成。\n将覆盖层应用于输入。",
        "标准合成，反向。\n将输入层应用于覆盖层；可用于修正某些错误的导入。",
        "完全用覆盖层替换输入。\n可用于将目标文件选择为输入，将源文件选择为覆盖。",
        "用覆盖层的一些通道替换输入的通道。\n对于多通道地图非常有用。",
    };

    private static readonly IReadOnlyList<string> ResizeOpLabels = new string[]
    {
        "不调整大小",
        "调整覆盖到输入",
        "调整输入到覆盖",
    };

    private static ResizeOp GetActualResizeOp(ResizeOp resizeOp, CombineOp combineOp)
        => combineOp switch
        {
            CombineOp.LeftCopy      => ResizeOp.LeftOnly,
            CombineOp.LeftMultiply  => ResizeOp.LeftOnly,
            CombineOp.RightCopy     => ResizeOp.RightOnly,
            CombineOp.RightMultiply => ResizeOp.RightOnly,
            CombineOp.Over          => resizeOp,
            CombineOp.Under         => resizeOp,
            CombineOp.CopyChannels  => resizeOp,
            _                       => throw new ArgumentException($"Invalid combine operation {combineOp}"),
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
