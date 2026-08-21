// SafetyExit.cs
//
// A way OUT of the experience that does not require a controller.
//
// WHY THIS EXISTS
// ---------------
// Leaving was only ever possible via the Meta system button on the right
// controller. That is hardware, not application code, and it has no equivalent
// in a hands-only session -- so a patient using hand tracking had no way to
// stop. For an environment whose entire purpose is to induce craving, "the
// patient cannot stop" is a safety defect, not a missing feature.
//
// THE CONTROL: a small red button that rides at your upper right, always in
// reach. Grab it and hold briefly and you leave; let go and you stay. It is a
// real grabbable, so it works identically with a controller grip and with a
// hand-tracking grasp -- no separate input path to get wrong.
//
// It follows the camera's POSITION AND YAW ONLY, never its pitch or roll. A
// button parented straight to the camera swings around as you look at it,
// which makes it maddening to actually grab.
//
// THE BACKUP GESTURE (off by default): both hands covering the face, held.
//
// Chosen because it is:
//   * already what a distressed person does, so it needs no learning
//   * two-handed and orientation-specific, so it cannot happen by accident
//     while reaching for a bottle
//   * built from joint POSITIONS only -- see PalmFacesHead -- so it does not
//     depend on any assumed joint-orientation convention
//
// It cannot collide with the sit/stand gesture in SeatedTableScenario, which
// requires both palms ABOVE head height. This one requires them BELOW it. The
// two are mutually exclusive by construction rather than by tuning.
//
// The screen darkens as the hold progresses, so the gesture explains itself:
// keep holding and you leave, let go and the room comes back. That same fade
// doubles as the exit transition, which matters because cutting a patient
// straight from a bar to the system menu is exactly the kind of jolt this
// scenario is otherwise careful to avoid.

using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class SafetyExit : MonoBehaviour
{
    [Tooltip("Seconds the button or gesture must be held before the app closes.")]
    public float HoldSeconds = 1f;

    // Right, up, forward -- relative to the head, yaw only.
    //
    // The first attempt was (0.34, -0.32, 0.02): beside the shoulder, where a
    // hand rests. That is about 85 degrees off the forward axis, and the Quest
    // 3S sees roughly +/-48 degrees horizontally, so the button was simply
    // never on screen. Placement has to answer "where do the EYES fall", not
    // "where is it comfortable to reach".
    //
    // This sits ~35 degrees right and slightly ABOVE eye level, 0.52 m away:
    // inside peripheral vision with a glance, still an easy reach, and high
    // enough that it never hovers among the props while seated -- where it
    // could be grabbed by mistake instead of a glass.
    [Tooltip("Where the button sits, relative to the head: right, up, forward.")]
    public Vector3 ButtonOffset = new Vector3(0.30f, 0.06f, 0.42f);

    [Tooltip("Size of the button (m).")]
    public float ButtonSize = 0.07f;

    [Tooltip("How fast the button catches up to you. Low is floaty, high snaps.")]
    public float ButtonFollow = 8f;

    [Tooltip("Also allow the two-handed face-cover gesture.")]
    public bool EnableFaceGesture = false;

    [Tooltip("Build the grabbable quit button. Off: the only red box in the " +
             "scene is the seated scenario's STAND UP button, and two boxes " +
             "that look alike but do different things is a trap.")]
    public bool ShowQuitButton = false;

    [Tooltip("How close to the head both palms must be (m).")]
    public float HeadDistance = 0.45f;

    [Tooltip("How squarely the palms must face the head. 0 = any, 1 = exact.")]
    public float FacingThreshold = 0.45f;

    [Tooltip("How quickly the fade recovers when the gesture is released.")]
    public float ReleaseSpeed = 3f;

    XROrigin _origin;
    XRHandSubsystem _hands;

    InputAction _menu;
    float _hold;
    bool _leaving;

    Transform _button;
    Material _buttonMaterial;
    bool _buttonHeld;

    GameObject _fadeSphere;
    Material _fadeMaterial;

    void Start()
    {
        _origin = GetComponentInParent<XROrigin>();
        BuildFade();
        if (ShowQuitButton)
            BuildButton();

        // Controller route as well. On the Oculus Touch profile the LEFT menu
        // button is the one applications may use; the right is reserved by the
        // system. Kept because a clinician holding controllers should not have
        // to perform a patient's gesture.
        _menu = new InputAction("SafetyExit", InputActionType.Button);
        _menu.AddBinding("<XRController>{LeftHand}/menuButton");
        _menu.Enable();
    }

    void OnDestroy()
    {
        if (_menu != null)
        {
            _menu.Disable();
            _menu.Dispose();
            _menu = null;
        }
    }

    void Update()
    {
        if (_leaving)
            return;

        bool held = _buttonHeld
                    || (_menu != null && _menu.IsPressed())
                    || (EnableFaceGesture && GestureHeld());

        if (held)
            _hold += Time.deltaTime;
        else
            _hold -= Time.deltaTime * ReleaseSpeed;

        _hold = Mathf.Clamp(_hold, 0f, HoldSeconds);
        UpdateFade(_hold / Mathf.Max(0.05f, HoldSeconds));

        if (_hold >= HoldSeconds)
            Leave();
    }

    void LateUpdate()
    {
        PositionButton();
    }

    void Leave()
    {
        _leaving = true;
        UpdateFade(1f);
        Debug.Log("[SafetyExit] Patient exited via the safety gesture.");
        Application.Quit();
    }

    // ---------------------------------------------------------------- button

    /// <summary>
    /// Builds the exit button: a small emissive red cube that is a real
    /// XRGrabInteractable, so grabbing it uses exactly the same path as
    /// grabbing a bottle. That is deliberate -- a bespoke input route for the
    /// safety control would be the one path never exercised in normal use.
    /// </summary>
    void BuildButton()
    {
        if (_origin == null || _origin.Camera == null)
            return;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "ExitButton";
        go.transform.SetParent(_origin.transform, false);
        go.transform.localScale = Vector3.one * ButtonSize;
        _button = go.transform;

        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Emissive, because the room is deliberately gloomy and gets gloomier
        // when seated. An unlit-looking red reads clearly in every mood the
        // LightingDirector produces.
        _buttonMaterial = new Material(Shader.Find("Standard"));
        _buttonMaterial.color = new Color(0.75f, 0.06f, 0.06f);
        _buttonMaterial.EnableKeyword("_EMISSION");
        _buttonMaterial.SetColor("_EmissionColor", new Color(0.95f, 0.10f, 0.10f));
        // Without this a material built at RUNTIME is treated as having black
        // emission and the glow never appears, whatever the colour says.
        _buttonMaterial.globalIlluminationFlags =
            MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mr.sharedMaterial = _buttonMaterial;

        // Slightly larger than it looks: a 7 cm target is hard to hit with a
        // tracked hand, and this is the control that must never be fiddly.
        var box = go.GetComponent<BoxCollider>();
        box.size = Vector3.one * 1.35f;

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var grab = go.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.throwOnDetach = false;   // it is a button, not something to throw
        grab.useDynamicAttach = true;

        grab.selectEntered.AddListener(_ => _buttonHeld = true);
        grab.selectExited.AddListener(_ => _buttonHeld = false);

        PositionButton();
        Debug.Log($"[SafetyExit] Exit button created at {_button.position}, " +
                  $"offset {ButtonOffset} from the head.");
    }

    /// <summary>
    /// Keeps the button at the patient's upper right, following head position
    /// and YAW only. Using the camera's full rotation would swing the button
    /// away as they turned to look at it.
    /// </summary>
    void PositionButton()
    {
        if (_button == null || _origin == null || _origin.Camera == null)
            return;

        // While it is being held, the interactor owns it. Fighting XRI for the
        // transform would make the grab feel like it is slipping.
        if (_buttonHeld)
            return;

        Transform cam = _origin.Camera.transform;
        Quaternion yaw = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
        Vector3 target = cam.position + yaw * ButtonOffset;

        float t = 1f - Mathf.Exp(-ButtonFollow * Time.deltaTime);
        _button.position = Vector3.Lerp(_button.position, target, t);
        _button.rotation = Quaternion.Slerp(_button.rotation, yaw, t);
    }

    // --------------------------------------------------------------- gesture

    bool GestureHeld()
    {
        if (!AcquireHands() || _origin == null || _origin.Camera == null)
            return false;

        // Hand joint poses are in XR Origin space, so the head must be too.
        Vector3 headLocal = _origin.transform.InverseTransformPoint(
            _origin.Camera.transform.position);

        return CoversFace(_hands.leftHand, headLocal) &&
               CoversFace(_hands.rightHand, headLocal);
    }

    bool CoversFace(XRHand hand, Vector3 headLocal)
    {
        if (!hand.isTracked)
            return false;

        if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm))
            return false;

        // Below head height keeps this disjoint from the sit/stand gesture,
        // which needs both palms 0.12 m ABOVE the head.
        if (palm.position.y > headLocal.y + 0.05f)
            return false;

        if (Vector3.Distance(palm.position, headLocal) > HeadDistance)
            return false;

        return PalmFacesHead(hand, palm.position, headLocal);
    }

    /// <summary>
    /// True when the palm is turned toward the head.
    ///
    /// The back-of-hand normal is derived from joint POSITIONS -- the same
    /// construction RiggedHandPose uses -- rather than from the palm joint's
    /// rotation. Joint orientation conventions vary between runtimes and are
    /// easy to get subtly wrong; three positions and a cross product are
    /// unambiguous. The across-hand vector is mirrored for the left hand,
    /// because it points opposite ways on the two hands and would otherwise
    /// make this test succeed on one hand and fail on the other.
    /// </summary>
    bool PalmFacesHead(XRHand hand, Vector3 palmPos, Vector3 headLocal)
    {
        if (!hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose wrist) ||
            !hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out Pose index) ||
            !hand.GetJoint(XRHandJointID.MiddleProximal).TryGetPose(out Pose middle) ||
            !hand.GetJoint(XRHandJointID.LittleProximal).TryGetPose(out Pose little))
            return false;

        Vector3 across = little.position - index.position;
        if (hand.handedness == Handedness.Left)
            across = -across;

        Vector3 along = middle.position - wrist.position;

        Vector3 back = Vector3.Cross(along, across);
        if (back.sqrMagnitude < 1e-8f)
            return false;
        back.Normalize();

        Vector3 toHead = headLocal - palmPos;
        if (toHead.sqrMagnitude < 1e-8f)
            return false;
        toHead.Normalize();

        // Palm faces the head exactly when the BACK of the hand points away.
        return Vector3.Dot(back, toHead) < -FacingThreshold;
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

    // ------------------------------------------------------------------ fade

    void BuildFade()
    {
        if (_origin == null || _origin.Camera == null)
            return;

        _fadeSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _fadeSphere.name = "ExitFade";
        Destroy(_fadeSphere.GetComponent<Collider>());
        _fadeSphere.transform.SetParent(_origin.Camera.transform, false);
        _fadeSphere.transform.localPosition = Vector3.zero;
        _fadeSphere.transform.localScale = Vector3.one * -0.26f;

        var mr = _fadeSphere.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Standard in transparent mode, NOT Unlit/Color. Unlit/Color is opaque,
        // so it can only be fully black or fully absent -- no good for a fade
        // that has to communicate progress. Standard is guaranteed to be in the
        // build because the bar's own materials use it.
        _fadeMaterial = new Material(Shader.Find("Standard"));
        _fadeMaterial.SetFloat("_Mode", 3f);
        _fadeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _fadeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _fadeMaterial.SetInt("_ZWrite", 0);
        _fadeMaterial.DisableKeyword("_ALPHATEST_ON");
        _fadeMaterial.EnableKeyword("_ALPHABLEND_ON");
        _fadeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        _fadeMaterial.renderQueue = 3000;
        _fadeMaterial.color = new Color(0f, 0f, 0f, 0f);

        mr.sharedMaterial = _fadeMaterial;
        _fadeSphere.SetActive(false);
    }

    void UpdateFade(float progress)
    {
        if (_fadeSphere == null || _fadeMaterial == null)
            return;

        // Eased so the first moments of the hold are visible immediately --
        // the patient needs to see it working before they trust it.
        float alpha = Mathf.Clamp01(Mathf.Sqrt(Mathf.Clamp01(progress)));

        bool show = alpha > 0.01f;
        if (_fadeSphere.activeSelf != show)
            _fadeSphere.SetActive(show);

        if (show)
            _fadeMaterial.color = new Color(0f, 0f, 0f, alpha);
    }
}
