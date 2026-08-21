// HandLocomotion.cs
//
// Arm-swing locomotion for hand tracking, where there are no thumbsticks.
//
//   Swing both arms as if walking. You move in the direction you are looking,
//   at a speed set by how fast you swing. Stop swinging and you stop.
//
// WHY ARM SWING
// -------------
// It replaced a point-and-pinch teleport that felt janky. The jank was mostly
// gesture detection, not teleport: hand tracking frequently reports fingers as
// partly curled, so a "point with three fingers folded" test flickers in and
// out. Arm swing needs no finger poses at all -- only palm positions, which
// are the most reliable thing the tracker produces.
//
// It is also comfortable. Nausea in VR comes from visual motion without
// matching bodily motion; here the body genuinely moves, so the mismatch is
// small. That matters for a clinical population who may already feel unwell.
//
// BOTH HANDS REQUIRED
// -------------------
// Speed is driven by the SLOWER of the two hands. Real walking swings both
// arms; reaching for a bottle moves one. Taking the minimum means reaching,
// gesturing or grabbing cannot accidentally propel you across the room.

using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

public class HandLocomotion : MonoBehaviour
{
    [Tooltip("Hand speed below this is ignored, so idle drift and small " +
             "gestures do not move you (m/s).")]
    public float DeadZone = 0.28f;

    [Tooltip("Metres per second of travel per metre per second of hand swing.")]
    public float SpeedGain = 1.35f;

    [Tooltip("Fastest travel speed (m/s). Roughly a brisk walk.")]
    public float MaxSpeed = 2.0f;

    [Tooltip("How quickly speed follows your swing. Higher is more responsive " +
             "but jitterier.")]
    public float Smoothing = 8f;

    [Tooltip("Steer with head direction. Off means steer with body/hand facing.")]
    public bool SteerWithHead = true;

    XRHandSubsystem _subsystem;
    XROrigin _origin;
    CharacterController _controller;

    bool _hasPrev;
    Vector3 _prevLeft;
    Vector3 _prevRight;
    float _speed;

    void Start()
    {
        _origin = GetComponentInParent<XROrigin>();
        _controller = GetComponentInParent<CharacterController>();
    }

    void Update()
    {
        if (_subsystem == null || !_subsystem.running)
        {
            AcquireSubsystem();
            if (_subsystem == null)
                return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        XRHand left = _subsystem.leftHand;
        XRHand right = _subsystem.rightHand;

        // Both hands must be tracked; one-handed motion must not drive movement.
        if (!left.isTracked || !right.isTracked ||
            !TryPalm(left, out Vector3 lNow) || !TryPalm(right, out Vector3 rNow))
        {
            _hasPrev = false;
            Decay(dt);
            Apply(dt);
            return;
        }

        if (!_hasPrev)
        {
            _prevLeft = lNow;
            _prevRight = rNow;
            _hasPrev = true;
            return;
        }

        // Palm poses are in XR Origin space, so they already exclude the rig's
        // own motion -- otherwise moving would feed back into itself.
        float lSpeed = Vector3.Distance(lNow, _prevLeft) / dt;
        float rSpeed = Vector3.Distance(rNow, _prevRight) / dt;
        _prevLeft = lNow;
        _prevRight = rNow;

        // Slower hand governs. Reaching with one arm produces no movement.
        float swing = Mathf.Min(lSpeed, rSpeed);

        float target = swing > DeadZone
            ? Mathf.Min((swing - DeadZone) * SpeedGain, MaxSpeed)
            : 0f;

        _speed = Mathf.Lerp(_speed, target, 1f - Mathf.Exp(-Smoothing * dt));
        Apply(dt);
    }

    void Decay(float dt)
    {
        _speed = Mathf.Lerp(_speed, 0f, 1f - Mathf.Exp(-Smoothing * dt));
    }

    void Apply(float dt)
    {
        if (_controller == null || _speed < 0.01f)
            return;

        Transform head = _origin != null && _origin.Camera != null
            ? _origin.Camera.transform
            : null;

        Vector3 forward = SteerWithHead && head != null ? head.forward : transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return;

        // Horizontal only. Gravity stays with XRI's GravityProvider, so this
        // never fights it for vertical control.
        _controller.Move(forward.normalized * (_speed * dt));
    }

    bool TryPalm(XRHand hand, out Vector3 position)
    {
        if (hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose pose))
        {
            position = pose.position;
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    void AcquireSubsystem()
    {
        var found = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(found);
        for (int i = 0; i < found.Count; i++)
        {
            if (found[i].running)
            {
                _subsystem = found[i];
                Debug.Log("[HandLocomotion] Arm-swing ready.");
                return;
            }
        }
    }
}
