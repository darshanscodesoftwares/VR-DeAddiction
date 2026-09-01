// GrabClink.cs
//
// A glass clink when a bottle or a glass is actually picked up.
//
// WHY IT HANGS OFF selectEntered
// ------------------------------
// The requirement is that pressing grip on empty air stays silent -- the sound
// means "you have hold of something", not "you pressed a button". XRI raises
// selectEntered only when an interactable is genuinely selected, so binding to
// it satisfies that by construction rather than by testing for it. Nothing here
// ever looks at the input.
//
// WHY ONLY BOTTLES AND GLASSES
// ----------------------------
// This is attached in GrabbableUpright, which builds the drinkware.
// GrabbableChair does not attach it, so a chair stays silent instead of
// chiming like a wine glass. Selecting by construction rather than by matching
// object names, which would break the first time something was renamed.
//
// WHY A SHARED POOL AND NOT AN AudioSource PER OBJECT
// ---------------------------------------------------
// There are ~75 grabbables and at most two hands. Seventy-five AudioSources
// would sit idle to serve two, so a handful are shared and moved to whatever
// was just grabbed. They are 3D: the clink should come from the bottle, unlike
// footsteps, which come from you.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class GrabClink : MonoBehaviour
{
    // Assigned by the builder, per instance.
    //
    // NOT static. A static field is not serialised into the scene, so it would
    // hold the clip in the editor and be null in the player -- every grab
    // silent, with nothing in the log to say why. ~75 references to one clip
    // costs a pointer each and is always correct.
    [Tooltip("The clink. Assigned by PubEnvironmentBuilder.")]
    public AudioClip Clip;

    [Tooltip("Shortest gap between two clinks from the SAME object (s). Stops " +
             "a grab that flickers on and off from machine-gunning.")]
    public float Retrigger = 0.12f;

    XRGrabInteractable _grab;
    float _lastPlayed = -99f;

    void Start()
    {
        _grab = GetComponent<XRGrabInteractable>();
        if (_grab == null)
        {
            enabled = false;
            return;
        }
        _grab.selectEntered.AddListener(OnGrabbed);
    }

    void OnDestroy()
    {
        if (_grab != null)
            _grab.selectEntered.RemoveListener(OnGrabbed);
    }

    void OnGrabbed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _)
    {
        if (Clip == null || Time.time - _lastPlayed < Retrigger)
            return;

        _lastPlayed = Time.time;
        ClinkPool.Play(Clip, transform.position);
    }
}

/// <summary>
/// A few shared 3D AudioSources, created on first use and moved to wherever a
/// sound is needed. Round-robin, so a new clink never cuts off the tail of the
/// previous one -- which is what a single source would do.
/// </summary>
public static class ClinkPool
{
    const int Voices = 4;

    static AudioSource[] _sources;
    static int _next;

    static void Ensure()
    {
        if (_sources != null && _sources[0] != null)
            return;

        var root = new GameObject("ClinkPool");
        Object.DontDestroyOnLoad(root);
        _sources = new AudioSource[Voices];

        for (int i = 0; i < Voices; i++)
        {
            var go = new GameObject($"Clink_{i}");
            go.transform.SetParent(root.transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 1f;                 // it comes from the bottle
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 0.4f;
            s.maxDistance = 8f;
            s.dopplerLevel = 0f;
            _sources[i] = s;
        }
    }

    public static void Play(AudioClip clip, Vector3 at)
    {
        if (clip == null)
            return;

        Ensure();
        AudioSource s = _sources[_next];
        _next = (_next + 1) % Voices;

        s.transform.position = at;
        // Glass is never struck identically twice. A little pitch and level
        // spread is the difference between a prop and a sound effect.
        s.pitch = Random.Range(0.93f, 1.08f);
        s.PlayOneShot(clip, Random.Range(0.55f, 0.8f));
    }
}
