using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Whole-screen finish. Set up for the Inked Star Chart look: a little bloom and darker corners only.
// The CRT arcade parts are still here (raise curvature, colourFringe and scanlines to bring them back):
// - URP post-processing made in code: screen bulge (lens distortion), colour fringing at the edges, bloom so glows
//   bleed light, and a vignette
// - scanlines over everything, HUD included
// Every part has its own setting (0 = off). CombatManager adds it automatically
public class ScreenLook : MonoBehaviour
{
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

        ChromaticAberration fringe = profile.Add<ChromaticAberration>(true);
        fringe.intensity.Override(colourFringe);

        Bloom glow = profile.Add<Bloom>(true);
        glow.intensity.Override(bloom);
        glow.threshold.Override(bloomThreshold);
        glow.scatter.Override(0.65f);

        Vignette corners = profile.Add<Vignette>(true);
        corners.intensity.Override(vignette);
        corners.smoothness.Override(0.45f);

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
    }

    // One texture repeat per few screen pixels, so lines stay sharp at any window size
    void FitScanlines()
    {
        fittedHeight = Screen.height;
        lines.uvRect = new Rect(0f, 0f, 1f, Screen.height / (float)scanlineSpacing);
    }
}
