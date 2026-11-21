using System.Collections.Generic;
using UnityEngine;

// Every sound effect in the game, by name (e.g. "hit.Slash", "card.shuffle", "ui.click"). Lives at
// Resources/Audio/SfxLibrary. Scripts/Editor/AudioSetup fills it from the clips in Assets/Audio/SFX by file name,
// and anything can be changed by hand in the inspector afterwards (the setup only fills empty cues)
[CreateAssetMenu(fileName = "SfxLibrary", menuName = "Stratafield/Sfx Library")]
public class SfxLibrary : ScriptableObject
{
    [System.Serializable]
    public class Cue
    {
        public string id;
        public AudioClip[] clips;                         // One is picked at random, never the same twice in a row
        [Range(0f, 1f)] public float volume = 0.8f;
        public Vector2 pitch = new Vector2(0.94f, 1.06f); // Random pitch range so repeats don't sound robotic
        public float cooldown = 0.04f;                    // Same cue can't stack up faster than this
        [HideInInspector] public float[] startTimes;      // Seconds of silence to skip at the start of each clip (set by AudioSetup)
        [HideInInspector] public float[] gains;           // Evens out loud and quiet clips (set by AudioSetup)

        public float StartTime(int index) => startTimes != null && startTimes.Length == clips.Length ? startTimes[index] : 0f;
        public float Gain(int index) => gains != null && gains.Length == clips.Length ? gains[index] : 1f;
    }

    public List<Cue> cues = new List<Cue>();
    [HideInInspector] public int setupVersion; // AudioSetup refills everything once when its cue list changes a lot

    private Dictionary<string, Cue> lookup;

    public Cue Find(string id)
    {
        if (lookup == null || lookup.Count != cues.Count)
        {
            lookup = new Dictionary<string, Cue>();
            foreach (Cue cue in cues)
                if (cue != null && !string.IsNullOrEmpty(cue.id)) lookup[cue.id] = cue;
        }
        return lookup.TryGetValue(id, out Cue found) ? found : null;
    }

    void OnValidate() => lookup = null;
}
