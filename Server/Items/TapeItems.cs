using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace PlayableMusicTapes.Server;

public static class TapeItems
{
    public static void Register(TemplateTable templates, TapeData data, ISptLogger<TapeMod> logger)
    {
        foreach (var (id, template) in data.Items)
        {
            templates.Items[new MongoId(id)] = template;
        }

        AddHandbookCategories(templates, data);
        int slots = AllowInSpecialSlots(templates, data);
        logger.Info($"[Playable Music Tapes] recorder allowed in {slots} special slots");
        AddHandbookEntries(templates, data);

        foreach (var (id, price) in data.Prices)
        {
            templates.Prices.TryAdd(new MongoId(id), price);
        }
    }

    public static void ApplyFleaRules(TemplateTable templates, RagfairConfig ragfairConfig, TapeConfig config, List<MongoId> tapes)
    {
        foreach (MongoId tape in tapes)
        {
            bool fleaAllowed = config.TapeSources.TryGetValue(tape.ToString(), out TapeConfig.Source? source) && source.Flea;
            if (!fleaAllowed)
            {
                ragfairConfig.Dynamic.Blacklist.Custom.Add(tape);
            }

            if (templates.Items.TryGetValue(tape, out TemplateItem? item) && item.Properties != null)
            {
                item.Properties.CanSellOnRagfair = fleaAllowed;
            }
        }
    }

    public static (int Containers, int Shelves) AllowTapesIn(TemplateTable templates, TapeConfig config, List<MongoId> tapes)
    {
        int containers = 0;
        foreach (string id in config.TapesFitIn)
        {
            if (!templates.Items.TryGetValue(new MongoId(id), out TemplateItem? item))
            {
                continue;
            }

            bool changed = false;
            foreach (Grid grid in item.Properties?.Grids ?? [])
            {
                HashSet<MongoId>? filter = grid.Properties?.Filters?.FirstOrDefault()?.Filter;
                changed |= filter != null && AddAll(filter, tapes);
            }

            containers += changed ? 1 : 0;
        }

        int shelves = 0;
        foreach (string id in config.TapesOnShelves.Stashes)
        {
            if (!templates.Items.TryGetValue(new MongoId(id), out TemplateItem? stash))
            {
                continue;
            }

            foreach (Slot slot in stash.Properties?.Slots ?? [])
            {
                if (slot.Name == null || !slot.Name.StartsWith(config.TapesOnShelves.SlotPrefix))
                {
                    continue;
                }

                HashSet<MongoId>? filter = slot.Properties?.Filters?.FirstOrDefault()?.Filter;
                shelves += filter != null && AddAll(filter, tapes) ? 1 : 0;
            }
        }

        return (containers, shelves);
    }

    private static bool AddAll(HashSet<MongoId> filter, List<MongoId> tapes)
    {
        bool added = false;
        foreach (MongoId tape in tapes)
        {
            added |= filter.Add(tape);
        }

        return added;
    }

    private static void AddHandbookCategories(TemplateTable templates, TapeData data)
    {
        var existing = templates.Handbook.Categories.Select(category => category.Id).ToHashSet();
        foreach (HandbookCategory category in data.Categories)
        {
            if (existing.Add(category.Id))
            {
                templates.Handbook.Categories.Add(category);
            }
        }
    }

    private static void AddHandbookEntries(TemplateTable templates, TapeData data)
    {
        var existing = templates.Handbook.Items.Select(entry => entry.Id).ToHashSet();
        foreach (HandbookItem entry in data.Handbook)
        {
            if (existing.Add(entry.Id))
            {
                templates.Handbook.Items.Add(entry);
            }
        }
    }

    private static int AllowInSpecialSlots(TemplateTable templates, TapeData data)
    {
        var allowed = data.SpecialSlotItems.Select(id => new MongoId(id)).ToList();
        int slotsChanged = 0;

        foreach (TemplateItem item in templates.Items.Values)
        {
            foreach (Slot slot in item.Properties?.Slots ?? [])
            {
                HashSet<MongoId>? filter = SpecialSlotFilter(slot);
                if (filter == null)
                {
                    continue;
                }

                foreach (MongoId template in allowed)
                {
                    if (filter.Add(template))
                    {
                        slotsChanged++;
                    }
                }
            }
        }

        return slotsChanged;
    }

    private static HashSet<MongoId>? SpecialSlotFilter(Slot slot)
    {
        if (slot.Name == null || !slot.Name.StartsWith("SpecialSlot"))
        {
            return null;
        }

        return slot.Properties?.Filters?.FirstOrDefault()?.Filter;
    }
}
