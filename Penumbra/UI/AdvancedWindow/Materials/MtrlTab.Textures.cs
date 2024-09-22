using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.GameData;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.String.Classes;
using static Penumbra.GameData.Files.MaterialStructs.SamplerFlags;
using static Penumbra.GameData.Files.ShpkFile;

namespace Penumbra.UI.AdvancedWindow.Materials;

public partial class MtrlTab
{
    public readonly List<(string Label, int TextureIndex, int SamplerIndex, string Description, bool MonoFont)> Textures = new(4);

    public readonly HashSet<int>  UnfoldedTextures = new(4);
    public readonly HashSet<uint> SamplerIds       = new(16);
    public          float         TextureLabelWidth;

    private void UpdateTextures()
    {
        Textures.Clear();
        SamplerIds.Clear();
        if (_associatedShpk == null)
        {
            SamplerIds.UnionWith(Mtrl.ShaderPackage.Samplers.Select(sampler => sampler.SamplerId));
            if (Mtrl.Table != null)
                SamplerIds.Add(TableSamplerId);

            foreach (var (sampler, index) in Mtrl.ShaderPackage.Samplers.WithIndex())
                Textures.Add(($"0x{sampler.SamplerId:X8}", sampler.TextureIndex, index, string.Empty, true));
        }
        else
        {
            foreach (var index in _vertexShaders)
                SamplerIds.UnionWith(_associatedShpk.VertexShaders[index].Samplers.Select(sampler => sampler.Id));
            foreach (var index in _pixelShaders)
                SamplerIds.UnionWith(_associatedShpk.PixelShaders[index].Samplers.Select(sampler => sampler.Id));
            if (!_shadersKnown)
            {
                SamplerIds.UnionWith(Mtrl.ShaderPackage.Samplers.Select(sampler => sampler.SamplerId));
                if (Mtrl.Table != null)
                    SamplerIds.Add(TableSamplerId);
            }

            foreach (var samplerId in SamplerIds)
            {
                var shpkSampler = _associatedShpk.GetSamplerById(samplerId);
                if (shpkSampler is not { Slot: 2 })
                    continue;

                var dkData     = TryGetShpkDevkitData<DevkitSampler>("Samplers", samplerId, true);
                var hasDkLabel = !string.IsNullOrEmpty(dkData?.Label);

                var sampler = Mtrl.GetOrAddSampler(samplerId, dkData?.DefaultTexture ?? string.Empty, out var samplerIndex);
                Textures.Add((hasDkLabel ? dkData!.Label : shpkSampler.Value.Name, sampler.TextureIndex, samplerIndex,
                    dkData?.Description ?? string.Empty, !hasDkLabel));
            }

            if (SamplerIds.Contains(TableSamplerId))
                Mtrl.Table ??= new ColorTable();
        }

        Textures.Sort((x, y) => string.CompareOrdinal(x.Label, y.Label));

        TextureLabelWidth = 50f * UiHelpers.Scale;

        float helpWidth;
        using (var _ = ImRaii.PushFont(UiBuilder.IconFont))
        {
            helpWidth = ImGui.GetStyle().ItemSpacing.X + ImGui.CalcTextSize(FontAwesomeIcon.InfoCircle.ToIconString()).X;
        }

        foreach (var (label, _, _, description, monoFont) in Textures)
        {
            if (!monoFont)
                TextureLabelWidth = Math.Max(TextureLabelWidth, ImGui.CalcTextSize(label).X + (description.Length > 0 ? helpWidth : 0.0f));
        }

        using (var _ = ImRaii.PushFont(UiBuilder.MonoFont))
        {
            foreach (var (label, _, _, description, monoFont) in Textures)
            {
                if (monoFont)
                    TextureLabelWidth = Math.Max(TextureLabelWidth,
                        ImGui.CalcTextSize(label).X + (description.Length > 0 ? helpWidth : 0.0f));
            }
        }

        TextureLabelWidth = TextureLabelWidth / UiHelpers.Scale + 4;
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

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        if (!ImGui.CollapsingHeader("纹理和采样器", ImGuiTreeNodeFlags.DefaultOpen))
            return false;

        var       frameHeight = ImGui.GetFrameHeight();
        var       ret         = false;
        using var table       = ImRaii.Table("##Textures", 3);

        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, frameHeight);
        ImGui.TableSetupColumn("Path",       ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Name",       ImGuiTableColumnFlags.WidthFixed, TextureLabelWidth * UiHelpers.Scale);
        foreach (var (label, textureI, samplerI, description, monoFont) in Textures)
        {
            using var _        = ImRaii.PushId(samplerI);
            var       tmp      = Mtrl.Textures[textureI].Path;
            var       unfolded = UnfoldedTextures.Contains(samplerI);
            ImGui.TableNextColumn();
            if (ImGuiUtil.DrawDisabledButton((unfolded ? FontAwesomeIcon.CaretDown : FontAwesomeIcon.CaretRight).ToIconString(),
                    new Vector2(frameHeight),
                    "此纹理及其相关采样器的设置", false, true))
            {
                unfolded = !unfolded;
                if (unfolded)
                    UnfoldedTextures.Add(samplerI);
                else
                    UnfoldedTextures.Remove(samplerI);
            }

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.InputText(string.Empty, ref tmp, Utf8GamePath.MaxGamePathLength,
                    disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None)
             && tmp.Length > 0
             && tmp != Mtrl.Textures[textureI].Path)
            {
                ret                          = true;
                Mtrl.Textures[textureI].Path = tmp;
            }

            ImGui.TableNextColumn();
            using (var font = ImRaii.PushFont(UiBuilder.MonoFont, monoFont))
            {
                ImGui.AlignTextToFramePadding();
                if (description.Length > 0)
                    ImGuiUtil.LabeledHelpMarker(label, description);
                else
                    ImGui.TextUnformatted(label);
            }

            if (unfolded)
            {
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ret |= DrawMaterialSampler(disabled, textureI, samplerI);
                ImGui.TableNextColumn();
            }
        }

        return ret;
    }

    private static bool ComboTextureAddressMode(ReadOnlySpan<byte> label, ref TextureAddressMode value)
    {
        using var c = ImUtf8.Combo(label, value.ToString());
        if (!c)
            return false;

        var ret = false;
        foreach (var mode in Enum.GetValues<TextureAddressMode>())
        {
            if (ImGui.Selectable(mode.ToString(), mode == value))
            {
                value = mode;
                ret   = true;
            }

            ImUtf8.SelectableHelpMarker(TextureAddressModeTooltip(mode));
        }

        return ret;
    }

    private bool DrawMaterialSampler(bool disabled, int textureIdx, int samplerIdx)
    {
        var     ret     = false;
        ref var texture = ref Mtrl.Textures[textureIdx];
        ref var sampler = ref Mtrl.ShaderPackage.Samplers[samplerIdx];

        var dx11 = texture.DX11;
        if (ImUtf8.Checkbox("在 DirectX 11 中，将文件名前加上 --"u8, ref dx11))
        {
            texture.DX11 = dx11;
            ret          = true;
        }

        ref var samplerFlags = ref Wrap(ref sampler.Flags);

        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        var addressMode = samplerFlags.UAddressMode;
        if (ComboTextureAddressMode("##UAddressMode"u8, ref addressMode))
        {
            samplerFlags.UAddressMode = addressMode;
            ret                       = true;
            SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        ImGui.SameLine();
        ImUtf8.LabeledHelpMarker("U 地址模式"u8, "用于解析超出 0 到 1 范围的 U 纹理坐标的方法。");

        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        addressMode = samplerFlags.VAddressMode;
        if (ComboTextureAddressMode("##VAddressMode"u8, ref addressMode))
        {
            samplerFlags.VAddressMode = addressMode;
            ret                       = true;
            SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        ImGui.SameLine();
        ImUtf8.LabeledHelpMarker("V 地址模式"u8, "用于解析超出 0 到 1 范围的 V 纹理坐标的方法。");

        var lodBias = samplerFlags.LodBias;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        if (ImUtf8.DragScalar("##LoDBias"u8, ref lodBias, -8.0f, 7.984375f, 0.1f))
        {
            samplerFlags.LodBias = lodBias;
            ret                  = true;
            SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        ImGui.SameLine();
        ImUtf8.LabeledHelpMarker("细节层级偏差"u8,
            "来自计算的 mipmap 层级的偏移量。\n\n更高的值意味着纹理在更近的距离开始失去细节。\n更低的值意味着纹理在更远的距离保持细节。");

        var minLod = samplerFlags.MinLod;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        if (ImUtf8.DragScalar("##MinLoD"u8, ref minLod, 0, 15, 0.1f))
        {
            samplerFlags.MinLod = minLod;
            ret                 = true;
            SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        ImGui.SameLine();
        ImUtf8.LabeledHelpMarker("最小细节层级"u8,
            "使用的最详细的 mipmap 层级。\n\n0 是全尺寸纹理，1 是半尺寸纹理，2 是四分之一尺寸纹理，以此类推。\n15 将强制将纹理减少到其最小的 mipmap。");

        using var t = ImUtf8.TreeNode("高级设置"u8);
        if (!t)
            return ret;

        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        if (ImUtf8.InputScalar("纹理标志"u8, ref texture.Flags, "%04X"u8,
                flags: disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None))
            ret = true;

        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        if (ImUtf8.InputScalar("采样器标志"u8, ref sampler.Flags, "%08X"u8,
                flags: ImGuiInputTextFlags.CharsHexadecimal | (disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None)))
        {
            ret = true;
            SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        return ret;
    }
}
