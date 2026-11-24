using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The black hole between the menu and the game, hiding all loading (the old LoadingScene isn't used):
//  1. Swallow: a snapshot of the menu spirals into a growing black hole with a glowing gold ring until it fills the
//     screen. The next scene starts loading in the background straight away
//  2. Covered: the scene switches while the screen is black (only a faint gold glow turns round the edge). This
//     lasts exactly as long as loading takes
//  3. Flash: a gold-white flash
//  4. Close: the hole closes back in on itself while the new scene (rendered live by its own camera) swirls in and
//     settles around it, ending in a spark. The draft waits for this to finish before it shuffles
// Drawn with Resources/VFX/BlackHole.shader on a top canvas that survives the scene change.
// Use BlackHoleTransition.Play("GameScene")
public class BlackHoleTransition : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }

    const float SwallowTime = 2f, MinCovered = 0.45f, FlashTime = 0.28f, CloseTime = 1.6f;

    private Material material;
    private RawImage screen;
    private Texture2D snapshot;
    private RenderTexture live;
    private Camera liveCamera;
    private float spin;

    public static void Play(string sceneName)
    {
        if (IsPlaying) return;
        IsPlaying = true;
        GameObject holder = new GameObject("Black Hole Transition");
        DontDestroyOnLoad(holder);
        holder.AddComponent<BlackHoleTransition>().StartCoroutine(holder.GetComponent<BlackHoleTransition>().Run(sceneName));
    }

    IEnumerator Run(string sceneName)
    {
        // Snapshot the menu exactly as it looks (UI included) before covering it
        yield return new WaitForEndOfFrame();
        snapshot = ScreenCapture.CaptureScreenshotAsTexture();
        BuildOverlay(snapshot != null ? (Texture)snapshot : Texture2D.blackTexture);

        AsyncOperation loading = SceneManager.LoadSceneAsync(sceneName);
        loading.allowSceneActivation = false; // Load now, switch once the screen is covered

        // 1. Swallow
        GameSfx.Play("charge", 0.9f);
        GameSfx.Play("whoosh.heavy", 0.7f);
        for (float t = 0f; t < SwallowTime; t += Time.unscaledDeltaTime)
        {
            float p = t / SwallowTime;
            Set(hole: Cube(p) * 1.08f, pull: Ease(p), twist: Ease(p) * 3.5f, darken: 0.85f, ring: Mathf.Min(1f, p * 4f), spinSpeed: 2.2f * (1f + p * 2f));
            yield return null;
        }

        // 2. Covered: switch scenes, wait for the new one to be ready (and at least a moment, so it reads as a beat)
        Set(hole: 1.2f, pull: 0f, twist: 0f, darken: 0f, ring: 0f, spinSpeed: 1.3f);
        material.SetFloat("_EdgeGlow", 1f);
        float coveredSince = Time.unscaledTime;
        loading.allowSceneActivation = true;
        while (!loading.isDone) { Spin(1.3f); yield return null; }
        for (int i = 0; i < 3; i++) { Spin(1.3f); yield return null; } // Let the new scene's Start() run
        while (Time.unscaledTime < coveredSince + MinCovered) { Spin(1.3f); yield return null; }

        // Show the new scene through the effect: its camera draws into a texture instead of the screen
        liveCamera = Camera.main;
        if (liveCamera != null)
        {
            live = new RenderTexture(Screen.width, Screen.height, 24);
            liveCamera.targetTexture = live;
            screen.texture = live;
        }

        // 3. Flash
        GameSfx.Play("thump", 0.9f);
        GameSfx.Play("bell", 0.5f);
        material.SetFloat("_EdgeGlow", 0f);
        for (float t = 0f; t < FlashTime; t += Time.unscaledDeltaTime)
        {
            float p = t / FlashTime;
            material.SetFloat("_Flash", p < 0.35f ? p / 0.35f : 1f - (p - 0.35f) / 0.65f * 0.55f);
            Set(hole: 1.08f, pull: 0.5f, twist: -3.5f, darken: 0.6f, ring: 1f, spinSpeed: -6.6f);
            yield return null;
        }

        // 4. Close back in on itself while the board swirls in around it
        bool sparked = false;
        for (float t = 0f; t < CloseTime; t += Time.unscaledDeltaTime)
        {
            float p = t / CloseTime, q = Ease(p);
            Set(hole: (1f - q) * 1.08f, pull: (1f - q) * 0.5f, twist: -(1f - q) * 3.5f, darken: 0.6f * (1f - q), ring: 1f, spinSpeed: -2.2f * (3f - p * 2f));
            material.SetFloat("_Flash", 0.45f * (1f - Mathf.Min(1f, p * 3f)));
            float spark = p > 0.86f ? Mathf.Sin((p - 0.86f) / 0.14f * Mathf.PI) : 0f;
            material.SetFloat("_Spark", spark);
            if (!sparked && p > 0.86f) { sparked = true; GameSfx.Play("sparkle", 0.8f); }
            yield return null;
        }

        Finish();
    }

    void Finish()
    {
        if (liveCamera != null) liveCamera.targetTexture = null;
        if (live != null) { live.Release(); Destroy(live); }
        if (snapshot != null) Destroy(snapshot);
        if (material != null) Destroy(material);
        IsPlaying = false;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (liveCamera != null && liveCamera.targetTexture == live) liveCamera.targetTexture = null;
        IsPlaying = false;
    }

    // ---- Drawing ----

    void BuildOverlay(Texture picture)
    {
        GameObject canvasObject = new GameObject("Black Hole Canvas", typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // Above everything, in both scenes

        screen = new GameObject("Screen", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        screen.rectTransform.SetParent(canvasObject.transform, false);
        screen.rectTransform.anchorMin = Vector2.zero;
        screen.rectTransform.anchorMax = Vector2.one;
        screen.rectTransform.offsetMin = screen.rectTransform.offsetMax = Vector2.zero;
        screen.raycastTarget = true; // Swallows clicks while it plays

        Shader shader = Shader.Find("Stratafield/Black Hole");
        material = new Material(shader != null ? shader : Shader.Find("UI/Default"));
        screen.material = material;
        screen.texture = picture;
        material.SetFloat("_Aspect", Screen.width / (float)Mathf.Max(1, Screen.height));
    }

    void Set(float hole, float pull, float twist, float darken, float ring, float spinSpeed)
    {
        material.SetFloat("_Hole", hole);
        material.SetFloat("_Pull", pull);
        material.SetFloat("_Twist", twist);
        material.SetFloat("_Darken", darken);
        material.SetFloat("_RingAlpha", ring);
        Spin(spinSpeed);
    }

    void Spin(float speed)
    {
        spin += speed * Time.unscaledDeltaTime;
        material.SetFloat("_Spin", spin);
        material.SetFloat("_Aspect", Screen.width / (float)Mathf.Max(1, Screen.height));
    }

    static float Ease(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    static float Cube(float t) => t * t * t;
}
