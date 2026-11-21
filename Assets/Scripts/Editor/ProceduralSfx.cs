using System.IO;
using UnityEditor;
using UnityEngine;

// Makes the "punchy cartoon" sounds no free pack had: whooshes, magic launches, projectile travel loops, sparkle
// tails, sub thumps, charge-ups, chimes, pops, wind, a dizzy stun and a soft space ambience. Written as WAVs into
// Assets/Audio/SFX/Generated (names start with gen_). Original sounds, so no licence to worry about.
// Runs once automatically if the folder is missing, or from Stratafield > Audio > Generate Sounds
[InitializeOnLoad]
public static class ProceduralSfx
{
    const string Folder = "Assets/Audio/SFX/Generated";
    const int Rate = 44100;

    static ProceduralSfx()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !AssetDatabase.IsValidFolder(Folder)) Generate();
        };
    }

    [MenuItem("Stratafield/Audio/Generate Sounds")]
    static void Generate()
    {
        Directory.CreateDirectory(Folder);

        for (int i = 0; i < 4; i++) Save($"gen_whoosh_light_{i + 1}", Whoosh(0.2f + i * 0.02f, 600f + i * 120f, 3600f + i * 300f, 100 + i));
        for (int i = 0; i < 3; i++) Save($"gen_whoosh_heavy_{i + 1}", Whoosh(0.34f + i * 0.03f, 260f + i * 40f, 1900f + i * 150f, 200 + i, true));
        for (int i = 0; i < 3; i++) Save($"gen_charge_{i + 1}", Charge(0.24f, 280f + i * 60f, 300 + i));
        for (int i = 0; i < 3; i++) Save($"gen_magic_launch_{i + 1}", MagicLaunch(1500f - i * 150f, 400 + i));
        Save("gen_travel_magic", TravelMagic());
        Save("gen_travel_air", TravelAir());
        for (int i = 0; i < 4; i++) Save($"gen_sparkle_{i + 1}", Sparkle(0.6f, 500 + i));
        for (int i = 0; i < 2; i++) Save($"gen_thump_{i + 1}", Thump(115f - i * 15f, 600 + i));
        for (int i = 0; i < 3; i++) Save($"gen_snap_{i + 1}", Snap(700 + i));
        for (int i = 0; i < 3; i++) Save($"gen_chime_up_{i + 1}", Chime(true, 800 + i));
        for (int i = 0; i < 3; i++) Save($"gen_chime_down_{i + 1}", Chime(false, 900 + i));
        for (int i = 0; i < 3; i++) Save($"gen_heal_{i + 1}", Heal(1000 + i));
        for (int i = 0; i < 3; i++) Save($"gen_pop_{i + 1}", Pop(700f + i * 120f, 1100 + i));
        for (int i = 0; i < 2; i++) Save($"gen_wind_{i + 1}", Wind(1200 + i));
        Save("gen_beam", Beam());
        Save("gen_dizzy", Dizzy());
        Save("gen_poof", Poof(1300));
        Save("gen_ambient_space", Ambient());

        AssetDatabase.Refresh();
        AudioSetup.Fill(false); // Put the new sounds into the library
        Debug.Log("Generated sounds in " + Folder);
    }

    // ---- Sounds ----

    // Band-passed noise sweeping up then down, loudest just before the end (moving toward the hit)
    static float[] Whoosh(float length, float low, float high, int seed, bool heavy = false)
    {
        float[] s = Buffer(length);
        Filter filter = new Filter();
        System.Random rng = new System.Random(seed);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)s.Length;
            float shape = t < 0.7f ? Mathf.Pow(t / 0.7f, 2f) : 1f - (t - 0.7f) / 0.3f;
            float cutoff = Mathf.Lerp(low, high, Mathf.Sin(Mathf.Clamp01(t / 0.8f) * Mathf.PI * 0.5f)) * (t > 0.8f ? Mathf.Lerp(1f, 0.6f, (t - 0.8f) / 0.2f) : 1f);
            float v = filter.Band(Noise(rng), cutoff, 1.4f) * shape;
            if (heavy) v += Mathf.Sin(2f * Mathf.PI * (90f - 30f * t) * i / Rate) * 0.25f * shape;
            s[i] = v;
        }
        return Normalize(s, 0.8f);
    }

    // Rising swell of noise and tone that stops just short of the end, leaving a tiny gap before the release
    static float[] Charge(float length, float baseFrequency, int seed)
    {
        float[] s = Buffer(length);
        Filter filter = new Filter();
        System.Random rng = new System.Random(seed);
        double phase = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)s.Length;
            float env = t < 0.85f ? Mathf.Pow(t / 0.85f, 1.6f) : Mathf.Max(0f, 1f - (t - 0.85f) / 0.05f);
            float frequency = baseFrequency * Mathf.Lerp(1f, 3.2f, t * t) * (1f + 0.02f * Mathf.Sin(2f * Mathf.PI * 9f * i / Rate));
            phase += frequency / Rate;
            float tone = Mathf.Sin((float)(2 * System.Math.PI * phase)) + 0.3f * Mathf.Sin((float)(4 * System.Math.PI * phase));
            float air = filter.Band(Noise(rng), Mathf.Lerp(400f, 5000f, t), 1.2f);
            s[i] = (tone * 0.35f + air * 0.8f) * env;
        }
        return Normalize(s, 0.75f);
    }

    // Soft "fwip": a quick whoosh, a falling chirp with harmonics and a couple of sparkles
    static float[] MagicLaunch(float startFrequency, int seed)
    {
        float[] whoosh = Whoosh(0.16f, 900f, 4200f, seed);
        float[] s = Buffer(0.32f);
        double phase = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Exp(-t * 14f) * Mathf.Clamp01(t / 0.004f);
            phase += Mathf.Lerp(startFrequency, startFrequency * 0.45f, Mathf.Clamp01(t / 0.18f)) / Rate;
            float tone = Mathf.Sin((float)(2 * System.Math.PI * phase)) + 0.35f * Mathf.Sin((float)(6 * System.Math.PI * phase));
            s[i] = tone * env * 0.5f + (i < whoosh.Length ? whoosh[whoosh.Length - 1 - i] * 0.6f : 0f); // Reversed whoosh = sucks in, then out
        }
        Mix(s, Sparkle(0.3f, seed + 50), 0.03f, 0.35f);
        return Normalize(s, 0.8f);
    }

    // Shimmering tone cluster with a flutter, seamless 1 second loop, follows magic projectiles
    static float[] TravelMagic()
    {
        float[] s = Buffer(1f);
        float[] notes = { 880f, 1320f, 1760f, 2640f };
        System.Random rng = new System.Random(77);
        Filter filter = new Filter();
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float v = 0f;
            for (int n = 0; n < notes.Length; n++) v += Mathf.Sin(2f * Mathf.PI * notes[n] * t) / (n + 1.5f);
            float flutter = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 14f * t);
            s[i] = v * flutter * 0.5f + filter.Band(Noise(rng), 3200f, 2f) * 0.35f;
        }
        return Normalize(LoopFade(s), 0.55f);
    }

    // Airy rush for arrows, hooks and other physical projectiles, seamless 1 second loop
    static float[] TravelAir()
    {
        float[] s = Buffer(1f);
        System.Random rng = new System.Random(78);
        Filter filter = new Filter();
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float wobble = 1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 6f * t);
            s[i] = filter.Band(Noise(rng), 1300f * wobble, 1.6f) * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 3f * t));
        }
        return Normalize(LoopFade(s), 0.5f);
    }

    // Little bell pings on a pentatonic scale, scattered and fading: the magical "tail" on spells and heals
    static float[] Sparkle(float length, int seed)
    {
        float[] s = Buffer(length);
        System.Random rng = new System.Random(seed);
        float[] scale = { 1f, 1.125f, 1.25f, 1.5f, 1.6667f, 2f, 2.25f, 2.5f };
        int pings = 6;
        for (int p = 0; p < pings; p++)
        {
            float start = p * length * 0.12f + (float)rng.NextDouble() * 0.03f;
            float frequency = 2093f * scale[rng.Next(scale.Length)];
            float loud = 1f - p / (float)pings * 0.6f;
            int from = (int)(start * Rate);
            for (int i = from; i < s.Length; i++)
            {
                float t = (i - from) / (float)Rate;
                float env = Mathf.Exp(-t * 18f) * Mathf.Clamp01(t / 0.002f);
                if (t > 0.01f && env < 0.001f) break; // Faded out (after the attack)
                s[i] += (Mathf.Sin(2f * Mathf.PI * frequency * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * frequency * 2.76f * t)) * env * loud * 0.4f;
            }
        }
        return Normalize(s, 0.6f);
    }

    // Deep sine that drops in pitch with a click on top: the weight under big hits
    static float[] Thump(float startFrequency, int seed)
    {
        float[] s = Buffer(0.4f);
        System.Random rng = new System.Random(seed);
        double phase = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            phase += (40f + (startFrequency - 40f) * Mathf.Exp(-t * 25f)) / Rate;
            float body = Mathf.Sin((float)(2 * System.Math.PI * phase)) * Mathf.Exp(-t * 9f);
            float click = Noise(rng) * Mathf.Exp(-t * 300f) * 0.5f;
            s[i] = Mathf.Clamp(body * 1.4f, -1f, 1f) + click;
        }
        return Normalize(s, 0.9f);
    }

    // Very short bright crack to sharpen the start of an impact
    static float[] Snap(int seed)
    {
        float[] s = Buffer(0.07f);
        System.Random rng = new System.Random(seed);
        Filter filter = new Filter();
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            s[i] = filter.High(Noise(rng), 2500f) * Mathf.Exp(-t * 70f) + Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-t * 120f) * 0.4f;
        }
        return Normalize(s, 0.85f);
    }

    // Three or four bell notes going up (buff) or down with a wobble (debuff)
    static float[] Chime(bool up, int seed)
    {
        float[] s = Buffer(0.75f);
        System.Random rng = new System.Random(seed);
        float root = up ? 784f * (1f + (float)rng.NextDouble() * 0.06f) : 1046f;
        float[] steps = up ? new[] { 1f, 1.25f, 1.5f, 2f } : new[] { 1f, 0.84f, 0.71f, 0.6f };
        for (int n = 0; n < steps.Length; n++)
        {
            int from = (int)(n * 0.06f * Rate);
            float frequency = root * steps[n];
            for (int i = from; i < s.Length; i++)
            {
                float t = (i - from) / (float)Rate;
                float bend = up ? 1f : 1f - 0.04f * t; // Debuff notes sag
                float env = Mathf.Exp(-t * (up ? 7f : 9f)) * Mathf.Clamp01(t / 0.003f);
                if (t > 0.01f && env < 0.001f) break; // Faded out (after the attack)
                float f = frequency * bend;
                s[i] += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * f * 3f * t)) * env * 0.35f;
            }
        }
        return Normalize(s, 0.7f);
    }

    // Warm rising shimmer with soft chimes (stands in until the healing pack is added)
    static float[] Heal(int seed)
    {
        float[] s = Buffer(0.9f);
        System.Random rng = new System.Random(seed);
        Filter filter = new Filter();
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)s.Length;
            float env = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.3f);
            s[i] = filter.Band(Noise(rng), Mathf.Lerp(1500f, 6000f, t), 3f) * env * 0.5f;
        }
        Mix(s, Chime(true, seed + 3), 0.02f, 0.7f);
        Mix(s, Sparkle(0.6f, seed + 9), 0.25f, 0.5f);
        return Normalize(s, 0.7f);
    }

    // Cartoon bubble pop: a fast falling blip
    static float[] Pop(float startFrequency, int seed)
    {
        float[] s = Buffer(0.12f);
        double phase = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            phase += Mathf.Lerp(startFrequency, startFrequency * 0.3f, Mathf.Clamp01(t / 0.05f)) / Rate;
            s[i] = Mathf.Sin((float)(2 * System.Math.PI * phase)) * Mathf.Exp(-t * 45f) * Mathf.Clamp01(t / 0.002f);
        }
        return Normalize(s, 0.75f);
    }

    // A gust: resonant noise that swells and flutters
    static float[] Wind(int seed)
    {
        float[] s = Buffer(0.65f);
        System.Random rng = new System.Random(seed);
        Filter filter = new Filter();
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)s.Length;
            float env = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 1.3f);
            float flutter = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 17f * i / Rate);
            float cutoff = Mathf.Lerp(450f, 1600f, Mathf.Sin(t * Mathf.PI));
            s[i] = filter.Band(Noise(rng), cutoff, 4f) * env * flutter;
        }
        return Normalize(s, 0.8f);
    }

    // Sustained glowing beam: detuned tone cluster plus hiss, swelling then fading
    static float[] Beam()
    {
        float[] s = Buffer(0.7f);
        System.Random rng = new System.Random(55);
        Filter filter = new Filter();
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate, u = i / (float)s.Length;
            float env = Mathf.Clamp01(u / 0.08f) * Mathf.Clamp01((1f - u) / 0.35f);
            float v = Mathf.Sin(2f * Mathf.PI * 440f * t) + Mathf.Sin(2f * Mathf.PI * 443f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 880f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 1320f * t);
            s[i] = (v * 0.3f + filter.Band(Noise(rng), 2500f, 1.5f) * 0.5f) * env;
        }
        Mix(s, Sparkle(0.5f, 56), 0.1f, 0.4f);
        return Normalize(s, 0.75f);
    }

    // Cartoon dizzy birds: alternating high wobbly tweets
    static float[] Dizzy()
    {
        float[] s = Buffer(0.9f);
        for (int n = 0; n < 5; n++)
        {
            int from = (int)(n * 0.16f * Rate);
            float frequency = n % 2 == 0 ? 2400f : 2900f;
            double phase = 0;
            for (int i = from; i < s.Length && i < from + (int)(0.12f * Rate); i++)
            {
                float t = (i - from) / (float)Rate;
                phase += frequency * (1f + 0.08f * Mathf.Sin(2f * Mathf.PI * 30f * t)) / Rate;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.12f) * Mathf.PI);
                s[i] += Mathf.Sin((float)(2 * System.Math.PI * phase)) * env * 0.4f;
            }
        }
        return Normalize(s, 0.6f);
    }

    // Puff of smoke: low noise burst that darkens as it fades
    static float[] Poof(int seed)
    {
        float[] s = Buffer(0.5f);
        System.Random rng = new System.Random(seed);
        Filter filter = new Filter();
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            s[i] = filter.Low(Noise(rng), Mathf.Lerp(2500f, 300f, Mathf.Clamp01(t / 0.4f))) * Mathf.Exp(-t * 7f) * Mathf.Clamp01(t / 0.01f);
        }
        return Normalize(s, 0.8f);
    }

    // Quiet starry room tone: a slow low pad, soft rumble and rare twinkles. Seamless 24 second loop
    static float[] Ambient()
    {
        const float length = 24f;
        float[] s = Buffer(length);
        System.Random rng = new System.Random(99);
        Filter filter = new Filter();
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            // Frequencies and LFOs all fit a whole number of cycles in 24 s so the loop is seamless
            float pad = Mathf.Sin(2f * Mathf.PI * 55f * t) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / 24f))
                      + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 12f + 1f)) * 0.6f
                      + Mathf.Sin(2f * Mathf.PI * 110.25f * t) * 0.25f;
            s[i] = pad * 0.25f + filter.Low(Noise(rng), 220f) * 0.9f;
        }
        s = LoopFade(s, 1.5f);
        for (float at = 1.7f; at < length - 2f; at += 2.3f + (float)rng.NextDouble() * 1.8f)
            Mix(s, Sparkle(0.6f, (int)(at * 100)), at, 0.12f);
        return Normalize(s, 0.35f);
    }

    // ---- Tools ----

    // State variable filter (one sample at a time)
    class Filter
    {
        float low, band;
        float Step(float input, float cutoff, float q)
        {
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Clamp(cutoff, 20f, Rate * 0.2f) / Rate);
            float high = input - low - band / q;
            band += f * high;
            low += f * band;
            return high;
        }
        public float Band(float input, float cutoff, float q) { Step(input, cutoff, q); return band; }
        public float Low(float input, float cutoff) { Step(input, cutoff, 0.7f); return low; }
        public float High(float input, float cutoff) => Step(input, cutoff, 0.7f);
    }

    static float Noise(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);
    static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

    // Adds another sound in at a time offset
    static void Mix(float[] into, float[] add, float at, float volume)
    {
        int from = (int)(at * Rate);
        for (int i = 0; i < add.Length && from + i < into.Length; i++) into[from + i] += add[i] * volume;
    }

    // Crossfades the end into the start so the clip loops without a click
    static float[] LoopFade(float[] s, float seconds = 0.1f)
    {
        int fade = Mathf.Min(s.Length / 3, (int)(seconds * Rate));
        float[] result = new float[s.Length - fade];
        for (int i = 0; i < result.Length; i++) result[i] = s[i];
        for (int i = 0; i < fade; i++)
        {
            float w = i / (float)fade;
            result[i] = s[i] * w + s[result.Length + i] * (1f - w);
        }
        return result;
    }

    static float[] Normalize(float[] s, float peak)
    {
        float max = 0.0001f;
        foreach (float v in s) max = Mathf.Max(max, Mathf.Abs(v));
        for (int i = 0; i < s.Length; i++) s[i] = s[i] / max * peak;
        // Tiny fade at both ends so nothing clicks
        int edge = Mathf.Min(64, s.Length / 4);
        for (int i = 0; i < edge; i++) { float w = i / (float)edge; s[s.Length - 1 - i] *= w; }
        return s;
    }

    // 16-bit mono WAV
    static void Save(string name, float[] samples)
    {
        using (FileStream file = new FileStream(Path.Combine(Folder, name + ".wav"), FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(file))
        {
            int bytes = samples.Length * 2;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + bytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(Rate);
            writer.Write(Rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(bytes);
            foreach (float v in samples) writer.Write((short)(Mathf.Clamp(v, -1f, 1f) * 32767f));
        }
    }
}
