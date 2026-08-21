// TableContainment.cs
//
// Keeps the feature table's props on the table.
//
// Three layers, weakest first, because a single hard barrier feels like a
// glass box and breaks the illusion the scenario is trying to build:
//
//   1. INVISIBLE LIP   a low physics rim around the table edge. Stops things
//                      rolling off on their own without being visible.
//   2. THROW DAMPING   release velocity is capped, so an object cannot be
//                      hurled across the room. You can still put it down
//                      firmly; you just cannot launch it.
//   3. RETURN          anything that leaves the volume anyway is returned to
//                      where it started, gently, after a short delay.
//
// Layer 3 is the guarantee. Layers 1 and 2 exist so it almost never fires --
// an object visibly teleporting back is jarring, so it should be the fallback,
// not the mechanism.

using System.Collections.Generic;
using UnityEngine;

public class TableContainment : MonoBehaviour
{
    [Tooltip("Half-extents of the region props may occupy, around this object.")]
    public Vector3 Bounds = new Vector3(0.75f, 1.20f, 0.60f);

    [Tooltip("Fastest an object may be moving when released (m/s).")]
    public float MaxReleaseSpeed = 1.6f;

    [Tooltip("Fastest an object may be spinning when released (rad/s).")]
    public float MaxReleaseSpin = 6f;

    [Tooltip("Seconds an object may be outside the volume before it returns.")]
    public float GraceSeconds = 1.2f;

    [Tooltip("Seconds the return glide takes. Zero would be a hard snap.")]
    public float ReturnSeconds = 0.35f;

    class Tracked
    {
        public Rigidbody Body;
        public Vector3 HomePosition;
        public Quaternion HomeRotation;
        public float OutsideFor;
        public float ReturnT;
        public Vector3 ReturnFrom;
        public Quaternion ReturnFromRot;
        public bool Returning;
    }

    readonly List<Tracked> _items = new List<Tracked>();

    /// <summary>
    /// Registers a prop. Its current transform becomes the place it returns to,
    /// so the builder should call this once everything is positioned.
    /// </summary>
    public void Register(Rigidbody body)
    {
        if (body == null)
            return;

        _items.Add(new Tracked
        {
            Body = body,
            HomePosition = body.transform.position,
            HomeRotation = body.transform.rotation,
        });
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 centre = transform.position;

        for (int i = 0; i < _items.Count; i++)
        {
            Tracked item = _items[i];
            if (item.Body == null)
                continue;

            if (item.Returning)
            {
                StepReturn(item, dt);
                continue;
            }

            // Cap speed rather than blocking motion: a hard clamp on position
            // fights the physics solver and jitters, which we already learned
            // the hard way with grab movement.
            if (!item.Body.isKinematic &&
                item.Body.linearVelocity.magnitude > MaxReleaseSpeed)
            {
                item.Body.linearVelocity =
                    item.Body.linearVelocity.normalized * MaxReleaseSpeed;
            }

            // Spin needs the same treatment. Letting go of a bottle always
            // imparts some rotation, and an uncapped tumble walks it off the
            // table under its own momentum before it can settle.
            if (!item.Body.isKinematic &&
                item.Body.angularVelocity.magnitude > MaxReleaseSpin)
            {
                item.Body.angularVelocity =
                    item.Body.angularVelocity.normalized * MaxReleaseSpin;
            }

            Vector3 delta = item.Body.transform.position - centre;
            bool inside = Mathf.Abs(delta.x) <= Bounds.x &&
                          Mathf.Abs(delta.y) <= Bounds.y &&
                          Mathf.Abs(delta.z) <= Bounds.z;

            if (inside)
            {
                item.OutsideFor = 0f;
                continue;
            }

            // Held objects are never dragged back -- the patient is allowed to
            // lift a bottle up and look at it.
            if (item.Body.isKinematic)
            {
                item.OutsideFor = 0f;
                continue;
            }

            item.OutsideFor += dt;
            if (item.OutsideFor >= GraceSeconds)
                BeginReturn(item);
        }
    }

    void BeginReturn(Tracked item)
    {
        item.Returning = true;
        item.ReturnT = 0f;
        item.ReturnFrom = item.Body.transform.position;
        item.ReturnFromRot = item.Body.transform.rotation;

        item.Body.linearVelocity = Vector3.zero;
        item.Body.angularVelocity = Vector3.zero;
        item.Body.isKinematic = true;   // glide without physics interference
    }

    void StepReturn(Tracked item, float dt)
    {
        item.ReturnT += dt / Mathf.Max(0.05f, ReturnSeconds);
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(item.ReturnT));

        item.Body.transform.SetPositionAndRotation(
            Vector3.Lerp(item.ReturnFrom, item.HomePosition, t),
            Quaternion.Slerp(item.ReturnFromRot, item.HomeRotation, t));

        if (item.ReturnT < 1f)
            return;

        item.Returning = false;
        item.OutsideFor = 0f;
        item.Body.isKinematic = false;
        item.Body.linearVelocity = Vector3.zero;
        item.Body.angularVelocity = Vector3.zero;
    }

    /// <summary>Puts every prop back. Used when a session restarts.</summary>
    public void ResetAll()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            Tracked item = _items[i];
            if (item.Body == null)
                continue;

            item.Returning = false;
            item.OutsideFor = 0f;
            item.Body.linearVelocity = Vector3.zero;
            item.Body.angularVelocity = Vector3.zero;
            item.Body.transform.SetPositionAndRotation(item.HomePosition, item.HomeRotation);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireCube(transform.position, Bounds * 2f);
    }
}
