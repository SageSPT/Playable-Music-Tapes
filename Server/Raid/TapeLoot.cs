using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace PlayableMusicTapes.Server;

public static class TapeLoot
{
    public static int Register(LocationTable locations, TapeConfig config)
    {
        var recorder = new List<MongoId> { new(TapeIds.RecorderTpl) };
        var containerTypes = new HashSet<string>(config.Loot.RecorderChance.Keys);

        foreach (var (locationKey, location) in locations.GetDictionary())
        {
            var tapesHere = TapesSpawningOn(locationKey, config);
            foreach (var (_, source) in tapesHere)
            {
                containerTypes.UnionWith(source.Loot!.Containers.Keys);
            }

            location.StaticLoot?.AddTransformer(loot =>
            {
                if (loot == null)
                {
                    return loot;
                }

                foreach (var (container, chance) in config.Loot.RecorderChance)
                {
                    AddToContainer(loot, container, recorder, chance);
                }

                foreach (var (tapeTpl, source) in tapesHere)
                {
                    foreach (var (container, chance) in source.Loot!.Containers)
                    {
                        AddToContainer(loot, container, [new MongoId(tapeTpl)], chance);
                    }
                }

                return loot;
            });
        }

        return containerTypes.Count;
    }

    private static List<KeyValuePair<string, TapeConfig.Source>> TapesSpawningOn(string locationKey, TapeConfig config)
    {
        string location = locationKey.ToLowerInvariant();
        return config.TapeSources
            .Where(pair => pair.Value.Loot != null && pair.Value.Loot.Locations.Any(name => name.ToLowerInvariant() == location))
            .ToList();
    }

    private static void AddToContainer(Dictionary<MongoId, StaticLootDetails> loot, string container, List<MongoId> items, double chance)
    {
        if (chance <= 0 || !loot.TryGetValue(new MongoId(container), out StaticLootDetails? details) || details.ItemDistribution == null)
        {
            return;
        }

        var distribution = details.ItemDistribution.ToList();
        double totalWeight = distribution.Sum(entry => entry.RelativeProbability ?? 0);
        double weightForChance = totalWeight * chance / Math.Max(1e-6, 1 - chance * items.Count);
        float weight = (float)Math.Max(1, Math.Round(weightForChance));

        foreach (MongoId item in items)
        {
            if (distribution.All(entry => entry.Tpl != item))
            {
                distribution.Add(new ItemDistribution { Tpl = item, RelativeProbability = weight });
            }
        }

        details.ItemDistribution = distribution;
    }
}
