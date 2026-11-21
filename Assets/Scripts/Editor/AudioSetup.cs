using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Sets up sound effects:
// - Creates Resources/Audio/SfxLibrary with every cue the game plays, and fills any EMPTY cue with clips from
//   Assets/Audio/SFX picked by file name (Kenney and lentikula names). Runs after scripts reload and from
//   Stratafield > Audio > Fill Sound Library. Cues you've filled by hand are never touched
// - Stratafield > Audio > Add Music + SFX Sliders copies the settings panel's volume slider in the open scene
[InitializeOnLoad]
public static class AudioSetup
{
    const string LibraryPath = "Assets/Resources/Audio/SfxLibrary.asset";
    const string SfxFolder = "Assets/Audio/SFX";

    // Bump this when the cue list changes meaning: every cue is cleared and refilled once
    const int SetupVersion = 2;

    // Cue, volume, file name patterns (first pattern that finds clips wins). Regex, case-insensitive.
    // GameSfx builds attacks out of these raw sounds (see its recipes)
    static readonly (string id, float volume, string[] patterns)[] Cues =
    {
        // UI
        ("ui.hover", 0.3f,  new[] { @"^rollover\d", @"^tick_00" }),
        ("ui.click", 0.5f,  new[] { @"^click_00", @"^click\d" }),
        ("ui.select", 0.55f, new[] { @"^select_00", @"^switch_00" }),
        ("ui.back", 0.45f,  new[] { @"^back_00", @"^close_00" }),
        ("ui.error", 0.5f,  new[] { @"^error_00" }),
        ("ui.endTurn", 0.6f, new[] { @"^confirmation_00" }),
        ("ui.slider", 0.3f, new[] { @"^tick_00", @"^scroll_00" }),

        // Cards
        ("card.hover", 0.2f,   new[] { @"^cardSlide[1-4]" }),
        ("card.pick", 0.6f,    new[] { @"^cardSlide[5-8]", @"^cardFan" }),
        ("card.place", 0.7f,   new[] { @"^cardPlace" }),
        ("card.deal", 0.45f,   new[] { @"^cardSlide" }),
        ("card.gather", 0.6f,  new[] { @"^cardFan", @"^cardShove" }),
        ("card.split", 0.55f,  new[] { @"^cardShove" }),
        ("card.shuffle", 0.8f, new[] { @"^cardShuffle" }),
        ("card.inspect", 0.6f, new[] { @"^cardFan", @"^bookFlip" }),
        ("card.inspectClose", 0.45f, new[] { @"^cardSlide", @"^bookClose" }),

        // Game flow
        ("turn.start", 0.45f,    new[] { @"^bong_00", @"^pluck_00" }),
        ("banner", 0.55f,        new[] { @"^jingles_HIT", @"^jingles_PIZZI" }),
        ("banner.battle", 0.7f,  new[] { @"^jingles_STEEL0[0-4]", @"^jingles_HIT" }),
        ("victory", 0.8f,        new[] { @"^jingles_PIZZI0[0-3]", @"^jingles_NES" }),
        ("ambient", 1f,          new[] { @"^gen_ambient" }),

        // Made by ProceduralSfx (the cartoon palette)
        ("whoosh.light", 0.7f,  new[] { @"^gen_whoosh_light" }),
        ("whoosh.heavy", 0.75f, new[] { @"^gen_whoosh_heavy" }),
        ("charge", 0.6f,        new[] { @"^gen_charge" }),
        ("magic.launch", 0.7f,  new[] { @"^gen_magic_launch" }),
        ("travel.magic", 0.6f,  new[] { @"^gen_travel_magic" }),
        ("travel.air", 0.6f,    new[] { @"^gen_travel_air" }),
        ("sparkle", 0.55f,      new[] { @"^gen_sparkle" }),
        ("thump", 0.8f,         new[] { @"^gen_thump" }),
        ("snap", 0.6f,          new[] { @"^gen_snap" }),
        ("chime.up", 0.6f,      new[] { @"^gen_chime_up" }),
        ("chime.down", 0.6f,    new[] { @"^gen_chime_down" }),
        ("heal", 0.75f,         new[] { @"^healing", @"heal.*impact", @"^gen_heal" }), // The healing pack once it's in, else the made one
        ("pop", 0.6f,           new[] { @"^gen_pop" }),
        ("wind", 0.7f,          new[] { @"^gen_wind" }),
        ("beam", 0.7f,          new[] { @"^gen_beam" }),
        ("dizzy", 0.6f,         new[] { @"^gen_dizzy" }),
        ("poof", 0.7f,          new[] { @"^gen_poof" }),

        // Kenney impacts and foley
        ("punch.medium", 0.8f, new[] { @"^impactPunch_medium" }),
        ("punch.heavy", 0.9f,  new[] { @"^impactPunch_heavy" }),
        ("soft.medium", 0.75f, new[] { @"^impactSoft_medium" }),
        ("soft.heavy", 0.8f,   new[] { @"^impactSoft_heavy" }),
        ("slice", 0.85f,       new[] { @"^knifeSlice" }),
        ("knife.draw", 0.6f,   new[] { @"^drawKnife" }),
        ("metal.light", 0.75f, new[] { @"^impactMetal_light" }),
        ("metal.heavy", 0.8f,  new[] { @"^impactMetal_heavy" }),
        ("wood.medium", 0.7f,  new[] { @"^impactWood_medium" }),
        ("wood.heavy", 0.75f,  new[] { @"^impactWood_heavy" }),
        ("glass.heavy", 0.8f,  new[] { @"^impactGlass_heavy" }),
        ("bell", 0.6f,         new[] { @"^impactBell_heavy" }),
        ("crunch", 0.8f,       new[] { @"^explosionCrunch" }),
        ("saw", 0.55f,         new[] { @"^engineCircular" }),
        ("laser.small", 0.45f, new[] { @"^laserSmall" }),

        // lentikula spell impacts
        ("spell.ice", 0.85f,       new[] { @"^Ice Spell" }),
        ("spell.water", 0.85f,     new[] { @"^Water Spell" }),
        ("spell.fire", 0.8f,       new[] { @"^Fire Spell" }),
        ("spell.lightning", 0.8f,  new[] { @"^Lightning Spell" }),

        // Character voices (empty until recorded; drop files with these names into Assets/Audio/SFX)
        ("voice.roar", 0.9f, new[] { @"roar" }),
    };

    // Loops and the ambience keep their own level instead of being evened out
    static bool KeepsLevel(string id) => id == "ambient" || id.StartsWith("travel.");

    static AudioSetup() => EditorApplication.delayCall += () => Fill(false);

    [MenuItem("Stratafield/Audio/Fill Sound Library")]
    static void FillFromMenu() => Fill(true);

    public static void Fill(bool report)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        SfxLibrary library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
        if (library == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            library = ScriptableObject.CreateInstance<SfxLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        // Every clip in the SFX folder by file name
        List<AudioClip> clips = new List<AudioClip>();
        if (AssetDatabase.IsValidFolder(SfxFolder))
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SfxFolder }))
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null) clips.Add(clip);
            }

        bool changed = false;
        int filled = 0;

        // The cue list changed meaning: start fresh once
        if (library.setupVersion < SetupVersion)
        {
            library.cues.Clear();
            library.setupVersion = SetupVersion;
            changed = true;
        }
        // Cues the game no longer uses
        changed |= library.cues.RemoveAll(c => !Cues.Any(k => k.id == c.id)) > 0;

        foreach ((string id, float volume, string[] patterns) in Cues)
        {
            SfxLibrary.Cue cue = library.cues.FirstOrDefault(c => c.id == id);
            if (cue == null)
            {
                cue = new SfxLibrary.Cue { id = id, volume = volume, clips = new AudioClip[0] };
                if (id == "ui.slider" || id == "card.hover") cue.cooldown = 0.07f;
                if (id.StartsWith("banner") || id == "victory" || id == "ambient") cue.pitch = Vector2.one; // Music stings stay in tune
                if (id.StartsWith("chime") || id == "sparkle" || id == "heal") cue.pitch = new Vector2(0.98f, 1.02f); // Musical, so barely nudged
                library.cues.Add(cue);
                changed = true;
            }
            // Already set (maybe by hand). Made stand-ins (gen_) get swapped once a real pack matches an earlier pattern
            bool standIn = cue.clips != null && cue.clips.Length > 0 && cue.clips.All(c => c != null && c.name.StartsWith("gen_"));
            if (cue.clips != null && cue.clips.Length > 0 && !standIn) continue;
            string[] tryPatterns = standIn ? patterns.Where(p => !p.StartsWith("^gen_")).ToArray() : patterns;

            foreach (string pattern in tryPatterns)
            {
                // Ignores - _ and spaces so "impactPunch_medium_000" and "impact-punch-medium-000" both match
                Regex regex = new Regex(pattern.Replace("_", ""), RegexOptions.IgnoreCase);
                Regex raw = new Regex(pattern, RegexOptions.IgnoreCase);
                AudioClip[] found = clips.Where(c => regex.IsMatch(Regex.Replace(c.name, @"[-_ ]", "")) || raw.IsMatch(c.name))
                                         .OrderBy(c => c.name).Take(6).ToArray();
                if (found.Length == 0) continue;
                cue.clips = found;
                changed = true;
                filled++;
                break;
            }
        }

        // Silence to skip and loudness, for any cue whose clips changed
        foreach (SfxLibrary.Cue cue in library.cues)
            if (cue.clips != null && (cue.startTimes == null || cue.startTimes.Length != cue.clips.Length
                                      || cue.gains == null || cue.gains.Length != cue.clips.Length))
            {
                Measure(cue);
                changed = true;
            }

        if (changed)
        {
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }
        if (report)
        {
            int empty = library.cues.Count(c => c.clips == null || c.clips.Length == 0);
            Debug.Log($"Sound library: {clips.Count} clips found in {SfxFolder}, {filled} cues filled, {empty} cues still empty");
            Selection.activeObject = library;
        }
    }

    // ---- Silence at the start of clips ----

    const float SilenceLevel = 0.02f; // Quieter than this counts as silence (about -34 dB)
    const float PreRoll = 0.005f;     // Keep a sliver before the sound so the attack isn't clipped

    const float TargetLoudness = 0.15f; // RMS of a clip's loudest moment that every clip is evened toward

    // Re-measures every clip in the library (silence at the start + loudness) and lists the ones that started late
    [MenuItem("Stratafield/Audio/Measure Sounds (Silence + Loudness)")]
    static void CheckSilence()
    {
        SfxLibrary library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
        if (library == null) { Debug.LogWarning("No sound library yet. Run Fill Sound Library first"); return; }

        List<string> late = new List<string>();
        foreach (SfxLibrary.Cue cue in library.cues)
        {
            if (cue.clips == null) continue;
            Measure(cue);
            for (int i = 0; i < cue.clips.Length; i++)
                if (cue.startTimes[i] > 0f) late.Add($"{cue.id}: {cue.clips[i].name} starts {cue.startTimes[i] * 1000f:0} ms late (now skipped)");
        }
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log(late.Count == 0 ? "No clips with silence at the start" : $"{late.Count} clips had silence at the start:\n" + string.Join("\n", late));
    }

    static void Measure(SfxLibrary.Cue cue)
    {
        cue.startTimes = cue.clips.Select(LeadingSilence).ToArray();
        cue.gains = cue.clips.Select(c => KeepsLevel(cue.id) ? 1f : Gain(c)).ToArray();
    }

    // Volume multiplier that brings the clip's loudest 50 ms to the target (quiet clips up to 1.6x, loud ones down)
    static float Gain(AudioClip clip)
    {
        if (clip == null) return 1f;
        if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        if (clip.loadState != AudioDataLoadState.Loaded) return 1f;

        int channels = Mathf.Max(1, clip.channels);
        int frames = Mathf.Min(clip.samples, clip.frequency * 2);
        float[] data = new float[frames * channels];
        if (!clip.GetData(data, 0)) return 1f;

        int window = Mathf.Max(1, clip.frequency / 20) * channels;
        double sum = 0, loudest = 0;
        for (int i = 0; i < data.Length; i++)
        {
            sum += data[i] * data[i];
            if (i >= window) sum -= data[i - window] * data[i - window];
            if (i >= window - 1) loudest = System.Math.Max(loudest, sum / window);
        }
        if (data.Length < window) loudest = sum / Mathf.Max(1, data.Length);
        float rms = (float)System.Math.Sqrt(System.Math.Max(loudest, 1e-8));
        return Mathf.Clamp(TargetLoudness / rms, 0.25f, 1.6f);
    }

    // Seconds to skip before the clip gets loud. 0 if it starts within 15 ms
    static float LeadingSilence(AudioClip clip)
    {
        if (clip == null) return 0f;
        if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        if (clip.loadState != AudioDataLoadState.Loaded) return 0f;

        int channels = Mathf.Max(1, clip.channels);
        int frames = Mathf.Min(clip.samples, clip.frequency); // Only look at the first second
        float[] data = new float[frames * channels];
        if (!clip.GetData(data, 0)) return 0f;

        for (int i = 0; i < data.Length; i++)
        {
            if (Mathf.Abs(data[i]) < SilenceLevel) continue;
            float seconds = (i / channels) / (float)clip.frequency - PreRoll;
            return seconds > 0.015f ? seconds : 0f;
        }
        return 0f;
    }

    // ---- Volume sliders ----

    [MenuItem("Stratafield/Audio/Add Music + SFX Sliders To Open Scene")]
    static void AddSliders()
    {
        OptionsManager options = Object.FindAnyObjectByType<OptionsManager>(FindObjectsInactive.Include);
        if (options == null || options.masterVolumeSlider == null)
        {
            EditorUtility.DisplayDialog("Volume Sliders", "No OptionsManager with a Master Volume Slider in this scene.", "OK");
            return;
        }

        RectTransform master = (RectTransform)options.masterVolumeSlider.transform;
        RectTransform label = FindLabel(master);
        float step = master.rect.height + 26f;

        if (options.musicVolumeSlider == null) options.musicVolumeSlider = CopyRow(master, label, "Music", step);
        if (options.sfxVolumeSlider == null) options.sfxVolumeSlider = CopyRow(master, label, "Effects", step * 2f);

        EditorUtility.SetDirty(options);
        EditorSceneManager.MarkSceneDirty(options.gameObject.scene);
        Debug.Log("Added Music and Effects sliders under the volume slider. Move them where you want, then save the scene");
    }

    // The text next to the slider (same height, to its left)
    static RectTransform FindLabel(RectTransform slider)
    {
        RectTransform best = null;
        float bestGap = 40f;
        foreach (Transform sibling in slider.parent)
        {
            if (sibling == slider || sibling.GetComponent<TMP_Text>() == null) continue;
            RectTransform rect = (RectTransform)sibling;
            float gap = Mathf.Abs(rect.anchoredPosition.y - slider.anchoredPosition.y);
            if (gap < bestGap && rect.anchoredPosition.x < slider.anchoredPosition.x) { best = rect; bestGap = gap; }
        }
        return best;
    }

    static Slider CopyRow(RectTransform master, RectTransform label, string name, float down)
    {
        GameObject slider = Object.Instantiate(master.gameObject, master.parent);
        slider.name = name + " Slider";
        ((RectTransform)slider.transform).anchoredPosition = master.anchoredPosition - new Vector2(0f, down);
        Undo.RegisterCreatedObjectUndo(slider, "Add " + name + " Slider");

        if (label != null)
        {
            GameObject text = Object.Instantiate(label.gameObject, label.parent);
            text.name = name;
            ((RectTransform)text.transform).anchoredPosition = label.anchoredPosition - new Vector2(0f, down);
            text.GetComponent<TMP_Text>().text = name;
            Undo.RegisterCreatedObjectUndo(text, "Add " + name + " Label");
        }

        Slider copy = slider.GetComponent<Slider>();
        copy.onValueChanged = new Slider.SliderEvent(); // Don't keep the master slider's scene listeners
        copy.value = 1f;
        return copy;
    }
}
