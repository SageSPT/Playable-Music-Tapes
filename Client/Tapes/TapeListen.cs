using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace PlayableMusicTapes.Client;

internal static class TapeListen
{
    private const string RaiseWithTape = "OUT TO IDLE";
    private const string RaiseWithoutTape = "OUT TO IDLE NO TAPE";
    private const string TapeOutState = "TAPE OUT";
    private const string PressPlayState = "TAPE IN PLAY";
    private const string PutAwayWithTape = "IDLE TO OUT";
    private const string PutAwayWithoutTape = "IDLE TO OUT NO TAPE";

    private const float StillFrame = 0.66f;
    private const float RaiseClipLength = 0.967f;
    private const float PlayPressTime = 0.37f;
    private const float PlayReturnTime = 0.52f;
    private const float EjectPressTime = 0.19f;
    private const float EjectReturnTime = 0.9f;
    private const float BlendInSeconds = 0.25f;
    private const float BlendBackSeconds = 0.3f;

    private const float PickupWaitSeconds = 5f;
    private const float ListenDelaySeconds = 0.3f;
    private const float SpawnTimeoutSeconds = 3f;
    private const float WeapOutTimeoutSeconds = 15f;
    private const float HideTimeoutSeconds = 2f;
    private const float MoveTimeoutSeconds = 1f;

    private static readonly int ActiveParameter = Animator.StringToHash("Active");
    private static readonly int PressPlayHash = Animator.StringToHash("TAPE IN PLAY");
    private static readonly int ButtonPlayHash = Animator.StringToHash("BUTTON PLAY");

    private enum HoldPhase
    {
        Raising,
        Acting,
        Returning,
        Still,
        PuttingAway
    }

    private sealed class AnimationCue
    {
        public int StateHash;
        public float NormalizedTime;
        public Action Action;
    }

    private static readonly List<AnimationCue> Cues = new List<AnimationCue>();

    private static Player _player;
    private static Item _recorder;
    private static Item _tape;
    private static Item _previousHandsItem;
    private static RecorderAnimation _animation;
    private static bool _skipToNextSong;
    private static bool _playbackStarted;
    private static bool _recorderSpawned;

    private static bool _holdMode;
    private static HoldPhase _phase;
    private static bool _waitingForMove;
    private static bool _restoreHandsOnPutAway;
    private static string _stillState;
    private static BaseSoundPlayer _recorderSounds;

    public static bool Busy => _recorder != null;

    public static bool Holding => _holdMode && Busy;

    public static bool HoldReady => Holding && _phase == HoldPhase.Still && !_waitingForMove;

    public static Item HeldRecorder => Holding ? _recorder : null;

    public static void AddListenAction(GamePlayerOwner owner, Item rootItem, AvailableInteractionState state)
    {
        if (state?.Actions == null || owner?.Player == null || !TapeCatalog.IsTape(rootItem) || !EquippedRecorders(owner.Player).Any())
        {
            return;
        }

        int takeIndex = state.Actions.FindIndex(action => action.Name == "Take");
        if (takeIndex < 0 || state.Actions.Any(action => action.Name == "LISTEN"))
        {
            return;
        }

        InteractionAction take = state.Actions[takeIndex];
        Player player = owner.Player;
        state.Actions.Insert(takeIndex + 1, new InteractionAction
        {
            Name = "LISTEN",
            TargetName = take.TargetName,
            Action = () =>
            {
                take.Action();
                Runner.Host.StartCoroutine(ListenAfterPickup(player, rootItem));
            }
        });
    }

    public static bool PlayWithAnimation(Player player, Item recorder, Item tape, RecorderAnimation animation, bool skipToNextSong)
    {
        if (recorder == null)
        {
            Log.Warning("Tapes", "LISTEN: no recorder in the inventory");
            return false;
        }

        if (Busy)
        {
            Log.Warning("Tapes", "LISTEN: recorder already in use");
            return false;
        }

        _player = player;
        _recorder = recorder;
        _tape = tape;
        _previousHandsItem = player.HandsController?.Item;
        _animation = animation;
        _skipToNextSong = skipToNextSong;
        _playbackStarted = false;
        _recorderSpawned = false;

        try
        {
            player.Proceed<Player.UsableItemController>(recorder, result =>
            {
                if (result.Failed)
                {
                    Log.Warning("Tapes", "LISTEN: recorder could not be taken in hands (" + result.Error + ") - playing without the animation");
                    FinishUsing(false);
                }
            });
        }
        catch (Exception error)
        {
            Log.Error("Tapes", "LISTEN: " + error);
            FinishUsing(false);
            return true;
        }

        Runner.Host.StartCoroutine(WatchForStuckRecorder());
        Runner.Host.StartCoroutine(WatchAnimation(player));
        return true;
    }

    public static void PrepareRecorderRig(Player.UsableItemController controller, Player player, WeaponPrefab weaponPrefab)
    {
        if (weaponPrefab == null || weaponPrefab.FirearmsAnimator != null || player == null || !TapeDeck.IsRecorder(controller.Item))
        {
            return;
        }

        weaponPrefab.Init(player, true);
        controller.AnimationEventsEmitter = weaponPrefab.AnimationEventsEmitter;
        controller._objectInHandsAnimator = weaponPrefab.FirearmsAnimator;
    }

    public static void InitRecorderSounds(Player.UsableItemController controller, Player player)
    {
        if (player == null || controller?.Item == null || controller.Item.StringTemplateId != TapeCatalog.RecorderTpl)
        {
            return;
        }

        BaseSoundPlayer sounds = controller._controllerObject != null ? controller._controllerObject.GetComponent<BaseSoundPlayer>() : null;
        if (sounds == null)
        {
            Log.Warning("Tapes", "LISTEN: recorder rig has no sound player");
            return;
        }

        sounds.Init(controller, player.PlayerBones.WeaponRoot, player);
        _recorderSounds = sounds;
    }

    public static bool IsMuted(BaseSoundPlayer sounds)
    {
        return Holding && (_phase == HoldPhase.Returning || _phase == HoldPhase.Still) && sounds == _recorderSounds;
    }

    public static void OnSpawn(object spawnOperation)
    {
        var controller = Traverse.Create(spawnOperation).Field("Controller").GetValue<Player.UsableItemController>();
        if (!Busy || controller == null || controller.Item != _recorder)
        {
            return;
        }

        _recorderSpawned = true;
        bool tapeGoesIn = _animation != RecorderAnimation.PlayOnly;
        bool oldTapeComesOut = _animation == RecorderAnimation.EjectAndInsert;

        FirearmsAnimator animator = controller.FirearmsAnimator;
        animator.SetMagInWeapon(tapeGoesIn);
        animator.Animator.SetBool(AnimationControllerParametersTable.BOOL_MAGOUT, oldTapeComesOut);
    }

    public static void OnWeapOut(Player.UsableItemController controller)
    {
        if (!Busy || controller == null || controller.Item != _recorder)
        {
            return;
        }

        FinishUsing(!_holdMode || _restoreHandsOnPutAway);
        Runner.Host.StartCoroutine(FinishPuttingAway(controller));
    }

    public static void ToggleHold(Player player)
    {
        if (Holding)
        {
            if (HoldReady)
            {
                PutAway();
            }

            return;
        }

        if (Busy || player == null)
        {
            return;
        }

        List<Item> recorders = TapeRecorder.CarriedRecorders();
        Item recorder = recorders.FirstOrDefault(TapeDeck.HasTape) ?? recorders.FirstOrDefault();
        if (recorder == null)
        {
            TapeNotificationViews.WarnNoRecorder();
            return;
        }

        bool hasTape = TapeDeck.HasTape(recorder);
        _holdMode = true;
        _phase = HoldPhase.Raising;
        _waitingForMove = false;
        _restoreHandsOnPutAway = true;
        Cues.Clear();
        _stillState = hasTape ? RaiseWithTape : RaiseWithoutTape;

        RecorderAnimation raise = hasTape ? RecorderAnimation.EjectAndInsert : RecorderAnimation.Insert;
        if (!PlayWithAnimation(player, recorder, null, raise, false))
        {
            _holdMode = false;
        }
    }

    public static void HoldPlayStop()
    {
        PressPlay(false);
    }

    public static void HoldNextSong()
    {
        PressPlay(true);
    }

    public static void HoldRestart()
    {
        Item tape = HoldReady ? TapeDeck.TapeIn(_recorder) : null;
        if (tape == null)
        {
            return;
        }

        StartAction(PressPlayState);
        AddCue(PressPlayState, PlayPressTime, () => TapeRecorder.PlayFromStart(tape));
        AddReturnToStill(PressPlayState, PlayReturnTime, RaiseWithTape);
    }

    public static void HoldInsert(Item tape)
    {
        if (!HoldReady || tape == null || TapeDeck.TapeIn(_recorder) == tape)
        {
            return;
        }

        Item recorder = _recorder;
        Item oldTape = TapeDeck.TapeIn(recorder);
        _waitingForMove = true;
        TapeDeck.Load(_player.InventoryController, recorder, tape, _ =>
        {
            _waitingForMove = false;
            if (!Holding || _recorder != recorder)
            {
                return;
            }

            StartAction(null);
            if (oldTape != null)
            {
                AddCue(TapeOutState, EjectPressTime, () => StopIfPlaying(oldTape));
            }

            AddCue(PressPlayState, PlayPressTime, () => TapeRecorder.PlayItem(tape, false));
            AddReturnToStill(PressPlayState, PlayReturnTime, RaiseWithTape);
        });
        Runner.Host.StartCoroutine(StopWaitingForMoveLater());
    }

    public static void HoldEject()
    {
        Item tape = HoldReady ? TapeDeck.TapeIn(_recorder) : null;
        if (tape == null)
        {
            return;
        }

        Item recorder = _recorder;
        _waitingForMove = true;
        bool ejecting = TapeDeck.Eject(_player.InventoryController, recorder, () =>
        {
            _waitingForMove = false;
            if (!Holding || _recorder != recorder)
            {
                return;
            }

            StartAction(null);
            AddCue(TapeOutState, EjectPressTime, () => StopIfPlaying(tape));
            AddReturnToStill(TapeOutState, EjectReturnTime, RaiseWithoutTape);
        });

        if (!ejecting)
        {
            _waitingForMove = false;
            return;
        }

        Runner.Host.StartCoroutine(StopWaitingForMoveLater());
    }

    public static void PutAway()
    {
        if (!HoldReady)
        {
            return;
        }

        _restoreHandsOnPutAway = true;
        StartAction(TapeDeck.HasTape(_recorder) ? PutAwayWithTape : PutAwayWithoutTape);
        _phase = HoldPhase.PuttingAway;
    }

    public static void Reset()
    {
        _holdMode = false;
        _phase = HoldPhase.Raising;
        _waitingForMove = false;
        Cues.Clear();
        _recorder = null;
        _tape = null;
        _previousHandsItem = null;
    }

    private static IEnumerable<Item> EquippedRecorders(Player player)
    {
        return TapeDeck.Recorders(player.InventoryController.Inventory.Equipment.GetAllItems());
    }

    private static IEnumerator ListenAfterPickup(Player player, Item tape)
    {
        for (float waited = 0f; waited < PickupWaitSeconds; waited += Time.unscaledDeltaTime)
        {
            if (player == null || tape == null)
            {
                yield break;
            }

            if (TapeDeck.IsCarried(player, tape))
            {
                yield return new WaitForSeconds(ListenDelaySeconds);
                TapeRecorder.PlayFromMenu(tape);
                yield break;
            }

            yield return null;
        }

        Log.Warning("Tapes", "LISTEN: the tape did not reach the inventory within 5 s");
    }

    private static IEnumerator WatchAnimation(Player player)
    {
        if (_holdMode)
        {
            yield return HoldLoop(player);
            yield break;
        }

        int lastState = 0;
        while (Busy && player != null)
        {
            if (player.HandsController is Player.UsableItemController controller && controller.Item == _recorder && controller.FirearmsAnimator?.Animator != null)
            {
                RecorderTapeLabel.Show(controller, TapeDeck.TapeIn(_recorder) ?? _tape);
                int state = controller.FirearmsAnimator.Animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
                if (state != lastState)
                {
                    lastState = state;
                    if (state == PressPlayHash || state == ButtonPlayHash)
                    {
                        StartPlayback();
                    }
                }
            }

            yield return null;
        }
    }

    private static void StartPlayback()
    {
        if (_playbackStarted)
        {
            return;
        }

        _playbackStarted = true;
        if (_tape != null)
        {
            TapeRecorder.PlayItem(_tape, _skipToNextSong);
        }
    }

    private static void FinishUsing(bool restoreHands)
    {
        Player player = _player;
        Item previousHandsItem = _previousHandsItem;
        StartPlayback();
        _recorder = null;
        _tape = null;
        _previousHandsItem = null;
        _holdMode = false;

        if (!restoreHands || player == null)
        {
            return;
        }

        bool previousStillEquipped = previousHandsItem?.CurrentAddress != null
            && previousHandsItem.CurrentAddress.IsChildOf(player.InventoryController.Inventory.Equipment);
        if (previousStillEquipped)
        {
            player.TryProceed(previousHandsItem, _ => { });
        }
        else
        {
            player.SetFirstAvailableItem(_ => { });
        }
    }

    private static IEnumerator FinishPuttingAway(Player.UsableItemController controller)
    {
        for (float waited = 0f; waited < HideTimeoutSeconds && controller != null; waited += Time.deltaTime)
        {
            if (controller.CurrentOperation is Player.UsableItemController.Remove)
            {
                controller.FastForwardCurrentState();
                yield break;
            }

            yield return null;
        }

        if (controller != null && _player != null && _player.HandsController == controller)
        {
            Log.Warning("Tapes", "LISTEN: hands still on the recorder 2 s after WeapOut");
        }
    }

    private static IEnumerator WatchForStuckRecorder()
    {
        float startedAt = Time.time;
        while (Busy && (_holdMode || Time.time - startedAt < WeapOutTimeoutSeconds))
        {
            if (!_recorderSpawned && Time.time - startedAt > SpawnTimeoutSeconds)
            {
                Log.Warning("Tapes", "LISTEN: the recorder did not come out within 3 s - hands back, tape plays");
                FinishUsing(true);
                yield break;
            }

            yield return null;
        }

        if (!Busy)
        {
            yield break;
        }

        Log.Warning("Tapes", "LISTEN: no WeapOut event within 15 s - restoring hands");
        var controller = _player?.HandsController as Player.UsableItemController;
        FinishUsing(true);
        if (controller != null)
        {
            Runner.Host.StartCoroutine(FinishPuttingAway(controller));
        }
    }

    private static void PressPlay(bool skipToNextSong)
    {
        Item tape = HoldReady ? TapeDeck.TapeIn(_recorder) : null;
        if (tape == null)
        {
            if (HoldReady)
            {
                TapeNotificationViews.WarnNoTape();
            }

            return;
        }

        bool stopInstead = !skipToNextSong && TapeRecorder.PlayingItem == tape;
        StartAction(PressPlayState);
        AddCue(PressPlayState, PlayPressTime, () =>
        {
            if (stopInstead)
            {
                TapeRecorder.StopPlaying();
            }
            else
            {
                TapeRecorder.PlayItem(tape, skipToNextSong);
            }
        });
        AddReturnToStill(PressPlayState, PlayReturnTime, RaiseWithTape);
    }

    private static void StopIfPlaying(Item tape)
    {
        if (TapeRecorder.PlayingItem == tape)
        {
            TapeRecorder.StopPlaying();
        }
    }

    private static IEnumerator StopWaitingForMoveLater()
    {
        yield return new WaitForSeconds(MoveTimeoutSeconds);
        _waitingForMove = false;
    }

    private static void StartAction(string crossFadeTo)
    {
        IAnimator animator = HeldAnimator(out _);
        if (animator == null)
        {
            return;
        }

        Cues.Clear();
        _phase = HoldPhase.Acting;
        animator.SetBool(ActiveParameter, true);
        animator.speed = 1f;
        if (crossFadeTo != null && !CrossFade(animator, crossFadeTo, BlendInSeconds, 0f))
        {
            animator.Play(crossFadeTo, 0, 0f);
        }
    }

    private static void AddCue(string state, float normalizedTime, Action action)
    {
        Cues.Add(new AnimationCue { StateHash = Animator.StringToHash(state), NormalizedTime = normalizedTime, Action = action });
    }

    private static void AddReturnToStill(string state, float normalizedTime, string stillState)
    {
        AddCue(state, normalizedTime, () => ReturnToStill(stillState));
    }

    private static void ReturnToStill(string stillState)
    {
        IAnimator animator = HeldAnimator(out _);
        if (animator == null)
        {
            return;
        }

        _stillState = stillState;
        _phase = HoldPhase.Returning;
        float startAt = Mathf.Max(0f, StillFrame * RaiseClipLength - BlendBackSeconds);
        if (!CrossFade(animator, stillState, BlendBackSeconds, startAt))
        {
            FreezeInStillPose(animator);
        }
    }

    private static IAnimator HeldAnimator(out Player.UsableItemController controller)
    {
        controller = _player?.HandsController as Player.UsableItemController;
        if (controller == null || controller.Item != _recorder)
        {
            return null;
        }

        return controller.FirearmsAnimator?.Animator;
    }

    private static bool CrossFade(IAnimator animator, string state, float blendSeconds, float startAt)
    {
        Animator unityAnimator = (animator as AnimationSystem.UnityAnimatorWrapper)?.Animator;
        if (unityAnimator == null)
        {
            return false;
        }

        unityAnimator.CrossFadeInFixedTime(state, blendSeconds, 0, startAt);
        return true;
    }

    private static IEnumerator HoldLoop(Player player)
    {
        while (Holding && player != null)
        {
            IAnimator animator = HeldAnimator(out Player.UsableItemController controller);
            if (animator == null)
            {
                bool handsSwitched = _recorderSpawned && player.HandsController != null && !(player.HandsController is Player.UsableItemController);
                if (handsSwitched)
                {
                    Log.Info("Tapes", "HOLD: hands changed - recorder HUD closed");
                    FinishUsing(false);
                    yield break;
                }

                yield return null;
                continue;
            }

            RecorderTapeLabel.Show(controller, TapeDeck.TapeIn(_recorder));

            if (controller.CurrentOperation is Player.UsableItemController.Remove && _phase != HoldPhase.PuttingAway)
            {
                _restoreHandsOnPutAway = false;
                _phase = HoldPhase.PuttingAway;
                Cues.Clear();
                animator.speed = 1f;
            }

            UpdateHoldPhase(animator);
            yield return null;
        }
    }

    private static void UpdateHoldPhase(IAnimator animator)
    {
        AnimatorStateInfoWrapper current = animator.GetCurrentAnimatorStateInfo(0);
        bool blending = animator.IsInTransition(0);
        AnimatorStateInfoWrapper next = blending ? animator.GetNextAnimatorStateInfo(0) : current;

        switch (_phase)
        {
            case HoldPhase.Still:
                if (animator.speed != 0f)
                {
                    animator.speed = 0f;
                }

                break;

            case HoldPhase.Raising:
            case HoldPhase.Returning:
                int stillHash = Animator.StringToHash(_stillState);
                bool nextReachedStill = blending && next.shortNameHash == stillHash && next.normalizedTime >= StillFrame;
                bool currentReachedStill = current.shortNameHash == stillHash && current.normalizedTime >= StillFrame;
                if (nextReachedStill || currentReachedStill)
                {
                    FreezeInStillPose(animator);
                }

                break;

            case HoldPhase.Acting:
                animator.SetBool(ActiveParameter, true);
                RunDueCues(current, next, blending);
                break;
        }
    }

    private static void RunDueCues(AnimatorStateInfoWrapper current, AnimatorStateInfoWrapper next, bool blending)
    {
        for (int i = 0; i < Cues.Count && _phase == HoldPhase.Acting; i++)
        {
            AnimationCue cue = Cues[i];
            bool currentReached = current.shortNameHash == cue.StateHash && current.normalizedTime >= cue.NormalizedTime;
            bool nextReached = blending && next.shortNameHash == cue.StateHash && next.normalizedTime >= cue.NormalizedTime;
            if (currentReached || nextReached)
            {
                Cues.RemoveAt(i);
                i--;
                cue.Action();
            }
        }
    }

    private static void FreezeInStillPose(IAnimator animator)
    {
        animator.Play(_stillState, 0, StillFrame);
        animator.speed = 0f;
        _phase = HoldPhase.Still;
        Cues.Clear();
    }
}
