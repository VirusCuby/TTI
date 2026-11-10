using UnityEngine;

/// <summary>
/// Sonidos sintetizados por código (no hay archivos de audio): clic al tocar, acierto,
/// zumbido de cortocircuito y "zap" de LED quemado. Cada clip se genera una sola vez.
/// </summary>
public static class CircuitSounds
{
    private const int SampleRate = 22050;
    private static AudioClip click, success, zap, burn;

    public static void Click()   => Play(ref click, MakeClick, 0.35f);
    public static void Success() => Play(ref success, MakeSuccess, 0.5f);
    public static void Zap()     => Play(ref zap, MakeZap, 0.45f);
    public static void Burn()    => Play(ref burn, MakeBurn, 0.5f);

    private static void Play(ref AudioClip clip, System.Func<AudioClip> make, float volume)
    {
        if (!AppSettings.SoundEnabled) return;
        if (clip == null) clip = make();

        Camera cam = Camera.main;
        Vector3 at = cam != null ? cam.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(clip, at, volume);
    }

    private static AudioClip Build(string name, float seconds, System.Func<float, float> sample)
    {
        int n = Mathf.Max(1, (int)(SampleRate * seconds));
        var data = new float[n];
        for (int i = 0; i < n; i++)
            data[i] = Mathf.Clamp(sample(i / (float)SampleRate), -1f, 1f);

        var clip = AudioClip.Create(name, n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip MakeClick()
    {
        var rng = new System.Random(7);
        return Build("clic", 0.06f, t =>
        {
            float env = Mathf.Exp(-t * 90f);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            return (noise * 0.5f + Mathf.Sin(2f * Mathf.PI * 1200f * t) * 0.5f) * env;
        });
    }

    private static AudioClip MakeSuccess()
    {
        // Tres notas ascendentes (do, mi, sol).
        float[] freqs = { 523.25f, 659.25f, 783.99f };
        return Build("acierto", 0.6f, t =>
        {
            int k = Mathf.Min((int)(t / 0.17f), 2);
            float local = t - k * 0.17f;
            float env = Mathf.Exp(-local * 7f) * Mathf.Clamp01(local * 400f);
            return Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * env * 0.7f;
        });
    }

    private static AudioClip MakeZap()
    {
        var rng = new System.Random(11);
        return Build("zap", 0.35f, t =>
        {
            float env = Mathf.Exp(-t * 7f);
            float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 110f * t)); // onda cuadrada grave
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            return (buzz * 0.35f + noise * 0.45f) * env;
        });
    }

    private static AudioClip MakeBurn()
    {
        var rng = new System.Random(3);
        return Build("quemado", 0.5f, t =>
        {
            float env = Mathf.Exp(-t * 5f);
            float freq = Mathf.Lerp(900f, 90f, Mathf.Clamp01(t / 0.5f)); // silbido que cae
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            return (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f + noise * 0.4f) * env;
        });
    }
}
