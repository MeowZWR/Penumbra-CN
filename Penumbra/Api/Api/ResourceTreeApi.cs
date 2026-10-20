using Dalamud.Game.ClientState.Objects.Types;
using Newtonsoft.Json.Linq;
using Penumbra.Api.Enums;
using Penumbra.GameData.Interop;
using Penumbra.Interop.ResourceTree;
using Penumbra.Interop.Services;

namespace Penumbra.Api.Api;

public class ResourceTreeApi(ResourceTreeFactory resourceTreeFactory, ObjectManager objects, ModSphereMapManager sphereMaps)
    : IPenumbraApiResourceTree, Luna.IApiService
{
    public Dictionary<string, HashSet<string>>?[] GetGameObjectResourcePaths(params ushort[] gameObjects)
    {
        var characters       = gameObjects.Select(index => objects.GetDalamudObject((int)index)).OfType<ICharacter>();
        var resourceTrees    = resourceTreeFactory.FromCharacters(characters, 0);
        var pathDictionaries = ResourceTreeApiHelper.GetResourcePathDictionaries(resourceTrees);

        return Array.ConvertAll(gameObjects, obj => pathDictionaries.GetValueOrDefault(obj));
    }

    public Dictionary<int, Dictionary<int, string>>?[] GetGameObjectSphereMapPaths(params ushort[] gameObjects)
    {
        var characters    = gameObjects.Select(index => objects.GetDalamudObject((int)index)).OfType<ICharacter>();
        var resourceTrees = resourceTreeFactory.FromCharacters(characters, 0);
        Dictionary<ushort, Dictionary<int, Dictionary<int, string>>> paths = [];

        foreach (var (gameObject, resourceTree) in resourceTrees)
        {
            if (paths.ContainsKey(gameObject.ObjectIndex))
                continue;

            Dictionary<int, Dictionary<int, string>> materials = [];
            paths.Add(gameObject.ObjectIndex, materials);
            foreach (var node in resourceTree.FlatNodes)
            {
                if (node.Type is not ResourceType.Mtrl || node.PossibleGamePaths.Length == 0
                 || (sphereMaps.GetSlotFiles(node.ResourceHandle) ?? sphereMaps.GetSlotFiles(node.FullPath.ToPath()))
                    is not { Count: > 0 } slots)
                    continue;

                foreach (var gamePath in node.PossibleGamePaths)
                    materials[gamePath.Path.Crc32] = slots.ToDictionary(static pair => pair.Key, static pair => pair.Value);
            }
        }

        return Array.ConvertAll(gameObjects, obj => paths.GetValueOrDefault(obj));
    }

    public Dictionary<ushort, Dictionary<string, HashSet<string>>> GetPlayerResourcePaths()
    {
        var resourceTrees = resourceTreeFactory.FromObjectTable(ResourceTreeFactory.Flags.LocalPlayerRelatedOnly);
        return ResourceTreeApiHelper.GetResourcePathDictionaries(resourceTrees);
    }

    public GameResourceDict?[] GetGameObjectResourcesOfType(ResourceType type, bool withUiData,
        params ushort[] gameObjects)
    {
        var characters      = gameObjects.Select(index => objects.GetDalamudObject((int)index)).OfType<ICharacter>();
        var resourceTrees   = resourceTreeFactory.FromCharacters(characters, withUiData ? ResourceTreeFactory.Flags.WithUiData : 0);
        var resDictionaries = ResourceTreeApiHelper.GetResourcesOfType(resourceTrees, type);

        return Array.ConvertAll(gameObjects, obj => resDictionaries.GetValueOrDefault(obj));
    }

    public Dictionary<ushort, GameResourceDict> GetPlayerResourcesOfType(ResourceType type,
        bool withUiData)
    {
        var resourceTrees = resourceTreeFactory.FromObjectTable(ResourceTreeFactory.Flags.LocalPlayerRelatedOnly
          | (withUiData ? ResourceTreeFactory.Flags.WithUiData : 0));
        return ResourceTreeApiHelper.GetResourcesOfType(resourceTrees, type);
    }

    public JObject?[] GetGameObjectResourceTrees(bool withUiData, params ushort[] gameObjects)
    {
        var characters    = gameObjects.Select(index => objects.GetDalamudObject((int)index)).OfType<ICharacter>();
        var resourceTrees = resourceTreeFactory.FromCharacters(characters, withUiData ? ResourceTreeFactory.Flags.WithUiData : 0);
        var resDictionary = ResourceTreeApiHelper.EncapsulateResourceTrees(resourceTrees);

        return Array.ConvertAll(gameObjects, obj => resDictionary.GetValueOrDefault(obj));
    }

    public Dictionary<ushort, JObject> GetPlayerResourceTrees(bool withUiData)
    {
        var resourceTrees = resourceTreeFactory.FromObjectTable(ResourceTreeFactory.Flags.LocalPlayerRelatedOnly
          | (withUiData ? ResourceTreeFactory.Flags.WithUiData : 0));
        var resDictionary = ResourceTreeApiHelper.EncapsulateResourceTrees(resourceTrees);

        return resDictionary;
    }
}
