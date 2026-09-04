using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class FanCheck
{
    [MenuItem("Tools/Check Fans")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/unity-VR.unity");
        int n = 0;
        foreach (FanSpinner f in Object.FindObjectsByType<FanSpinner>(FindObjectsSortMode.None))
        {
            GameObject g = f.gameObject;
            var mr = g.GetComponent<MeshRenderer>();
            Debug.Log($"[FanCheck] spinner on '{g.name}' parent='{(g.transform.parent ? g.transform.parent.name : "-")}'\n" +
                $"   activeInHierarchy={g.activeInHierarchy} enabled={f.enabled} dps={f.DegreesPerSecond}\n" +
                $"   isStatic={g.isStatic} flags={GameObjectUtility.GetStaticEditorFlags(g)}\n" +
                $"   localRot={g.transform.localEulerAngles.ToString("F1")} localPos={g.transform.localPosition.ToString("F3")}\n" +
                $"   renderer={(mr ? mr.name : "NONE")}");
            n++;
        }
        Debug.Log($"[FanCheck] {n} spinners.");
        // what does one fan's hierarchy look like?
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name != "Fan_0") continue;
            foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
                Debug.Log($"[FanCheck]   Fan_0 '{c.name}' static={c.gameObject.isStatic} " +
                          $"spinner={(c.GetComponent<FanSpinner>() != null)} " +
                          $"localRot={c.localEulerAngles.ToString("F0")} " +
                          $"localUp(world)={c.TransformDirection(Vector3.up).ToString("F2")} " +
                          $"localFwd(world)={c.TransformDirection(Vector3.forward).ToString("F2")}");
            break;
        }
    }
}
