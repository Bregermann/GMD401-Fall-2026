
using UnityEngine;

public class SoundManager : MonoBehaviour
{
   public static SoundManager Instance;

    private static float savedMasterVolume = 1f;
    private static bool volumeInitialized = false;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource healingSource;
    public AudioSource rangedSource;
    public AudioSource meleeSource;
    public AudioSource magicSource;

    public float masterVolume = 1f;

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
        volumeInitialized = true;
    }

    masterVolume = savedMasterVolume;
    ApplyVolume();
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

    private void ApplyVolume()
    {
        AudioListener.volume = masterVolume;
    }
}
