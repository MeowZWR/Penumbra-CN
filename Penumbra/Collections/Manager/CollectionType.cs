using Penumbra.GameData.Enums;

namespace Penumbra.Collections.Manager;

public enum CollectionType : byte
{
    // Special Collections
    Yourself = Api.Enums.ApiCollectionType.Yourself,

    MalePlayerCharacter      = Api.Enums.ApiCollectionType.MalePlayerCharacter,
    FemalePlayerCharacter    = Api.Enums.ApiCollectionType.FemalePlayerCharacter,
    MaleNonPlayerCharacter   = Api.Enums.ApiCollectionType.MaleNonPlayerCharacter,
    FemaleNonPlayerCharacter = Api.Enums.ApiCollectionType.FemaleNonPlayerCharacter,
    NonPlayerChild           = Api.Enums.ApiCollectionType.NonPlayerChild,
    NonPlayerElderly         = Api.Enums.ApiCollectionType.NonPlayerElderly,

    MaleMidlander    = Api.Enums.ApiCollectionType.MaleMidlander,
    FemaleMidlander  = Api.Enums.ApiCollectionType.FemaleMidlander,
    MaleHighlander   = Api.Enums.ApiCollectionType.MaleHighlander,
    FemaleHighlander = Api.Enums.ApiCollectionType.FemaleHighlander,

    MaleWildwood    = Api.Enums.ApiCollectionType.MaleWildwood,
    FemaleWildwood  = Api.Enums.ApiCollectionType.FemaleWildwood,
    MaleDuskwight   = Api.Enums.ApiCollectionType.MaleDuskwight,
    FemaleDuskwight = Api.Enums.ApiCollectionType.FemaleDuskwight,

    MalePlainsfolk   = Api.Enums.ApiCollectionType.MalePlainsfolk,
    FemalePlainsfolk = Api.Enums.ApiCollectionType.FemalePlainsfolk,
    MaleDunesfolk    = Api.Enums.ApiCollectionType.MaleDunesfolk,
    FemaleDunesfolk  = Api.Enums.ApiCollectionType.FemaleDunesfolk,

    MaleSeekerOfTheSun    = Api.Enums.ApiCollectionType.MaleSeekerOfTheSun,
    FemaleSeekerOfTheSun  = Api.Enums.ApiCollectionType.FemaleSeekerOfTheSun,
    MaleKeeperOfTheMoon   = Api.Enums.ApiCollectionType.MaleKeeperOfTheMoon,
    FemaleKeeperOfTheMoon = Api.Enums.ApiCollectionType.FemaleKeeperOfTheMoon,

    MaleSeawolf      = Api.Enums.ApiCollectionType.MaleSeawolf,
    FemaleSeawolf    = Api.Enums.ApiCollectionType.FemaleSeawolf,
    MaleHellsguard   = Api.Enums.ApiCollectionType.MaleHellsguard,
    FemaleHellsguard = Api.Enums.ApiCollectionType.FemaleHellsguard,

    MaleRaen    = Api.Enums.ApiCollectionType.MaleRaen,
    FemaleRaen  = Api.Enums.ApiCollectionType.FemaleRaen,
    MaleXaela   = Api.Enums.ApiCollectionType.MaleXaela,
    FemaleXaela = Api.Enums.ApiCollectionType.FemaleXaela,

    MaleHelion   = Api.Enums.ApiCollectionType.MaleHelion,
    FemaleHelion = Api.Enums.ApiCollectionType.FemaleHelion,
    MaleLost     = Api.Enums.ApiCollectionType.MaleLost,
    FemaleLost   = Api.Enums.ApiCollectionType.FemaleLost,

    MaleRava    = Api.Enums.ApiCollectionType.MaleRava,
    FemaleRava  = Api.Enums.ApiCollectionType.FemaleRava,
    MaleVeena   = Api.Enums.ApiCollectionType.MaleVeena,
    FemaleVeena = Api.Enums.ApiCollectionType.FemaleVeena,

    MaleMidlanderNpc    = Api.Enums.ApiCollectionType.MaleMidlanderNpc,
    FemaleMidlanderNpc  = Api.Enums.ApiCollectionType.FemaleMidlanderNpc,
    MaleHighlanderNpc   = Api.Enums.ApiCollectionType.MaleHighlanderNpc,
    FemaleHighlanderNpc = Api.Enums.ApiCollectionType.FemaleHighlanderNpc,

    MaleWildwoodNpc    = Api.Enums.ApiCollectionType.MaleWildwoodNpc,
    FemaleWildwoodNpc  = Api.Enums.ApiCollectionType.FemaleWildwoodNpc,
    MaleDuskwightNpc   = Api.Enums.ApiCollectionType.MaleDuskwightNpc,
    FemaleDuskwightNpc = Api.Enums.ApiCollectionType.FemaleDuskwightNpc,

    MalePlainsfolkNpc   = Api.Enums.ApiCollectionType.MalePlainsfolkNpc,
    FemalePlainsfolkNpc = Api.Enums.ApiCollectionType.FemalePlainsfolkNpc,
    MaleDunesfolkNpc    = Api.Enums.ApiCollectionType.MaleDunesfolkNpc,
    FemaleDunesfolkNpc  = Api.Enums.ApiCollectionType.FemaleDunesfolkNpc,

    MaleSeekerOfTheSunNpc    = Api.Enums.ApiCollectionType.MaleSeekerOfTheSunNpc,
    FemaleSeekerOfTheSunNpc  = Api.Enums.ApiCollectionType.FemaleSeekerOfTheSunNpc,
    MaleKeeperOfTheMoonNpc   = Api.Enums.ApiCollectionType.MaleKeeperOfTheMoonNpc,
    FemaleKeeperOfTheMoonNpc = Api.Enums.ApiCollectionType.FemaleKeeperOfTheMoonNpc,

    MaleSeawolfNpc      = Api.Enums.ApiCollectionType.MaleSeawolfNpc,
    FemaleSeawolfNpc    = Api.Enums.ApiCollectionType.FemaleSeawolfNpc,
    MaleHellsguardNpc   = Api.Enums.ApiCollectionType.MaleHellsguardNpc,
    FemaleHellsguardNpc = Api.Enums.ApiCollectionType.FemaleHellsguardNpc,

    MaleRaenNpc    = Api.Enums.ApiCollectionType.MaleRaenNpc,
    FemaleRaenNpc  = Api.Enums.ApiCollectionType.FemaleRaenNpc,
    MaleXaelaNpc   = Api.Enums.ApiCollectionType.MaleXaelaNpc,
    FemaleXaelaNpc = Api.Enums.ApiCollectionType.FemaleXaelaNpc,

    MaleHelionNpc   = Api.Enums.ApiCollectionType.MaleHelionNpc,
    FemaleHelionNpc = Api.Enums.ApiCollectionType.FemaleHelionNpc,
    MaleLostNpc     = Api.Enums.ApiCollectionType.MaleLostNpc,
    FemaleLostNpc   = Api.Enums.ApiCollectionType.FemaleLostNpc,

    MaleRavaNpc    = Api.Enums.ApiCollectionType.MaleRavaNpc,
    FemaleRavaNpc  = Api.Enums.ApiCollectionType.FemaleRavaNpc,
    MaleVeenaNpc   = Api.Enums.ApiCollectionType.MaleVeenaNpc,
    FemaleVeenaNpc = Api.Enums.ApiCollectionType.FemaleVeenaNpc,

    Default   = Api.Enums.ApiCollectionType.Default,   // The default collection was changed
    Interface = Api.Enums.ApiCollectionType.Interface, // The ui collection was changed
    Current   = Api.Enums.ApiCollectionType.Current,   // The current collection was changed
    Individual,                                        // An individual collection was changed
    Inactive,                                          // A collection was added or removed
    Temporary,                                         // A temporary collections was set or deleted via IPC
}

public static class CollectionTypeExtensions
{
    public static bool IsSpecial(this CollectionType collectionType)
        => collectionType < CollectionType.Default;

    public static bool CanBeRemoved(this CollectionType collectionType)
        => collectionType.IsSpecial() || collectionType is CollectionType.Individual;

    public static readonly (CollectionType, string, string)[] Special = Enum.GetValues<CollectionType>()
        .Where(IsSpecial)
        .Select(s => (s, s.ToName(), s.ToDescription()))
        .ToArray();

    public static CollectionType FromParts(Gender gender, bool npc)
    {
        gender = gender switch
        {
            Gender.MaleNpc   => Gender.Male,
            Gender.FemaleNpc => Gender.Female,
            _                => gender,
        };

        return (gender, npc) switch
        {
            (Gender.Male, false)   => CollectionType.MalePlayerCharacter,
            (Gender.Female, false) => CollectionType.FemalePlayerCharacter,
            (Gender.Male, true)    => CollectionType.MaleNonPlayerCharacter,
            (Gender.Female, true)  => CollectionType.FemaleNonPlayerCharacter,
            _                      => CollectionType.Inactive,
        };
    }

    // @formatter:off
    private static readonly IReadOnlyList<CollectionType> DefaultList      = new[] { CollectionType.Default };
    private static readonly IReadOnlyList<CollectionType> MalePlayerList   = new[] { CollectionType.MalePlayerCharacter,      CollectionType.Default };
    private static readonly IReadOnlyList<CollectionType> FemalePlayerList = new[] { CollectionType.FemalePlayerCharacter,    CollectionType.Default };
    private static readonly IReadOnlyList<CollectionType> MaleNpcList      = new[] { CollectionType.MaleNonPlayerCharacter,   CollectionType.Default };
    private static readonly IReadOnlyList<CollectionType> FemaleNpcList    = new[] { CollectionType.FemaleNonPlayerCharacter, CollectionType.Default };
    // @formatter:on

    /// <summary> A list of definite redundancy possibilities. </summary>
    public static IReadOnlyList<CollectionType> InheritanceOrder(this CollectionType collectionType)
        => collectionType switch
        {
            CollectionType.Yourself                 => DefaultList,
            CollectionType.MalePlayerCharacter      => DefaultList,
            CollectionType.FemalePlayerCharacter    => DefaultList,
            CollectionType.MaleNonPlayerCharacter   => DefaultList,
            CollectionType.FemaleNonPlayerCharacter => DefaultList,
            CollectionType.MaleMidlander            => MalePlayerList,
            CollectionType.FemaleMidlander          => FemalePlayerList,
            CollectionType.MaleHighlander           => MalePlayerList,
            CollectionType.FemaleHighlander         => FemalePlayerList,
            CollectionType.MaleWildwood             => MalePlayerList,
            CollectionType.FemaleWildwood           => FemalePlayerList,
            CollectionType.MaleDuskwight            => MalePlayerList,
            CollectionType.FemaleDuskwight          => FemalePlayerList,
            CollectionType.MalePlainsfolk           => MalePlayerList,
            CollectionType.FemalePlainsfolk         => FemalePlayerList,
            CollectionType.MaleDunesfolk            => MalePlayerList,
            CollectionType.FemaleDunesfolk          => FemalePlayerList,
            CollectionType.MaleSeekerOfTheSun       => MalePlayerList,
            CollectionType.FemaleSeekerOfTheSun     => FemalePlayerList,
            CollectionType.MaleKeeperOfTheMoon      => MalePlayerList,
            CollectionType.FemaleKeeperOfTheMoon    => FemalePlayerList,
            CollectionType.MaleSeawolf              => MalePlayerList,
            CollectionType.FemaleSeawolf            => FemalePlayerList,
            CollectionType.MaleHellsguard           => MalePlayerList,
            CollectionType.FemaleHellsguard         => FemalePlayerList,
            CollectionType.MaleRaen                 => MalePlayerList,
            CollectionType.FemaleRaen               => FemalePlayerList,
            CollectionType.MaleXaela                => MalePlayerList,
            CollectionType.FemaleXaela              => FemalePlayerList,
            CollectionType.MaleHelion               => MalePlayerList,
            CollectionType.FemaleHelion             => FemalePlayerList,
            CollectionType.MaleLost                 => MalePlayerList,
            CollectionType.FemaleLost               => FemalePlayerList,
            CollectionType.MaleRava                 => MalePlayerList,
            CollectionType.FemaleRava               => FemalePlayerList,
            CollectionType.MaleVeena                => MalePlayerList,
            CollectionType.FemaleVeena              => FemalePlayerList,
            CollectionType.MaleMidlanderNpc         => MaleNpcList,
            CollectionType.FemaleMidlanderNpc       => FemaleNpcList,
            CollectionType.MaleHighlanderNpc        => MaleNpcList,
            CollectionType.FemaleHighlanderNpc      => FemaleNpcList,
            CollectionType.MaleWildwoodNpc          => MaleNpcList,
            CollectionType.FemaleWildwoodNpc        => FemaleNpcList,
            CollectionType.MaleDuskwightNpc         => MaleNpcList,
            CollectionType.FemaleDuskwightNpc       => FemaleNpcList,
            CollectionType.MalePlainsfolkNpc        => MaleNpcList,
            CollectionType.FemalePlainsfolkNpc      => FemaleNpcList,
            CollectionType.MaleDunesfolkNpc         => MaleNpcList,
            CollectionType.FemaleDunesfolkNpc       => FemaleNpcList,
            CollectionType.MaleSeekerOfTheSunNpc    => MaleNpcList,
            CollectionType.FemaleSeekerOfTheSunNpc  => FemaleNpcList,
            CollectionType.MaleKeeperOfTheMoonNpc   => MaleNpcList,
            CollectionType.FemaleKeeperOfTheMoonNpc => FemaleNpcList,
            CollectionType.MaleSeawolfNpc           => MaleNpcList,
            CollectionType.FemaleSeawolfNpc         => FemaleNpcList,
            CollectionType.MaleHellsguardNpc        => MaleNpcList,
            CollectionType.FemaleHellsguardNpc      => FemaleNpcList,
            CollectionType.MaleRaenNpc              => MaleNpcList,
            CollectionType.FemaleRaenNpc            => FemaleNpcList,
            CollectionType.MaleXaelaNpc             => MaleNpcList,
            CollectionType.FemaleXaelaNpc           => FemaleNpcList,
            CollectionType.MaleHelionNpc            => MaleNpcList,
            CollectionType.FemaleHelionNpc          => FemaleNpcList,
            CollectionType.MaleLostNpc              => MaleNpcList,
            CollectionType.FemaleLostNpc            => FemaleNpcList,
            CollectionType.MaleRavaNpc              => MaleNpcList,
            CollectionType.FemaleRavaNpc            => FemaleNpcList,
            CollectionType.MaleVeenaNpc             => MaleNpcList,
            CollectionType.FemaleVeenaNpc           => FemaleNpcList,
            CollectionType.Individual               => DefaultList,
            _                                       => Array.Empty<CollectionType>(),
        };

    public static CollectionType FromParts(SubRace race, Gender gender, bool npc)
    {
        gender = gender switch
        {
            Gender.MaleNpc   => Gender.Male,
            Gender.FemaleNpc => Gender.Female,
            _                => gender,
        };

        return (race, gender, npc) switch
        {
            (SubRace.Midlander, Gender.Male, false)       => CollectionType.MaleMidlander,
            (SubRace.Highlander, Gender.Male, false)      => CollectionType.MaleHighlander,
            (SubRace.Wildwood, Gender.Male, false)        => CollectionType.MaleWildwood,
            (SubRace.Duskwight, Gender.Male, false)       => CollectionType.MaleDuskwight,
            (SubRace.Plainsfolk, Gender.Male, false)      => CollectionType.MalePlainsfolk,
            (SubRace.Dunesfolk, Gender.Male, false)       => CollectionType.MaleDunesfolk,
            (SubRace.SeekerOfTheSun, Gender.Male, false)  => CollectionType.MaleSeekerOfTheSun,
            (SubRace.KeeperOfTheMoon, Gender.Male, false) => CollectionType.MaleKeeperOfTheMoon,
            (SubRace.Seawolf, Gender.Male, false)         => CollectionType.MaleSeawolf,
            (SubRace.Hellsguard, Gender.Male, false)      => CollectionType.MaleHellsguard,
            (SubRace.Raen, Gender.Male, false)            => CollectionType.MaleRaen,
            (SubRace.Xaela, Gender.Male, false)           => CollectionType.MaleXaela,
            (SubRace.Helion, Gender.Male, false)          => CollectionType.MaleHelion,
            (SubRace.Lost, Gender.Male, false)            => CollectionType.MaleLost,
            (SubRace.Rava, Gender.Male, false)            => CollectionType.MaleRava,
            (SubRace.Veena, Gender.Male, false)           => CollectionType.MaleVeena,

            (SubRace.Midlander, Gender.Female, false)       => CollectionType.FemaleMidlander,
            (SubRace.Highlander, Gender.Female, false)      => CollectionType.FemaleHighlander,
            (SubRace.Wildwood, Gender.Female, false)        => CollectionType.FemaleWildwood,
            (SubRace.Duskwight, Gender.Female, false)       => CollectionType.FemaleDuskwight,
            (SubRace.Plainsfolk, Gender.Female, false)      => CollectionType.FemalePlainsfolk,
            (SubRace.Dunesfolk, Gender.Female, false)       => CollectionType.FemaleDunesfolk,
            (SubRace.SeekerOfTheSun, Gender.Female, false)  => CollectionType.FemaleSeekerOfTheSun,
            (SubRace.KeeperOfTheMoon, Gender.Female, false) => CollectionType.FemaleKeeperOfTheMoon,
            (SubRace.Seawolf, Gender.Female, false)         => CollectionType.FemaleSeawolf,
            (SubRace.Hellsguard, Gender.Female, false)      => CollectionType.FemaleHellsguard,
            (SubRace.Raen, Gender.Female, false)            => CollectionType.FemaleRaen,
            (SubRace.Xaela, Gender.Female, false)           => CollectionType.FemaleXaela,
            (SubRace.Helion, Gender.Female, false)          => CollectionType.FemaleHelion,
            (SubRace.Lost, Gender.Female, false)            => CollectionType.FemaleLost,
            (SubRace.Rava, Gender.Female, false)            => CollectionType.FemaleRava,
            (SubRace.Veena, Gender.Female, false)           => CollectionType.FemaleVeena,

            (SubRace.Midlander, Gender.Male, true)       => CollectionType.MaleMidlanderNpc,
            (SubRace.Highlander, Gender.Male, true)      => CollectionType.MaleHighlanderNpc,
            (SubRace.Wildwood, Gender.Male, true)        => CollectionType.MaleWildwoodNpc,
            (SubRace.Duskwight, Gender.Male, true)       => CollectionType.MaleDuskwightNpc,
            (SubRace.Plainsfolk, Gender.Male, true)      => CollectionType.MalePlainsfolkNpc,
            (SubRace.Dunesfolk, Gender.Male, true)       => CollectionType.MaleDunesfolkNpc,
            (SubRace.SeekerOfTheSun, Gender.Male, true)  => CollectionType.MaleSeekerOfTheSunNpc,
            (SubRace.KeeperOfTheMoon, Gender.Male, true) => CollectionType.MaleKeeperOfTheMoonNpc,
            (SubRace.Seawolf, Gender.Male, true)         => CollectionType.MaleSeawolfNpc,
            (SubRace.Hellsguard, Gender.Male, true)      => CollectionType.MaleHellsguardNpc,
            (SubRace.Raen, Gender.Male, true)            => CollectionType.MaleRaenNpc,
            (SubRace.Xaela, Gender.Male, true)           => CollectionType.MaleXaelaNpc,
            (SubRace.Helion, Gender.Male, true)          => CollectionType.MaleHelionNpc,
            (SubRace.Lost, Gender.Male, true)            => CollectionType.MaleLostNpc,
            (SubRace.Rava, Gender.Male, true)            => CollectionType.MaleRavaNpc,
            (SubRace.Veena, Gender.Male, true)           => CollectionType.MaleVeenaNpc,

            (SubRace.Midlander, Gender.Female, true)       => CollectionType.FemaleMidlanderNpc,
            (SubRace.Highlander, Gender.Female, true)      => CollectionType.FemaleHighlanderNpc,
            (SubRace.Wildwood, Gender.Female, true)        => CollectionType.FemaleWildwoodNpc,
            (SubRace.Duskwight, Gender.Female, true)       => CollectionType.FemaleDuskwightNpc,
            (SubRace.Plainsfolk, Gender.Female, true)      => CollectionType.FemalePlainsfolkNpc,
            (SubRace.Dunesfolk, Gender.Female, true)       => CollectionType.FemaleDunesfolkNpc,
            (SubRace.SeekerOfTheSun, Gender.Female, true)  => CollectionType.FemaleSeekerOfTheSunNpc,
            (SubRace.KeeperOfTheMoon, Gender.Female, true) => CollectionType.FemaleKeeperOfTheMoonNpc,
            (SubRace.Seawolf, Gender.Female, true)         => CollectionType.FemaleSeawolfNpc,
            (SubRace.Hellsguard, Gender.Female, true)      => CollectionType.FemaleHellsguardNpc,
            (SubRace.Raen, Gender.Female, true)            => CollectionType.FemaleRaenNpc,
            (SubRace.Xaela, Gender.Female, true)           => CollectionType.FemaleXaelaNpc,
            (SubRace.Helion, Gender.Female, true)          => CollectionType.FemaleHelionNpc,
            (SubRace.Lost, Gender.Female, true)            => CollectionType.FemaleLostNpc,
            (SubRace.Rava, Gender.Female, true)            => CollectionType.FemaleRavaNpc,
            (SubRace.Veena, Gender.Female, true)           => CollectionType.FemaleVeenaNpc,
            _                                              => CollectionType.Inactive,
        };
    }

    public static bool TryParse(string text, out CollectionType type)
    {
        if (Enum.TryParse(text, true, out type))
            return type is not CollectionType.Inactive and not CollectionType.Temporary;

        if (string.Equals(text, "character", StringComparison.OrdinalIgnoreCase))
        {
            type = CollectionType.Individual;
            return true;
        }

        if (string.Equals(text, "base", StringComparison.OrdinalIgnoreCase))
        {
            type = CollectionType.Default;
            return true;
        }

        if (string.Equals(text, "ui", StringComparison.OrdinalIgnoreCase))
        {
            type = CollectionType.Interface;
            return true;
        }

        if (string.Equals(text, "selected", StringComparison.OrdinalIgnoreCase))
        {
            type = CollectionType.Current;
            return true;
        }

        foreach (var t in Enum.GetValues<CollectionType>())
        {
            if (t is CollectionType.Inactive or CollectionType.Temporary)
                continue;

            if (string.Equals(text, t.ToName(), StringComparison.OrdinalIgnoreCase))
            {
                type = t;
                return true;
            }
        }

        return false;
    }

    public static string ToName(this CollectionType collectionType)
        => collectionType switch
        {
            CollectionType.Yourself                 => "你的角色",
            CollectionType.NonPlayerChild           => "儿童NPC",
            CollectionType.NonPlayerElderly         => "老年NPC",
            CollectionType.MalePlayerCharacter      => "男性玩家角色",
            CollectionType.MaleNonPlayerCharacter   => "男性NPC",
            CollectionType.MaleMidlander            => $"男性{SubRace.Midlander.ToName()}",
            CollectionType.MaleHighlander           => $"男性{SubRace.Highlander.ToName()}",
            CollectionType.MaleWildwood             => $"男性{SubRace.Wildwood.ToName()}",
            CollectionType.MaleDuskwight            => $"男性{SubRace.Duskwight.ToName()}",
            CollectionType.MalePlainsfolk           => $"男性{SubRace.Plainsfolk.ToName()}",
            CollectionType.MaleDunesfolk            => $"男性{SubRace.Dunesfolk.ToName()}",
            CollectionType.MaleSeekerOfTheSun       => $"男性{SubRace.SeekerOfTheSun.ToName()}",
            CollectionType.MaleKeeperOfTheMoon      => $"男性{SubRace.KeeperOfTheMoon.ToName()}",
            CollectionType.MaleSeawolf              => $"男性{SubRace.Seawolf.ToName()}",
            CollectionType.MaleHellsguard           => $"男性{SubRace.Hellsguard.ToName()}",
            CollectionType.MaleRaen                 => $"男性{SubRace.Raen.ToName()}",
            CollectionType.MaleXaela                => $"男性{SubRace.Xaela.ToName()}",
            CollectionType.MaleHelion               => $"男性{SubRace.Helion.ToName()}",
            CollectionType.MaleLost                 => $"男性{SubRace.Lost.ToName()}",
            CollectionType.MaleRava                 => $"男性{SubRace.Rava.ToName()}",
            CollectionType.MaleVeena                => $"男性{SubRace.Veena.ToName()}",
            CollectionType.MaleMidlanderNpc         => $"男性{SubRace.Midlander.ToName()} (NPC)",
            CollectionType.MaleHighlanderNpc        => $"男性{SubRace.Highlander.ToName()} (NPC)",
            CollectionType.MaleWildwoodNpc          => $"男性{SubRace.Wildwood.ToName()} (NPC)",
            CollectionType.MaleDuskwightNpc         => $"男性{SubRace.Duskwight.ToName()} (NPC)",
            CollectionType.MalePlainsfolkNpc        => $"男性{SubRace.Plainsfolk.ToName()} (NPC)",
            CollectionType.MaleDunesfolkNpc         => $"男性{SubRace.Dunesfolk.ToName()} (NPC)",
            CollectionType.MaleSeekerOfTheSunNpc    => $"男性{SubRace.SeekerOfTheSun.ToName()} (NPC)",
            CollectionType.MaleKeeperOfTheMoonNpc   => $"男性{SubRace.KeeperOfTheMoon.ToName()} (NPC)",
            CollectionType.MaleSeawolfNpc           => $"男性{SubRace.Seawolf.ToName()} (NPC)",
            CollectionType.MaleHellsguardNpc        => $"男性{SubRace.Hellsguard.ToName()} (NPC)",
            CollectionType.MaleRaenNpc              => $"男性{SubRace.Raen.ToName()} (NPC)",
            CollectionType.MaleXaelaNpc             => $"男性{SubRace.Xaela.ToName()} (NPC)",
            CollectionType.MaleHelionNpc            => $"男性{SubRace.Helion.ToName()} (NPC)",
            CollectionType.MaleLostNpc              => $"男性{SubRace.Lost.ToName()} (NPC)",
            CollectionType.MaleRavaNpc              => $"男性{SubRace.Rava.ToName()} (NPC)",
            CollectionType.MaleVeenaNpc             => $"男性{SubRace.Veena.ToName()} (NPC)",
            CollectionType.FemalePlayerCharacter    => "女性玩家角色",
            CollectionType.FemaleNonPlayerCharacter => "女性NPC",
            CollectionType.FemaleMidlander          => $"女性{SubRace.Midlander.ToName()}",
            CollectionType.FemaleHighlander         => $"女性{SubRace.Highlander.ToName()}",
            CollectionType.FemaleWildwood           => $"女性{SubRace.Wildwood.ToName()}",
            CollectionType.FemaleDuskwight          => $"女性{SubRace.Duskwight.ToName()}",
            CollectionType.FemalePlainsfolk         => $"女性{SubRace.Plainsfolk.ToName()}",
            CollectionType.FemaleDunesfolk          => $"女性{SubRace.Dunesfolk.ToName()}",
            CollectionType.FemaleSeekerOfTheSun     => $"女性{SubRace.SeekerOfTheSun.ToName()}",
            CollectionType.FemaleKeeperOfTheMoon    => $"女性{SubRace.KeeperOfTheMoon.ToName()}",
            CollectionType.FemaleSeawolf            => $"女性{SubRace.Seawolf.ToName()}",
            CollectionType.FemaleHellsguard         => $"女性{SubRace.Hellsguard.ToName()}",
            CollectionType.FemaleRaen               => $"女性{SubRace.Raen.ToName()}",
            CollectionType.FemaleXaela              => $"女性{SubRace.Xaela.ToName()}",
            CollectionType.FemaleHelion             => $"女性{SubRace.Helion.ToName()}",
            CollectionType.FemaleLost               => $"女性{SubRace.Lost.ToName()}",
            CollectionType.FemaleRava               => $"女性{SubRace.Rava.ToName()}",
            CollectionType.FemaleVeena              => $"女性{SubRace.Veena.ToName()}",
            CollectionType.FemaleMidlanderNpc       => $"女性{SubRace.Midlander.ToName()} (NPC)",
            CollectionType.FemaleHighlanderNpc      => $"女性{SubRace.Highlander.ToName()} (NPC)",
            CollectionType.FemaleWildwoodNpc        => $"女性{SubRace.Wildwood.ToName()} (NPC)",
            CollectionType.FemaleDuskwightNpc       => $"女性{SubRace.Duskwight.ToName()} (NPC)",
            CollectionType.FemalePlainsfolkNpc      => $"女性{SubRace.Plainsfolk.ToName()} (NPC)",
            CollectionType.FemaleDunesfolkNpc       => $"女性{SubRace.Dunesfolk.ToName()} (NPC)",
            CollectionType.FemaleSeekerOfTheSunNpc  => $"女性{SubRace.SeekerOfTheSun.ToName()} (NPC)",
            CollectionType.FemaleKeeperOfTheMoonNpc => $"女性{SubRace.KeeperOfTheMoon.ToName()} (NPC)",
            CollectionType.FemaleSeawolfNpc         => $"女性{SubRace.Seawolf.ToName()} (NPC)",
            CollectionType.FemaleHellsguardNpc      => $"女性{SubRace.Hellsguard.ToName()} (NPC)",
            CollectionType.FemaleRaenNpc            => $"女性{SubRace.Raen.ToName()} (NPC)",
            CollectionType.FemaleXaelaNpc           => $"女性{SubRace.Xaela.ToName()} (NPC)",
            CollectionType.FemaleHelionNpc          => $"女性{SubRace.Helion.ToName()} (NPC)",
            CollectionType.FemaleLostNpc            => $"女性{SubRace.Lost.ToName()} (NPC)",
            CollectionType.FemaleRavaNpc            => $"女性{SubRace.Rava.ToName()} (NPC)",
            CollectionType.FemaleVeenaNpc           => $"女性{SubRace.Veena.ToName()} (NPC)",
            CollectionType.Inactive                 => "合集",
            CollectionType.Default                  => "基础",
            CollectionType.Interface                => "界面",
            CollectionType.Individual               => "独立",
            CollectionType.Current                  => "当前",
            _                                       => string.Empty,
        };

    public static string ToDescription(this CollectionType collectionType)
        => collectionType switch
        {
            CollectionType.Default => "世界，音乐，家具，未指定分配的角色和怪物。",
            CollectionType.Interface => "用户界面，图标，地图，窗体样式材质。",
            CollectionType.Yourself => "你的角色，无论什么名称。可用于登陆界面。",
            CollectionType.MalePlayerCharacter => "所有男性玩家角色。",
            CollectionType.FemalePlayerCharacter => "所有女性玩家角色。",
            CollectionType.MaleNonPlayerCharacter => "所有男性人类NPC。",
            CollectionType.FemaleNonPlayerCharacter => "所有女性人类NPC。",
            _                                       => string.Empty,
        };
}
