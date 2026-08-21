// PerformanceProbe.cs
//
// Logs real render-loop framerate to logcat so we can measure headset
// performance without installing extra tooling:
//
//   adb logcat -s Unity | grep PERF
//
// The Quest compositor always reports the display rate (72 Hz) because it
// reprojects frames the app fails to deliver. That makes compositor stats
// useless for judging our own cost -- this measures the app's actual loop.
//
// Diagnostic only. Remove, or set Enabled = false, before any patient-facing
// build; it is not part of the clinical application.

using UnityEngine;

public class PerformanceProbe : MonoBehaviour
{
    public static bool Enabled = true;

    const float WindowSeconds = 2f;

    float _elapsed;
    int _frames;
    float _worstFrameMs;

    void Update()
    {
        if (!Enabled)
            return;

        float dt = Time.unscaledDeltaTime;
        _elapsed += dt;
        _frames++;

        float ms = dt * 1000f;
        if (ms > _worstFrameMs)
            _worstFrameMs = ms;

        if (_elapsed < WindowSeconds)
            return;

        float avgFps = _frames / _elapsed;
        float avgMs = (_elapsed / _frames) * 1000f;

        // 72 Hz target = 13.9 ms budget. Report headroom against it.
        // Camera height is logged alongside framerate because a wrong eye
        // height is invisible in a screenshot but makes everything unreachable.
        Camera cam = Camera.main;
        string height = cam != null
            ? $" camY={cam.transform.position.y:F2} rigY={transform.position.y:F2}"
            : "";

        Debug.Log($"PERF avg={avgFps:F1}fps ({avgMs:F1}ms) " +
                  $"worstFrame={_worstFrameMs:F1}ms " +
                  $"budget13.9ms {(avgMs <= 13.9f ? "OK" : "OVER")}{height}");

        _elapsed = 0f;
        _frames = 0;
        _worstFrameMs = 0f;
    }
}
