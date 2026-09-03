// GrabPushOut.cs
//
// Nudges a held object clear of the fingers WITHOUT changing how it is grabbed.
//
// THE PROBLEM
// -----------
// With dynamic attach, an object keeps the pose it had at the instant of the
// grab. Reach so your hand overlaps a bottle, squeeze, and it stays overlapping
// -- the geometry sits inside the hand for as long as you hold it.
//
// WHAT WAS TRIED BEFORE, AND WHY IT WAS WRONG
// -------------------------------------------
// A fixed grip point per object, with dynamic attach off. It tidied the
// intersection and broke grabbing: every object snapped to one pose, which
// fights how you actually reach for things. Freedom to grab anything anywhere,
// at any angle, matters more than how it looks in the hand.
//
// WHAT THIS DOES INSTEAD
// ----------------------
// Nothing until the grab has already happened, and nothing to the grab itself.
// The object is grabbed exactly as before -- same point, same orientation --
// and then its attach point is slid outward along the line from the object's
// centre to the grab point, which pushes the body away from the hand along the
// axis you took hold of. A grab by the neck pushes down the neck; a grab across
// the middle pushes sideways. The hand's position and the object's rotation are
// both untouched, so it still reads as the same grab.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class GrabPushOut : MonoBehaviour
{
    [Tooltip("How far to push the object out of the hand, in metres. Small: " +
             "enough to clear the fingers, not enough to look like it is " +
             "floating beside them.")]
    public float PushOut = 0.028f;

    XRGrabInteractable _grab;
    Collider _shape;

    void Start()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _shape = GetComponent<Collider>();
        if (_grab != null)
            _grab.selectEntered.AddListener(OnGrabbed);
    }

    void OnDestroy()
    {
        if (_grab != null)
            _grab.selectEntered.RemoveListener(OnGrabbed);
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        if (_shape == null || args.interactorObject == null)
            return;

        Transform attach = _grab.GetAttachTransform(args.interactorObject);
        if (attach == null || !attach.IsChildOf(transform))
            return;   // not a dynamic attach on this object; leave it alone

        Vector3 centre = _shape.bounds.center;
        Vector3 outward = attach.position - centre;
        outward.y *= 0.35f;          // mostly sideways: a bottle should not
                                     // slide up out of the fist

        if (outward.sqrMagnitude < 1e-6f)
            return;

        // Moving the attach point further from the centre moves the BODY the
        // other way, out of the hand. The hand does not move; the object does.
        attach.position += outward.normalized * PushOut;
    }
}
