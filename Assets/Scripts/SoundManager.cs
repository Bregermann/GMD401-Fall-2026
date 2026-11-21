
using UnityEngine;

public class SoundManager : MonoBehaviour
{
   public static SoundManager Instance;

    private static float savedMasterVolume = 1f;
    private static bool volumeInitialized = false;
    private static float savedMusicVolume = 1f; // Added to save the music slider between scenes
    private static float savedSfxVolume = 1f;   // Added to save the SFX slider between scenes

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource healingSource;
    public AudioSource rangedSource;
    public AudioSource meleeSource;
    public AudioSource magicSource;

    public float masterVolume = 1f;
    public float musicVolume = 1f; // Added for the music slider
    public float sfxVolume = 1f;   // Added for the SFX slider

    private const int SfxVoices = 12;  // Added: how many sound effects can play at once
    private AudioSource[] sfxPool;
    private int nextVoice;

    private void Awake()
    {
        if (Instance != null && Instance != this)
    {
        Destroy(gameObject);
        return;
    }

    Instance = this;
    DontDestroyOnLoad(gameObject);

    if (!volumeInitialized)
    {
        savedMasterVolume = masterVolume;
        savedMusicVolume = musicVolume;
        savedSfxVolume = sfxVolume;
        volumeInitialized = true;
    }

    masterVolume = savedMasterVolume;
    musicVolume = savedMusicVolume;
    sfxVolume = savedSfxVolume;
    BuildSfxPool();
    ApplyVolume();
    }

    // Added so GameScene still has sound when played on its own (no main menu first)
    public static SoundManager EnsureExists()
    {
        if (Instance == null && Application.isPlaying) new GameObject("SoundManager").AddComponent<SoundManager>();
        return Instance;
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        savedMasterVolume = masterVolume;

        ApplyVolume();
    }

    public float GetMasterVolume()
    {
        return savedMasterVolume;
    }

    // Added to link up with the music slider in OptionsManager
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        savedMusicVolume = musicVolume;
        ApplyVolume();
    }

    public float GetMusicVolume() => savedMusicVolume;

    // Added to link up with the SFX slider in OptionsManager
    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        savedSfxVolume = sfxVolume;
        ApplyVolume();
    }

    public float GetSfxVolume() => savedSfxVolume;

    // Added to play one sound effect (used by GameSfx). Volume is scaled by the SFX slider.
    // startTime skips silence at the start of the clip so it lines up with the hit. pan = left (-1) to right (1)
    public void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f, float startTime = 0f, float pan = 0f)
    {
        if (clip == null || sfxPool == null) return;
        AudioSource voice = sfxPool[nextVoice];
        nextVoice = (nextVoice + 1) % sfxPool.Length;
        voice.clip = clip;
        voice.pitch = pitch;
        voice.panStereo = pan;
        voice.volume = Mathf.Clamp01(volume) * sfxVolume;
        voice.time = Mathf.Clamp(startTime, 0f, clip.length - 0.01f);
        voice.Play();
    }

    private void ApplyVolume()
    {
        AudioListener.volume = masterVolume;

        // Added: music and effect sources follow their own sliders
        if (musicSource != null) musicSource.volume = musicVolume;
        foreach (AudioSource source in new[] { healingSource, rangedSource, meleeSource, magicSource })
            if (source != null) source.volume = sfxVolume;
    }

    // Added: a few 2D sources so several effects can overlap
    private void BuildSfxPool()
    {
        sfxPool = new AudioSource[SfxVoices];
        for (int i = 0; i < SfxVoices; i++)
        {
            sfxPool[i] = gameObject.AddComponent<AudioSource>();
            sfxPool[i].playOnAwake = false;
            sfxPool[i].spatialBlend = 0f;
            sfxPool[i].ignoreListenerPause = true; // Menu clicks still play while paused
        }
    }
}
