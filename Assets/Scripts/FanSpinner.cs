// FanSpinner.cs
//
// Spins a ceiling fan's blades.
//
// A transform rotated in Update is far cheaper on a Quest than an Animator per
// fan: no clip sampling, no state machine, no per-object animation job. Six of
// these cost essentially nothing, where six Animators would each carry their own
// evaluation overhead for what is one number changing over time.
//
// Speed is deliberately well below a real fan. A ceiling fan runs 60-100 rpm
// (360-600 deg/s); at 72 Hz that puts a blade most of the way round between
// frames and it strobes -- blades appear to crawl backwards or stand still, and
// in a headset the flicker is unpleasant to sit under. This is the same wagon
// wheel effect that makes filmed rotors look wrong. Slower reads as a tired
// fan, which suits the room, and stays legible frame to frame.

using UnityEngine;

public class FanSpinner : MonoBehaviour
{
    [Tooltip("Degrees per second about the blades' own up-axis.")]
    public float DegreesPerSecond = 210f;

    void Start()
    {
        // One line per fan at startup. If the blades are not turning, this says
        // immediately whether the script is running at all or whether something
        // downstream (batching, a frozen transform) is eating the rotation --
        // which is otherwise indistinguishable from the outside.
        Debug.Log($"[FanSpinner] '{name}' awake, {DegreesPerSecond} deg/s, " +
                  $"static={gameObject.isStatic}, parentStatic=" +
                  $"{(transform.parent != null && transform.parent.gameObject.isStatic)}, " +
                  $"localUp(world)={transform.TransformDirection(Vector3.up).ToString("F2")}");
    }

    void Update()
    {
        // WORLD up, not the blades' own up.
        //
        // Unity's FBX importer puts the Z-up to Y-up conversion on the model's
        // parent as a 270 degree X rotation, which leaves this object's LOCAL
        // axes still in Blender's frame: its local up points sideways in the
        // world and its local FORWARD is what points at the sky. Spinning about
        // local up therefore tumbled the blades end over end instead of turning
        // them flat.
        //
        // Rotating about world up sidesteps the whole question. It is correct
        // whatever convention the model arrived with, and a ceiling fan turns
        // about the world vertical by definition. The pivot is already on the
        // fan's shaft, so this spins rather than orbits.
        transform.Rotate(Vector3.up, DegreesPerSecond * Time.deltaTime, Space.World);
    }
}
