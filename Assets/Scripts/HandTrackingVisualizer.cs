// HandTrackingVisualizer.cs
//
// Renders camera-tracked hands from the XRHandSubsystem, and hides the
// controller stand-in hands whenever real hands are being tracked.
//
// Joints are drawn as small procedural cubes -- no imported hand mesh, keeping
// to the project's no-external-assets rule. 26 joints per hand.
//
// Joint poses arrive in XR Origin space, NOT world space, so every pose is
// pushed through the origin's transform. Skipping that puts the hands in the
// wrong place the moment the player moves or turns.

using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

public class HandTrackingVisualizer : MonoBehaviour
{
    [Tooltip("Material for the joint cubes. Assigned by XRRigBuilder.")]
    public Material JointMaterial;

    [Tooltip("Controller-driven hand visuals, hidden while real hands track.")]
    public GameObject LeftControllerHand;
    public GameObject RightControllerHand;

    [Tooltip("Controller interactors, disabled while real hands track so the " +
             "two do not both try to grab.")]
    public UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor LeftControllerInteractor;
    public UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor RightControllerInteractor;

    [Tooltip("Interactors that follow the tracked hands and select on grasp.")]
    public UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor LeftHandInteractor;
    public UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor RightHandInteractor;

    [Tooltip("Fingertip-to-palm distance treated as a fully closed hand (m).")]
    public float ClosedDistance = 0.045f;

    [Tooltip("Fingertip-to-palm distance treated as a fully open hand (m).")]
    public float OpenDistance = 0.105f;

    [Tooltip("Closure fraction (0 open, 1 fist) at which a grab starts.")]
    public float GrabThreshold = 0.55f;

    [Tooltip("Closure fraction at which a held object is released. Lower than " +
             "GrabThreshold so tracking jitter does not drop what you hold.")]
    public float ReleaseThreshold = 0.35f;

    [Tooltip("Size of each joint cube, in metres.")]
    public float JointSize = 0.013f;

    bool _leftGrasping;
    bool _rightGrasping;

    XRHandSubsystem _subsystem;
    XROrigin _origin;

    Transform[] _leftJoints;
    Transform[] _rightJoints;
    GameObject _leftRoot;
    GameObject _rightRoot;

    static int JointCount => XRHandJointID.EndMarker.ToIndex();

    void Start()
    {
        _origin = GetComponentInParent<XROrigin>();

        _leftRoot = new GameObject("TrackedHand_Left");
        _rightRoot = new GameObject("TrackedHand_Right");
        _leftRoot.transform.SetParent(transform, false);
        _rightRoot.transform.SetParent(transform, false);

        _leftJoints = BuildJoints(_leftRoot.transform, "L");
        _rightJoints = BuildJoints(_rightRoot.transform, "R");

        SetHandVisible(_leftRoot, false);
        SetHandVisible(_rightRoot, false);
    }

    Transform[] BuildJoints(Transform parent, string tag)
    {
        var joints = new Transform[JointCount];
        for (int i = 0; i < joints.Length; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Joint_{tag}_{i}";
            Destroy(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            cube.transform.localScale = Vector3.one * JointSize;

            if (JointMaterial != null)
                cube.GetComponent<MeshRenderer>().sharedMaterial = JointMaterial;

            joints[i] = cube.transform;
        }
        return joints;
    }

    void Update()
    {
        // The subsystem only exists once OpenXR has started, so poll until it
        // appears rather than assuming it is ready in Start().
        if (_subsystem == null || !_subsystem.running)
        {
            AcquireSubsystem();
            if (_subsystem == null)
                return;
        }

        UpdateHand(_subsystem.leftHand, _leftJoints, _leftRoot, LeftControllerHand,
                   LeftControllerInteractor, LeftHandInteractor, ref _leftGrasping);
        UpdateHand(_subsystem.rightHand, _rightJoints, _rightRoot, RightControllerHand,
                   RightControllerInteractor, RightHandInteractor, ref _rightGrasping);
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
                Debug.Log("[HandTracking] Subsystem acquired.");
                return;
            }
        }
    }

    void UpdateHand(XRHand hand, Transform[] joints, GameObject root, GameObject controllerHand,
                    UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor controllerInteractor,
                    UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor handInteractor,
                    ref bool grasping)
    {
        bool tracked = hand.isTracked;

        SetHandVisible(root, tracked);

        // Show the controller stand-in only when real hands are not tracked, so
        // you never see two hands at once.
        if (controllerHand != null && controllerHand.activeSelf == tracked)
            controllerHand.SetActive(!tracked);

        // Exactly one interactor per hand may be live, or both race to grab the
        // same object and the controller one -- parked at the origin while the
        // controller is down -- wins by being closer to whatever is at spawn.
        if (controllerInteractor != null && controllerInteractor.enabled == tracked)
            controllerInteractor.enabled = !tracked;

        if (handInteractor != null && handInteractor.gameObject.activeSelf != tracked)
            handInteractor.gameObject.SetActive(tracked);

        if (!tracked)
        {
            grasping = false;
            return;
        }

        UpdateGrasp(hand, handInteractor, ref grasping);

        for (int i = 0; i < joints.Length; i++)
        {
            XRHandJoint joint = hand.GetJoint(XRHandJointIDUtility.FromIndex(i));

            if (!joint.TryGetPose(out Pose pose))
            {
                joints[i].gameObject.SetActive(false);
                continue;
            }

            joints[i].gameObject.SetActive(true);

            // Origin space -> world space.
            if (_origin != null)
            {
                Transform o = _origin.transform;
                joints[i].SetPositionAndRotation(
                    o.TransformPoint(pose.position),
                    o.rotation * pose.rotation);
            }
            else
            {
                joints[i].SetPositionAndRotation(pose.position, pose.rotation);
            }
        }
    }

    static readonly XRHandJointID[] k_Fingertips =
    {
        XRHandJointID.IndexTip,
        XRHandJointID.MiddleTip,
        XRHandJointID.RingTip,
        XRHandJointID.LittleTip,
    };

    /// <summary>
    /// Drives the hand's interactor from a whole-hand grasp: close your fingers
    /// around an object to hold it, open them to let go. This is the natural
    /// motion for picking up a bottle or a glass.
    ///
    /// Closure is measured as the mean fingertip-to-palm distance, mapped
    /// between an open hand and a fist. That is robust to hand size and to the
    /// exact angle the hand is held at, unlike per-joint angle thresholds.
    ///
    /// Hysteresis (grab at 0.55, release at 0.35) keeps tracking jitter from
    /// dropping whatever you are holding.
    /// </summary>
    void UpdateGrasp(XRHand hand,
                     UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor interactor,
                     ref bool grasping)
    {
        if (interactor == null)
            return;

        XRHandJoint palmJoint = hand.GetJoint(XRHandJointID.Palm);
        if (!palmJoint.TryGetPose(out Pose palm))
        {
            grasping = false;
            SetSelect(interactor, false);
            return;
        }

        float total = 0f;
        int counted = 0;

        for (int i = 0; i < k_Fingertips.Length; i++)
        {
            if (hand.GetJoint(k_Fingertips[i]).TryGetPose(out Pose tip))
            {
                total += Vector3.Distance(tip.position, palm.position);
                counted++;
            }
        }

        if (counted == 0)
        {
            grasping = false;
            SetSelect(interactor, false);
            return;
        }

        float avg = total / counted;

        // 0 = open hand, 1 = closed fist.
        float closure = Mathf.InverseLerp(OpenDistance, ClosedDistance, avg);
        grasping = grasping ? closure > ReleaseThreshold : closure > GrabThreshold;

        // The interactor sits in the palm, slightly forward into the grip, so
        // held objects end up in the hand rather than floating at the fingertips.
        Vector3 pos = palm.position + palm.rotation * new Vector3(0f, 0f, 0.02f);
        Quaternion rot = palm.rotation;

        if (_origin != null)
        {
            Transform o = _origin.transform;
            pos = o.TransformPoint(pos);
            rot = o.rotation * rot;
        }

        interactor.transform.SetPositionAndRotation(pos, rot);
        SetSelect(interactor, grasping);
    }

    /// <summary>
    /// Drives the interactor's select input.
    ///
    /// Must go through QueueManualState, NOT the manualPerformed property.
    /// XRI begins a grab on the rising EDGE (ReadWasPerformedThisFrame), and
    /// setting manualPerformed only sets the level -- the edge flags stay false
    /// and no grab ever starts. QueueManualState derives both edges from the
    /// previous state, and applies them on the next frame.
    /// </summary>
    static void SetSelect(
        UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor interactor, bool on)
    {
        var reader = interactor.selectInput;
        if (reader == null)
            return;

        reader.QueueManualState(on, on ? 1f : 0f);
    }

    static void SetHandVisible(GameObject root, bool visible)
    {
        if (root != null && root.activeSelf != visible)
            root.SetActive(visible);
    }
}
