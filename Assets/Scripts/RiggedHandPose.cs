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
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class RiggedHandPose : MonoBehaviour
{
    public enum Side { Left, Right }

    [Tooltip("Which controller drives this hand.")]
    public Side Handedness = Side.Right;

    [Tooltip("Root of the hand rig (the wrist bone).")]
    public Transform Wrist;

    // Curl is applied ON TOP of whatever rest pose the model ships with, so this
    // number is only meaningful against a particular hand. 84 is calibrated for
    // the XR Hands sample hands, which rest at ~19 deg of bend per finger joint.
    [Tooltip("Degrees the knuckle rotates at full curl. Later bones scale up. " +
             "Tuned to the hand model's rest pose -- changing the model means " +
             "retuning this.")]
    public float MaxCurlDegrees = 84f;

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

    // Calibration for the grip limit, set against a reference pose.
    //
    // Two wrong answers came first: 0.085 m left the fingers nearly straight,
    // and 0.22 m folded them into a fist. The target is neither -- it is a C
    // shape, the fingers curled about half of their travel with the thumb
    // opposing, which is how a hand actually sits round a bottle.
    //
    // 0.105 m puts a 42 mm bottle at 0.60 curl, which with 84 degrees of
    // knuckle travel is a 50 degree bend: the C.
    [Tooltip("Scale for how much a held object opens the hand. Larger = the " +
             "fingers stay closer to a full fist.")]
    public float HandSpan = 0.105f;

    [Tooltip("However thick the object, the fingers close at least this far.")]
    [Range(0f, 1f)] public float MinGripCurl = 0.55f;

    InputAction _grip;
    InputAction _trigger;
    float _fingerCurl;
    float _thumbCurl;

    NearFarInteractor _interactor;
    Object _lastHeld;
    float _maxCurl = 1f;

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

        _interactor = GetComponentInParent<NearFarInteractor>();
    }

    /// <summary>
    /// How far the fingers may close, given what is in the hand.
    ///
    /// The curl was previously the same whatever was held, so the fingers shut
    /// to a fist-sized grip around a bottle far thicker than a fist's opening
    /// and the geometry passed straight through. A hand closing on a 84 mm
    /// bottle simply cannot curl as far as an empty one.
    ///
    /// Measured from the held object's own collider -- its narrowest horizontal
    /// dimension, which is what a hand actually closes across -- so it adapts
    /// to a fat bottle and a thin glass without either being special-cased.
    /// </summary>
    void UpdateGripLimit()
    {
        if (_interactor == null)
            return;

        Object held = null;
        if (_interactor.hasSelection && _interactor.interactablesSelected.Count > 0)
            held = _interactor.interactablesSelected[0] as Object;

        if (ReferenceEquals(held, _lastHeld))
            return;
        _lastHeld = held;

        if (held == null)
        {
            _maxCurl = 1f;            // empty hand: close all the way
            return;
        }

        var comp = held as Component;
        Collider col = comp != null ? comp.GetComponentInChildren<Collider>() : null;
        if (col == null)
        {
            _maxCurl = 1f;
            return;
        }

        Vector3 size = col.bounds.size;
        float across = Mathf.Min(size.x, size.z) * 0.5f;   // grip radius
        _maxCurl = Mathf.Clamp(1f - across / Mathf.Max(0.01f, HandSpan),
                               MinGripCurl, 1f);
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
        UpdateGripLimit();

        // The input still says "close as hard as you like"; what is in the hand
        // decides how far that actually goes.
        float targetFinger = Mathf.Min(Mathf.Clamp01(Mathf.Max(grip, trig * 0.85f)), _maxCurl);
        // The thumb closes a little further than the fingers, so it comes over
        // the object rather than stopping level with them -- but only a little.
        // At +0.22 it folded right across the palm, which is the fist pose, not
        // a grip.
        float targetThumb = Mathf.Min(Mathf.Clamp01(Mathf.Max(grip, trig)),
                                      Mathf.Clamp01(_maxCurl + 0.10f));

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
