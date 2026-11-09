using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Services.Commerce;

namespace PlayableMusicTapes.Server;

public static class TapeStarterGift
{
    private const int CollectionTimeHours = 720;

    private static GiftService? _giftService;
    private static ISptLogger<TapeMod>? _logger;

    public static void Register(TapeConfig config, GiftsConfig giftsConfig, GiftService giftService, ISptLogger<TapeMod> logger)
    {
        if (!config.StarterGift.Enabled)
        {
            return;
        }

        giftsConfig.Gifts[TapeIds.GiftId] = new Gift
        {
            Items = GiftItems(config.StarterGift.Tapes),
            Sender = GiftSenderType.Trader,
            Trader = new MongoId(config.Trader),
            LocaleTextId = TapeIds.GiftLocaleKey,
            CollectionTimeHours = CollectionTimeHours,
            MaxToSendPlayer = 1
        };

        _giftService = giftService;
        _logger = logger;
        var gameStart = AccessTools.Method(typeof(GameController), nameof(GameController.GameStart));
        new Harmony(TapeIds.ModGuid).Patch(gameStart, postfix: new HarmonyMethod(typeof(TapeStarterGift), nameof(AfterGameStart)));
    }

    private static List<Item> GiftItems(List<string> tapes)
    {
        MongoId parcel = TapeIds.Derived("gift/parent");
        MongoId recorder = TapeIds.Derived("gift/recorder");
        var items = new List<Item>
        {
            new()
            {
                Id = recorder,
                Template = new MongoId(TapeIds.RecorderTpl),
                ParentId = parcel,
                SlotId = "main",
                Upd = new Upd { StackObjectsCount = 1, SpawnedInSession = true }
            }
        };

        for (int i = 0; i < tapes.Count; i++)
        {
            bool loadedInRecorder = i == 0;
            items.Add(new Item
            {
                Id = TapeIds.Derived("gift/" + tapes[i]),
                Template = new MongoId(tapes[i]),
                ParentId = loadedInRecorder ? recorder : parcel,
                SlotId = loadedInRecorder ? TapeIds.TapeSlot : "main",
                Upd = new Upd { StackObjectsCount = 1, SpawnedInSession = true }
            });
        }

        return items;
    }

    private static void AfterGameStart(MongoId sessionId)
    {
        try
        {
            _giftService?.SendGiftWithSilentReceivedCheck(TapeIds.GiftId, sessionId, 1);
        }
        catch (Exception error)
        {
            _logger?.Error($"[Playable Music Tapes] starter gift failed: {error.Message}");
        }
    }
}
