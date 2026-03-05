using Dalamud.Interface;
using ImSharp;
using Luna;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.String.Classes;
using static Penumbra.GameData.Files.MaterialStructs.SamplerFlags;
using static Penumbra.GameData.Files.ShpkFile;

namespace Penumbra.UI.FileEditing.Materials;

public partial class MaterialEditor
{
    public readonly List<(string Label, int TextureIndex, int SamplerIndex, string Description, bool MonoFont)> Textures = new(4);

    public readonly HashSet<int>  UnfoldedTextures = new(4);
    public readonly HashSet<uint> TextureIds       = new(16);
    public readonly HashSet<uint> SamplerIds       = new(16);
    public          float         TextureLabelWidth;
    private         bool          _samplersPinned;

    private void UpdateTextures()
    {
        Textures.Clear();
        TextureIds.Clear();
        SamplerIds.Clear();
        if (_associatedShpk == null)
        {
            TextureIds.UnionWith(Mtrl.ShaderPackage.Samplers.Select(sampler => sampler.SamplerId));
            SamplerIds.UnionWith(Mtrl.ShaderPackage.Samplers.Select(sampler => sampler.SamplerId));
            if (Mtrl.Table != null)
                TextureIds.Add(TableSamplerId);

            foreach (var (index, sampler) in Mtrl.ShaderPackage.Samplers.Index())
                Textures.Add(($"0x{sampler.SamplerId:X8}", sampler.TextureIndex, index, string.Empty, true));
        }
        else
        {
            foreach (var index in _vertexShaders)
            {
                TextureIds.UnionWith(_associatedShpk.VertexShaders[index].Textures.Select(texture => texture.Id));
                SamplerIds.UnionWith(_associatedShpk.VertexShaders[index].Samplers.Select(sampler => sampler.Id));
            }

            foreach (var index in _pixelShaders)
            {
                TextureIds.UnionWith(_associatedShpk.PixelShaders[index].Textures.Select(texture => texture.Id));
                SamplerIds.UnionWith(_associatedShpk.PixelShaders[index].Samplers.Select(sampler => sampler.Id));
            }

            if (_samplersPinned || !_shadersKnown)
            {
                TextureIds.UnionWith(Mtrl.ShaderPackage.Samplers.Select(sampler => sampler.SamplerId));
                if (Mtrl.Table != null)
                    TextureIds.Add(TableSamplerId);
            }

            foreach (var textureId in TextureIds)
            {
                var shpkTexture = _associatedShpk.GetTextureById(textureId);
                if (shpkTexture is not { Slot: 2 } && (shpkTexture is not null || textureId == TableSamplerId))
                    continue;

                var dkData     = TryGetShpkDevkitData<DevkitSampler>("Samplers", textureId, true);
                var hasDkLabel = !string.IsNullOrEmpty(dkData?.Label);

                var sampler = Mtrl.GetOrAddSampler(textureId, dkData?.DefaultTexture ?? string.Empty, out var samplerIndex);
                Textures.Add((hasDkLabel ? dkData!.Label : shpkTexture!.Value.Name, sampler.TextureIndex, samplerIndex,
                    dkData?.Description ?? string.Empty, !hasDkLabel));
            }

            if (TextureIds.Contains(TableSamplerId))
                Mtrl.Table ??= new ColorTable();
        }

        Textures.Sort((x, y) => string.CompareOrdinal(x.Label, y.Label));

        TextureLabelWidth = 50f * Im.Style.GlobalScale;

        var helpWidth = Im.Style.ItemSpacing.X + ImEx.Icon.CalculateSize(FontAwesomeIcon.InfoCircle.Icon()).X;

        foreach (var (label, _, _, description, monoFont) in Textures)
        {
            if (!monoFont)
                TextureLabelWidth = Math.Max(TextureLabelWidth, Im.Font.CalculateSize(label).X + (description.Length > 0 ? helpWidth : 0.0f));
        }

        using (Im.Font.PushMono())
        {
            foreach (var (label, _, _, description, monoFont) in Textures)
            {
                if (monoFont)
                    TextureLabelWidth = Math.Max(TextureLabelWidth, Im.Font.CalculateSize(label).X + (description.Length > 0 ? helpWidth : 0.0f));
            }
        }

        TextureLabelWidth = TextureLabelWidth / Im.Style.GlobalScale + 4;
    }

    private static ReadOnlySpan<byte> TextureAddressModeTooltip(TextureAddressMode addressMode)
        => addressMode switch
        {
            TextureAddressMode.Wrap =>
                "在每个 UV 整数交界处平铺纹理。\n\n例如，对于 U 值在 0 到 3 之间，纹理重复三次。"u8,
            TextureAddressMode.Mirror =>
                "在每个 UV 整数交界处翻转纹理。\n\n例如，对于 U 值在 0 到 1 之间，纹理正常使用；在 1 到 2 之间，纹理镜像；在 2 到 3 之间，纹理再次正常；依此类推。"u8,
            TextureAddressMode.Clamp =>
                "超出范围 [0.0, 1.0] 的纹理坐标将分别设置为 0.0 或 1.0 处的纹理颜色。"u8,
            TextureAddressMode.Border => "超出范围 [0.0, 1.0] 的纹理坐标将设置为边缘颜色（通常为黑色）。"u8,
            _                         => ""u8,
        };

    private bool DrawTextureSection(bool disabled)
    {
        if (Textures.Count == 0)
            return false;

        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        if (!Im.Tree.Header("纹理和采样器"u8, TreeNodeFlags.DefaultOpen))
            return false;

        var       frameHeight = Im.Style.FrameHeight;
        var       ret         = false;
        using var table       = Im.Table.Begin("##Textures"u8, 3);

        table.SetupColumn(StringU8.Empty, TableColumnFlags.WidthFixed, frameHeight);
        table.SetupColumn("Path"u8,       TableColumnFlags.WidthStretch);
        table.SetupColumn("Name"u8,       TableColumnFlags.WidthFixed, TextureLabelWidth * Im.Style.GlobalScale);
        foreach (var (label, textureI, samplerI, description, monoFont) in Textures)
        {
            using var _        = Im.Id.Push(samplerI);
            var       tmp      = Mtrl.Textures[textureI].Path;
            var       unfolded = UnfoldedTextures.Contains(samplerI);
            table.NextColumn();
            if (ImEx.Icon.Button(unfolded ? LunaStyle.ExpandDownIcon : LunaStyle.CollapseUpIcon, "此纹理及其相关采样器的设置"u8))
            {
                unfolded = !unfolded;
                if (unfolded)
                    UnfoldedTextures.Add(samplerI);
                else
                    UnfoldedTextures.Remove(samplerI);
            }

            table.NextColumn();
            Im.Item.SetNextWidth(Im.ContentRegion.Available.X);
            if (Im.Input.Text(StringU8.Empty, ref tmp, default, disabled ? InputTextFlags.ReadOnly : InputTextFlags.None, Utf8GamePath.MaxGamePathLength)
             && tmp.Length > 0
             && tmp != Mtrl.Textures[textureI].Path)
            {
                ret                          = true;
                Mtrl.Textures[textureI].Path = tmp;
            }

            table.NextColumn();
            using (Im.Font.Mono.Push(monoFont))
            {
                if (description.Length > 0)
                    LunaStyle.DrawHelpMarkerLabel(label, description);
                else
                    ImEx.TextFrameAligned(label);
            }

            if (unfolded)
            {
                table.NextColumn();
                table.NextColumn();
                ret |= DrawMaterialSampler(disabled, textureI, samplerI);
                table.NextColumn();
            }
        }

        return ret;
    }

    private static bool ComboTextureAddressMode(ReadOnlySpan<byte> label, ref TextureAddressMode value)
    {
        using var c = Im.Combo.Begin(label, value.ToNameU8());
        if (!c)
            return false;

        var ret = false;
        foreach (var mode in TextureAddressMode.Values)
        {
            if (Im.Selectable(mode.ToNameU8(), mode == value))
            {
                value = mode;
                ret   = true;
            }

            LunaStyle.DrawRightAlignedHelpMarker(TextureAddressModeTooltip(mode));
        }

        return ret;
    }

    private bool DrawMaterialSampler(bool disabled, int textureIdx, int samplerIdx)
    {
        var     ret     = false;
        ref var texture = ref Mtrl.Textures[textureIdx];
        ref var sampler = ref Mtrl.ShaderPackage.Samplers[samplerIdx];

        var dx11 = texture.DX11;
        if (Im.Checkbox("在 DirectX 11 中，将文件名前加上 --"u8, ref dx11))
        {
            texture.DX11 = dx11;
            ret          = true;
        }

        if (SamplerIds.Contains(sampler.SamplerId))
        {
            ref var samplerFlags = ref Wrap(ref sampler.Flags);

            Im.Item.SetNextWidthScaled(100.0f);
            var addressMode = samplerFlags.UAddressMode;
            if (ComboTextureAddressMode("##UAddressMode"u8, ref addressMode))
            {
                samplerFlags.UAddressMode = addressMode;
                ret                       = true;
                SetSamplerFlags(sampler.SamplerId, sampler.Flags);
            }

            Im.Line.SameInner();
            LunaStyle.DrawHelpMarkerLabel("U 地址模式"u8,
                "用于解析超出 0 到 1 范围的 U 纹理坐标的方法。"u8);

            Im.Item.SetNextWidthScaled(100.0f);
            addressMode = samplerFlags.VAddressMode;
            if (ComboTextureAddressMode("##VAddressMode"u8, ref addressMode))
            {
                samplerFlags.VAddressMode = addressMode;
                ret                       = true;
                SetSamplerFlags(sampler.SamplerId, sampler.Flags);
            }

            Im.Line.Same();
            LunaStyle.DrawHelpMarkerLabel("V 地址模式"u8,
                "用于解析超出 0 到 1 范围的 V 纹理坐标的方法。"u8);

            var lodBias = samplerFlags.LodBias;
            Im.Item.SetNextWidthScaled(100.0f);
            if (Im.Drag("##LoDBias"u8, ref lodBias, -8.0f, 7.984375f, 0.1f))
            {
                samplerFlags.LodBias = lodBias;
                ret                  = true;
                SetSamplerFlags(sampler.SamplerId, sampler.Flags);
            }

            Im.Line.Same();
            LunaStyle.DrawHelpMarkerLabel("细节层级偏差"u8,
                "来自计算的 mipmap 层级的偏移量。\n\n更高的值意味着纹理在更近的距离开始失去细节。\n更低的值意味着纹理在更远的距离保持细节。"u8);

            var minLod = samplerFlags.MinLod;
            Im.Item.SetNextWidthScaled(100.0f);
            if (Im.Drag("##MinLoD"u8, ref minLod, 0, 15, 0.1f))
            {
                samplerFlags.MinLod = minLod;
                ret                 = true;
                SetSamplerFlags(sampler.SamplerId, sampler.Flags);
            }

            Im.Line.Same();
            LunaStyle.DrawHelpMarkerLabel("最小细节层级"u8,
                "使用的最详细的 mipmap 层级。\n\n0 是全尺寸纹理，1 是半尺寸纹理，2 是四分之一尺寸纹理，以此类推。\n15 将强制将纹理减少到其最小的 mipmap。"u8);
        }
        else
        {
            Im.Text("此纹理没有专用的采样器。"u8);
        }

        using var t = Im.Tree.Node("高级设置"u8);
        if (!t)
            return ret;

        Im.Item.SetNextWidthScaled(100.0f);
        if (Im.Input.Scalar("纹理标志"u8, ref texture.Flags, "%04X"u8,
                flags: disabled ? InputTextFlags.ReadOnly : InputTextFlags.None))
            ret = true;

        Im.Item.SetNextWidthScaled(100.0f);
        if (Im.Input.Scalar("采样器标志"u8, ref sampler.Flags, "%08X"u8, flags: InputTextFlags.CharsHexadecimal | (disabled ? InputTextFlags.ReadOnly : InputTextFlags.None)))
        {
            ret = true;
            SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        return ret;
    }
}
