using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Utils;

namespace PlayableMusicTapes.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 91000)]
public class TapeMod(
    ISptLogger<TapeMod> logger,
    TemplateTable templates,
    LocaleTable localeTable,
    LocationTable locationTable,
    TradersTable traders,
    GiftsConfig giftsConfig,
    RagfairConfig ragfairConfig,
    GiftService giftService,
    ImageRouter imageRouter,
    HideoutTable hideout,
    JsonUtil jsonUtil) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            TapeConfig config = await ReadJson<TapeConfig>(cancellationToken, "config.json") ?? new TapeConfig();
            TapeData data = (await ReadJson<TapeData>(cancellationToken, "db", "items.json"))!;
            TapeAchievements achievements = await ReadJson<TapeAchievements>(cancellationToken, "db", "achievements.json") ?? new TapeAchievements();
            List<MongoId> tapes = TapeTemplates(data);

            TapeItems.Register(templates, data, logger);
            TapeItems.ApplyFleaRules(templates, ragfairConfig, config, tapes);
            var (fitIn, shelves) = TapeItems.AllowTapesIn(templates, config, tapes);
            logger.Info($"[Playable Music Tapes] tapes fit in {fitIn} containers and {shelves} Hall of Fame shelf slots");
            RegisterAchievements(achievements);
            imageRouter.AddRoute("/files/handbook/icon_music", TapeIds.File("images", "icon_music.png"));
            await RegisterLocales(cancellationToken);

            int offers = TapeOffers.Register(traders, templates, config, logger);
            int questRewards = TapeRewards.RegisterQuestRewards(templates, config, logger);
            int crafts = TapeRewards.RegisterCrafts(hideout, config, jsonUtil);
            int containerTypes = TapeLoot.Register(locationTable, config);
            TapeStarterGift.Register(config, giftsConfig, giftService, logger);

            logger.Success($"[Playable Music Tapes] {tapes.Count} tapes registered: {offers} trader offers/barters, {questRewards} quest rewards, {crafts} crafts, loot in {containerTypes} container types");
        }
        catch (Exception error)
        {
            logger.Error($"[Playable Music Tapes] load failed: {error}");
        }
    }

    private async Task<T?> ReadJson<T>(CancellationToken cancellationToken, params string[] path) where T : class
    {
        string json = await File.ReadAllTextAsync(TapeIds.File(path), cancellationToken);
        return jsonUtil.Deserialize<T>(json);
    }

    private static List<MongoId> TapeTemplates(TapeData data)
    {
        return data.Items
            .Where(pair => pair.Key != TapeIds.RecorderTpl && pair.Value.Type == "Item")
            .Select(pair => new MongoId(pair.Key))
            .ToList();
    }

    private void RegisterAchievements(TapeAchievements achievements)
    {
        foreach (Achievement achievement in achievements.Achievements)
        {
            templates.Achievements.RemoveAll(existing => existing.Id == achievement.Id);
            templates.Achievements.Add(achievement);

            string image = Path.GetFileNameWithoutExtension(achievement.ImageUrl ?? "");
            imageRouter.AddRoute("/files/achievement/" + image, TapeIds.File("images", image + ".png"));
        }

        string ids = string.Join(", ", achievements.Achievements.Select(achievement => achievement.Id));
        logger.Info($"[Playable Music Tapes] {achievements.Achievements.Count} achievement(s): " + ids);
    }

    private async Task RegisterLocales(CancellationToken cancellationToken)
    {
        string folder = TapeIds.File("db", "locales");
        Dictionary<string, string> english = await ReadLocale(Path.Combine(folder, "en.json"), cancellationToken);

        foreach (var (language, table) in localeTable.Global)
        {
            Dictionary<string, string> texts = await ReadLocale(Path.Combine(folder, language + ".json"), cancellationToken);
            foreach (var (key, value) in english)
            {
                texts.TryAdd(key, value);
            }

            table.AddTransformer(gameTexts =>
            {
                if (gameTexts != null)
                {
                    foreach (var (key, value) in texts)
                    {
                        gameTexts.TryAdd(key, value);
                    }
                }

                return gameTexts;
            });
        }
    }

    private async Task<Dictionary<string, string>> ReadLocale(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>();
        }

        string json = await File.ReadAllTextAsync(path, cancellationToken);
        return jsonUtil.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }
}
