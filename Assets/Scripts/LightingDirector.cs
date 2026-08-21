// LightingDirector.cs
//
// Drives the whole room between lighting moods, and cross-fades smoothly
// between them so a transition can be reversed halfway through.
//
//   Bar     gloomy warm -- dim, no white, the room reads as a late-evening bar
//   Seated  the table is warm and bright, the rest of the room falls away
//
// PERFORMANCE
// -----------
// Changing colour and intensity on existing lights is free: no extra draw
// calls, no shader work. Crucially this NEVER adds a pixel light -- the scene
// runs 4 ForcePixel + 23 ForceVertex, and going to 6 pixel lights previously
// took the headset from 72 fps to ~36. The table focus light is promoted from
// the existing budget by demoting a distant pendant, so the count never rises.
//
// The background is darkened with FOG rather than by dimming 23 lights. Fog is
// free, pushes the far end of the hall away visually, and leaves the table lit.
// Dimming every light individually is slower and reads flat.

using System.Collections.Generic;
using UnityEngine;

public class LightingDirector : MonoBehaviour
{
    [System.Serializable]
    public class Mood
    {
        public Color WarmTint = new Color(1f, 0.845f, 0.615f);
        [Tooltip("Multiplier applied to every light's authored intensity.")]
        public float RoomIntensity = 1f;
        [Tooltip("How far each light's colour is pushed toward WarmTint (0-1).")]
        public float Warmth = 1f;

        public Color Ambient = new Color(0.070f, 0.072f, 0.082f);
        public Color FogColor = new Color(0.115f, 0.100f, 0.086f);
        public float FogStart = 14f;
        public float FogEnd = 60f;

        [Tooltip("Intensity multiplier for the light over the feature table.")]
        public float FocusIntensity = 0f;
    }

    [Tooltip("Lights making up the room. Populated by PubEnvironmentBuilder.")]
    public List<Light> RoomLights = new List<Light>();

    [Tooltip("The light over the feature table, driven separately.")]
    public Light FocusLight;

    [Tooltip("Distant pendant demoted to vertex when the focus light is " +
             "promoted, so the pixel-light count never increases.")]
    public Light DemotableLight;

    public Mood BarMood = new Mood
    {
        WarmTint = new Color(1f, 0.780f, 0.520f),
        RoomIntensity = 0.72f,
        Warmth = 0.85f,
        Ambient = new Color(0.055f, 0.050f, 0.048f),
        FogColor = new Color(0.100f, 0.085f, 0.070f),
        FogStart = 12f,
        FogEnd = 46f,
        FocusIntensity = 0f,
    };

    public Mood SeatedMood = new Mood
    {
        WarmTint = new Color(1f, 0.735f, 0.450f),
        RoomIntensity = 0.30f,        // room falls away
        Warmth = 1f,
        Ambient = new Color(0.030f, 0.026f, 0.022f),
        FogColor = new Color(0.045f, 0.036f, 0.030f),
        FogStart = 2.6f,              // pulls the background in close
        FogEnd = 16f,
        FocusIntensity = 2.6f,        // table is the brightest thing in view
    };

    [Tooltip("Seconds for a full mood change. Reversible at any point.")]
    public float FadeSeconds = 1.4f;

    readonly List<Color> _baseColors = new List<Color>();
    readonly List<float> _baseIntensities = new List<float>();

    float _focusBaseIntensity;
    float _t;          // 0 = bar, 1 = seated
    float _target;

    void Awake()
    {
        // Cache what the builder authored, so moods scale the original values
        // rather than compounding each time the mood changes.
        for (int i = 0; i < RoomLights.Count; i++)
        {
            Light l = RoomLights[i];
            _baseColors.Add(l != null ? l.color : Color.white);
            _baseIntensities.Add(l != null ? l.intensity : 0f);
        }

        _focusBaseIntensity = FocusLight != null ? FocusLight.intensity : 1f;

        if (FocusLight != null)
        {
            FocusLight.intensity = 0f;
            FocusLight.renderMode = LightRenderMode.ForceVertex;
        }

        Apply(0f);
    }

    /// <summary>Seated when true, bar when false. Safe to call every frame.</summary>
    public void SetSeated(bool seated)
    {
        _target = seated ? 1f : 0f;
    }

    /// <summary>0 = bar, 1 = fully seated. Useful for tests and tooling.</summary>
    public float Blend => _t;

    void Update()
    {
        if (Mathf.Approximately(_t, _target))
            return;

        // Constant-rate move toward the target, so interrupting mid-fade
        // reverses at the same speed instead of snapping or easing oddly.
        float step = Time.deltaTime / Mathf.Max(0.05f, FadeSeconds);
        _t = Mathf.MoveTowards(_t, _target, step);
        Apply(_t);
    }

    void Apply(float t)
    {
        Color tint = Color.Lerp(BarMood.WarmTint, SeatedMood.WarmTint, t);
        float roomIntensity = Mathf.Lerp(BarMood.RoomIntensity, SeatedMood.RoomIntensity, t);
        float warmth = Mathf.Lerp(BarMood.Warmth, SeatedMood.Warmth, t);

        for (int i = 0; i < RoomLights.Count; i++)
        {
            Light l = RoomLights[i];
            if (l == null || l == FocusLight)
                continue;

            l.color = Color.Lerp(_baseColors[i], tint, warmth);
            l.intensity = _baseIntensities[i] * roomIntensity;
        }

        RenderSettings.ambientSkyColor = Color.Lerp(BarMood.Ambient, SeatedMood.Ambient, t);
        RenderSettings.ambientEquatorColor = RenderSettings.ambientSkyColor * 0.8f;
        RenderSettings.ambientGroundColor = RenderSettings.ambientSkyColor * 0.5f;

        RenderSettings.fogColor = Color.Lerp(BarMood.FogColor, SeatedMood.FogColor, t);
        RenderSettings.fogStartDistance = Mathf.Lerp(BarMood.FogStart, SeatedMood.FogStart, t);
        RenderSettings.fogEndDistance = Mathf.Lerp(BarMood.FogEnd, SeatedMood.FogEnd, t);

        if (FocusLight != null)
        {
            float focus = Mathf.Lerp(BarMood.FocusIntensity, SeatedMood.FocusIntensity, t);
            FocusLight.intensity = _focusBaseIntensity * focus;

            // Swap pixel-light budget rather than growing it: once the focus
            // light is doing real work it becomes the pixel light, and a
            // distant pendant drops to vertex to pay for it.
            bool focusIsPixel = t > 0.35f;
            FocusLight.renderMode = focusIsPixel
                ? LightRenderMode.ForcePixel : LightRenderMode.ForceVertex;

            if (DemotableLight != null)
            {
                DemotableLight.renderMode = focusIsPixel
                    ? LightRenderMode.ForceVertex : LightRenderMode.ForcePixel;
            }
        }
    }
}
