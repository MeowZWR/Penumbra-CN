using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.GameData.Data;
using Penumbra.Import.Textures;
using Penumbra.Interop.Hooks.ResourceLoading;
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
    private readonly IDataManager        _gameData;
    private readonly ResourceService     _resources;
    private readonly TextureManager      _textures;
    private readonly CharacterUtility    _utility;
    private readonly CommunicatorService _communicator;

    private readonly ConcurrentDictionary<string, Entry>                   _entries     = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<nint, (Entry? Entry, int Generation)> _materials   = new();
    private readonly ConcurrentDictionary<nint, byte>                      _ownTextures = new();
    private readonly Dictionary<string, Task>                              _imports     = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string>                            _errors      = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(SafeResourceHandle Handle, nint Texture, long Frame)> _pendingReleases = [];

    private byte[]?                    _vanilla;
    private ModSphereMaps.ArrayLayout? _layout;
    private bool                       _layoutLoaded;
    private volatile string            _basePath = string.Empty;
    private volatile int               _generation;
    private long                       _frame;
    private volatile bool              _active;

    public ModSphereMapManager(ModManager modManager, Configuration config, IFramework framework, IDataManager gameData,
        ResourceService resources, TextureManager textures, CharacterUtility utility, CommunicatorService communicator)
    {
        _modManager   = modManager;
        _config       = config;
        _framework    = framework;
        _gameData     = gameData;
        _resources    = resources;
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
        _materials.Clear();
        lock (_pendingReleases)
        {
            foreach (var (handle, _, _) in _pendingReleases)
                handle.Dispose();
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
            if (_layoutLoaded)
                return _layout;

            _layoutLoaded = true;
            try
            {
                _vanilla = _gameData.GetFile(GamePaths.Tex.SphereDArray)?.Data;
                _layout  = _vanilla is null ? null : ModSphereMaps.ReadLayout(_vanilla);
            }
            catch (Exception ex)
            {
                Penumbra.Log.Error($"Could not read vanilla {GamePaths.Tex.SphereDArray}:\n{ex}");
            }

            if (_layout is null)
                Penumbra.Log.Error($"Unsupported layout of vanilla {GamePaths.Tex.SphereDArray}, mod sphere maps are unavailable.");
            return _layout;
        }
    }

    /// <summary> The vanilla array, used for previews of undeclared slots. </summary>
    public TextureResourceHandle* VanillaHandle
        => (TextureResourceHandle*)_utility.DefaultSphereDArrayResource;

    public IReadOnlyDictionary<int, string>? GetSlots(Mod mod)
        => _entries.TryGetValue(mod.Identifier, out var entry) ? entry.Slots : null;

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
        return entry.Handle is { IsInvalid: false } handle ? (TextureResourceHandle*)handle.ResourceHandle : null;
    }

    public void Import(Mod mod, int slot, string pngPath)
    {
        if (!ModSphereMaps.IsEditableSlot(slot) || IsBusy(mod))
            return;

        var layout = Layout;
        if (layout is null)
        {
            _errors[mod.Identifier] = "无法读取原版球面贴图数组。";
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
        if (PathDataHandler.Split(name, out var actualPath, out _))
            name = actualPath;
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
            _entries[modPath.Name] = new Entry(modPath, table);
        Invalidate();
    }

    private void RemoveEntry(Entry entry)
    {
        entry.Removed = true;
        _entries.TryRemove(new KeyValuePair<string, Entry>(entry.ModPath.Name, entry));
        Release(entry);
    }

    private void Invalidate()
    {
        Interlocked.Increment(ref _generation);
        _materials.Clear();
    }

    private void Release(Entry entry)
    {
        var handle = entry.Handle;
        var texture = entry.Texture;
        entry.Texture = nint.Zero;
        entry.Handle  = null;
        if (handle is null)
            return;

        lock (_pendingReleases)
            _pendingReleases.Add((handle, texture, _frame + ReleaseDelayFrames));
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        ++_frame;
        var enabled = Enabled && _utility.Ready;
        _active = enabled && !_entries.IsEmpty;
        foreach (var entry in _entries.Values)
        {
            if (!enabled || entry.Handle is not null && _frame - entry.LastUse > IdleFrames)
                Release(entry);
            else if (entry is { Handle: null, Loading: false, Requested: true, LoadError: null })
                StartLoad(entry);
        }

        lock (_pendingReleases)
        {
            for (var i = _pendingReleases.Count - 1; i >= 0; --i)
            {
                var (handle, texture, frame) = _pendingReleases[i];
                if (frame > _frame)
                    continue;

                _ownTextures.TryRemove(texture, out _);
                handle.Dispose();
                _pendingReleases.RemoveAt(i);
            }
        }
    }

    private void StartLoad(Entry entry)
    {
        var layout = Layout;
        if (layout is null || _vanilla is null)
        {
            entry.LoadError = "无法读取原版球面贴图数组。";
            return;
        }

        entry.Loading   = true;
        entry.Requested = false;
        var vanilla = _vanilla;
        Task.Run(() => WriteArray(entry, vanilla, layout)).ContinueWith(t => _framework.RunOnFrameworkThread(() => FinishLoad(entry, t)),
            TaskScheduler.Default);
    }

    /// <summary> Write the composite array into the hidden directory, reusing an identical existing file. </summary>
    private static string WriteArray(Entry entry, byte[] vanilla, ModSphereMaps.ArrayLayout layout)
    {
        var data     = ModSphereMaps.BuildArray(vanilla, layout, entry.ModPath, entry.Slots);
        var dir      = ModSphereMaps.GetDirectory(entry.ModPath);
        var existing = dir.EnumerateFiles($"{ModSphereMaps.ArrayPrefix}*.tex").ToList();
        var target   = existing.FirstOrDefault(f => f.Length == data.Length && File.ReadAllBytes(f.FullName).AsSpan().SequenceEqual(data));
        if (target is null)
        {
            target = new FileInfo(Path.Combine(dir.FullName, $"{ModSphereMaps.ArrayPrefix}{DateTime.UtcNow.Ticks:X}.tex"));
            File.WriteAllBytes(target.FullName, data);
        }

        foreach (var file in existing.Where(f => f.FullName != target.FullName))
        {
            try
            {
                file.Delete();
            }
            catch (Exception ex)
            {
                Penumbra.Log.Debug($"Could not delete outdated sphere map array {file.FullName}:\n{ex}");
            }
        }

        return target.FullName;
    }

    private void FinishLoad(Entry entry, Task<string> task)
    {
        entry.Loading = false;
        if (task.Exception is { } ex)
        {
            Penumbra.Log.Error($"Could not build sphere map array for {entry.ModPath.Name}:\n{ex}");
            entry.LoadError = $"生成球面贴图数组失败：{ex.InnerException?.Message ?? ex.Message}";
            return;
        }

        if (entry.Removed || !Enabled || !_utility.Ready)
            return;

        var handle  = _resources.GetSafeResource(ResourceCategory.Chara, ResourceType.Tex, new FullPath(task.Result).InternalName);
        var texture = handle.IsInvalid ? null : ((TextureResourceHandle*)handle.ResourceHandle)->Texture;
        if (texture is null || texture->ArraySize != Layout!.ArraySize)
        {
            handle.Dispose();
            Penumbra.Log.Error($"Could not load sphere map array {task.Result}.");
            entry.LoadError = "无法加载球面贴图数组。";
            return;
        }

        entry.Handle             = handle;
        _ownTextures[(nint)texture] = 0;
        entry.Texture            = (nint)texture;
        entry.LastUse            = _frame;
        Penumbra.Log.Debug($"Loaded sphere map array for {entry.ModPath.Name} with slots {string.Join(", ", entry.Slots.Keys)}.");
    }

    private sealed class Entry(DirectoryInfo modPath, IReadOnlyDictionary<int, string> slots)
    {
        public readonly DirectoryInfo                    ModPath = modPath;
        public readonly IReadOnlyDictionary<int, string> Slots   = slots;

        public volatile bool        Removed;
        public volatile bool        Requested;
        public volatile nint        Texture;
        public          long        LastUse;
        public          bool        Loading;
        public          string?     LoadError;
        public          SafeResourceHandle? Handle;
    }
}
