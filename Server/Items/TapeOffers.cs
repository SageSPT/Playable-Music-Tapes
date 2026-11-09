using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace PlayableMusicTapes.Server;

public class TapeOffers(TradersTable traders, TemplateTable templates, ISptLogger<TapeMod> logger)
{
    private readonly Dictionary<MongoId, TraderAssort> _newAssorts = new();
    private int _offerCount;

    public static int Register(TradersTable traders, TemplateTable templates, TapeConfig config, ISptLogger<TapeMod> logger)
    {
        var offers = new TapeOffers(traders, templates, logger);
        offers.AddAll(config);
        offers.MergeIntoTraders();
        return offers._offerCount;
    }

    private void AddAll(TapeConfig config)
    {
        TapeConfig.Offer recorder = config.RecorderOffer;
        AddOffer(recorder.Trader ?? config.Trader, new MongoId(TapeIds.RecorderTpl), recorder.LoyaltyLevel, recorder.Stock, recorder.BuyLimit, Roubles(recorder.Price));

        foreach (var (tapeTpl, source) in config.TapeSources)
        {
            var tape = new MongoId(tapeTpl);
            foreach (TapeConfig.Offer offer in source.Offers)
            {
                AddOffer(offer.Trader ?? config.Trader, tape, offer.LoyaltyLevel, offer.Stock, offer.BuyLimit, Roubles(offer.Price));
                templates.Prices[tape] = Math.Max(templates.Prices.GetValueOrDefault(tape), offer.Price);
            }

            foreach (TapeConfig.Barter barter in source.Barters)
            {
                AddOffer(barter.Trader, tape, barter.LoyaltyLevel, barter.Stock, barter.BuyLimit, BarterItems(barter));
            }
        }
    }

    private void AddOffer(string traderId, MongoId item, int loyaltyLevel, int stock, int buyLimit, List<BarterScheme> cost)
    {
        var traderKey = new MongoId(traderId);
        if (!traders.TryGetValue(traderKey, out Trader? trader))
        {
            logger.Warning($"[Playable Music Tapes] trader {traderId} not found, offer for {item} skipped");
            return;
        }

        MongoId offerId = TapeIds.Derived($"offer/{traderId}/{item}");
        if (trader.Assort.Items.Any(existing => existing.Id == offerId))
        {
            return;
        }

        TraderAssort assort = NewAssortFor(traderKey);
        assort.Items.Add(new Item
        {
            Id = offerId,
            Template = item,
            ParentId = "hideout",
            SlotId = "hideout",
            Upd = new Upd
            {
                UnlimitedCount = false,
                StackObjectsCount = stock,
                BuyRestrictionMax = buyLimit,
                BuyRestrictionCurrent = 0
            }
        });
        assort.BarterScheme[offerId] = [cost];
        assort.LoyalLevelItems[offerId] = loyaltyLevel;
        _offerCount++;
    }

    private TraderAssort NewAssortFor(MongoId traderKey)
    {
        if (!_newAssorts.TryGetValue(traderKey, out TraderAssort? assort))
        {
            assort = new TraderAssort { Items = [], BarterScheme = new(), LoyalLevelItems = new() };
            _newAssorts[traderKey] = assort;
        }

        return assort;
    }

    private void MergeIntoTraders()
    {
        foreach (var (traderKey, assort) in _newAssorts)
        {
            traders[traderKey].Assort.MergeAssorts(assort);
        }
    }

    private static List<BarterScheme> Roubles(int price)
    {
        return [new BarterScheme { Count = price, Template = new MongoId(TapeIds.Roubles) }];
    }

    private static List<BarterScheme> BarterItems(TapeConfig.Barter barter)
    {
        return barter.Items
            .Select(pair => new BarterScheme { Count = pair.Value, Template = new MongoId(pair.Key) })
            .ToList();
    }
}
