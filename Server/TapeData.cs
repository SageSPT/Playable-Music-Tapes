using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace PlayableMusicTapes.Server;

public sealed class TapeData
{
    [JsonPropertyName("items")] public Dictionary<string, TemplateItem> Items { get; set; } = new();
    [JsonPropertyName("handbook")] public List<HandbookItem> Handbook { get; set; } = new();
    [JsonPropertyName("categories")] public List<HandbookCategory> Categories { get; set; } = new();
    [JsonPropertyName("prices")] public Dictionary<string, double> Prices { get; set; } = new();
    [JsonPropertyName("specialSlotItems")] public List<string> SpecialSlotItems { get; set; } = new();
}

public class TapeAchievements
{
    [JsonPropertyName("achievements")] public List<Achievement> Achievements { get; set; } = new();
}
