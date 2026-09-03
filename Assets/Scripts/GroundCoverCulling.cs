// GroundCoverCulling.cs
//
// Culls ground cover beyond a short radius.
//
// WHY THIS EXISTS
// ---------------
// 108 grass pieces over a 23 x 31 m yard took the headset from 92% of frames on
// budget to 48%, with a worst frame of 41.7 ms. Triangle count is only half the
// story: the grass is ALPHA-CUT, which on a tile-based mobile GPU defeats early
// depth rejection, so every card shades whatever is behind it as well.
//
// The player can only ever stand in one part of the yard, so most of that cover
// is being drawn to occupy a few pixels near the horizon. Unity has no
// per-renderer cull distance -- it is per LAYER, per camera -- so ground cover
// gets its own layer and this sets the distance on whichever camera is active.
//
// This keeps the density the brief asked for where it can be seen, and stops
// paying for it where it cannot.

using UnityEngine;

public class GroundCoverCulling : MonoBehaviour
{
    [Tooltip("Ground cover beyond this many metres is not drawn.")]
    public float CullDistance = 17f;

    [Tooltip("Layer name the ground cover was built on.")]
    public string LayerName = "GroundCover";

    void Start()
    {
        Apply(Camera.main);
    }

    public void Apply(Camera cam)
    {
        if (cam == null)
            return;

        int layer = LayerMask.NameToLayer(LayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[GroundCoverCulling] No '{LayerName}' layer; " +
                             "ground cover will draw at full range.");
            return;
        }

        // layerCullDistances must be assigned as a whole array -- writing one
        // element of the returned copy changes nothing, which is a quiet way to
        // have this do absolutely nothing.
        float[] d = cam.layerCullDistances;
        d[layer] = CullDistance;
        cam.layerCullDistances = d;

        // Spherical, not planar: planar culling pops cover in and out as the
        // head turns, because the distance is measured along the view axis.
        cam.layerCullSpherical = true;
    }
}
