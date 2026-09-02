// FootstepAudio.cs
//
// Footsteps, timed to actual movement, alternating left and right.
//
// WHY THE SOUND IS SYNTHESISED, NOT A FILE
// ----------------------------------------
// This project ships no audio assets and no texture files -- the sky, the
// grime and the environment are all generated. A footstep on a hard floor is
// a simple sound to build, so generating it keeps that rule intact and costs a
// few kilobytes of RAM instead of a licensing question.
//
// WHY DISTANCE, NOT INPUT AND NOT A TIMER
// ---------------------------------------
// A timer desynchronises the moment you change speed, and input does not know
// whether you actually moved -- walking into a wall would still play steps.
// Accumulating the distance the rig REALLY travelled means the cadence follows
// your speed for free and works identically for thumbstick and arm-swing,
// because it does not care how the rig was moved.
//
// TWO BUGS THIS REPLACES
// ----------------------
// 1. The old version gated on CharacterController.isGrounded and re-primed the
//    distance accumulator to 0.75 of a stride on every frame the gate failed.
//    isGrounded flickers constantly, so each flicker re-primed the accumulator
//    and the very next moving frame crossed the threshold -- producing steps at
//    unpredictable moments. Priming now happens ONCE, on a real stop.
// 2. It read CharacterController.velocity, which reports the last Move() call
//    and is perturbed by the gravity provider every frame. Measuring the rig's
//    own displacement is what actually happened.

using UnityEngine;

public class FootstepAudio : MonoBehaviour
{
    [Tooltip("Metres of travel between footfalls. An adult walking stride.")]
    public float StrideLength = 0.70f;

    [Tooltip("Below this speed (m/s) walking is considered stopped.")]
    public float MinSpeed = 0.30f;

    [Tooltip("How long below MinSpeed before the gait resets (s). Stops brief " +
             "stalls -- a doorway, a bump -- from restarting the cycle.")]
    public float StopGrace = 0.18f;

    [Tooltip("Loudness of a footstep at walking pace. Balanced against " +
             "GrabClink.Volume so one headset volume suits both.")]
    public float Volume = 0.45f;

    [Tooltip("Stereo separation between the feet. 0 = centred, 1 = hard sides.")]
    [Range(0f, 1f)] public float FootSpread = 0.35f;

    [Tooltip("Variants per foot when synthesising. Unused if clips are set.")]
    public int Variants = 4;

    [Header("Recorded clips (preferred)")]
    [Tooltip("Left-foot samples. If empty, footsteps are synthesised instead.")]
    public AudioClip[] LeftClips;

    [Tooltip("Right-foot samples. If empty, footsteps are synthesised instead.")]
    public AudioClip[] RightClips;

    // A single frame can never legitimately move this far. Anything larger is
    // the seat glide or a ground-recovery reset, and must not become footsteps.
    const float MaxFrameStep = 0.35f;

    const int SampleRate = 44100;
    const float ClipSeconds = 0.22f;

    AudioSource _left, _right;
    SeatedTableScenario _seated;

    AudioClip[] _clipsL, _clipsR;
    Vector3 _lastPos;
    float _distance;
    float _stoppedFor;
    bool _walking;
    bool _leftFoot;
    int _lastIndex = -1;

    void Start()
    {
        _seated = GetComponentInParent<SeatedTableScenario>();
        _lastPos = Flat(transform.position);

        // One source per foot, panned apart. A single source cannot overlap a
        // step with the tail of the previous one, and hard-alternating pan on a
        // shared source retriggers audibly.
        _left = NewSource(-FootSpread);
        _right = NewSource(FootSpread);

        // Recorded clips if we have them, synthesis only as a fallback.
        //
        // The synthesised version is a decent envelope but it reads as a low
        // clicky drum, not a shoe -- a footstep's character is in the messy
        // detail of a real recording, which is exactly what an oscillator and a
        // noise burst cannot produce. It stays as a fallback so a missing or
        // failed import degrades to an audible step rather than to silence,
        // which would look like the feature simply not working.
        bool haveRecorded = LeftClips != null && LeftClips.Length > 0 &&
                            RightClips != null && RightClips.Length > 0;

        if (haveRecorded)
        {
            _clipsL = LeftClips;
            _clipsR = RightClips;
        }
        else
        {
            Debug.LogWarning("[FootstepAudio] No recorded clips; synthesising.");
            int n = Mathf.Max(1, Variants);
            _clipsL = new AudioClip[n];
            _clipsR = new AudioClip[n];
            for (int i = 0; i < n; i++)
            {
                _clipsL[i] = BuildFootstep(i * 2, 0.94f);
                _clipsR[i] = BuildFootstep(i * 2 + 1, 1.06f);
            }
        }
    }

    AudioSource NewSource(float pan)
    {
        var go = new GameObject(pan < 0 ? "Footstep_L" : "Footstep_R");
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f;      // the patient's OWN feet, not a sound in the room
        s.panStereo = pan;
        s.dopplerLevel = 0f;
        return s;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    void Update()
    {
        Vector3 pos = Flat(transform.position);
        float moved = Vector3.Distance(pos, _lastPos);
        _lastPos = pos;

        float dt = Mathf.Max(Time.deltaTime, 1e-4f);

        bool seated = _seated != null &&
                      _seated.Current == SeatedTableScenario.State.Seated;

        // Discard teleports and the seated glide outright.
        if (seated || moved > MaxFrameStep)
        {
            ResetGait();
            return;
        }

        float speed = moved / dt;

        if (speed < MinSpeed)
        {
            // Grace period, so a momentary stall does not reset the gait and
            // then fire a step the instant you move again.
            _stoppedFor += dt;
            if (_stoppedFor >= StopGrace)
                ResetGait();
            return;
        }

        _stoppedFor = 0f;

        if (!_walking)
        {
            // First step of a departure lands after a short part of a stride,
            // not immediately and not after a full one.
            _walking = true;
            _distance = StrideLength * 0.45f;
        }

        _distance += moved;
        if (_distance < StrideLength)
            return;

        _distance -= StrideLength;
        Step(speed);
    }

    void ResetGait()
    {
        _walking = false;
        _distance = 0f;
    }

    void Step(float speed)
    {
        AudioSource src = _leftFoot ? _left : _right;
        AudioClip[] bank = _leftFoot ? _clipsL : _clipsR;
        _leftFoot = !_leftFoot;

        int i = Random.Range(0, bank.Length);
        if (bank.Length > 1 && i == _lastIndex)
            i = (i + 1) % bank.Length;
        _lastIndex = i;

        src.pitch = Random.Range(0.97f, 1.03f);
        float loud = Volume * Mathf.Clamp(speed / 1.4f, 0.65f, 1.15f);
        src.PlayOneShot(bank[i], loud * Random.Range(0.9f, 1f));
    }

    /// <summary>
    /// Builds one footstep on a hard floor.
    ///
    /// Three parts, because a single noise burst is what made the old version
    /// sound like static rather than a shoe:
    ///
    ///   HEEL   band-passed noise under a very fast decay. Band-passed, not
    ///          low-passed -- the low end has to go or it booms, and the top
    ///          has to go or it hisses. What is left is the click of a sole.
    ///   BODY   a short low sine, the weight landing.
    ///   TOE    a quieter second click a few tens of milliseconds later, which
    ///          is what stops it sounding like a stick tapping.
    /// </summary>
    AudioClip BuildFootstep(int seed, float weight)
    {
        Random.State prior = Random.state;
        Random.InitState(unchecked(4703 + seed * 613));

        int count = Mathf.RoundToInt(SampleRate * ClipSeconds);
        float[] data = new float[count];

        float bodyHz = Random.Range(84f, 122f) * weight;
        float heelDecay = Random.Range(0.018f, 0.030f);
        float bodyDecay = Random.Range(0.030f, 0.048f);
        float toeDelay = Random.Range(0.038f, 0.058f);
        float toeDecay = Random.Range(0.014f, 0.024f);
        float toeGain = Random.Range(0.30f, 0.50f);

        // One-pole pair -> band-pass. hi keeps the click, lo removes the rumble.
        float hiCoef = Random.Range(0.34f, 0.46f);
        float loCoef = Random.Range(0.030f, 0.055f);

        float lpHi = 0f, lpLo = 0f;
        float peak = 0f;

        for (int n = 0; n < count; n++)
        {
            float t = n / (float)SampleRate;

            float white = Random.Range(-1f, 1f);
            lpHi += hiCoef * (white - lpHi);
            lpLo += loCoef * (lpHi - lpLo);
            float band = lpHi - lpLo;

            float attack = Mathf.Clamp01(t / 0.0015f);
            float heel = band * Mathf.Exp(-t / heelDecay);

            float body = Mathf.Sin(2f * Mathf.PI * bodyHz * t)
                         * Mathf.Exp(-t / bodyDecay) * 0.42f;

            float toe = 0f;
            if (t > toeDelay)
                toe = band * Mathf.Exp(-(t - toeDelay) / toeDecay) * toeGain;

            float s = attack * (heel + toe + body);
            data[n] = s;

            float a = Mathf.Abs(s);
            if (a > peak) peak = a;
        }

        // Normalise so random parameters do not make some variants louder,
        // then soft-clip to round the transient instead of letting it spit.
        if (peak > 1e-4f)
        {
            float g = 0.85f / peak;
            for (int n = 0; n < count; n++)
                data[n] = (float)System.Math.Tanh(data[n] * g);
        }

        AudioClip clip = AudioClip.Create($"Step_{seed}", count, 1, SampleRate, false);
        clip.SetData(data, 0);

        Random.state = prior;
        return clip;
    }
}
