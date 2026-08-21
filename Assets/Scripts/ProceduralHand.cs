// ProceduralHand.cs
//
// Animates a procedurally-built hand: fingers curl with the grip button, thumb
// and index respond to the trigger. Geometry is created by XRRigBuilder from
// Unity primitives, so this stays within the "no imported assets" constraint.
//
// This is a CONTROLLER visual -- it follows the controller's tracked pose and
// reacts to buttons. It is not hand tracking (which reads real finger joints
// from the headset cameras and needs the com.unity.xr.hands package).
//
// Curl is applied as a rotation per segment, so a fully curled finger wraps
// rather than folding flat.

using UnityEngine;
using UnityEngine.InputSystem;

public class ProceduralHand : MonoBehaviour
{
    public enum Side { Left, Right }

    [Tooltip("Which controller this hand follows.")]
    public Side Handedness = Side.Right;

    [Tooltip("Index/middle/ring/little segments, outer array per finger.")]
    public Transform[] FingerSegments;

    [Tooltip("Number of segments per finger, used to walk FingerSegments.")]
    public int SegmentsPerFinger = 3;

    [Tooltip("Thumb segments, curled by the trigger alongside the index.")]
    public Transform[] ThumbSegments;

    [Tooltip("Degrees each segment rotates at full curl.")]
    public float MaxCurlDegrees = 55f;

    [Tooltip("How quickly the fingers follow the input.")]
    public float CurlSpeed = 14f;

    InputAction _grip;
    InputAction _trigger;

    float _fingerCurl;
    float _thumbCurl;

    Quaternion[] _fingerRest;
    Quaternion[] _thumbRest;

    void Awake()
    {
        string hand = Handedness == Side.Left ? "LeftHand" : "RightHand";

        // Analog values, so the fingers follow a partial squeeze rather than
        // snapping between open and closed.
        _grip = new InputAction("HandGrip", InputActionType.Value,
                                $"<XRController>{{{hand}}}/grip",
                                expectedControlType: "Axis");
        _trigger = new InputAction("HandTrigger", InputActionType.Value,
                                   $"<XRController>{{{hand}}}/trigger",
                                   expectedControlType: "Axis");
        _grip.Enable();
        _trigger.Enable();

        _fingerRest = CacheRest(FingerSegments);
        _thumbRest = CacheRest(ThumbSegments);
    }

    void OnDestroy()
    {
        Dispose(ref _grip);
        Dispose(ref _trigger);
    }

    static void Dispose(ref InputAction a)
    {
        if (a == null) return;
        a.Disable();
        a.Dispose();
        a = null;
    }

    static Quaternion[] CacheRest(Transform[] segments)
    {
        if (segments == null) return new Quaternion[0];

        var rest = new Quaternion[segments.Length];
        for (int i = 0; i < segments.Length; i++)
            rest[i] = segments[i] != null ? segments[i].localRotation : Quaternion.identity;
        return rest;
    }

    void Update()
    {
        float grip = _grip?.ReadValue<float>() ?? 0f;
        float trig = _trigger?.ReadValue<float>() ?? 0f;

        // Grip closes the fist. Trigger also closes index and thumb, so
        // pointing and pinching both read correctly to an observer.
        float targetFinger = Mathf.Clamp01(Mathf.Max(grip, trig * 0.85f));
        float targetThumb = Mathf.Clamp01(Mathf.Max(grip, trig));

        float t = 1f - Mathf.Exp(-CurlSpeed * Time.deltaTime);
        _fingerCurl = Mathf.Lerp(_fingerCurl, targetFinger, t);
        _thumbCurl = Mathf.Lerp(_thumbCurl, targetThumb, t);

        ApplyCurl(FingerSegments, _fingerRest, _fingerCurl, MaxCurlDegrees);
        ApplyCurl(ThumbSegments, _thumbRest, _thumbCurl, MaxCurlDegrees * 0.65f);
    }

    void ApplyCurl(Transform[] segments, Quaternion[] rest, float curl, float maxDegrees)
    {
        if (segments == null) return;

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;

            // Segments further along the finger curl slightly more, which is
            // what makes a closing hand read as a fist rather than a hinge.
            int indexInFinger = SegmentsPerFinger > 0 ? i % SegmentsPerFinger : 0;
            float weight = 0.75f + 0.25f * indexInFinger;

            segments[i].localRotation =
                rest[i] * Quaternion.Euler(curl * maxDegrees * weight, 0f, 0f);
        }
    }
}
