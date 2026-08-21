using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class GrabCheck
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/unity-VR.unity", OpenSceneMode.Single);

        int chairs = 0, chairGrab = 0, chairColl = 0;
        foreach (var gi in Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None))
        {
            if (!gi.name.StartsWith("Chair") && !gi.name.StartsWith("Stacked")) continue;
            chairs++;
            chairGrab++;
            chairColl += gi.colliders != null ? gi.colliders.Count : 0;

            if (chairs == 1)
            {
                Debug.Log($"GRABCHECK sample={gi.name} interactableColliders={(gi.colliders == null ? -1 : gi.colliders.Count)} " +
                          $"componentColliders={gi.GetComponents<Collider>().Length} " +
                          $"childColliders={gi.GetComponentsInChildren<Collider>().Length} " +
                          $"rbKinematic={gi.GetComponent<Rigidbody>().isKinematic} " +
                          $"layer={gi.gameObject.layer} " +
                          $"movement={gi.movementType}");
            }
        }
        Debug.Log($"GRABCHECK chairs={chairs} withGrab={chairGrab} totalCollidersRegistered={chairColl}");

        int bottles = 0;
        foreach (var gi in Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None))
            if (gi.name.Contains("Bottle") || gi.name.Contains("Glass")) bottles++;
        Debug.Log($"GRABCHECK bottlesAndGlasses={bottles}");
    }
}
