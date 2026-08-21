// GroundRecovery.cs
//
// Safety net for the XR rig. Two independent guarantees:
//
//   1. MANUAL RESET  - press B (right controller) to return to the spawn point
//                      on the ground, from any situation.
//   2. AUTO UNSTICK  - the instant the rig is airborne and not already
//                      descending, it falls. No delay: any hang time at all
//                      reads as floating. The fall accelerates like gravity
//                      rather than moving at a fixed speed.
//
// Why this exists: XRI's GravityProvider decides "grounded" with a spherecast.
// Climbing onto furniture -- especially the grabbable chairs and tables, which
// are dynamic rigidbodies -- can leave that test satisfied while the rig is
// stranded above the floor, and it then never falls. Rather than fight the
// provider's internal state, this watches the outcome and corrects it.
//
// Deliberately independent of XRI so it still works if locomotion changes.

using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class GroundRecovery : MonoBehaviour
{
    [Tooltip("Seconds airborne without losing height before we force a descent. " +
             "Zero means the drop starts on the very first frame -- any delay " +
             "at all reads as hanging in mid-air.")]
    public float StuckSeconds = 0f;

    [Tooltip("Downward acceleration while falling (m/s^2). Real gravity, so " +
             "the drop eases in instead of snapping to a fixed speed.")]
    public float FallAcceleration = 9.81f;

    [Tooltip("Fastest fall speed (m/s).")]
    public float TerminalSpeed = 8f;

    [Tooltip("Height above spawn beyond which we always consider it a fault.")]
    public float MaxSaneHeight = 6f;

    CharacterController _cc;
    Vector3 _spawn;
    float _airborneFor;
    float _lastY;
    float _fallSpeed;
    InputAction _resetAction;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _spawn = transform.position;
        _lastY = transform.position.y;

        // B button on the right controller. Generic OpenXR path, so it maps on
        // any vendor's controller that exposes a secondary button.
        _resetAction = new InputAction(
            "Reset To Ground", InputActionType.Button,
            "<XRController>{RightHand}/secondaryButton");
        _resetAction.Enable();
    }

    void OnDestroy()
    {
        _resetAction?.Disable();
        _resetAction?.Dispose();
    }

    void Update()
    {
        if (_resetAction != null && _resetAction.WasPressedThisFrame())
        {
            ResetToSpawn();
            return;
        }

        float y = transform.position.y;

        // Absurd height is always a fault, regardless of grounded state.
        if (y > _spawn.y + MaxSaneHeight)
        {
            ResetToSpawn();
            return;
        }

        bool descending = y < _lastY - 0.001f;
        _lastY = y;

        if (_cc.isGrounded)
        {
            _airborneFor = 0f;
            _fallSpeed = 0f;
            return;
        }

        // Already falling under something else's control: leave it alone rather
        // than adding a second source of gravity on top.
        if (descending)
        {
            _airborneFor = 0f;
            _fallSpeed = 0f;
            return;
        }

        _airborneFor += Time.deltaTime;
        if (_airborneFor < StuckSeconds)
            return;

        // Airborne and not descending -- drop, accelerating like gravity so it
        // eases in rather than yanking at a constant speed.
        _fallSpeed = Mathf.Min(_fallSpeed + FallAcceleration * Time.deltaTime,
                               TerminalSpeed);
        _cc.Move(Vector3.down * (_fallSpeed * Time.deltaTime));
    }

    public void ResetToSpawn()
    {
        // Disable the controller while teleporting, or it resists the move.
        _cc.enabled = false;

        Vector3 target = _spawn;
        if (Physics.Raycast(_spawn + Vector3.up * 3f, Vector3.down,
                            out RaycastHit hit, 20f, Physics.DefaultRaycastLayers,
                            QueryTriggerInteraction.Ignore))
        {
            target = hit.point;
        }

        transform.position = target;
        _cc.enabled = true;

        _airborneFor = 0f;
        _fallSpeed = 0f;
        _lastY = transform.position.y;
        Debug.Log($"[GroundRecovery] Reset to ground at {target}");
    }
}
