using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI.Screens;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Networking;

namespace PlayableMusicTapes.Client;

internal class TapeRecorder : MonoBehaviour
{
    private const float InventoryCheckInterval = 0.5f;
    private const float PositionSaveInterval = 10f;
    private const float StopAnimationSeconds = 0.6f;
    private const string StopSoundFile = "recorder_button_off_with_cloth";

    private static readonly Vector3 SpeakerPosition = new Vector3(0f, 1.2f, 0.15f);

    private static TapeRecorder _instance;

    private readonly HashSet<string> _knownItems = new HashSet<string>();
    private Player _player;
    private AudioSource _audio;
    private bool? _spatialApplied;
    private TapeInfo _tape;
    private Item _tapeItem;
    private int _trackIndex;
    private float _trackStartedAt;
    private float _nextInventoryCheck;
    private float _nextPositionSave;
    private bool _loading;
    private bool _stopping;
    private AudioClip _stopSound;
    private TapeProgressNotification _nowPlaying;

    public static string KeyLabel => Plugin.Label(Plugin.TapeKey);

    public static bool IsPlaying => _instance != null && _instance._tape != null;

    public static bool Busy => _instance != null && (_instance._loading || _instance._stopping);

    public static Player RaidPlayer => _instance?._player;

    public static Item PlayingItem => _instance?._tapeItem;

    public static TrackInfo PlayingTrack => IsPlaying ? _instance._tape.Tracks[_instance._trackIndex] : null;

    public static int PlayingTrackNumber => _instance != null ? _instance._trackIndex + 1 : 0;

    public static int PlayingTrackCount => IsPlaying ? _instance._tape.Tracks.Count : 0;

    public static float PlayingTime => _instance != null && _instance._audio != null ? _instance._audio.time : 0f;

    public static List<Item> CarriedTapes()
    {
        return _instance == null ? new List<Item>() : _instance.InventoryItems().Where(TapeCatalog.IsTape).ToList();
    }

    public static List<Item> CarriedRecorders()
    {
        return _instance == null ? new List<Item>() : TapeDeck.Recorders(_instance.InventoryItems()).ToList();
    }

    public static void OnRaidStarted(GameWorld world)
    {
        if (!TapeCatalog.Loaded)
        {
            return;
        }

        CreateInstance();
        _instance.StartRaid(world.MainPlayer);
    }

    public static void OnRaidEnded()
    {
        _instance?.EndRaid();
    }

    public static void PlayFromMenu(Item item)
    {
        if (!TapeCatalog.IsTape(item) && !TapeDeck.IsRecorder(item))
        {
            return;
        }

        CreateInstance();
        Player player = _instance._player;
        bool inRaid = player != null && player.HealthController.IsAlive;
        ItemController controller = inRaid ? player.InventoryController : TapeDeck.MenuController();
        List<Item> items = (inRaid ? _instance.InventoryItems() : TapeDeck.MenuItems(controller)).ToList();

        if (!FindRecorderAndTape(item, items, out Item recorder, out Item tape))
        {
            return;
        }

        if (inRaid && TapeListen.Busy)
        {
            return;
        }

        TapeDeck.Load(controller, recorder, tape, animation =>
        {
            bool animated = inRaid && _instance._player != null && TapeListen.PlayWithAnimation(_instance._player, recorder, tape, animation, false);
            if (!animated)
            {
                PlayItem(tape, false);
            }
        });
    }

    public static void NextFromMenu()
    {
        if (_instance?._tapeItem != null)
        {
            _instance.SkipToNextSong();
        }
    }

    public static void StopFromMenu()
    {
        if (!IsPlaying)
        {
            return;
        }

        if (_instance._player != null && !_instance._stopping)
        {
            _instance.StartCoroutine(_instance.StopWithButtonPress());
        }
        else
        {
            _instance.Stop(true);
        }
    }

    public static void PlayItem(Item item, bool skipToNextSong)
    {
        TapeInfo tape = TapeCatalog.Playable(item);
        if (tape == null)
        {
            return;
        }

        CreateInstance();
        ThirdPartyCompat.StopTheirTape();
        _instance._knownItems.Add(item.Id);
        _instance.ResumePosition(item, out int track, out float time);
        _instance.Stop(true);

        if (skipToNextSong)
        {
            track++;
            time = 0f;
        }

        if (track < 0 || track >= tape.Tracks.Count)
        {
            track = 0;
            time = 0f;
        }

        _instance.StartCoroutine(_instance.PlayTrack(tape, item, track, time));
    }

    public static void PlayFromStart(Item item)
    {
        TapeInfo tape = TapeCatalog.Playable(item);
        if (tape == null)
        {
            return;
        }

        CreateInstance();
        ThirdPartyCompat.StopTheirTape();
        _instance.Stop(false);
        TapePositions.Reset(item.Id);
        _instance.StartCoroutine(_instance.PlayTrack(tape, item, 0, 0f));
    }

    public static void StopPlaying()
    {
        _instance?.Stop(true);
    }

    private static void CreateInstance()
    {
        if (_instance != null)
        {
            return;
        }

        var host = new GameObject("PlayableMusicTapes_TapeRecorder");
        DontDestroyOnLoad(host);
        _instance = host.AddComponent<TapeRecorder>();
        host.AddComponent<TapeHud>();
    }

    private static bool FindRecorderAndTape(Item clicked, List<Item> items, out Item recorder, out Item tape)
    {
        if (TapeDeck.IsRecorder(clicked))
        {
            recorder = clicked;
            tape = TapeDeck.TapeIn(clicked);
            if (tape == null)
            {
                TapeNotificationViews.WarnNoTape();
                return false;
            }

            return true;
        }

        tape = clicked;
        recorder = TapeDeck.RecorderHolding(clicked) ?? TapeDeck.Recorders(items).OrderBy(TapeDeck.HasTape).FirstOrDefault();
        if (recorder == null)
        {
            TapeNotificationViews.WarnNoRecorder();
            return false;
        }

        return true;
    }

    private void StartRaid(Player player)
    {
        StartCoroutine(LoadStopSound());
        EndRaid();
        _player = player;
        _knownItems.Clear();
        foreach (Item item in InventoryItems())
        {
            _knownItems.Add(item.Id);
        }
    }

    private void EndRaid()
    {
        TapeListen.Reset();
        Stop(true);
        _player = null;
    }

    private IEnumerable<Item> InventoryItems()
    {
        if (_player == null || _player.Inventory == null)
        {
            return Enumerable.Empty<Item>();
        }

        try
        {
            return _player.Inventory.AllRealPlayerItems.ToList();
        }
        catch (Exception)
        {
            return Enumerable.Empty<Item>();
        }
    }

    private void Update()
    {
        if (_player == null)
        {
            UpdateInMenu();
            return;
        }

        if (!_player.HealthController.IsAlive)
        {
            EndRaid();
            return;
        }

        if (Time.time >= _nextInventoryCheck)
        {
            _nextInventoryCheck = Time.time + InventoryCheckInterval;
            CheckInventory();
        }

        UpdatePlayback();
    }

    private void UpdateInMenu()
    {
        if (_tape == null)
        {
            return;
        }

        if (TapeDeck.RecorderHolding(_tapeItem) == null)
        {
            Stop(true);
            return;
        }

        UpdatePlayback();
    }

    private void CheckInventory()
    {
        List<Item> items = InventoryItems().ToList();
        bool hasRecorder = items.Any(TapeDeck.IsRecorder);
        foreach (Item item in items)
        {
            bool isNew = _knownItems.Add(item.Id);
            if (isNew && TapeCatalog.IsTape(item) && TapeDeck.RecorderHolding(item) == null)
            {
                NotificationManager.DisplayNotification(new TapeCollectedNotification(item.LocalizedName(), hasRecorder));
            }
        }

        if (_tape == null || TapeListen.Busy)
        {
            return;
        }

        Item recorder = TapeDeck.RecorderHolding(_tapeItem);
        if (recorder == null || !items.Contains(recorder))
        {
            Stop(true);
        }
    }

    private void ResumePosition(Item item, out int track, out float time)
    {
        if (_tapeItem == item)
        {
            track = _trackIndex;
            time = _audio != null ? _audio.time : 0f;
            return;
        }

        TapePosition saved = TapePositions.Get(item.Id);
        track = saved.Track;
        time = saved.Time;
    }

    private void SkipToNextSong()
    {
        if (TapeListen.Busy || _loading || _stopping)
        {
            return;
        }

        if (_player == null)
        {
            PlayItem(_tapeItem, true);
            return;
        }

        if (!FindLoadedTape(out Item recorder, out Item tape))
        {
            return;
        }

        if (!TapeListen.PlayWithAnimation(_player, recorder, tape, RecorderAnimation.PlayOnly, true))
        {
            PlayItem(tape, true);
        }
    }

    private bool FindLoadedTape(out Item recorder, out Item tape)
    {
        if (_tapeItem != null)
        {
            tape = _tapeItem;
            recorder = TapeDeck.RecorderHolding(tape);
            return recorder != null;
        }

        List<Item> recorders = TapeDeck.Recorders(InventoryItems()).ToList();
        recorder = recorders.FirstOrDefault(TapeDeck.HasTape);
        tape = TapeDeck.TapeIn(recorder);

        if (recorders.Count == 0)
        {
            TapeNotificationViews.WarnNoRecorder();
            return false;
        }

        if (tape == null)
        {
            TapeNotificationViews.WarnNoTape();
            return false;
        }

        return true;
    }

    private IEnumerator PlayTrack(TapeInfo tape, Item item, int trackIndex, float startTime)
    {
        TrackInfo track = tape.Tracks[trackIndex];
        AudioClip clip = null;
        _loading = true;
        yield return TapeAudio.LoadClip(tape, track, loaded => clip = loaded);
        _loading = false;
        if (clip == null)
        {
            yield break;
        }

        CreateAudioSource();
        _audio.GetComponent<TapeFilter>().TapeId = tape.TapeId;
        _audio.volume = TapeFilterSettings.Volume;
        _audio.clip = clip;
        if (startTime > 0f && startTime < clip.length - 1f)
        {
            _audio.time = startTime;
        }

        _audio.Play();
        _tape = tape;
        _tapeItem = item;
        _trackIndex = trackIndex;
        _trackStartedAt = Time.time;
        _nextPositionSave = Time.time + PositionSaveInterval;

        string where = _player != null ? "in raid" : "in menu";
        string from = startTime.ToString("0.0", CultureInfo.InvariantCulture);
        Log.Info("Tapes", $"playing {tape.TapeId} track {trackIndex + 1}/{tape.Tracks.Count} ({track.Label}) from {from} s {where}");

        _nowPlaying?.Close();
        _nowPlaying = new TapeProgressNotification(track.Label, track.Length, trackIndex + 1, tape.Tracks.Count);
        NotificationManager.DisplayNotification(_nowPlaying);
    }

    private void CreateAudioSource()
    {
        if (_audio != null)
        {
            return;
        }

        var speaker = new GameObject("PlayableMusicTapes_Audio");
        if (_player != null)
        {
            speaker.transform.SetParent(_player.Transform.Original, false);
            speaker.transform.localPosition = SpeakerPosition;
        }
        else
        {
            speaker.transform.SetParent(transform, false);
            speaker.transform.localPosition = Vector3.zero;
        }

        _audio = speaker.AddComponent<AudioSource>();
        speaker.AddComponent<TapeFilter>();
        _audio.playOnAwake = false;
        _audio.volume = TapeFilterSettings.Volume;
        _audio.dopplerLevel = 0f;
        _audio.spread = 90f;
        _audio.rolloffMode = AudioRolloffMode.Logarithmic;
        _audio.minDistance = TapeCatalog.Playback?.MinDistance ?? 1.5f;
        _audio.maxDistance = TapeCatalog.Playback?.MaxDistance ?? 25f;
        _spatialApplied = null;
        ApplySpatial();
    }

    private IEnumerator StopWithButtonPress()
    {
        _stopping = true;
        PlayStopSound();

        bool openedInventory = _player != null && !_player.IsInventoryOpened;
        if (openedInventory)
        {
            _player.SetInventoryOpened(true);
        }

        yield return new WaitForSeconds(StopAnimationSeconds);

        if (openedInventory && _player != null && _player.IsInventoryOpened && !InventoryScreenOpen())
        {
            _player.SetInventoryOpened(false);
        }

        _stopping = false;
        Stop(true);
    }

    private static bool InventoryScreenOpen()
    {
        return EftScreenManager.Instance.CheckCurrentScreen(EEftScreenType.Inventory);
    }

    private void PlayStopSound()
    {
        if (_stopSound == null || _player == null || !Singleton<BetterAudio>.Instantiated)
        {
            return;
        }

        Transform headBone = _player.PlayerBones?.Head?.Original;
        Vector3 head = headBone != null ? headBone.position : _player.Position + Vector3.up * 1.6f;
        Camera camera = Camera.main;
        float distance = camera != null ? Vector3.Distance(camera.transform.position, head) : 0f;
        Singleton<BetterAudio>.Instance.PlayAtPoint(head, _stopSound, distance, BetterAudio.AudioSourceGroupType.Character, 5);
    }

    private IEnumerator LoadStopSound()
    {
        if (_stopSound != null)
        {
            yield break;
        }

        string path = Path.Combine(TapeCatalog.DataDir, "sounds", StopSoundFile + ".wav");
        string url = "file:///" + path.Replace('\\', '/');
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Log.Warning("Tapes", "recorder stop sound not loaded: " + request.error);
                yield break;
            }

            _stopSound = DownloadHandlerAudioClip.GetContent(request);
            _stopSound.name = StopSoundFile;
        }
    }

    private void Stop(bool savePosition)
    {
        if (savePosition && _tapeItem != null && _audio != null)
        {
            TapePositions.Set(_tapeItem.Id, _trackIndex, _audio.time);
        }

        if (_audio != null)
        {
            _audio.Stop();
            Destroy(_audio.gameObject);
            _audio = null;
        }

        _tape = null;
        _tapeItem = null;
        _nowPlaying?.Close();
        _nowPlaying = null;
    }

    private void UpdatePlayback()
    {
        if (_tape == null || _audio == null)
        {
            return;
        }

        bool trackFinished = !_audio.isPlaying && !_loading && Time.time - _trackStartedAt > 1f;
        if (trackFinished)
        {
            PlayNextTrack();
            return;
        }

        if (Time.time >= _nextPositionSave)
        {
            _nextPositionSave = Time.time + PositionSaveInterval;
            TapePositions.Set(_tapeItem.Id, _trackIndex, _audio.time);
        }

        _audio.volume = TapeFilterSettings.Volume;
        ApplySpatial();
        _nowPlaying?.SetTime(_audio.time);
    }

    private void ApplySpatial()
    {
        bool spatial = _player != null && TapeFilterSettings.Spatial3D;
        if (_spatialApplied == spatial)
        {
            return;
        }

        _spatialApplied = spatial;
        _audio.spatialBlend = spatial ? 1f : 0f;
        if (!Singleton<BetterAudio>.Instantiated)
        {
            return;
        }

        BetterAudio audio = Singleton<BetterAudio>.Instance;
        AudioMixerGroup group;
        if (_player == null)
        {
            group = audio.MasterMixerGroup;
        }
        else if (spatial)
        {
            group = audio.VeryStandartMixerGroup;
        }
        else
        {
            group = audio.NonSpatialBypassMixer;
        }

        _audio.outputAudioMixerGroup = group != null ? group : audio.MasterMixerGroup;
    }

    private void PlayNextTrack()
    {
        TapeInfo tape = _tape;
        Item item = _tapeItem;
        int nextTrack = _trackIndex + 1;
        Stop(false);

        if (nextTrack < tape.Tracks.Count)
        {
            TapePositions.Set(item.Id, nextTrack, 0f);
            StartCoroutine(PlayTrack(tape, item, nextTrack, 0f));
        }
        else
        {
            TapePositions.Reset(item.Id);
            Log.Info("Tapes", tape.TapeId + ": end of tape");
        }
    }
}
