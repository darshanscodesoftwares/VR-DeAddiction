// SeatedTableScenario.cs
//
// The first interactive scenario: approach a marked table, sit, and interact
// with what is on it.
//
//   BAR        free movement. The marker pulses on the floor by the table.
//   AT MARKER  you are standing on the marker. A prompt says how to sit.
//   SEATED     the view is at the table, the room falls away, and the props
//              become grabbable.
//
// SEAMLESS, NOT A SCENE LOAD
// --------------------------
// Everything happens in one Unity scene. No LoadScene, so the XR rig, hand
// tracking and tracking origin are never destroyed and there is no loading
// pause. The bar stays visible around the patient, which matters clinically:
// the surrounding bar IS the cue, and a table floating in isolation is a
// weaker trigger than one sitting in a room full of other drinkers.
//
// SITTING IS ALWAYS REVERSIBLE
// ----------------------------
// The patient can sit and stand at any moment. This is a safety property, not
// a convenience: someone in cue exposure must be able to break out without
// hunting for how. Nothing here ever locks the patient in, and the lighting
// transition reverses cleanly if interrupted half way.
//
// INTERACTION GATE
// ----------------
// Table props are non-grabbable unless seated, so a patient cannot walk past
// and swipe a bottle off the table in passing.

using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Hands;

public class SeatedTableScenario : MonoBehaviour
{
    public enum State { Bar, AtMarker, Seated }

    [Header("Wiring (set by PubEnvironmentBuilder)")]
    public Transform Marker;
    public Transform SeatAnchor;
    public LightingDirector Lighting;
    public TableContainment Containment;
    public List<XRGrabInteractable> TableProps = new List<XRGrabInteractable>();

    [Header("Approach")]
    [Tooltip("How close the player must be to the marker to be able to sit (m).")]
    public float MarkerRadius = 0.85f;

    [Header("Seating")]
    [Tooltip("Seconds the view takes to move to and from the seat.")]
    public float SeatGlideSeconds = 0.9f;

    [Tooltip("Eye height above the floor once seated (m). A seated adult's " +
             "eyes sit around 1.15 m; standing is 1.55-1.75 m.")]
    public float SeatedEyeHeight = 1.18f;

    [Header("Hand-tracking control")]
    [Tooltip("Seconds standing on the marker before sitting, with hands only.")]
    public float HandDwellSeconds = 1.5f;

    [Tooltip("Seconds after standing during which sitting is refused. Without " +
             "it, standing up leaves you inside the marker and the dwell timer " +
             "drags you straight back into the chair.")]
    public float StandCooldown = 3f;

    [Tooltip("Seconds both palms must be held above head height to stand.")]
    public float HandRaiseSeconds = 0.8f;

    [Tooltip("How far above the head both palms must be raised (m).")]
    public float HandRaiseClearance = 0.12f;

    [Header("Marker look")]
    public float PulseSpeed = 2.2f;
    public float PulseAmount = 0.16f;

    State _state = State.Bar;
    XROrigin _origin;
    CharacterController _controller;

    // Every way the patient can move must be suspended while seated, not just
    // the hand one. Leaving the thumbstick providers live meant a patient with
    // controllers could walk out of the chair while still in the seated state.
    HandLocomotion _handLocomotion;
    ContinuousMoveProvider _moveProvider;
    SnapTurnProvider _turnProvider;
    GravityProvider _gravityProvider;
    GroundRecovery _groundRecovery;

    GameObject _fadeSphere;
    Material _fadeMaterial;

    Transform _standButton;
    Material _standButtonMaterial;

    Vector3 _standingPosition;
    Quaternion _standingRotation;

    float _glide;              // 0 = standing, 1 = seated
    bool _glidingToSeat;
    Vector3 _seatTarget;
    float _seatDrop;
    float _offsetBaseY;
    Quaternion _seatRotation = Quaternion.identity;

    InputAction _sitToggle;
    Vector3 _markerBaseScale;

    // Hand-tracking route. A controller binding alone is not enough: with hands
    // only there is no XRController device at all, so <XRController>/primaryButton
    // never fires and a hand-tracking patient could neither sit nor stand.
    XRHandSubsystem _hands;
    float _markerDwell;
    float _raiseHold;
    float _standLock;
    bool _mustLeaveMarker;

    void Start()
    {
        _origin = GetComponentInParent<XROrigin>();
        _controller = GetComponentInParent<CharacterController>();
        _handLocomotion = GetComponentInParent<HandLocomotion>();
        _moveProvider = GetComponentInParent<ContinuousMoveProvider>();
        _turnProvider = GetComponentInParent<SnapTurnProvider>();
        _gravityProvider = GetComponentInParent<GravityProvider>();
        _groundRecovery = GetComponentInParent<GroundRecovery>();

        BuildFade();
        BuildStandButton();

        if (_origin != null && _origin.CameraFloorOffsetObject != null)
            _offsetBaseY = _origin.CameraFloorOffsetObject.transform.localPosition.y;

        if (Marker != null)
            _markerBaseScale = Marker.localScale;

        // Explicit control, deliberately. Detecting "sat down" from headset
        // height alone would exclude anyone who stays physically seated for the
        // whole session -- a wheelchair user could never trigger it.
        // A/X on either controller, so handedness does not matter.
        _sitToggle = new InputAction("SitToggle", InputActionType.Button);
        _sitToggle.AddBinding("<XRController>{RightHand}/primaryButton");
        _sitToggle.AddBinding("<XRController>{LeftHand}/primaryButton");
        _sitToggle.Enable();

        SetPropsGrabbable(false);
    }

    void OnDestroy()
    {
        _sitToggle?.Disable();
        _sitToggle?.Dispose();
    }

    void Update()
    {
        UpdateMarker();
        UpdateState();
        UpdateGlide();
    }

    void LateUpdate()
    {
        // After the camera has its final pose for the frame, or the button
        // trails a frame behind the head and visibly swims.
        UpdateStandButton();
    }

    // ------------------------------------------------------- hand-only input

    /// <summary>
    /// Sit by dwelling on the marker; stand by raising both hands above head
    /// height. Both use palm POSITION only -- no finger poses, which are the
    /// least reliable thing hand tracking reports and what made the old
    /// point-and-pinch teleport feel broken.
    ///
    /// Raising both hands is chosen because it cannot happen by accident while
    /// reaching for something on a table, and it needs no learning.
    /// </summary>
    bool HandSitRequested(float dt, bool atMarker)
    {
        if (!AcquireHands())
            return false;

        if (!atMarker)
        {
            _markerDwell = 0f;
            return false;
        }

        _markerDwell += dt;
        if (_markerDwell < HandDwellSeconds)
            return false;

        _markerDwell = 0f;
        return true;
    }

    bool HandStandRequested(float dt)
    {
        if (!AcquireHands() || _origin == null || _origin.Camera == null)
        {
            _raiseHold = 0f;
            return false;
        }

        if (!PalmAbove(_hands.leftHand) || !PalmAbove(_hands.rightHand))
        {
            _raiseHold = 0f;
            return false;
        }

        _raiseHold += dt;
        if (_raiseHold < HandRaiseSeconds)
            return false;

        _raiseHold = 0f;
        return true;
    }

    bool PalmAbove(XRHand hand)
    {
        if (!hand.isTracked || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm))
            return false;

        // Palm poses are in origin space; the camera's local height is too.
        float headY = _origin.Camera.transform.localPosition.y;
        return palm.position.y > headY + HandRaiseClearance;
    }

    bool AcquireHands()
    {
        if (_hands != null && _hands.running)
            return true;

        var found = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(found);
        for (int i = 0; i < found.Count; i++)
        {
            if (found[i].running)
            {
                _hands = found[i];
                return true;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------ state

    void UpdateState()
    {
        float dt = Time.deltaTime;
        bool pressed = _sitToggle != null && _sitToggle.WasPressedThisFrame();

        switch (_state)
        {
            case State.Bar:
            case State.AtMarker:
                bool at = AtMarker();
                _state = at ? State.AtMarker : State.Bar;
                _raiseHold = 0f;

                // Two locks after standing up, because one is not enough.
                //
                // The marker sits 1.35 m from the seat with an 0.85 m radius,
                // so you stand up practically inside it and walking away keeps
                // you in it for a second or more -- long enough for the dwell
                // timer to seat you again before you have got clear.
                //
                // The TIMER covers the moment of standing. The EXIT
                // REQUIREMENT covers the rest: you must leave the marker's
                // radius once before sitting can arm again, so lingering near
                // the chair never re-seats you however long you stay.
                if (_standLock > 0f)
                {
                    _standLock -= dt;
                    _markerDwell = 0f;
                    break;
                }

                if (_mustLeaveMarker)
                {
                    if (at)
                    {
                        _markerDwell = 0f;
                        break;
                    }
                    _mustLeaveMarker = false;
                }

                if ((pressed && at) || HandSitRequested(dt, at))
                    Sit();
                break;

            case State.Seated:
                // Standing is available from anywhere, always, by any route.
                _markerDwell = 0f;
                if (pressed || HandStandRequested(dt))
                    Stand();
                break;
        }
    }

    bool AtMarker()
    {
        if (Marker == null || _origin == null || _origin.Camera == null)
            return false;

        Vector3 head = _origin.Camera.transform.position;
        Vector3 mark = Marker.position;
        head.y = 0f;
        mark.y = 0f;

        return Vector3.Distance(head, mark) <= MarkerRadius;
    }

    void Sit()
    {
        if (SeatAnchor == null || _origin == null)
            return;

        _standingPosition = _origin.transform.position;
        _standingRotation = _origin.transform.rotation;

        // LOWER THE VIEW. In Floor tracking mode the camera reports the
        // patient's real head height above the floor, so moving the rig to a
        // seat anchor at floor level left them STANDING at the table -- which
        // is the "floating off nothing" -- rather than sitting at it.
        //
        // The rig is dropped by however far the head has to fall to reach a
        // seated eye height. This works whether the patient is physically
        // standing or already sitting in a real chair.
        float headAboveFloor = _origin.Camera != null
            ? _origin.Camera.transform.localPosition.y
            : 1.6f;

        // FACE THE TABLE. Where the patient looks is rig yaw PLUS physical head
        // yaw, so the head's yaw is subtracted to make the sum land on the table.
        float headYawForSeat = _origin.Camera != null
            ? _origin.Camera.transform.localEulerAngles.y
            : 0f;
        // Lower the CAMERA OFFSET, not the rig.
        //
        // Dropping the rig 0.4 m put the collision capsule (which spans rigY to
        // rigY + 1.75) a full 0.4 m below the floor collider. Physics resolved
        // that overlap by pushing the capsule back up, which shoved the patient
        // straight out of the seat again -- the "I'm not seated at the chair".
        //
        // XROrigin has a CameraFloorOffsetObject for exactly this. Moving it
        // changes eye height while the capsule stays standing on the floor.
        _seatDrop = Mathf.Max(0f, headAboveFloor - SeatedEyeHeight);

        // Move the rig so the CAMERA lands on the seat, not the rig origin.
        //
        // In Floor tracking the camera sits wherever the patient physically is
        // within their play space, which can be a metre or more from the rig
        // origin. Setting the rig to the seat therefore left the patient
        // standing wherever they happened to be -- next to the marker, out of
        // reach of the table. The old teleport corrected for this; sitting
        // never did.
        Vector3 camWorld = _origin.Camera != null
            ? _origin.Camera.transform.position
            : _origin.transform.position;
        Vector3 camFlat = new Vector3(camWorld.x, _origin.transform.position.y, camWorld.z);
        Vector3 rigToCam = _origin.transform.position - camFlat;

        // The rig also rotates as we sit, so that offset has to rotate with it.
        float yawDelta = (SeatAnchor.eulerAngles.y - headYawForSeat)
                         - _origin.transform.eulerAngles.y;
        _seatTarget = SeatAnchor.position + Quaternion.Euler(0f, yawDelta, 0f) * rigToCam;

        _seatRotation = Quaternion.Euler(0f, SeatAnchor.eulerAngles.y - headYawForSeat, 0f);

        _state = State.Seated;
        _glidingToSeat = true;

        SetLocomotion(false);

        // GroundRecovery off while seated. It captured the STANDING spawn in
        // Awake with no setter, so its B-button reset would drop a seated
        // patient back on the street outside the compound gate, and its
        // auto-fall would fight the seated pose.
        if (_groundRecovery != null)
            _groundRecovery.enabled = false;

        if (Lighting != null)
            Lighting.SetSeated(true);

        SetPropsGrabbable(true);
    }

    void Stand()
    {
        _state = State.Bar;
        _glidingToSeat = false;
        _standLock = StandCooldown;
        _mustLeaveMarker = true;
        _markerDwell = 0f;

        if (Lighting != null)
            Lighting.SetSeated(false);

        // Props lock immediately on standing, so nothing can be carried away.
        SetPropsGrabbable(false);

        SetLocomotion(true);

        if (_groundRecovery != null)
            _groundRecovery.enabled = true;
    }

    void SetLocomotion(bool on)
    {
        if (_handLocomotion != null) _handLocomotion.enabled = on;
        if (_moveProvider != null) _moveProvider.enabled = on;
        if (_turnProvider != null) _turnProvider.enabled = on;
        // Gravity too. Left running it pulls on the rig for the whole seated
        // session, fighting whatever holds the seated pose.
        if (_gravityProvider != null) _gravityProvider.enabled = on;
    }

    // ----------------------------------------------------------- stand button

    /// <summary>
    /// A grabbable block that appears ONLY while seated, and stands the patient
    /// up when grabbed.
    ///
    /// WHY IT EXISTS
    /// -------------
    /// Standing already worked two ways: A/X on a controller, or raising both
    /// palms above the head. With hands alone the gesture was the only route,
    /// and holding both hands over your head for most of a second to leave a
    /// chair is neither obvious nor dignified. Something you simply reach out
    /// and take hold of needs no explaining.
    ///
    /// It is a real XRGrabInteractable, so a controller grip and a
    /// hand-tracking grasp both drive it through the same path as picking up a
    /// glass -- no separate input route that only this control exercises.
    ///
    /// GREEN, NOT RED, AND ONLY WHEN SEATED. It sits where the quit button used
    /// to, so it must not be mistaken for it: different colour, and it is the
    /// only box in the scene, because two similar boxes doing different things
    /// is exactly how someone leaves the app when they meant to stand up.
    /// </summary>
    void BuildStandButton()
    {
        if (_origin == null || _origin.Camera == null)
            return;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "StandUpButton";
        go.transform.SetParent(_origin.transform, false);
        go.transform.localScale = Vector3.one * 0.075f;
        _standButton = go.transform;

        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        _standButtonMaterial = new Material(Shader.Find("Standard"));
        _standButtonMaterial.color = new Color(0.16f, 0.72f, 0.30f);
        _standButtonMaterial.EnableKeyword("_EMISSION");
        _standButtonMaterial.SetColor("_EmissionColor", new Color(0.18f, 0.85f, 0.34f));
        // A material built at runtime is treated as having black emission
        // unless this is set, and the glow never appears.
        _standButtonMaterial.globalIlluminationFlags =
            MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mr.sharedMaterial = _standButtonMaterial;

        // Bigger than it looks: this is the control a patient reaches for when
        // they want out of the chair, so it must never be fiddly to hit.
        go.GetComponent<BoxCollider>().size = Vector3.one * 1.4f;

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var grab = go.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.selectEntered.AddListener(_ =>
        {
            if (_state == State.Seated)
                Stand();
        });

        go.SetActive(false);
        Debug.Log("[Scenario] Stand-up button built (hidden until seated).");
    }

    /// <summary>
    /// Shows the button only while seated, and parks it at the patient's upper
    /// right following head position and YAW ONLY. Following full head rotation
    /// would swing it away exactly as they turned to look at it, and above eye
    /// level keeps it clear of the props -- reaching for a glass must never
    /// stand you up by accident.
    /// </summary>
    void UpdateStandButton()
    {
        if (_standButton == null || _origin == null || _origin.Camera == null)
            return;

        bool show = _state == State.Seated;
        bool justAppeared = show && !_standButton.gameObject.activeSelf;

        if (_standButton.gameObject.activeSelf != show)
            _standButton.gameObject.SetActive(show);

        if (!show)
            return;

        Transform cam = _origin.Camera.transform;
        Quaternion yaw = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
        Vector3 target = cam.position + yaw * new Vector3(0.30f, 0.06f, 0.42f);

        // Snap on the first frame it is shown. Easing in from wherever it was
        // parked would send it flying across the room into place.
        if (justAppeared)
        {
            _standButton.SetPositionAndRotation(target, yaw);
            return;
        }

        float t = 1f - Mathf.Exp(-8f * Time.deltaTime);
        _standButton.position = Vector3.Lerp(_standButton.position, target, t);
        _standButton.rotation = Quaternion.Slerp(_standButton.rotation, yaw, t);
    }

    // ------------------------------------------------------------------- fade

    /// <summary>
    /// An inverted sphere on the camera, used to black out the move to and from
    /// the seat. Sliding the view across the room unfaded is a direct
    /// visual-vestibular mismatch -- the exact thing that causes nausea, and
    /// worse for a patient who may already feel unwell.
    /// </summary>
    void BuildFade()
    {
        if (_origin == null || _origin.Camera == null)
            return;

        _fadeSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _fadeSphere.name = "SeatFade";
        Destroy(_fadeSphere.GetComponent<Collider>());
        _fadeSphere.transform.SetParent(_origin.Camera.transform, false);
        _fadeSphere.transform.localPosition = Vector3.zero;
        // Inside the near clip plane, and inverted so we see its inner surface.
        _fadeSphere.transform.localScale = Vector3.one * -0.28f;

        var mr = _fadeSphere.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        Shader shader = Shader.Find("Unlit/Color");
        _fadeMaterial = new Material(shader != null ? shader : Shader.Find("Standard"));
        _fadeMaterial.color = Color.black;
        mr.sharedMaterial = _fadeMaterial;

        _fadeSphere.SetActive(false);
    }

    void UpdateFade(float glide)
    {
        if (_fadeSphere == null || _fadeMaterial == null)
            return;

        // Peak black at the midpoint of the move, clear at both ends.
        float alpha = Mathf.Clamp01(1f - Mathf.Abs(glide - 0.5f) * 2f);

        bool show = alpha > 0.01f;
        if (_fadeSphere.activeSelf != show)
            _fadeSphere.SetActive(show);

        if (show)
            _fadeMaterial.color = new Color(0f, 0f, 0f, 1f) * alpha;
    }

    // ------------------------------------------------------------------ glide

    void UpdateGlide()
    {
        float target = _glidingToSeat ? 1f : 0f;
        if (Mathf.Approximately(_glide, target))
            return;

        // Constant rate, so interrupting mid-move reverses at the same speed.
        _glide = Mathf.MoveTowards(_glide, target,
                                   Time.deltaTime / Mathf.Max(0.05f, SeatGlideSeconds));
        UpdateFade(_glide);

        if (_origin == null || SeatAnchor == null)
            return;

        float eased = Mathf.SmoothStep(0f, 1f, _glide);

        // The controller resists direct transform writes; disable it for the
        // duration of the move, exactly as the teleport did.
        bool hadController = _controller != null && _controller.enabled;
        if (hadController)
            _controller.enabled = false;

        _origin.transform.position =
            Vector3.Lerp(_standingPosition, _seatTarget, eased);
        _origin.transform.rotation =
            Quaternion.Slerp(_standingRotation, _seatRotation, eased);

        // Eye height rides the same glide, so sitting and standing read as one
        // continuous movement rather than a teleport plus a separate drop.
        if (_origin.CameraFloorOffsetObject != null)
        {
            Transform off = _origin.CameraFloorOffsetObject.transform;
            Vector3 lp = off.localPosition;
            lp.y = _offsetBaseY - _seatDrop * eased;
            off.localPosition = lp;
        }

        if (hadController)
            _controller.enabled = true;
    }

    // ------------------------------------------------------------------ props

    void SetPropsGrabbable(bool grabbable)
    {
        for (int i = 0; i < TableProps.Count; i++)
        {
            if (TableProps[i] != null)
                TableProps[i].enabled = grabbable;
        }
    }

    // ----------------------------------------------------------------- marker

    void UpdateMarker()
    {
        if (Marker == null)
            return;

        // Hidden while seated: you are already there.
        bool show = _state != State.Seated;
        if (Marker.gameObject.activeSelf != show)
            Marker.gameObject.SetActive(show);

        if (!show)
            return;

        // Slow pulse, and a firmer one once you are standing on it, so the
        // marker confirms "you are here" without needing text.
        float amount = _state == State.AtMarker ? PulseAmount * 2f : PulseAmount;
        float pulse = 1f + Mathf.Sin(Time.time * PulseSpeed) * amount;
        Marker.localScale = new Vector3(_markerBaseScale.x * pulse,
                                        _markerBaseScale.y,
                                        _markerBaseScale.z * pulse);
    }

    public State Current => _state;
}
