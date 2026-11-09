using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace PlayableMusicTapes.Client;

internal static class RecorderTapeLabel
{
    private const string TapeRendererName = "item_recorder_tape_LOD0";
    private const string LabelProperty = "_MainTex";
    private const float KeepOldLabelSeconds = 8f;

    private static readonly int TapeInHash = Animator.StringToHash("TAPE IN");

    private static readonly Dictionary<string, Texture> Labels = new Dictionary<string, Texture>();

    private static GameObject _rig;
    private static Renderer _renderer;
    private static Texture _originalLabel;
    private static string _shownTapeId;
    private static Item _comingOut;
    private static float _comingOutUntil;

    public static void KeepUntilSwapped(Item oldTape)
    {
        _comingOut = oldTape;
        _comingOutUntil = Time.time + KeepOldLabelSeconds;
    }

    public static void Show(Player.UsableItemController controller, Item tape)
    {
        if (!FindTapeRenderer(controller))
        {
            return;
        }

        if (_comingOut != null)
        {
            IAnimator animator = controller.FirearmsAnimator?.Animator;
            bool newTapeComingIn = animator != null && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == TapeInHash;
            if (newTapeComingIn || Time.time > _comingOutUntil)
            {
                _comingOut = null;
            }
            else
            {
                tape = _comingOut;
            }
        }

        string tapeId = TapeCatalog.TapeIdOf(tape);
        if (tapeId == _shownTapeId)
        {
            return;
        }

        _shownTapeId = tapeId;
        Texture label = tapeId != null ? FindLabel(tapeId) : null;
        _renderer.material.SetTexture(LabelProperty, label != null ? label : _originalLabel);
    }

    private static bool FindTapeRenderer(Player.UsableItemController controller)
    {
        GameObject rig = controller?._controllerObject;
        if (rig == null)
        {
            return false;
        }

        if (rig != _rig || _renderer == null)
        {
            _rig = rig;
            _renderer = rig.GetComponentsInChildren<Renderer>(true).FirstOrDefault(renderer => renderer.name == TapeRendererName);
            _originalLabel = _renderer != null ? _renderer.sharedMaterial.GetTexture(LabelProperty) : null;
            _shownTapeId = null;
            if (_renderer != null)
            {
                RecorderTapeFlip.Attach(_renderer);
            }
        }

        return _renderer != null;
    }

    private static Texture FindLabel(string tapeId)
    {
        if (Labels.TryGetValue(tapeId, out Texture cached) && cached != null)
        {
            return cached;
        }

        string textureName = "playabletapes_tape_" + tapeId + "_D";
        Texture label = Resources.FindObjectsOfTypeAll<Texture2D>().FirstOrDefault(texture => texture.name == textureName);
        if (label == null)
        {
            Log.Warning("Tapes", "label texture " + textureName + " is not loaded - the recorder shows the plain tape");
            return null;
        }

        Labels[tapeId] = label;
        return label;
    }
}
