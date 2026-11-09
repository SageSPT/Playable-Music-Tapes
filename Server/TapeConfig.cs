using System.Text.Json.Serialization;

namespace PlayableMusicTapes.Server;

public sealed class TapeConfig
{
    [JsonPropertyName("trader")] public string Trader { get; set; } = "5c0647fdd443bc2504c2d371";
    [JsonPropertyName("recorderOffer")] public Offer RecorderOffer { get; set; } = new();
    [JsonPropertyName("loot")] public LootChances Loot { get; set; } = new();
    [JsonPropertyName("starterGift")] public Gift StarterGift { get; set; } = new();

    [JsonPropertyName("tapeSources")] public Dictionary<string, Source> TapeSources { get; set; } = new();
    [JsonPropertyName("tapesFitIn")] public List<string> TapesFitIn { get; set; } = new();
    [JsonPropertyName("hallOfFame")] public HallOfFame TapesOnShelves { get; set; } = new();

    public sealed class HallOfFame
    {
        [JsonPropertyName("stashes")] public List<string> Stashes { get; set; } = new();
        [JsonPropertyName("slotPrefix")] public string SlotPrefix { get; set; } = "smallTrophies";
    }

    public sealed class Offer
    {
        [JsonPropertyName("trader")] public string? Trader { get; set; }
        [JsonPropertyName("price")] public int Price { get; set; }
        [JsonPropertyName("loyaltyLevel")] public int LoyaltyLevel { get; set; } = 1;
        [JsonPropertyName("stock")] public int Stock { get; set; } = 2;
        [JsonPropertyName("buyLimit")] public int BuyLimit { get; set; } = 1;
    }

    public sealed class Barter
    {
        [JsonPropertyName("trader")] public string Trader { get; set; } = "";
        [JsonPropertyName("loyaltyLevel")] public int LoyaltyLevel { get; set; } = 1;
        [JsonPropertyName("items")] public Dictionary<string, int> Items { get; set; } = new();
        [JsonPropertyName("stock")] public int Stock { get; set; } = 1;
        [JsonPropertyName("buyLimit")] public int BuyLimit { get; set; } = 1;
    }

    public sealed class Craft
    {
        [JsonPropertyName("areaType")] public int AreaType { get; set; } = 11;
        [JsonPropertyName("areaLevel")] public int AreaLevel { get; set; } = 1;
        [JsonPropertyName("seconds")] public int Seconds { get; set; } = 7200;
        [JsonPropertyName("items")] public Dictionary<string, int> Items { get; set; } = new();
        [JsonPropertyName("tools")] public List<string> Tools { get; set; } = new();
    }

    public sealed class LootSpot
    {
        [JsonPropertyName("locations")] public List<string> Locations { get; set; } = new();
        [JsonPropertyName("containers")] public Dictionary<string, double> Containers { get; set; } = new();
    }

    public sealed class Source
    {
        [JsonPropertyName("offers")] public List<Offer> Offers { get; set; } = new();
        [JsonPropertyName("barters")] public List<Barter> Barters { get; set; } = new();
        [JsonPropertyName("questRewards")] public List<string> QuestRewards { get; set; } = new();
        [JsonPropertyName("craft")] public Craft? Craft { get; set; }
        [JsonPropertyName("loot")] public LootSpot? Loot { get; set; }
        [JsonPropertyName("flea")] public bool Flea { get; set; }
    }

    public sealed class LootChances
    {
        [JsonPropertyName("recorderChance")] public Dictionary<string, double> RecorderChance { get; set; } = new();
    }

    public sealed class Gift
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("tapes")] public List<string> Tapes { get; set; } = new();
    }
}
