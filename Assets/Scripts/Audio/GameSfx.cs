using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Plays sound effects by name from the SfxLibrary through SoundManager, in the "punchy cartoon" style:
// - Attacks are built from LAYERS: a wind-up (whoosh for melee, charge for magic/ranged), a launch and a travel loop
//   that follows the projectile across the screen, then the impact = a sharp transient + the theme's body + a tail
//   (sparkle, debris), each a few ms apart. Big hits add a low thump and dip the ambience.
// - Every theme has a recipe below; signature moves have their own recipe instead of their theme's impact.
// - Sounds pan left/right with where the card is on screen, clips get a random pick (no repeats) and a pitch nudge,
//   and each clip's loudness is evened out (AudioSetup measures it). Missing cues are just silent
public static class GameSfx
{
    // ---- Recipes ----

    public struct Layer
    {
        public string cue; public float delay, volume;
        public Layer(string cue, float delay, float volume) { this.cue = cue; this.delay = delay; this.volume = volume; }
    }

    static Layer L(string cue, float volume = 1f, float delay = 0f) => new Layer(cue, delay, volume);

    class Recipe
    {
        public string whoosh = "whoosh.light";   // Melee lunge
        public string cast = "charge";           // Ranged, magic and self moves winding up
        public string launch = "magic.launch";   // Projectile leaving
        public string travel = "travel.air";     // Loop that follows the projectile
        public Layer[] impact;
    }

    static readonly Dictionary<VfxTheme, Recipe> themes = new Dictionary<VfxTheme, Recipe>
    {
        { VfxTheme.Default, new Recipe { impact = new[] { L("snap", 0.4f), L("punch.medium") } } },
        { VfxTheme.Slash,  new Recipe { cast = "knife.draw", launch = "whoosh.light", impact = new[] { L("snap", 0.6f), L("slice") } } },
        { VfxTheme.Claw,   new Recipe { launch = "whoosh.light", impact = new[] { L("snap", 0.4f), L("slice", 0.9f), L("soft.medium", 0.7f, 0.01f) } } },
        { VfxTheme.Blunt,  new Recipe { whoosh = "whoosh.heavy", launch = "whoosh.heavy", impact = new[] { L("punch.heavy"), L("wood.heavy", 0.6f, 0.01f), L("thump", 0.7f) } } },
        { VfxTheme.Pierce, new Recipe { cast = "knife.draw", launch = "whoosh.light", impact = new[] { L("snap", 0.6f), L("metal.light", 0.8f) } } },
        { VfxTheme.Ice,    new Recipe { travel = "travel.magic", impact = new[] { L("snap", 0.4f), L("spell.ice"), L("sparkle", 0.5f, 0.04f) } } },
        { VfxTheme.Water,  new Recipe { impact = new[] { L("spell.water"), L("pop", 0.4f, 0.02f) } } },
        { VfxTheme.Wind,   new Recipe { whoosh = "whoosh.heavy", launch = "whoosh.heavy", impact = new[] { L("snap", 0.3f), L("wind", 0.9f), L("whoosh.light", 0.6f) } } },
        { VfxTheme.Light,  new Recipe { travel = "travel.magic", impact = new[] { L("spell.lightning", 0.7f), L("bell", 0.45f, 0.01f), L("sparkle", 0.7f, 0.03f) } } },
        { VfxTheme.Lunar,  new Recipe { travel = "travel.magic", impact = new[] { L("soft.medium", 0.6f), L("sparkle", 0.8f), L("chime.up", 0.35f, 0.02f) } } },
        { VfxTheme.Nature, new Recipe { impact = new[] { L("soft.heavy", 0.7f), L("pop", 0.5f, 0.01f), L("sparkle", 0.4f, 0.04f) } } },
        { VfxTheme.Tech,   new Recipe { whoosh = "whoosh.heavy", cast = "saw", launch = "laser.small", impact = new[] { L("snap", 0.5f), L("metal.heavy", 0.9f), L("thump", 0.4f) } } },
        { VfxTheme.Trick,  new Recipe { travel = "travel.magic", impact = new[] { L("pop", 0.8f), L("poof", 0.4f), L("sparkle", 0.7f, 0.02f) } } },
        { VfxTheme.Food,   new Recipe { launch = "whoosh.light", impact = new[] { L("soft.medium", 0.8f), L("pop", 0.6f, 0.02f) } } },
        { VfxTheme.Rally,  new Recipe { impact = new[] { L("chime.up", 0.8f), L("thump", 0.5f) } } },
        { VfxTheme.Gas,    new Recipe { launch = "wind", impact = new[] { L("wind", 0.6f), L("poof", 0.6f), L("chime.down", 0.3f, 0.05f) } } },
    };

    // Any heal, whatever its theme
    static readonly Layer[] healImpact = { L("heal"), L("sparkle", 0.5f, 0.1f) };

    // Signature moves: played instead of their theme's impact
    static readonly Dictionary<VfxSignature, Layer[]> signatures = new Dictionary<VfxSignature, Layer[]>
    {
        { VfxSignature.Starfall,          new[] { L("thump", 0.8f), L("bell", 0.5f), L("sparkle", 0.8f), L("spell.lightning", 0.4f, 0.02f), L("sparkle", 0.6f, 0.12f), L("sparkle", 0.5f, 0.24f) } },
        { VfxSignature.LunarBeam,         new[] { L("soft.heavy", 0.5f), L("sparkle", 0.8f), L("chime.up", 0.4f) } },
        { VfxSignature.GlacialShatter,    new[] { L("glass.heavy"), L("spell.ice", 0.8f), L("thump", 0.6f), L("sparkle", 0.6f, 0.05f) } },
        { VfxSignature.BuzzsawSpinner,    new[] { L("snap", 0.5f), L("saw", 0.7f), L("metal.heavy", 0.9f), L("metal.heavy", 0.6f, 0.12f) } },
        { VfxSignature.RadiantPunishment, new[] { L("spell.lightning", 0.9f), L("bell", 0.7f), L("thump", 0.7f), L("sparkle", 0.8f, 0.04f) } },
        { VfxSignature.HurricaneSlash,    new[] { L("wind", 0.9f), L("slice", 0.9f), L("whoosh.heavy", 0.7f, 0.05f), L("slice", 0.7f, 0.12f) } },
        { VfxSignature.SweepingTide,      new[] { L("spell.water"), L("wind", 0.4f), L("spell.water", 0.7f, 0.1f) } },
        { VfxSignature.Roar,              new[] { L("voice.roar"), L("thump", 0.9f), L("wind", 0.6f), L("chime.up", 0.6f, 0.05f) } },
        { VfxSignature.GardeniaBloom,     new[] { L("heal"), L("pop", 0.5f, 0.05f), L("sparkle", 0.8f, 0.1f), L("sparkle", 0.5f, 0.25f) } },
        { VfxSignature.AllInSlam,         new[] { L("punch.heavy"), L("thump"), L("wood.heavy", 0.6f, 0.01f), L("crunch", 0.6f, 0.02f) } },
    };

    // Signature moves with their own launch sound
    static readonly Dictionary<VfxSignature, string> signatureLaunch = new Dictionary<VfxSignature, string>
    {
        { VfxSignature.LunarBeam, "beam" },
    };

    // Everything else
    public static readonly Layer[] Buff = { L("chime.up", 0.6f) };
    public static readonly Layer[] Debuff = { L("chime.down", 0.6f) };
    public static readonly Layer[] Bonus = { L("chime.up", 0.5f), L("sparkle", 0.6f) };
    public static readonly Layer[] Execute = { L("crunch"), L("thump", 0.8f) };
    public static readonly Layer[] Defeat = { L("glass.heavy", 0.8f), L("poof", 0.7f), L("chime.down", 0.5f, 0.1f) };
    public static readonly Layer[] Knockback = { L("wood.medium", 0.7f), L("whoosh.light", 0.5f) };
    public static readonly Layer[] Burn = { L("spell.fire", 0.7f) };
    public static readonly Layer[] Poison = { L("chime.down", 0.5f), L("pop", 0.5f, 0.03f) };
    public static readonly Layer[] Regen = { L("heal", 0.7f) };
    public static readonly Layer[] Stun = { L("snap", 0.3f), L("dizzy", 0.8f) };

    // ---- Attacks ----

    static Recipe RecipeFor(AttackData attack) => themes.TryGetValue(attack.vfxTheme, out Recipe recipe) ? recipe : themes[VfxTheme.Default];

    // Wind-up: whoosh into a melee lunge, or a charge for ranged/magic/self moves
    public static void Windup(AttackData attack, bool melee, Vector3 at)
    {
        Recipe recipe = RecipeFor(attack);
        Play(melee ? recipe.whoosh : recipe.cast, 0.8f, PanAt(at));
    }

    // Projectile leaving: the launch sound, then a loop that follows it (stop it with StopLoop). Null if none
    public static AudioSource Launch(AttackData attack, Vector3 at)
    {
        Recipe recipe = RecipeFor(attack);
        bool heal = attack.damageType == DamageType.Heal;
        string launch = signatureLaunch.TryGetValue(attack.vfxSignature, out string own) ? own : heal ? "magic.launch" : recipe.launch;
        Play(launch, 0.75f, PanAt(at));
        if (signatureLaunch.ContainsKey(attack.vfxSignature)) return null; // Beams draw their own travel
        return PlayLoop(heal ? "travel.magic" : recipe.travel, 0.35f, PanAt(at));
    }

    // The moment the attack lands. k (0.6-1.6) is how big the hit is
    public static void Impact(AttackData attack, float k, Vector3 at)
    {
        float volume = Mathf.Lerp(0.75f, 1f, Mathf.InverseLerp(0.6f, 1.6f, k));
        Layer[] layers = signatures.TryGetValue(attack.vfxSignature, out Layer[] own) ? own
                       : attack.damageType == DamageType.Heal ? healImpact : RecipeFor(attack).impact;
        PlayLayers(layers, volume, PanAt(at));

        if (k > 1.3f && attack.damageType != DamageType.Heal)
        {
            bool hasThump = false;
            foreach (Layer layer in layers) hasThump |= layer.cue == "thump";
            if (!hasThump) Play("thump", 0.6f, PanAt(at));
            Runner.Duck(0.45f, 0.5f); // Big hit: the ambience dips for a moment
        }
    }

    // A buff move that hit nothing (Roar, Hibernate): its theme or signature, else a chime
    public static void BuffMove(AttackData attack, Vector3 at)
    {
        Layer[] layers = signatures.TryGetValue(attack.vfxSignature, out Layer[] own) ? own
                       : attack.vfxTheme == VfxTheme.Rally ? RecipeFor(attack).impact : Buff;
        PlayLayers(layers, 1f, PanAt(at));
    }

    // ---- Playing ----

    // Left/right from where this point is on screen (-0.7 to 0.7)
    public static float PanAt(Vector3 world)
    {
        Camera cam = Camera.main;
        if (cam == null) return 0f;
        return Mathf.Clamp((cam.WorldToViewportPoint(world).x - 0.5f) * 1.4f, -0.7f, 0.7f);
    }

    public static void PlayLayers(Layer[] layers, float volumeScale = 1f, float pan = 0f)
    {
        if (layers == null || !Application.isPlaying) return;
        foreach (Layer layer in layers)
        {
            if (layer.delay <= 0f) Play(layer.cue, layer.volume * volumeScale, pan);
            else Runner.After(layer.delay, () => Play(layer.cue, layer.volume * volumeScale, pan));
        }
    }

    // Plays the first of these cues that has clips
    public static void PlayFirst(float volumeScale, params string[] ids)
    {
        foreach (string id in ids)
            if (Has(id)) { Play(id, volumeScale); return; }
    }

    public static void Play(string id, float volumeScale = 1f, float pan = 0f)
    {
        if (!Pick(id, true, out AudioClip clip, out float volume, out float pitch, out float start)) return;
        SoundManager manager = SoundManager.EnsureExists();
        if (manager != null) manager.PlaySfx(clip, volume * volumeScale, pitch, start, pan);
    }

    // A looping sound (projectile travel). Move it with SetPan, stop it with StopLoop
    public static AudioSource PlayLoop(string id, float volumeScale, float pan)
    {
        if (!Pick(id, false, out AudioClip clip, out float volume, out float pitch, out _)) return null;
        return Runner.Loop(clip, volume * volumeScale, pitch, pan);
    }

    public static void SetPan(AudioSource loop, Vector3 world) { if (loop != null) loop.panStereo = PanAt(world); }
    public static void StopLoop(AudioSource loop, float fade = 0.06f) { if (loop != null) Runner.Stop(loop, fade); }

    // The quiet space ambience under the battle
    public static void StartAmbience(float volume = 0.35f)
    {
        if (Pick("ambient", false, out AudioClip clip, out float cueVolume, out _, out _)) Runner.Ambience(clip, cueVolume * volume);
    }

    public static void StopAmbience() { if (runner != null) runner.Ambience(null, 0f); }

    public static bool Has(string id)
    {
        SfxLibrary.Cue cue = Library != null ? Library.Find(id) : null;
        return cue != null && cue.clips != null && cue.clips.Length > 0;
    }

    // ---- Library ----

    static SfxLibrary library;
    static bool loaded;
    static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    static readonly Dictionary<string, int> lastClip = new Dictionary<string, int>();

    static SfxLibrary Library
    {
        get
        {
            if (!loaded) { library = Resources.Load<SfxLibrary>("Audio/SfxLibrary"); loaded = true; }
            return library;
        }
    }

    // Random clip (never the same twice in a row), loudness-evened volume, pitch nudge and silence to skip
    static bool Pick(string id, bool useCooldown, out AudioClip clip, out float volume, out float pitch, out float start)
    {
        clip = null; volume = pitch = 1f; start = 0f;
        if (!Application.isPlaying || Library == null || string.IsNullOrEmpty(id)) return false;
        SfxLibrary.Cue cue = Library.Find(id);
        if (cue == null || cue.clips == null || cue.clips.Length == 0) return false;

        float now = Time.unscaledTime;
        if (useCooldown && lastPlayed.TryGetValue(id, out float last) && now - last < cue.cooldown) return false;
        lastPlayed[id] = now;

        int index = Random.Range(0, cue.clips.Length);
        if (cue.clips.Length > 1 && lastClip.TryGetValue(id, out int previous) && index == previous)
            index = (index + 1 + Random.Range(0, cue.clips.Length - 1)) % cue.clips.Length;
        lastClip[id] = index;

        clip = cue.clips[index];
        if (clip == null) return false;
        volume = cue.volume * cue.Gain(index);
        pitch = Random.Range(cue.pitch.x, cue.pitch.y);
        start = cue.StartTime(index);
        return true;
    }

    // ---- Timing, loops and ambience (a hidden helper object) ----

    static SfxRunner runner;
    static SfxRunner Runner
    {
        get
        {
            if (runner == null)
            {
                GameObject helper = new GameObject("Sfx Runner") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(helper);
                runner = helper.AddComponent<SfxRunner>();
            }
            return runner;
        }
    }

    class SfxRunner : MonoBehaviour
    {
        AudioSource ambience;
        float ambienceVolume, duck = 1f, duckUntil;
        readonly List<(AudioSource source, float volume)> loops = new List<(AudioSource, float)>();

        static float SfxVolume => SoundManager.Instance != null ? SoundManager.Instance.sfxVolume : 1f;

        public void After(float delay, System.Action action) => StartCoroutine(Wait(delay, action));

        IEnumerator Wait(float delay, System.Action action)
        {
            yield return new WaitForSecondsRealtime(delay); // Real time so hit-stop doesn't stretch the layers apart
            action();
        }

        public AudioSource Loop(AudioClip clip, float volume, float pitch, float pan)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.pitch = pitch;
            source.panStereo = pan;
            source.volume = volume * SfxVolume;
            source.time = Random.Range(0f, clip.length * 0.9f);
            source.Play();
            loops.Add((source, volume));
            return source;
        }

        public void Stop(AudioSource source, float fade) => StartCoroutine(FadeOut(source, fade));

        IEnumerator FadeOut(AudioSource source, float fade)
        {
            loops.RemoveAll(l => l.source == source);
            float from = source != null ? source.volume : 0f;
            for (float t = 0f; t < fade && source != null; t += Time.unscaledDeltaTime)
            {
                source.volume = from * (1f - t / fade);
                yield return null;
            }
            if (source != null) Destroy(source);
        }

        public void Ambience(AudioClip clip, float volume)
        {
            if (ambience == null)
            {
                ambience = gameObject.AddComponent<AudioSource>();
                ambience.loop = true;
            }
            ambienceVolume = volume;
            if (ambience.clip == clip) return;
            ambience.clip = clip;
            if (clip != null) ambience.Play(); else ambience.Stop();
        }

        public void Duck(float amount, float seconds)
        {
            duck = Mathf.Min(duck, amount);
            duckUntil = Time.unscaledTime + seconds;
        }

        // Keeps loops and the ambience following the SFX slider, and eases the ambience back after a duck
        void Update()
        {
            if (Time.unscaledTime > duckUntil) duck = Mathf.MoveTowards(duck, 1f, Time.unscaledDeltaTime * 2f);
            if (ambience != null) ambience.volume = ambienceVolume * duck * SfxVolume;
            foreach ((AudioSource source, float volume) in loops)
                if (source != null) source.volume = volume * SfxVolume;
        }
    }
}
