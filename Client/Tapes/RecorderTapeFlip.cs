using UnityEngine;

namespace PlayableMusicTapes.Client;

internal class RecorderTapeFlip : MonoBehaviour
{
    private Quaternion _half;
    private Vector3 _shift;
    private bool _applied;
    private Quaternion _appliedRotation;
    private Vector3 _appliedPosition;

    public static void Attach(Renderer renderer)
    {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null || renderer.GetComponent<RecorderTapeFlip>() != null)
        {
            return;
        }

        Bounds bounds = filter.sharedMesh.bounds;
        Vector3 size = bounds.size;
        Vector3 thin = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
        RecorderTapeFlip flip = renderer.gameObject.AddComponent<RecorderTapeFlip>();
        flip._half = Quaternion.AngleAxis(180f, thin);
        flip._shift = bounds.center - flip._half * bounds.center;
    }

    private void OnEnable()
    {
        Application.onBeforeRender += Apply;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= Apply;
    }

    private void Apply()
    {
        Transform tape = transform;
        if (_applied && tape.localRotation == _appliedRotation && tape.localPosition == _appliedPosition)
        {
            return;
        }

        tape.localPosition += tape.localRotation * Vector3.Scale(tape.localScale, _shift);
        tape.localRotation *= _half;
        _appliedRotation = tape.localRotation;
        _appliedPosition = tape.localPosition;
        _applied = true;
    }
}
