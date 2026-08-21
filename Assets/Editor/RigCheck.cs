using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine;

public static class RigCheck
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/unity-VR.unity", OpenSceneMode.Single);
        var origin = Object.FindFirstObjectByType<XROrigin>();
        if (origin == null) { Debug.Log("RIGCHECK no XROrigin"); return; }

        Debug.Log($"RIGCHECK rigPos={origin.transform.position:F3} " +
                  $"mode={origin.RequestedTrackingOriginMode} " +
                  $"camYOffset={origin.CameraYOffset} " +
                  $"offsetObjPos={(origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform.localPosition.ToString("F3") : "-")} " +
                  $"camLocal={(origin.Camera != null ? origin.Camera.transform.localPosition.ToString("F3") : "-")}");

        var cc = origin.GetComponent<CharacterController>();
        if (cc != null)
            Debug.Log($"RIGCHECK ccHeight={cc.height} ccCenter={cc.center:F3}");
    }
}
