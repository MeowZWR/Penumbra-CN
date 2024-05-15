using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Internal.Notifications;
using Newtonsoft.Json.Linq;
using OtterGui.Classes;
using Penumbra.GameData.Actors;
using Penumbra.GameData.DataContainers.Bases;
using Penumbra.GameData.Structs;
using Penumbra.Services;
using Penumbra.String;

namespace Penumbra.Collections.Manager;

public partial class IndividualCollections
{
    public JArray ToJObject()
    {
        var ret = new JArray();
        foreach (var (name, identifiers, collection) in Assignments)
        {
            var tmp = identifiers[0].ToJson();
            tmp.Add("Collection", collection.Name);
            tmp.Add("Display",    name);
            ret.Add(tmp);
        }

        return ret;
    }

    public bool ReadJObject(SaveService saver, ActiveCollections parent, JArray? obj, CollectionStorage storage)
    {
        if (_actors.Awaiter.IsCompletedSuccessfully)
        {
            var ret = ReadJObjectInternal(obj, storage);
            return ret;
        }

        Penumbra.Log.Debug("[Collections] Delayed reading individual assignments until actor service is ready...");
        _actors.Awaiter.ContinueWith(_ =>
        {
            if (ReadJObjectInternal(obj, storage))
                saver.ImmediateSave(parent);
            IsLoaded = true;
            Loaded.Invoke();
        }, TaskScheduler.Default);
        return false;
    }

    private bool ReadJObjectInternal(JArray? obj, CollectionStorage storage)
    {
        Penumbra.Log.Debug("[Collections] 读取单个分配...");
        if (obj == null)
        {
            Penumbra.Log.Debug($"[Collections] 完成读取 {Count} 个单个分配...");
            return true;
        }

        bool changes = false;

        void LogChange(string message, NotificationType type)
        {
            changes = true;
            Penumbra.Messager.NotificationMessage(message, type);
        }

        foreach (var data in obj)
        {
            try
            {
                var identifier = _actors.FromJson(data as JObject);
                var group = GetGroup(identifier);
                if (group.Length == 0 || group.Any(i => !i.IsValid))
                {
                    LogChange("无法加载未知的独立合集，已删除。", NotificationType.Error);
                    continue;
                }

                var collectionName = data["Collection"]?.ToObject<string>() ?? string.Empty;
                if (collectionName.Length == 0 || !storage.ByName(collectionName, out var collection))
                {
                    LogChange($"无法加载合集 \"{collectionName}\" 作为 {identifier} 的独立合集，已设为None。", NotificationType.Warning);
                    continue;
                }

                if (!Add(group, collection))
                {
                    LogChange($"无法添加 {identifier} 的独立合集，已删除。", NotificationType.Warning);
                }
            }
            catch (Exception e)
            {
                LogChange(e.ToString(), NotificationType.Error);
            }
        }

        Penumbra.Log.Debug($"完成读取 {Count} 个单独分配...");

        return changes;
    }


    internal void Migrate0To1(Dictionary<string, ModCollection> old)
    {
        static bool FindDataId(string name, NameDictionary data, out NpcId dataId)
        {
            var kvp = data.FirstOrDefault(kvp => kvp.Value.Equals(name, StringComparison.OrdinalIgnoreCase),
                new KeyValuePair<NpcId, string>(uint.MaxValue, string.Empty));
            dataId = kvp.Key;
            return kvp.Value.Length > 0;
        }

        foreach (var (name, collection) in old)
        {
            var kind      = ObjectKind.None;
            var lowerName = name.ToLowerInvariant();
            // Prefer matching NPC names, fewer false positives than preferring players.
            if (FindDataId(lowerName, _actors.Data.Companions, out var dataId))
                kind = ObjectKind.Companion;
            else if (FindDataId(lowerName, _actors.Data.Mounts, out dataId))
                kind = ObjectKind.MountType;
            else if (FindDataId(lowerName, _actors.Data.BNpcs, out dataId))
                kind = ObjectKind.BattleNpc;
            else if (FindDataId(lowerName, _actors.Data.ENpcs, out dataId))
                kind = ObjectKind.EventNpc;

            var identifier = _actors.CreateNpc(kind, dataId);
            if (identifier.IsValid)
            {
                // If the name corresponds to a valid npc, add it as a group. If this fails, notify users.
                var group = GetGroup(identifier);
                var ids   = string.Join(", ", group.Select(i => i.DataId.ToString()));
                if (Add($"{_actors.Data.ToName(kind, dataId)} ({kind.ToName()})", group, collection))
                    Penumbra.Log.Information($"Migrated {name} ({kind.ToName()}) to NPC Identifiers [{ids}].");
                else
                    Penumbra.Messager.NotificationMessage(
                        $"Could not migrate {name} ({collection.AnonymizedName}) which was assumed to be a {kind.ToName()} with IDs [{ids}], please look through your individual collections.",
                        NotificationType.Error);
            }
            // If it is not a valid NPC name, check if it can be a player name.
            else if (ActorIdentifierFactory.VerifyPlayerName(name))
            {
                identifier = _actors.CreatePlayer(ByteString.FromStringUnsafe(name, false), ushort.MaxValue);
                var shortName = string.Join(" ", name.Split().Select(n => $"{n[0]}."));
                // Try to migrate the player name without logging full names.
                if (Add($"{name} ({_actors.Data.ToWorldName(identifier.HomeWorld)})", [identifier], collection))
                    Penumbra.Log.Information($"Migrated {shortName} ({collection.AnonymizedName}) to Player Identifier.");
                else
                    Penumbra.Messager.NotificationMessage(
                        $"Could not migrate {shortName} ({collection.AnonymizedName}), please look through your individual collections.",
                        NotificationType.Error);
            }
            else
            {
                Penumbra.Messager.NotificationMessage(
                    $"Could not migrate {name} ({collection.AnonymizedName}), which can not be a player name nor is it a known NPC name, please look through your individual collections.",
                    NotificationType.Error);
            }
        }
    }
}
