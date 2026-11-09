using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace PlayableMusicTapes.Server;

public static class TapeRewards
{
    public static int RegisterQuestRewards(TemplateTable templates, TapeConfig config, ISptLogger<TapeMod> logger)
    {
        int added = 0;
        foreach (var (tapeTpl, source) in config.TapeSources)
        {
            foreach (string questId in source.QuestRewards)
            {
                if (!templates.Quests.TryGetValue(new MongoId(questId), out Quest? quest) || quest.Rewards == null)
                {
                    logger.Warning($"[Playable Music Tapes] quest {questId} not found, reward {tapeTpl} skipped");
                    continue;
                }

                if (AddQuestReward(quest, questId, tapeTpl))
                {
                    added++;
                }
            }
        }

        return added;
    }

    public static int RegisterCrafts(HideoutTable hideout, TapeConfig config, JsonUtil jsonUtil)
    {
        int added = 0;
        foreach (var (tapeTpl, source) in config.TapeSources)
        {
            if (source.Craft == null || hideout.Production.Recipes == null)
            {
                continue;
            }

            HideoutProduction? recipe = jsonUtil.Deserialize<HideoutProduction>(CraftJson(tapeTpl, source.Craft));
            if (recipe == null)
            {
                continue;
            }

            hideout.Production.Recipes.RemoveAll(existing => existing.Id == recipe.Id);
            hideout.Production.Recipes.Add(recipe);
            added++;
        }

        return added;
    }

    private static bool AddQuestReward(Quest quest, string questId, string tapeTpl)
    {
        if (!quest.Rewards!.TryGetValue("Success", out List<Reward>? rewards))
        {
            rewards = new List<Reward>();
            quest.Rewards["Success"] = rewards;
        }

        MongoId rewardId = TapeIds.Derived($"quest/{questId}/{tapeTpl}");
        if (rewards.Any(reward => reward.Id == rewardId))
        {
            return false;
        }

        MongoId itemId = TapeIds.Derived($"quest/{questId}/{tapeTpl}/item");
        rewards.Add(new Reward
        {
            Id = rewardId,
            Type = RewardType.Item,
            Index = rewards.Count,
            Target = itemId.ToString(),
            Value = 1,
            FindInRaid = false,
            Unknown = false,
            IsHidden = false,
            IsEncoded = false,
            GameMode = ["regular", "pve"],
            AvailableInGameEditions = [],
            Items = [new Item { Id = itemId, Template = new MongoId(tapeTpl), Upd = new Upd { StackObjectsCount = 1 } }]
        });
        return true;
    }

    private static string CraftJson(string tapeTpl, TapeConfig.Craft craft)
    {
        var requirements = new List<object>
        {
            new { areaType = craft.AreaType, requiredLevel = craft.AreaLevel, type = "Area" }
        };

        foreach (var (itemTpl, count) in craft.Items)
        {
            requirements.Add(new
            {
                templateId = itemTpl,
                count,
                isEncoded = false,
                isFunctional = false,
                isSpawnedInSession = false,
                type = "Item"
            });
        }

        foreach (string tool in craft.Tools)
        {
            requirements.Add(new { templateId = tool, type = "Tool" });
        }

        var recipe = new
        {
            _id = TapeIds.Derived("craft/" + tapeTpl).ToString(),
            areaType = craft.AreaType,
            requirements,
            productionTime = craft.Seconds,
            endProduct = tapeTpl,
            isEncoded = false,
            locked = false,
            needFuelForAllProductionTime = false,
            continuous = false,
            count = 1,
            productionLimitCount = 0,
            isCodeProduction = false
        };

        return JsonSerializer.Serialize(recipe);
    }
}
