using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Whole-screen finish. Set up for the Inked Star Chart look: a little bloom and darker corners, plus
// - colour grading: a touch more contrast and saturation and a gentle tonemapper so the slate and gold pop
// - hit reactions: big hits and defeats flash the bloom and pulse a colour fringe for a moment (ScreenLook.Punch)
// - turn colour: the darker corners tint toward the colour of whoever's turn it is (draft, placement and battle)
// The CRT arcade parts are still here (raise curvature, colourFringe and scanlines to bring them back):
// - URP post-processing made in code: screen bulge (lens distortion), colour fringing at the edges, bloom so glows
//   bleed light, and a vignette
// - scanlines over everything, HUD included
// Every part has its own setting (0 = off). CombatManager adds it automatically
public class ScreenLook : MonoBehaviour
{
    [Header("Colour grading")]
    [SerializeField] private bool tonemap = true;                         // Neutral tonemapping: keeps bright glows from blowing out to white
    [SerializeField, Range(-30f, 30f)] private float contrast = 16f;
    [SerializeField, Range(-30f, 40f)] private float saturation = 22f;

    [Header("Hit reactions")]
    [SerializeField, Range(0f, 4f)] private float hitBloom = 2.5f;        // Extra bloom at the peak of the biggest hit
    [SerializeField, Range(0f, 1f)] private float hitFringe = 0.8f;       // Colour fringe at the peak
    [SerializeField, Range(0.05f, 1f)] private float hitFade = 0.45f;     // Seconds to fade back (real time, so hit-stop doesn't stretch it)
    [SerializeField, Range(0f, 1.5f)] private float hitBrighten = 0.45f;  // Whole screen briefly brighter at the peak (exposure stops)
    [SerializeField, Range(0f, 0.8f)] private float hitGlowAll = 0.45f;   // Lowers the bloom threshold at the peak so more of the screen glows

    [Header("Turn colour")]
    [SerializeField, Range(0f, 1f)] private float turnTint = 0.8f;        // How strongly the corners take the player's colour
    [SerializeField, Range(0f, 0.3f)] private float turnVignette = 0.14f; // Corners get this much darker during someone's turn

    [Header("CRT and base look")]
    [SerializeField] private bool crtLook = true;
    // The bulge is only drawn: the mouse still clicks the undistorted picture. Keep these small so clicks line up
    [SerializeField, Range(0f, 0.3f)] private float curvature = 0f;      // Screen bulge (about 2% at the far corners at 0.18). Off for the inked look
    [SerializeField, Range(0.95f, 1.1f)] private float curvatureZoom = 1f; // Above 1 hides stretched corners but shifts clicks near the edges
    [SerializeField, Range(0f, 1f)] private float colourFringe = 0f;      // Red/blue fringes toward the edges. Off for the inked look
    [SerializeField, Range(0f, 3f)] private float bloom = 0.6f;           // Glow around bright things
    [SerializeField, Range(0f, 1f)] private float bloomThreshold = 0.8f;  // How bright something must be to glow
    [SerializeField, Range(0f, 1f)] private float vignette = 0.3f;        // Darker corners
    [SerializeField, Range(0f, 1f)] private float scanlines = 0f;         // How dark the lines are (0.16 for the CRT look). Off for the inked look
    [SerializeField, Range(2, 8)] private int scanlineSpacing = 3;       // Screen pixels per line

    private Volume volume;
    private VolumeProfile profile;
    private Bloom glow;
    private ChromaticAberration fringe;
    private Vignette corners;
    private ColorAdjustments grade;
    private float punch;                                  // 0-1, set by big hits, fades out
    private Color tint = Color.black;                     // Current corner colour (eases toward the active player's)
    private float tintAmount;

    static ScreenLook instance;

    // A big hit or defeat landed: flash the bloom and pulse the colour fringe. strength 0-1
    public static void Punch(float strength)
    {
        if (instance != null) instance.punch = Mathf.Max(instance.punch, Mathf.Clamp01(strength));
    }

    void Awake() => instance = this;
    private GameObject overlay;
    private RawImage lines;
    private Texture2D lineTexture;
    private int fittedHeight;

    void Start()
    {
        if (!crtLook) return;
        MakePostProcessing();
        MakeScanlines();
    }

    void OnDestroy()
    {
        if (volume != null) Destroy(volume.gameObject);
        if (profile != null) Destroy(profile);
        if (overlay != null) Destroy(overlay);
        if (lineTexture != null) Destroy(lineTexture);
    }

    // ---- Post-processing ----

    void MakePostProcessing()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        UniversalAdditionalCameraData cameraData = cam.GetUniversalAdditionalCameraData();
        if (cameraData != null) cameraData.renderPostProcessing = true;

        profile = ScriptableObject.CreateInstance<VolumeProfile>();

        LensDistortion bulge = profile.Add<LensDistortion>(true);
        bulge.intensity.Override(curvature);
        bulge.scale.Override(curvatureZoom);

        fringe = profile.Add<ChromaticAberration>(true);
        fringe.intensity.Override(colourFringe);

        glow = profile.Add<Bloom>(true);
        glow.intensity.Override(bloom);
        glow.threshold.Override(bloomThreshold);
        glow.scatter.Override(0.65f);

        corners = profile.Add<Vignette>(true);
        corners.intensity.Override(vignette);
        corners.smoothness.Override(0.45f);
        corners.color.Override(Color.black);

        // Colour grading
        if (tonemap) profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
        grade = profile.Add<ColorAdjustments>(true);
        grade.contrast.Override(contrast);
        grade.saturation.Override(saturation);
        grade.postExposure.Override(0f);

        // Global, and above any default volume so these settings win
        GameObject volumeObject = new GameObject("CRT Look");
        volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
    }

    // ---- Scanlines ----

    // A see-through texture with one dark row, tiled down the whole screen on a canvas above every other UI
    void MakeScanlines()
    {
        if (scanlines <= 0f) return;

        lineTexture = new Texture2D(1, scanlineSpacing, TextureFormat.RGBA32, false);
        lineTexture.filterMode = FilterMode.Point;
        lineTexture.wrapMode = TextureWrapMode.Repeat;
        for (int y = 0; y < scanlineSpacing; y++)
            lineTexture.SetPixel(0, y, new Color(0f, 0f, 0f, y == 0 ? scanlines : 0f));
        lineTexture.Apply();

        overlay = new GameObject("Scanlines", typeof(Canvas));
        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        GameObject image = new GameObject("Lines", typeof(RectTransform), typeof(RawImage));
        image.transform.SetParent(overlay.transform, false);
        RectTransform rect = (RectTransform)image.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        lines = image.GetComponent<RawImage>();
        lines.texture = lineTexture;
        lines.raycastTarget = false; // Clicks go straight through
        FitScanlines();
    }

    void LateUpdate()
    {
        if (lines != null && Screen.height != fittedHeight) FitScanlines();
        UpdateReactions();
    }

    // Hit flash fading out, and the corners easing toward the active player's colour
    void UpdateReactions()
    {
        if (glow == null) return;
        float dt = Time.unscaledDeltaTime;

        punch = Mathf.MoveTowards(punch, 0f, dt / hitFade);
        float eased = punch * punch; // Sharp peak, quick settle
        glow.intensity.value = bloom + eased * hitBloom;
        glow.threshold.value = bloomThreshold - eased * hitGlowAll;
        fringe.intensity.value = colourFringe + eased * hitFringe;
        if (grade != null) grade.postExposure.value = eased * hitBrighten;

        int player = TurnPlayer();
        Color target = player > 0 ? Color.Lerp(Color.black, InkArt.PlayerColor(player), turnTint) : Color.black;
        float follow = 1f - Mathf.Exp(-4f * dt);
        tint = Color.Lerp(tint, target, follow);
        tintAmount = Mathf.Lerp(tintAmount, player > 0 ? 1f : 0f, follow);
        corners.color.value = tint;
        corners.intensity.value = vignette + turnVignette * tintAmount;
    }

    // Whose go it is right now: the active card's owner in battle, the placing player, or the drafting player. 0 = nobody
    static int TurnPlayer()
    {
        TurnManager turns = TurnManager.Instance;
        if (turns == null) return 0;
        if (turns.Phase == GamePhase.Battle) return turns.ActiveUnit != null && turns.ActiveUnit.Instance != null ? turns.ActiveUnit.Instance.owner : 0;
        if (turns.Phase == GamePhase.Placement) return turns.PlacingPlayer;
        if (turns.Phase == GamePhase.Draft && DraftManager.Instance != null && DraftManager.Instance.IsDrafting && !DraftManager.Instance.IsDealing)
            return DraftManager.Instance.PickingPlayer;
        return 0;
    }

    // One texture repeat per few screen pixels, so lines stay sharp at any window size
    void FitScanlines()
    {
        fittedHeight = Screen.height;
        lines.uvRect = new Rect(0f, 0f, 1f, Screen.height / (float)scanlineSpacing);
    }
}
