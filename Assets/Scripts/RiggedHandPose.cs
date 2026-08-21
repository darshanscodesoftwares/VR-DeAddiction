// RiggedHandPose.cs
//
// Curls the fingers of Unity's rigged XR Hands mesh from the controller's grip
// and trigger values, so grabbing looks like grabbing.
//
// WHY THE CURL AXIS IS DERIVED, NOT ASSUMED
// -----------------------------------------
// Every rig orients its bones differently, and rotating about the wrong local
// axis makes fingers splay sideways or twist instead of closing. Rather than
// guess "rotate about X", each bone works out its own axis at startup from the
// rig's own geometry, so a replacement hand needs no edits here.
//
// Fingers and thumbs get DIFFERENT axes, because they move differently:
//
//   FINGERS FLEX. The tip swings toward the palm, in the plane spanned by the
//   bone and the back-of-hand normal, so the axis is normal x boneLength.
//
//   THUMBS OPPOSE. The thumb sweeps ACROSS the palm toward the base of the
//   other fingers -- a different motion in a different plane. Its axis is
//   boneLength x (target - bonePosition), which swings the bone toward a
//   target point derived from the knuckles. Giving the thumb the finger axis
//   makes it bend in the fingers' plane and splay outward: a fifth finger.
//
// This is a CONTROLLER pose. When real hand tracking is running, XR Hands'
// own XRHandSkeletonDriver poses the same bones from measured joints, and this
// component should be disabled so the two do not fight.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RiggedHandPose : MonoBehaviour
{
    public enum Side { Left, Right }

    [Tooltip("Which controller drives this hand.")]
    public Side Handedness = Side.Right;

    [Tooltip("Root of the hand rig (the wrist bone).")]
    public Transform Wrist;

    [Tooltip("Degrees the knuckle rotates at full curl. Later bones scale up.")]
    public float MaxCurlDegrees = 62f;

    [Tooltip("How fast the fingers follow the input.")]
    public float CurlSpeed = 14f;

    class Bone
    {
        public Transform T;
        public Quaternion Rest;
        public Vector3 Axis;      // local-space curl axis
        public float Weight;      // later bones curl more
        public bool IsThumb;
    }

    readonly List<Bone> _bones = new List<Bone>();

    InputAction _grip;
    InputAction _trigger;
    float _fingerCurl;
    float _thumbCurl;

    static readonly string[] k_Fingers = { "Index", "Middle", "Ring", "Little" };
    static readonly string[] k_Segments = { "Proximal", "Intermediate", "Distal" };

    void Awake()
    {
        string p = Handedness == Side.Left ? "L_" : "R_";
        string hand = Handedness == Side.Left ? "LeftHand" : "RightHand";

        if (Wrist == null)
            Wrist = FindDeep(transform, p + "Wrist");

        if (Wrist == null)
        {
            Debug.LogWarning("[RiggedHandPose] No wrist bone; fingers will not curl.");
            enabled = false;
            return;
        }

        Vector3 backNormal = BackOfHandNormal(p);

        // A finger FLEXES: its tip swings toward the palm, in the plane spanned
        // by the bone and the back-of-hand normal. The axis for that is the
        // normal crossed with the bone's own length.
        foreach (string f in k_Fingers)
        {
            for (int i = 0; i < k_Segments.Length; i++)
            {
                Transform t = FindDeep(Wrist, p + f + k_Segments[i]);
                if (t != null)
                    AddBone(t, Vector3.Cross(backNormal, LengthWorld(t)), 0.85f + 0.20f * i, false);
            }
        }

        // A thumb does NOT flex like a finger -- it OPPOSES, sweeping ACROSS the
        // palm toward the base of the other fingers. Using the finger axis on it
        // bends it in the fingers' plane, which reads as a fifth finger splaying
        // sideways. Instead, aim each thumb bone at the point a thumb tip
        // actually reaches when a hand closes, and derive the axis from that:
        // rotating about (length x toTarget) swings the bone toward the target,
        // whatever direction the rig happens to hold the thumb in.
        Vector3 thumbTarget = ThumbTargetWorld(p, backNormal);
        string[] thumbSegs = { "Proximal", "Distal" };
        float[] thumbWeights = { 0.95f, 0.75f };

        for (int i = 0; i < thumbSegs.Length; i++)
        {
            Transform t = FindDeep(Wrist, p + "Thumb" + thumbSegs[i]);
            if (t == null) continue;

            Vector3 toTarget = thumbTarget - t.position;
            AddBone(t, Vector3.Cross(LengthWorld(t), toTarget), thumbWeights[i], true);
        }

        _grip = new InputAction("Grip", InputActionType.Value,
                                $"<XRController>{{{hand}}}/grip", expectedControlType: "Axis");
        _trigger = new InputAction("Trigger", InputActionType.Value,
                                   $"<XRController>{{{hand}}}/trigger", expectedControlType: "Axis");
        _grip.Enable();
        _trigger.Enable();
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

    /// <summary>
    /// Normal pointing out of the BACK of the hand. This, not the across-hand
    /// vector, is what the curl axis must be derived from.
    ///
    /// Why the previous version was wrong: it used index-knuckle to
    /// little-knuckle directly. That vector points in OPPOSITE directions on
    /// the two hands (on a right hand the little finger is on the +X side, on
    /// a left hand it is on -X), so the left hand curled backwards while the
    /// right curled correctly. The back-of-hand normal is the same on both,
    /// so an axis derived from it is handedness-correct by construction.
    ///
    /// The thumb does not use this as its axis -- see ThumbTargetWorld -- but it
    /// does use this normal to work out how deep into the palm to aim.
    /// </summary>
    Vector3 BackOfHandNormal(string prefix)
    {
        Transform index = FindDeep(Wrist, prefix + "IndexProximal");
        Transform little = FindDeep(Wrist, prefix + "LittleProximal");
        Transform middle = FindDeep(Wrist, prefix + "MiddleProximal");

        if (index == null || little == null || middle == null)
            return Wrist.up;

        Vector3 across = little.position - index.position;

        // Mirror the left hand so "across" means the same thing on both.
        if (Handedness == Side.Left)
            across = -across;

        Vector3 along = middle.position - Wrist.position;

        Vector3 n = Vector3.Cross(along, across);
        return n.sqrMagnitude > 1e-8f ? n.normalized : Wrist.up;
    }

    /// <summary>
    /// Records one bone, converting a world-space rotation axis into the bone's
    /// own space. The caller decides the axis, because fingers and thumbs move
    /// in genuinely different ways.
    /// </summary>
    void AddBone(Transform t, Vector3 axisWorld, float weight, bool isThumb)
    {
        Vector3 lengthLocal = LengthLocal(t);

        Vector3 axis = t.InverseTransformDirection(axisWorld);
        // Strip any along-bone component so this is a pure curl, never a twist.
        axis -= lengthLocal * Vector3.Dot(axis, lengthLocal);

        if (axis.sqrMagnitude < 1e-6f)
            axis = Vector3.right;

        _bones.Add(new Bone
        {
            T = t,
            Rest = t.localRotation,
            Axis = axis.normalized,
            Weight = weight,
            IsThumb = isThumb,
        });
    }

    /// <summary>
    /// Where a thumb tip goes when the hand closes: a third of the way from the
    /// middle knuckle toward the little one, then sunk into the palm. Derived
    /// from the rig's own knuckles, so it scales with any hand it is given.
    /// </summary>
    Vector3 ThumbTargetWorld(string prefix, Vector3 backNormal)
    {
        Transform index = FindDeep(Wrist, prefix + "IndexProximal");
        Transform middle = FindDeep(Wrist, prefix + "MiddleProximal");
        Transform little = FindDeep(Wrist, prefix + "LittleProximal");

        if (index == null || middle == null || little == null)
            return Wrist.position - backNormal * 0.05f;

        Vector3 acrossPalm = Vector3.Lerp(middle.position, little.position, 0.35f);
        float palmWidth = Vector3.Distance(index.position, little.position);

        return acrossPalm - backNormal * palmWidth * 0.45f;
    }

    /// <summary>Direction from a bone to its child, in world space.</summary>
    static Vector3 LengthWorld(Transform t)
    {
        if (t.childCount > 0)
        {
            Vector3 d = t.GetChild(0).position - t.position;
            if (d.sqrMagnitude > 1e-8f) return d.normalized;
        }
        return t.forward;
    }

    /// <summary>The same direction, in the bone's own space.</summary>
    static Vector3 LengthLocal(Transform t)
    {
        if (t.childCount > 0)
        {
            Vector3 d = t.GetChild(0).localPosition;
            if (d.sqrMagnitude > 1e-8f) return d.normalized;
        }
        return Vector3.forward;
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name)
                return t;
        return null;
    }

    void Update()
    {
        float grip = _grip?.ReadValue<float>() ?? 0f;
        float trig = _trigger?.ReadValue<float>() ?? 0f;

        // Grip closes the fist; the trigger also pulls index and thumb in, so
        // pinching and pointing both read correctly to an observer.
        float targetFinger = Mathf.Clamp01(Mathf.Max(grip, trig * 0.85f));
        float targetThumb = Mathf.Clamp01(Mathf.Max(grip, trig));

        float t = 1f - Mathf.Exp(-CurlSpeed * Time.deltaTime);
        _fingerCurl = Mathf.Lerp(_fingerCurl, targetFinger, t);
        _thumbCurl = Mathf.Lerp(_thumbCurl, targetThumb, t);

        for (int i = 0; i < _bones.Count; i++)
        {
            Bone b = _bones[i];
            if (b.T == null) continue;

            float curl = b.IsThumb ? _thumbCurl : _fingerCurl;
            float deg = curl * MaxCurlDegrees * b.Weight;

            b.T.localRotation = b.Rest * Quaternion.AngleAxis(deg, b.Axis);
        }
    }

    /// <summary>Sign flip, in case a rig curls the wrong way.</summary>
    public void InvertCurl()
    {
        for (int i = 0; i < _bones.Count; i++)
            _bones[i].Axis = -_bones[i].Axis;
    }
}
