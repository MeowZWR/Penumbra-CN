using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Luna;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Data;
using Penumbra.Import.Textures;
using Penumbra.Interop.PathResolving;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Services;

/// <summary> Builds and owns one sphere map array per mod with a slot table, and maps materials to the mod that provides them. </summary>
public sealed unsafe class ModSphereMapManager : IRequiredService, IDisposable
{
    private const int ReleaseDelayFrames = 10;
    private const int IdleFrames         = 1800;

    private readonly ModManager          _modManager;
    private readonly Configuration       _config;
    private readonly IFramework          _framework;
    private readonly TextureManager      _textures;
    private readonly CharacterUtility    _utility;
    private readonly CommunicatorService _communicator;

    private readonly ConcurrentDictionary<string, Entry>                       _entries     = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<nint, (Entry? Entry, int Generation)> _materials   = new();
    private readonly ConcurrentDictionary<nint, byte>                          _ownTextures = new();
    private readonly Dictionary<string, Task>                                  _imports     = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string>                                _errors      = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(SafeTextureHandle Handle, nint Preview, nint Texture, long Frame)> _pendingReleases = [];
    private readonly Lock                                                       _temporaryLock      = new();
    private readonly Dictionary<TemporaryRegistrationKey, TemporaryRegistration> _temporaryRegistrations = [];

    private IReadOnlyDictionary<TemporaryMaterialKey, Entry> _temporaryMaterials =
        new Dictionary<TemporaryMaterialKey, Entry>();
    private Entry[] _temporaryEntries = [];

    private byte[]?                    _vanilla;
    private ModSphereMaps.ArrayLayout? _layout;
    private string?                    _layoutError;
    private volatile string            _basePath = string.Empty;
    private volatile int               _generation;
    private long                       _frame;
    private long                       _temporaryOrder;
    private volatile bool              _active;

    public ModSphereMapManager(ModManager modManager, Configuration config, IFramework framework, TextureManager textures,
        CharacterUtility utility, CommunicatorService communicator)
    {
        _modManager   = modManager;
        _config       = config;
        _framework    = framework;
        _textures     = textures;
        _utility      = utility;
        _communicator = communicator;

        _communicator.ModDiscoveryFinished.Subscribe(OnModDiscoveryFinished, ModDiscoveryFinished.Priority.ModSphereMapManager);
        _communicator.ModPathChanged.Subscribe(OnModPathChanged, ModPathChanged.Priority.ModSphereMapManager);
        _framework.Update += OnFrameworkUpdate;
        OnModDiscoveryFinished();
    }

    public void Dispose()
    {
        _active = false;
        _framework.Update -= OnFrameworkUpdate;
        _communicator.ModDiscoveryFinished.Unsubscribe(OnModDiscoveryFinished);
        _communicator.ModPathChanged.Unsubscribe(OnModPathChanged);
        foreach (var entry in _entries.Values)
        {
            entry.Removed = true;
            Release(entry);
        }

        _entries.Clear();
        lock (_temporaryLock)
        {
            foreach (var entry in _temporaryEntries)
            {
                entry.Removed = true;
                Release(entry);
            }

            _temporaryRegistrations.Clear();
            _temporaryMaterials = new Dictionary<TemporaryMaterialKey, Entry>();
            _temporaryEntries   = [];
        }
        _materials.Clear();
        lock (_pendingReleases)
        {
            foreach (var (handle, preview, _, _) in _pendingReleases)
                Free(handle, preview);
            _pendingReleases.Clear();
        }

        _ownTextures.Clear();
    }

    /// <summary> Whether any mod array may currently be bound. Read by render threads. </summary>
    public bool Active
        => _active;

    private bool Enabled
        => _config.Main.EnableMods && _config.Advanced.EnableExtendedFeatures;

    public ModSphereMaps.ArrayLayout? Layout
    {
        get
        {
            if (_layout is not null || _layoutError is not null)
                return _layout;

            // Read back from the loaded GPU texture; Lumina fails to read this array from SqPack.
            var handle = (TextureResourceHandle*)_utility.DefaultSphereDArrayResource;
            if (handle is null || handle->Texture is null)
                return null;

            try
            {
                _vanilla = ReadVanilla(handle);
                _layout  = ModSphereMaps.ReadLayout(_vanilla);
                if (_layout is null)
                    throw new InvalidOperationException("回读的原版球面贴图布局无法识别。");
            }
            catch (Exception ex)
            {
                _layoutError = ex is InvalidOperationException ? ex.Message : "无法读取原版球面贴图数组。";
                Penumbra.Log.Error($"Could not read vanilla {GamePaths.Tex.SphereDArray}:\n{ex}");
            }

            return _layout;
        }
    }

    /// <summary> The vanilla array, used for previews of undeclared slots. </summary>
    public TextureResourceHandle* VanillaHandle
        => (TextureResourceHandle*)_utility.DefaultSphereDArrayResource;

    public IReadOnlyDictionary<int, string>? GetSlots(Mod mod)
        => _entries.TryGetValue(mod.Identifier, out var entry) ? entry.Slots : null;

    /// <summary> Get absolute sphere-map slot files for a material supplied by a regular mod. </summary>
    public IReadOnlyDictionary<int, string>? GetSlotFiles(string materialPath)
    {
        if (PathDataHandler.Split(materialPath.AsSpan(), out var actualPath, out _))
            materialPath = actualPath.ToString();

        return FindEntry(materialPath.Replace('\\', '/'))?.Files;
    }

    /// <summary> Get absolute sphere-map slot files for a currently loaded material resource. </summary>
    public IReadOnlyDictionary<int, string>? GetSlotFiles(nint materialResourceHandle)
        => materialResourceHandle == nint.Zero ? null : FindEntry((MaterialResourceHandle*)materialResourceHandle)?.Files;

    /// <summary> Replace one temporary sphere-map registration for a collection. </summary>
    public bool SetTemporary(string tag, LocalCollectionId collection, IReadOnlyDictionary<int, Dictionary<int, string>> materials,
        int priority)
    {
        if (string.IsNullOrWhiteSpace(tag) || collection.Id == 0)
            return false;

        Dictionary<int, IReadOnlyDictionary<int, string>> normalized = [];
        try
        {
            foreach (var (materialCrc, slots) in materials)
            {
                Dictionary<int, string> files = [];
                foreach (var (slot, path) in slots)
                {
                    if (!ModSphereMaps.IsEditableSlot(slot) || string.IsNullOrWhiteSpace(path))
                        return false;

                    var fullPath = Path.GetFullPath(path);
                    if (!File.Exists(fullPath))
                        return false;
                    files[slot] = fullPath;
                }

                if (files.Count > 0)
                    normalized[materialCrc] = files;
            }
        }
        catch
        {
            return false;
        }

        var key = new TemporaryRegistrationKey(tag, collection.Id, priority);
        lock (_temporaryLock)
        {
            RemoveTemporaryRegistration(key);
            if (normalized.Count > 0)
            {
                var order       = ++_temporaryOrder;
                var bySignature = new Dictionary<string, Entry>(StringComparer.Ordinal);
                Dictionary<int, Entry> entries = [];
                var entryIndex = 0;
                foreach (var (materialCrc, files) in normalized)
                {
                    var signature = string.Join('\n', files.OrderBy(static pair => pair.Key)
                        .Select(static pair => $"{pair.Key}:{pair.Value}"));
                    if (!bySignature.TryGetValue(signature, out var entry))
                    {
                        entry = new Entry($"temporary sphere maps {tag}/{collection.Id}/{priority}/{entryIndex++}", files, files);
                        bySignature.Add(signature, entry);
                    }

                    entries[materialCrc] = entry;
                }

                _temporaryRegistrations.Add(key, new TemporaryRegistration(priority, order, entries));
            }

            RebuildTemporarySnapshots();
        }

        Invalidate();
        return true;
    }

    /// <summary> Remove all temporary sphere-map registrations for a deleted collection. </summary>
    public void RemoveTemporary(LocalCollectionId collection)
    {
        lock (_temporaryLock)
        {
            foreach (var key in _temporaryRegistrations.Keys.Where(key => key.CollectionId == collection.Id).ToArray())
                RemoveTemporaryRegistration(key);
            RebuildTemporarySnapshots();
        }

        Invalidate();
    }

    public bool IsBusy(Mod mod)
        => _imports.TryGetValue(mod.Identifier, out var task) && !task.IsCompleted;

    public string? GetError(Mod mod)
        => _errors.GetValueOrDefault(mod.Identifier) ?? (_entries.TryGetValue(mod.Identifier, out var entry) ? entry.LoadError : null);

    /// <summary> Get the mod array for previews, requesting it to be loaded if necessary. Call from the framework thread. </summary>
    public TextureResourceHandle* GetPreview(Mod mod)
        => _entries.TryGetValue(mod.Identifier, out var entry) ? Touch(entry) : null;

    /// <summary> Get the mod array for a material file if it lies inside a mod with a slot table. </summary>
    public TextureResourceHandle* GetPreview(string filePath)
        => FindEntry(filePath.Replace('\\', '/')) is { } entry ? Touch(entry) : null;

    private TextureResourceHandle* Touch(Entry entry)
    {
        entry.LastUse   = _frame;
        entry.Requested = true;
        return entry.Preview;
    }

    public void Import(Mod mod, int slot, string pngPath)
    {
        if (!ModSphereMaps.IsEditableSlot(slot) || IsBusy(mod))
            return;

        var layout = Layout;
        if (layout is null)
        {
            _errors[mod.Identifier] = _layoutError ?? "原版球面贴图尚未加载。";
            return;
        }

        _errors.Remove(mod.Identifier);
        var modPath = mod.ModPath;
        _imports[mod.Identifier] = Task.Run(() =>
        {
            var relativePath = ModSphereMaps.SlotRelativePath(slot);
            ModSphereMaps.ImportPng(_textures, layout, pngPath, ModSphereMaps.ResolvePath(modPath, relativePath));
            var table = ModSphereMaps.LoadTable(modPath);
            table[slot] = relativePath;
            ModSphereMaps.SaveTable(modPath, table);
        }).ContinueWith(t => _framework.RunOnFrameworkThread(() =>
        {
            if (t.Exception is { } ex)
            {
                Penumbra.Log.Error($"Could not import sphere map {pngPath} into slot {slot} of {modPath.Name}:\n{ex}");
                _errors[mod.Identifier] = $"导入失败：{ex.InnerException?.Message ?? ex.Message}";
            }

            Reload(modPath);
        }), TaskScheduler.Default).Unwrap();
    }

    public void Remove(Mod mod, int slot)
    {
        if (IsBusy(mod))
            return;

        _errors.Remove(mod.Identifier);
        try
        {
            var table = ModSphereMaps.LoadTable(mod.ModPath);
            if (!table.Remove(slot, out var relativePath))
                return;

            File.Delete(ModSphereMaps.ResolvePath(mod.ModPath, relativePath));
            ModSphereMaps.SaveTable(mod.ModPath, table);
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Could not remove sphere map slot {slot} of {mod.Identifier}:\n{ex}");
            _errors[mod.Identifier] = $"删除失败：{ex.Message}";
        }

        Reload(mod.ModPath);
    }

    /// <summary> Get the Kernel texture to bind for a material, or null if it should use the default one. Called from render threads. </summary>
    public nint GetTexture(MaterialResourceHandle* material)
    {
        var generation = _generation;
        if (!_materials.TryGetValue((nint)material, out var cached) || cached.Generation != generation)
        {
            cached               = (FindEntry(material), generation);
            _materials[(nint)material] = cached;
        }

        var entry = cached.Entry;
        if (entry is null || entry.Removed)
            return nint.Zero;

        entry.LastUse   = _frame;
        entry.Requested = true;
        return entry.Texture;
    }

    public bool IsOwnTexture(nint texture)
        => _ownTextures.ContainsKey(texture);

    public void RemoveMaterial(nint resourceHandle)
        => _materials.TryRemove(resourceHandle, out _);

    private Entry? FindEntry(MaterialResourceHandle* material)
    {
        ReadOnlySpan<byte> name = material->FileName.AsSpan();
        if (PathDataHandler.Split(name, out var actualPath, out var additionalData))
        {
            if (PathDataHandler.ReadMtrl(additionalData, out var data)
             && data.Discriminator == PathDataHandler.Discriminator
             && Volatile.Read(ref _temporaryMaterials).TryGetValue(
                    new TemporaryMaterialKey(data.Collection.Id, data.OriginalPathCrc32), out var temporary))
                return temporary;
            name = actualPath;
        }
        if (!Utf8GamePath.IsRooted(name))
            return null;

        return FindEntry(Encoding.UTF8.GetString(name).Replace('\\', '/'));
    }

    private Entry? FindEntry(string path)
    {
        var basePath = _basePath;
        if (basePath.Length is 0 || !path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            return null;

        var end = path.IndexOf('/', basePath.Length);
        return end > basePath.Length ? _entries.GetValueOrDefault(path[basePath.Length..end]) : null;
    }

    private void OnModDiscoveryFinished()
    {
        foreach (var entry in _entries.Values)
            RemoveEntry(entry);

        _basePath = _modManager.Valid ? _modManager.BasePath.FullName.Replace('\\', '/').TrimEnd('/') + '/' : string.Empty;
        foreach (var mod in _modManager)
            Reload(mod.ModPath);
        Invalidate();
    }

    private void OnModPathChanged(in ModPathChanged.Arguments arguments)
    {
        if (arguments.OldDirectory is { } oldDirectory && _entries.TryGetValue(oldDirectory.Name, out var entry))
            RemoveEntry(entry);
        if (arguments.NewDirectory is { } newDirectory)
            Reload(newDirectory);
        Invalidate();
    }

    private void Reload(DirectoryInfo modPath)
    {
        if (_entries.TryGetValue(modPath.Name, out var old))
            RemoveEntry(old);

        var table = ModSphereMaps.LoadTable(modPath);
        if (table.Count > 0)
            _entries[modPath.Name] = Entry.ForMod(modPath, table);
        Invalidate();
    }

    private void RemoveTemporaryRegistration(TemporaryRegistrationKey key)
    {
        if (!_temporaryRegistrations.Remove(key, out var registration))
            return;

        foreach (var entry in registration.Materials.Values.Distinct())
        {
            entry.Removed = true;
            Release(entry);
        }
    }

    private void RebuildTemporarySnapshots()
    {
        Dictionary<TemporaryMaterialKey, (Entry Entry, int Priority, long Order)> winners = [];
        foreach (var (key, registration) in _temporaryRegistrations)
        {
            foreach (var (materialCrc, entry) in registration.Materials)
            {
                var materialKey = new TemporaryMaterialKey(key.CollectionId, materialCrc);
                if (!winners.TryGetValue(materialKey, out var current)
                 || registration.Priority > current.Priority
                 || registration.Priority == current.Priority && registration.Order > current.Order)
                    winners[materialKey] = (entry, registration.Priority, registration.Order);
            }
        }

        Volatile.Write(ref _temporaryMaterials,
            winners.ToDictionary(static pair => pair.Key, static pair => pair.Value.Entry));
        Volatile.Write(ref _temporaryEntries, _temporaryRegistrations.Values
            .SelectMany(static registration => registration.Materials.Values).Distinct().ToArray());
    }

    private void RemoveEntry(Entry entry)
    {
        entry.Removed = true;
        _entries.TryRemove(new KeyValuePair<string, Entry>(entry.Name, entry));
        Release(entry);
    }

    private void Invalidate()
    {
        Interlocked.Increment(ref _generation);
        _materials.Clear();
    }

    private void Release(Entry entry)
    {
        var handle  = entry.TextureHandle;
        var preview = entry.Preview;
        var texture = entry.Texture;
        entry.Texture       = nint.Zero;
        entry.Preview       = null;
        entry.TextureHandle = null;
        if (handle is null && preview is null)
            return;

        lock (_pendingReleases)
            _pendingReleases.Add((handle ?? SafeTextureHandle.CreateInvalid(), (nint)preview, texture, _frame + ReleaseDelayFrames));
    }

    private static void Free(SafeTextureHandle handle, nint preview)
    {
        handle.Dispose();
        if (preview != nint.Zero)
            NativeMemory.Free((void*)preview);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        ++_frame;
        var enabled = Enabled && _utility.Ready;
        var temporaryEntries = Volatile.Read(ref _temporaryEntries);
        _active = enabled && (!_entries.IsEmpty || temporaryEntries.Length > 0);
        foreach (var entry in _entries.Values.Concat(temporaryEntries))
        {
            if (entry.Removed)
                continue;

            if (!enabled || entry.TextureHandle is not null && _frame - entry.LastUse > IdleFrames)
                Release(entry);
            else if (entry is { TextureHandle: null, Loading: false, Requested: true, LoadError: null })
                StartLoad(entry);
        }

        lock (_pendingReleases)
        {
            for (var i = _pendingReleases.Count - 1; i >= 0; --i)
            {
                var (handle, preview, texture, frame) = _pendingReleases[i];
                if (frame > _frame)
                    continue;

                _ownTextures.TryRemove(texture, out _);
                Free(handle, preview);
                _pendingReleases.RemoveAt(i);
            }
        }
    }

    /// <summary> Copy every mip of every slice out of the vanilla GPU texture into a .tex file. </summary>
    private static byte[] ReadVanilla(TextureResourceHandle* handle)
    {
        var template = ModSphereMaps.CreateTemplate(new ReadOnlySpan<byte>(&handle->Header, 0x50));
        if (template is null)
            throw new InvalidOperationException("原版球面贴图文件头无法识别。");

        var (file, layout) = template.Value;
        var texture = handle->Texture;
        using var srv = new ComPtr<ID3D11ShaderResourceView>((ID3D11ShaderResourceView*)texture->D3D11ShaderResourceView);
        if (srv.Get() is null)
            throw new InvalidOperationException("原版球面贴图没有 GPU 资源。");

        using var resource = new ComPtr<ID3D11Resource>();
        srv.Get()->GetResource(resource.GetAddressOf());
        using var source = new ComPtr<ID3D11Texture2D>();
        Marshal.ThrowExceptionForHR(resource.As(&source));

        D3D11_TEXTURE2D_DESC desc;
        source.Get()->GetDesc(&desc);
        if (desc.Width != (uint)layout.Width
         || desc.Height != (uint)layout.Height
         || desc.ArraySize != (uint)layout.ArraySize
         || desc.MipLevels < (uint)layout.MipCount
         || desc.Format is not DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM and not DXGI_FORMAT.DXGI_FORMAT_BC1_TYPELESS)
            throw new InvalidOperationException(
                $"原版球面贴图 GPU 资源与文件头不符：{desc.Width}x{desc.Height} 层 {desc.ArraySize} Mip {desc.MipLevels} 格式 {desc.Format}。");

        desc.Usage          = D3D11_USAGE.D3D11_USAGE_STAGING;
        desc.BindFlags      = 0;
        desc.CPUAccessFlags = (uint)D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ;
        desc.MiscFlags      = 0;

        using var device = new ComPtr<ID3D11Device>();
        srv.Get()->GetDevice(device.GetAddressOf());
        using var staging = new ComPtr<ID3D11Texture2D>();
        Marshal.ThrowExceptionForHR(device.Get()->CreateTexture2D(&desc, null, staging.GetAddressOf()));
        using var context = new ComPtr<ID3D11DeviceContext>();
        device.Get()->GetImmediateContext(context.GetAddressOf());
        context.Get()->CopyResource((ID3D11Resource*)staging.Get(), (ID3D11Resource*)source.Get());

        for (var mip = 0; mip < layout.MipCount; ++mip)
        {
            var blocksH  = Math.Max(1, (Math.Max(1, layout.Height >> mip) + 3) / 4);
            var rowBytes = layout.SliceSizes[mip] / blocksH;
            if (rowBytes * blocksH != layout.SliceSizes[mip])
                throw new InvalidOperationException($"原版球面贴图第 {mip} 级 Mip 的大小无法按 BC1 分行。");

            for (var slice = 0; slice < layout.ArraySize; ++slice)
            {
                var sub = (uint)(mip + slice * desc.MipLevels);
                D3D11_MAPPED_SUBRESOURCE mapped;
                Marshal.ThrowExceptionForHR(context.Get()->Map((ID3D11Resource*)staging.Get(), sub, D3D11_MAP.D3D11_MAP_READ, 0, &mapped));
                try
                {
                    if ((int)mapped.RowPitch < rowBytes)
                        throw new InvalidOperationException($"原版球面贴图第 {mip} 级 Mip 的行距过小。");

                    var dst = layout.MipOffsets[mip] + slice * layout.SliceSizes[mip];
                    for (var row = 0; row < blocksH; ++row)
                        new ReadOnlySpan<byte>((byte*)mapped.pData + row * (int)mapped.RowPitch, rowBytes)
                            .CopyTo(file.AsSpan(dst + row * rowBytes));
                }
                finally
                {
                    context.Get()->Unmap((ID3D11Resource*)staging.Get(), sub);
                }
            }
        }

        return file;
    }

    private void StartLoad(Entry entry)
    {
        var layout = Layout;
        if (layout is null || _vanilla is null)
        {
            if (_layoutError is not null)
                entry.LoadError = _layoutError;
            return;
        }

        entry.Loading   = true;
        entry.Requested = false;
        var vanilla = _vanilla;
        Task.Run(() => ModSphereMaps.BuildArray(vanilla, layout, entry.Files))
            .ContinueWith(t => _framework.RunOnFrameworkThread(() => FinishLoad(entry, t)), TaskScheduler.Default);
    }

    private void FinishLoad(Entry entry, Task<byte[]> task)
    {
        entry.Loading = false;
        if (task.Exception is { } ex)
        {
            Penumbra.Log.Error($"Could not build sphere map array for {entry.Name}:\n{ex}");
            entry.LoadError = $"生成球面贴图数组失败：{ex.InnerException?.Message ?? ex.Message}";
            return;
        }

        if (entry.Removed || !Enabled || !_utility.Ready)
            return;

        var layout = Layout;
        if (layout is null)
            return;

        SafeTextureHandle? handle = null;
        TextureResourceHandle* preview = null;
        try
        {
            handle  = CreateArrayTexture(task.Result, layout);
            preview = (TextureResourceHandle*)NativeMemory.AllocZeroed((nuint)sizeof(TextureResourceHandle));
            preview->Texture = handle.Texture;
        }
        catch (Exception createEx)
        {
            if (preview is not null)
                NativeMemory.Free(preview);
            handle?.Dispose();
            Penumbra.Log.Error($"Could not create sphere map array for {entry.Name}:\n{createEx}");
            entry.LoadError = "无法创建球面贴图数组。";
            return;
        }

        var texture = (nint)handle.Texture;
        _ownTextures[texture] = 0;
        entry.TextureHandle   = handle;
        entry.Preview         = preview;
        entry.Texture         = texture;
        entry.LastUse         = _frame;
        Penumbra.Log.Debug($"Created sphere map array for {entry.Name} with slots {string.Join(", ", entry.Slots.Keys)}.");
    }

    /// <summary>
    ///   CreateTexture2D only creates 2D textures; the array type and layer count are patched in before InitializeContents uploads the pixels.
    /// </summary>
    private static SafeTextureHandle CreateArrayTexture(byte[] file, ModSphereMaps.ArrayLayout layout)
    {
        if (layout.MipOffsets.Length is 0 || layout.MipOffsets[0] >= file.Length || layout.ArraySize is < 1 or > byte.MaxValue)
            throw new InvalidOperationException("球面贴图像素数据不完整。");

        var size = stackalloc int[2];
        size[0] = layout.Width;
        size[1] = layout.Height;
        var created = Device.Instance()->CreateTexture2D(size, (byte)layout.MipCount, TextureFormat.BC1_UNORM,
            TextureFlags.TextureNoSwizzle | TextureFlags.Immutable | TextureFlags.Managed, 7);
        if (created is null)
            throw new InvalidOperationException("游戏未能分配球面贴图纹理。");

        var handle = new SafeTextureHandle(created, false);
        var texture = handle.Texture;
        texture->Flags     = texture->Flags & ~TextureFlags.TextureTypeMask | TextureFlags.TextureType2DArray;
        texture->ArraySize = (byte)layout.ArraySize;
        fixed (byte* pixels = file)
        {
            if (!texture->InitializeContents(pixels + layout.MipOffsets[0])
             || texture->D3D11ShaderResourceView is null
             || texture->ArraySize != layout.ArraySize)
            {
                handle.Dispose();
                throw new InvalidOperationException("游戏未能写入球面贴图数组。");
            }
        }

        return handle;
    }

    private readonly record struct TemporaryRegistrationKey(string Tag, int CollectionId, int Priority);
    private readonly record struct TemporaryMaterialKey(int CollectionId, int MaterialCrc);
    private sealed record TemporaryRegistration(int Priority, long Order, IReadOnlyDictionary<int, Entry> Materials);

    private sealed class Entry(string name, IReadOnlyDictionary<int, string> slots, IReadOnlyDictionary<int, string> files)
    {
        public readonly string                           Name    = name;
        public readonly IReadOnlyDictionary<int, string> Slots   = slots;
        public readonly IReadOnlyDictionary<int, string> Files   = files;

        public static Entry ForMod(DirectoryInfo modPath, IReadOnlyDictionary<int, string> slots)
            => new(modPath.Name, slots,
                slots.ToDictionary(static pair => pair.Key, pair => ModSphereMaps.ResolvePath(modPath, pair.Value)));

        public volatile bool        Removed;
        public volatile bool        Requested;
        public volatile nint        Texture;
        public          long        LastUse;
        public          bool                    Loading;
        public          string?                 LoadError;
        public          SafeTextureHandle?      TextureHandle;
        public          TextureResourceHandle*  Preview;
    }
}
