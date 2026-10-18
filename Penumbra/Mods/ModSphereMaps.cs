using Lumina.Data.Files;
using Lumina.Extensions;
using Luna;
using Newtonsoft.Json;
using OtterTex;
using Penumbra.Import.Textures;
using SixLabors.ImageSharp.Processing;

namespace Penumbra.Mods;

/// <summary> Mod-level sphere map slots, stored in a hidden directory that is never part of any option's redirections. </summary>
public static class ModSphereMaps
{
    public const string DirectoryName = ".spheremaps";
    public const string TableFileName = "slots.json";
    public const string ArrayPrefix   = "array_";
    public const int    FirstSlot     = 6;
    public const int    LastSlot      = 25;

    /// <summary> Layout of the vanilla sphere_d_array.tex, mip-major with all slices of a mip stored consecutively. </summary>
    public sealed record ArrayLayout(int Width, int Height, int ArraySize, int MipCount, int[] MipOffsets, int[] SliceSizes, int FileSize);

    public static bool IsEditableSlot(int slot)
        => slot is >= FirstSlot and <= LastSlot;

    public static DirectoryInfo GetDirectory(DirectoryInfo modPath)
        => new(Path.Combine(modPath.FullName, DirectoryName));

    public static string SlotRelativePath(int slot)
        => $"{DirectoryName}/slot_{slot:D2}.tex";

    /// <summary> Set the Hidden attribute that mod scans skip. Archives do not preserve it. </summary>
    public static void EnsureHidden(DirectoryInfo modPath)
    {
        var dir = GetDirectory(modPath);
        try
        {
            if (dir.Exists && !dir.IsHidden())
                dir.Attributes |= FileAttributes.Hidden;
        }
        catch (Exception ex)
        {
            Penumbra.Log.Warning($"Could not hide sphere map directory {dir.FullName}:\n{ex}");
        }
    }

    public static string ResolvePath(DirectoryInfo modPath, string relativePath)
        => Path.GetFullPath(Path.Combine(modPath.FullName, relativePath));

    private static bool IsInsideDirectory(DirectoryInfo modPath, string fullPath)
    {
        var dir = GetDirectory(modPath).FullName + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
    }

    public static SortedDictionary<int, string> LoadTable(DirectoryInfo modPath)
    {
        var ret  = new SortedDictionary<int, string>();
        var file = Path.Combine(GetDirectory(modPath).FullName, TableFileName);
        if (!File.Exists(file))
            return ret;

        EnsureHidden(modPath);
        try
        {
            var table = JsonConvert.DeserializeObject<Dictionary<int, string>>(File.ReadAllText(file));
            if (table is null)
                return ret;

            foreach (var (slot, relativePath) in table)
            {
                if (!IsEditableSlot(slot) || string.IsNullOrEmpty(relativePath))
                    continue;

                var fullPath = ResolvePath(modPath, relativePath);
                if (IsInsideDirectory(modPath, fullPath) && File.Exists(fullPath))
                    ret[slot] = relativePath;
            }
        }
        catch (Exception ex)
        {
            Penumbra.Log.Warning($"Could not read sphere map table {file}:\n{ex}");
        }

        return ret;
    }

    public static void SaveTable(DirectoryInfo modPath, IReadOnlyDictionary<int, string> table)
    {
        var dir  = GetDirectory(modPath);
        var file = Path.Combine(dir.FullName, TableFileName);
        if (table.Count is 0)
        {
            if (dir.Exists)
                dir.Delete(true);
            return;
        }

        dir.Create();
        EnsureHidden(modPath);
        File.WriteAllText(file, JsonConvert.SerializeObject(table, Formatting.Indented));
    }

    public static ArrayLayout? ReadLayout(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var r      = new BinaryReader(stream);
        var       header = r.ReadStructure<TexFile.TexHeader>();
        if (header.Format is not TexFile.TextureFormat.BC1 || header.ArraySize is 0 || header.MipCount is 0)
            return null;

        return ReadMips(header, data.Length, header.ArraySize);
    }

    private static unsafe ArrayLayout? ReadMips(TexFile.TexHeader header, int fileSize, int arraySize)
    {
        var mipCount = Math.Min(header.MipCount, 13);
        var offsets  = new int[mipCount];
        var sizes    = new int[mipCount];
        for (var i = 0; i < mipCount; ++i)
        {
            var start = (int)header.OffsetToSurface[i];
            var end   = i + 1 < mipCount ? (int)header.OffsetToSurface[i + 1] : fileSize;
            if (start <= 0 || end <= start || end > fileSize || (end - start) % arraySize is not 0)
                return null;

            offsets[i] = start;
            sizes[i]   = (end - start) / arraySize;
        }

        return new ArrayLayout(header.Width, header.Height, arraySize, mipCount, offsets, sizes, fileSize);
    }

    /// <summary> Convert a PNG to a single slice texture matching the vanilla slice size, mip chain and BC1 format. </summary>
    public static void ImportPng(TextureManager textures, ArrayLayout layout, string pngPath, string targetPath)
    {
        ScratchImage dds;
        using (var stream = File.OpenRead(pngPath))
        {
            using var bitmap = CustomBitmap.FromStream(stream);
            if (bitmap.Width != layout.Width || bitmap.Height != layout.Height)
                bitmap.Mutate(ctx => ctx.Resize(layout.Width, layout.Height, KnownResamplers.Lanczos3));
            var (rgba, width, height) = bitmap.GetPixelData();
            dds = TextureManager.ConvertToDds(rgba, width, height).AsDds!;
        }

        var compressed = textures.CreateCompressed(dds, layout.MipCount > 1, DXGIFormat.BC1UNorm, CancellationToken.None);
        if (compressed.Meta.MipLevels != layout.MipCount)
            throw new InvalidOperationException($"生成的 Mip 层数 ({compressed.Meta.MipLevels}) 与原版 ({layout.MipCount}) 不一致。");

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var tmp = targetPath + ".tmp";
        TextureManager.SaveTex(tmp, compressed);
        File.Move(tmp, targetPath, true);
    }

    /// <summary> Copy all mips of the declared slots over a copy of the vanilla array. </summary>
    public static byte[] BuildArray(byte[] vanilla, ArrayLayout layout, DirectoryInfo modPath, IReadOnlyDictionary<int, string> table)
    {
        var ret = (byte[])vanilla.Clone();
        foreach (var (slot, relativePath) in table)
        {
            var file = ResolvePath(modPath, relativePath);
            var data = File.ReadAllBytes(file);
            var mips = ReadSlotMips(data, layout);
            if (mips is null)
            {
                Penumbra.Log.Warning($"Sphere map {file} does not match the vanilla slice layout, skipped.");
                continue;
            }

            for (var i = 0; i < layout.MipCount; ++i)
                Buffer.BlockCopy(data, mips[i], ret, layout.MipOffsets[i] + slot * layout.SliceSizes[i], layout.SliceSizes[i]);
        }

        return ret;
    }

    private static int[]? ReadSlotMips(byte[] data, ArrayLayout layout)
    {
        using var stream = new MemoryStream(data);
        using var r      = new BinaryReader(stream);
        var       header = r.ReadStructure<TexFile.TexHeader>();
        if (header.Format is not TexFile.TextureFormat.BC1
         || header.Width != layout.Width
         || header.Height != layout.Height
         || header.MipCount < layout.MipCount)
            return null;

        var mips = ReadMips(header, data.Length, 1);
        if (mips is null)
            return null;

        for (var i = 0; i < layout.MipCount; ++i)
        {
            if (mips.SliceSizes[i] < layout.SliceSizes[i])
                return null;
        }

        return mips.MipOffsets;
    }
}
