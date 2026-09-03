// FlickeringLamp.cs
//
// A failing fluorescent tube: the light is driven by the SOUND, so the flashes
// and the strike-pops are the same event rather than two effects that happen to
// run side by side.
//
// HOW THE SYNC WORKS
// ------------------
// The builder measures the clip's amplitude envelope once and bakes it into
// Envelope[]. At runtime the component reads the AudioSource's own playback
// position and looks the envelope up at that instant. Nothing counts time
// independently, so the light cannot drift out of step with the audio however
// long it loops, and a loop point needs no special handling -- the position
// simply wraps.
//
// Matching by hand would have meant guessing at flash times from a waveform
// picture; reading the envelope means the light is exact by construction.
//
// ATTACK AND DECAY
// ----------------
// The envelope is sampled at 60 Hz but a tube does not behave like a meter: it
// strikes instantly and dies away over a moment. So the value snaps UP and eases
// DOWN. Without that the flashes read as single-frame specks at 72 fps.

using UnityEngine;

public class FlickeringLamp : MonoBehaviour
{
    [Header("Driven by the builder")]
    public AudioClip Clip;

    [Tooltip("Loudness per frame, 0-1, sampled at EnvelopeHz.")]
    public float[] Envelope;

    public float EnvelopeHz = 60f;

    [Tooltip("The light that flickers. Left steady if unset.")]
    public Light TargetLight;

    [Tooltip("The lamp face that glows with it.")]
    public Renderer GlowRenderer;

    [Header("Look")]
    [Tooltip("Light intensity at a full strike.")]
    public float PeakIntensity = 2.2f;

    [Tooltip("How much light remains between strikes. A dead tube still has a " +
             "little glow, and the sign must not go black.")]
    [Range(0f, 1f)] public float Floor = 0.14f;

    [Tooltip("Seconds for a flash to fade once the sound has passed.")]
    public float DecaySeconds = 0.13f;

    [Header("Sound")]
    [Range(0f, 1f)] public float Volume = 0.30f;
    public float MaxDistance = 14f;

    AudioSource _source;
    Material _glowMat;
    Color _emissionOn;
    float _level;

    void Start()
    {
        if (Clip != null)
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = Clip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = Volume;
            _source.spatialBlend = 1f;              // it comes from the sign
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = 2f;
            _source.maxDistance = MaxDistance;
            _source.dopplerLevel = 0f;
            _source.Play();
        }

        if (GlowRenderer != null)
        {
            // An instance, not the shared material: the other lamp housing uses
            // the same asset and must not flicker with this one.
            _glowMat = GlowRenderer.material;
            _emissionOn = _glowMat.HasProperty("_EmissionColor")
                ? _glowMat.GetColor("_EmissionColor")
                : Color.white;
            _glowMat.EnableKeyword("_EMISSION");
            _glowMat.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
    }

    void Update()
    {
        float target = Floor;

        if (Envelope != null && Envelope.Length > 0)
        {
            // Read the AUDIO's position, never a clock of our own.
            float t = _source != null && _source.clip != null
                ? _source.time
                : Time.time % (Envelope.Length / EnvelopeHz);

            int i = Mathf.Clamp(Mathf.FloorToInt(t * EnvelopeHz), 0, Envelope.Length - 1);
            target = Mathf.Max(Floor, Envelope[i]);
        }

        // Snap up, ease down: a tube strikes at once and dies away.
        _level = target > _level
            ? target
            : Mathf.Lerp(_level, target, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, DecaySeconds)));

        if (TargetLight != null)
            TargetLight.intensity = PeakIntensity * _level;

        if (_glowMat != null)
            _glowMat.SetColor("_EmissionColor", _emissionOn * _level);
    }
}
