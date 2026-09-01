// XRRigBuilder.cs
//
// Builds a hardware-independent XR rig procedurally, matching the project rule
// that code is the source of truth (see PubEnvironmentBuilder.cs).
//
//   Tools > VR Setup > Add XR Rig To Scene
//
// HARDWARE INDEPENDENCE
// --------------------
// Nothing here references Meta/Oculus APIs. Input is bound to generic OpenXR
// device paths (<XRHMD>, <XRController>{LeftHand|RightHand}), which the OpenXR
// runtime maps onto whatever interaction profile the headset reports. Moving to
// VIVE or PICO means enabling their interaction profile in OpenXR settings --
// this file does not change.
//
// Actions are created in code and assigned to serialized fields. Both
// InputActionProperty and XRInputValueReader.m_InputAction are [SerializeField],
// so the bindings persist into the saved scene without an .inputactions asset.

using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

public static class XRRigBuilder
{
    // Seated/standing fallback only. In Floor tracking mode the headset reports
    // real height and this offset is ignored.
    const float FallbackEyeHeight = 1.6f;

    // Comfort defaults. Slower than the desktop walk speed on purpose --
    // fast continuous locomotion is a common nausea trigger.
    const float MoveSpeed = 1.6f;
    const float SnapTurnDegrees = 45f;

    /// <summary>
    /// Creates the rig under <paramref name="parent"/>. Returns the root so the
    /// caller can keep counting objects.
    /// </summary>
    public static GameObject Build(Transform parent, Vector3 localPosition, float yawDegrees)
    {
        GameObject rig = new GameObject("XR Origin");
        rig.transform.SetParent(parent, false);
        rig.transform.localPosition = localPosition;
        rig.transform.localEulerAngles = new Vector3(0f, yawDegrees, 0f);

        // Collision body. XRBodyTransformer.useCharacterControllerIfExists is
        // true by default, so simply having this on the Origin makes locomotion
        // collide with the pub geometry.
        CharacterController cc = rig.AddComponent<CharacterController>();
        cc.height = 1.75f;
        // 0.20, not 0.30. The capsule radius is how close you can physically
        // stand to a table before its collider stops you -- and you need to be
        // close enough to reach objects on the far side of the top.
        cc.radius = 0.20f;
        // Small skin width: the default 0.08 adds another 8 cm of stand-off and
        // makes depenetration pushes larger.
        cc.skinWidth = 0.02f;
        cc.center = new Vector3(0f, 0.875f, 0f);
        cc.slopeLimit = 45f;
        // Low step offset: 0.3 was enough to let the capsule climb onto crates
        // and chair frames and then strand itself up there. Real thresholds in
        // this building are small, so 0.15 loses nothing.
        cc.stepOffset = 0.15f;

        XROrigin origin = rig.AddComponent<XROrigin>();

        GameObject offset = new GameObject("Camera Offset");
        offset.transform.SetParent(rig.transform, false);

        // ---- Head ----------------------------------------------------------
        // Named "XR Camera", not "Main Camera": the environment builder does
        // GameObject.Find("Main Camera") in a few places to park the review
        // camera, and we do not want it grabbing the headset camera. The
        // MainCamera *tag* is still required by XROrigin.
        GameObject camGo = new GameObject("XR Camera");
        camGo.transform.SetParent(offset.transform, false);
        camGo.tag = "MainCamera";

        Camera cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;   // low, so you can lean into a table
        cam.farClipPlane = 80f;      // compound fits well inside this
        camGo.AddComponent<AudioListener>();

        TrackedPoseDriver head = camGo.AddComponent<TrackedPoseDriver>();
        head.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        head.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
        head.positionInput = new InputActionProperty(
            Vec3Action("Head Position", "<XRHMD>/centerEyePosition"));
        head.rotationInput = new InputActionProperty(
            QuatAction("Head Rotation", "<XRHMD>/centerEyeRotation"));

        origin.Camera = cam;
        origin.CameraFloorOffsetObject = offset;
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        origin.CameraYOffset = FallbackEyeHeight;

        // ---- Interaction manager -------------------------------------------
        // One per scene; interactors and interactables find it automatically.
        if (Object.FindFirstObjectByType<XRInteractionManager>() == null)
        {
            var mgr = new GameObject("XR Interaction Manager");
            mgr.AddComponent<XRInteractionManager>();
            mgr.transform.SetParent(rig.transform.parent, false);
        }

        // ---- Hands ---------------------------------------------------------
        GameObject leftCtrl = BuildHand(offset.transform, "Left Controller", "LeftHand");
        GameObject rightCtrl = BuildHand(offset.transform, "Right Controller", "RightHand");

        // Camera-tracked hands. Swaps automatically with the controller visuals
        // depending on what the runtime is actually reporting.
        var handViz = rig.AddComponent<HandTrackingVisualizer>();
        handViz.JointMaterial = ControllerMaterial();
        handViz.LeftControllerHand = FindHandVisual(leftCtrl.transform);
        handViz.RightControllerHand = FindHandVisual(rightCtrl.transform);
        handViz.LeftControllerInteractor = leftCtrl.GetComponent<NearFarInteractor>();
        handViz.RightControllerInteractor = rightCtrl.GetComponent<NearFarInteractor>();

        // Interactors for tracked hands. They live under the origin (not the
        // camera offset) because the visualizer positions them in world space
        // from the pinch point each frame.
        handViz.LeftHandInteractor = BuildHandInteractor(rig.transform, "Left Hand Interactor");
        handViz.RightHandInteractor = BuildHandInteractor(rig.transform, "Right Hand Interactor");

        // ---- Locomotion ------------------------------------------------------
        // LocomotionMediator [RequireComponent]s XRBodyTransformer, so that is
        // added automatically alongside it.
        LocomotionMediator mediator = rig.AddComponent<LocomotionMediator>();

        GravityProvider gravity = rig.AddComponent<GravityProvider>();
        gravity.mediator = mediator;

        // Stop the collision capsule chasing the headset every frame.
        //
        // By default GravityProvider re-centres the CharacterController on the
        // camera's x/z each frame. Lean physically toward a table and the
        // capsule follows your head INTO the table collider; Unity resolves the
        // overlap by pushing the capsule out, which shoves the whole rig
        // backwards. That is the "pushed back when I reach for something".
        //
        // With this off the capsule stays where locomotion put it, so leaning
        // and reaching are never blocked. The trade-off is that physically
        // walking several metres could put your head through a wall -- far
        // preferable to the world shoving you, which is disorienting and a
        // nausea trigger in its own right.
        gravity.updateCharacterControllerCenterEachFrame = false;

        ContinuousMoveProvider move = rig.AddComponent<ContinuousMoveProvider>();
        move.mediator = mediator;
        move.moveSpeed = MoveSpeed;
        move.enableStrafe = true;
        move.forwardSource = camGo.transform;   // walk where you look
        BindVec2(move.leftHandMoveInput, "Move", "<XRController>{LeftHand}/thumbstick");

        // Snap turning rather than smooth turning: markedly less nauseating,
        // which matters for a clinical population.
        SnapTurnProvider turn = rig.AddComponent<SnapTurnProvider>();
        turn.mediator = mediator;
        turn.turnAmount = SnapTurnDegrees;
        BindVec2(turn.rightHandTurnInput, "Turn", "<XRController>{RightHand}/thumbstick");

        // Diagnostic framerate logger (logcat "PERF"). Remove for clinical builds.
        rig.AddComponent<PerformanceProbe>();

        // Safety net: B button returns to ground, and anything left hanging in
        // mid-air is forced back down. See GroundRecovery.cs.
        rig.AddComponent<GroundRecovery>();

        // Locomotion for hand tracking. The thumbstick providers above only
        // work with controllers, so with hands alone there was no way to move.
        // Arm swing, not teleport: it needs no finger-pose detection, which is
        // what made the previous point-and-pinch teleport feel unreliable.
        rig.AddComponent<HandLocomotion>();

        // Footsteps, synthesised rather than shipped as audio files, and timed
        // off the distance the character controller actually travelled -- so
        // the cadence follows your speed and nothing plays while you stand
        // still or sit. See FootstepAudio.cs.
        var steps = rig.AddComponent<FootstepAudio>();
        steps.LeftClips = LoadClips("L");
        steps.RightClips = LoadClips("R");

        // A way out that does not need a controller. Leaving was only possible
        // via the Meta system button, which does not exist in a hands-only
        // session -- so a hand-tracking patient could not stop a craving
        // induction session. Cover your face with both hands and hold.
        rig.AddComponent<SafetyExit>();

        return rig;
    }

    /// <summary>
    /// Loads the sliced footstep clips for one foot.
    ///
    /// They come from a single continuous walk recording, cut at its transients
    /// -- so the odd-numbered footfalls are genuinely one foot and the even ones
    /// the other, rather than the same sample pretending to alternate.
    /// </summary>
    static AudioClip[] LoadClips(string foot)
    {
        var clips = new List<AudioClip>();
        for (int i = 1; i <= 8; i++)
        {
            string path = $"Assets/Audio/Footsteps/Step_{foot}{i}.wav";
            AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (c != null)
                clips.Add(c);
        }

        if (clips.Count == 0)
            Debug.LogWarning($"[XRRig] No {foot} footstep clips found; will synthesise.");
        else
            Debug.Log($"[XRRig] Loaded {clips.Count} {foot} footstep clips.");

        return clips.ToArray();
    }

    static GameObject BuildHand(Transform parent, string name, string hand)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TrackedPoseDriver tpd = go.AddComponent<TrackedPoseDriver>();
        tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
        tpd.positionInput = new InputActionProperty(
            Vec3Action(name + " Position", $"<XRController>{{{hand}}}/devicePosition"));
        tpd.rotationInput = new InputActionProperty(
            QuatAction(name + " Rotation", $"<XRController>{{{hand}}}/deviceRotation"));

        GameObject handVisual = BuildControllerVisual(go.transform, name);

        // Where a grabbed object sits. Slightly ahead of the controller origin
        // so objects land in the palm rather than inside the wrist.
        GameObject attach = new GameObject("Attach");
        attach.transform.SetParent(go.transform, false);
        attach.transform.localPosition = new Vector3(0f, -0.01f, 0.05f);

        // Near-far: touch to grab, or point and grab at distance. Distance grab
        // matters here because bottles sit on tables across the room.
        var attachController = go.AddComponent<InteractionAttachController>();

        var interactor = go.AddComponent<NearFarInteractor>();
        interactor.interactionAttachController = attachController;
        interactor.enableNearCasting = true;
        interactor.enableFarCasting = true;
        interactor.attachTransform = attach.transform;

        // Grip button grabs. Trigger is left free for future scenario actions.
        BindButton(interactor.selectInput, name + " Select",
                   $"<XRController>{{{hand}}}/gripPressed",
                   $"<XRController>{{{hand}}}/grip");

        BindButton(interactor.activateInput, name + " Activate",
                   $"<XRController>{{{hand}}}/triggerPressed",
                   $"<XRController>{{{hand}}}/trigger");

        TuneReach(go, interactor, attachController, far: true);

        return go;
    }

    /// <summary>
    /// Instantiates Unity's rigged hand model from the XR Hands package.
    ///
    /// This replaces a hand I had built from tapered boxes in Blender. That was
    /// fine for bottles and tables but cannot produce a hand -- it read as a
    /// flat plank with slits cut in it, and no texture would have fixed that,
    /// because the geometry was the problem.
    ///
    /// The package model is a properly skinned mesh with a real finger rig, it
    /// ships with the package we already depend on, and it is Unity's own -- so
    /// it stays vendor-neutral, unlike Meta's Interaction SDK hands.
    /// </summary>
    static GameObject BuildControllerVisual(Transform parent, string name)
    {
        bool isLeft = name.StartsWith("Left");

        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Samples/XRHands/Models/" + (isLeft ? "LeftHand" : "RightHand") + ".fbx");

        if (src == null)
        {
            Debug.LogWarning("[XRRigBuilder] XR Hands model missing - no hand visual.");
            return null;
        }

        GameObject hand = new GameObject(name + "_Hand");
        hand.transform.SetParent(parent, false);

        GameObject inst = (GameObject)Object.Instantiate(src, hand.transform);
        inst.name = "HandMesh";
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = src.transform.localRotation;
        inst.transform.localScale = src.transform.localScale;

        Material skin = SkinMaterial();
        foreach (SkinnedMeshRenderer smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.sharedMaterial = skin;
        foreach (MeshRenderer mr in inst.GetComponentsInChildren<MeshRenderer>(true))
            mr.sharedMaterial = skin;

        // Curl the rig's finger bones from the grip/trigger values, so grabbing
        // reads as grabbing. Derives its own curl axis per bone -- see
        // RiggedHandPose.cs.
        var pose = hand.AddComponent<RiggedHandPose>();
        pose.Handedness = isLeft ? RiggedHandPose.Side.Left : RiggedHandPose.Side.Right;
        pose.Wrist = FindBone(inst.transform, isLeft ? "L_Wrist" : "R_Wrist");

        return hand;
    }

    /// <summary>
    /// Locates the "<name>_Hand" child, so the tracking visualizer can toggle
    /// the controller hand without touching the interactor or pose driver on
    /// the parent.
    /// </summary>
    static GameObject FindHandVisual(Transform controller)
    {
        foreach (Transform child in controller)
        {
            if (child.name.EndsWith("_Hand"))
                return child.gameObject;
        }
        return null;
    }

    /// <summary>
    /// Dials back how far grabbing reaches, so objects are picked up rather
    /// than attracted.
    ///
    /// The casters were never created by the builder, so NearFarInteractor made
    /// its own at runtime with package defaults -- a 0.1 m grab sphere and a
    /// 10 m far reach. Combined with the default far attach mode, which pulls a
    /// far-selected object to the hand, that meant pointing anywhere near a
    /// bottle across the room flung it into your grip. Hence "magnetic".
    ///
    /// Creating them explicitly also makes the values readable here instead of
    /// being invisible package defaults.
    /// </summary>
    static void TuneReach(GameObject go, NearFarInteractor interactor,
                          InteractionAttachController attach, bool far)
    {
        // Near reach: roughly a fingertip past the hand, not a 10 cm bubble
        // around it. Small enough that you must actually reach the object,
        // large enough to forgive tracked-hand jitter.
        var near = go.GetComponent<SphereInteractionCaster>();
        if (near == null)
            near = go.AddComponent<SphereInteractionCaster>();
        near.castRadius = 0.055f;
        interactor.nearInteractionCaster = near;

        if (far)
        {
            var curve = go.GetComponent<CurveInteractionCaster>();
            if (curve == null)
                curve = go.AddComponent<CurveInteractionCaster>();

            // 1.8 m, not 10: far grab now only covers what is just out of
            // reach -- the far side of a table -- rather than the whole hall.
            curve.castDistance = 1.8f;
            // Tighter aim, so it takes what you point at instead of whatever
            // happens to be near the ray.
            curve.sphereCastRadius = 0.045f;
            interactor.farInteractionCaster = curve;
        }

        // Distance-based velocity scaling and momentum let a held object be
        // reeled in and pushed out, and carry on drifting after the hand
        // stops. Both read as the object having a will of its own.
        attach.useDistanceBasedVelocityScaling = false;
        attach.useMomentum = false;
    }

    /// <summary>
    /// An interactor driven by hand tracking rather than a controller. Its
    /// select input stays in ManualValue mode so HandTrackingVisualizer can
    /// set it from the grasp gesture -- there is no button to bind.
    /// </summary>
    static NearFarInteractor BuildHandInteractor(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        GameObject attach = new GameObject("Attach");
        attach.transform.SetParent(go.transform, false);

        var attachController = go.AddComponent<InteractionAttachController>();

        var interactor = go.AddComponent<NearFarInteractor>();
        interactor.interactionAttachController = attachController;
        interactor.attachTransform = attach.transform;
        interactor.enableNearCasting = true;
        // No far casting for hands: ray-pointing with tracked hands is jittery.
        interactor.enableFarCasting = false;

        interactor.selectInput.inputSourceMode =
            XRInputButtonReader.InputSourceMode.ManualValue;
        interactor.activateInput.inputSourceMode =
            XRInputButtonReader.InputSourceMode.Unused;

        TuneReach(go, interactor, attachController, far: false);

        go.SetActive(false);   // enabled only while that hand is tracked
        return interactor;
    }

    const string SkinMatPath = "Assets/PubEnvironment/Materials/Mat_XR_Skin.mat";

    /// <summary>
    /// Skin, using the VertexGrime shader so the per-vertex tone baked in
    /// Blender is actually visible. Unity's Standard shader ignores vertex
    /// colours entirely, so with it the hands render as one flat plastic tone
    /// no matter what the mesh carries.
    ///
    /// Low smoothness: skin is matte. A glossy hand reads as a mannequin.
    /// </summary>
    static Material SkinMaterial()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(SkinMatPath);
        Shader grime = Shader.Find("PubEnvironment/VertexGrime");
        Shader use = grime != null ? grime : Shader.Find("Standard");

        if (m == null)
        {
            m = new Material(use);
            AssetDatabase.CreateAsset(m, SkinMatPath);
        }

        m.shader = use;
        m.SetColor("_Color", new Color(0.72f, 0.52f, 0.40f));
        m.SetFloat("_Glossiness", 0.16f);
        m.SetFloat("_Metallic", 0f);
        if (grime != null)
            m.SetFloat("_GrimeStrength", 1f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    const string ControllerMatPath = "Assets/PubEnvironment/Materials/Mat_XR_Controller.mat";

    /// <summary>Skin tone for the hands, warm enough to read against grey concrete.</summary>
    static Material ControllerMaterial()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(ControllerMatPath);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, ControllerMatPath);
        }
        m.shader = Shader.Find("Standard");
        m.SetColor("_Color", new Color(0.62f, 0.44f, 0.33f));
        m.SetFloat("_Glossiness", 0.22f);
        m.SetFloat("_Metallic", 0f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void BindButton(XRInputButtonReader reader, string name,
                           string pressedBinding, string valueBinding)
    {
        reader.inputSourceMode = XRInputButtonReader.InputSourceMode.InputAction;
        reader.inputActionPerformed =
            new InputAction(name + " Pressed", InputActionType.Button, pressedBinding);
        reader.inputActionValue =
            new InputAction(name + " Value", InputActionType.Value, valueBinding,
                            expectedControlType: "Axis");
    }

    static Transform FindBone(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name)
                return t;
        return null;
    }

    static InputAction Vec3Action(string name, string binding) =>
        new InputAction(name, InputActionType.Value, binding, expectedControlType: "Vector3");

    static InputAction QuatAction(string name, string binding) =>
        new InputAction(name, InputActionType.Value, binding, expectedControlType: "Quaternion");

    static void BindVec2(XRInputValueReader<Vector2> reader, string name, string binding)
    {
        reader.inputSourceMode = XRInputValueReader.InputSourceMode.InputAction;
        reader.inputAction =
            new InputAction(name, InputActionType.Value, binding, expectedControlType: "Vector2");
    }

    [MenuItem("Tools/VR Setup/Add XR Rig To Scene")]
    static void AddRigMenu()
    {
        GameObject rig = Build(null, Vector3.zero, 0f);
        Selection.activeGameObject = rig;
        Debug.Log("[XRRigBuilder] XR Origin added. Note: PubEnvironment > Build " +
                  "creates its own rig; this menu item is for scratch scenes.");
    }
}
