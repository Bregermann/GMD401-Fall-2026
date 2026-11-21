using UnityEngine;
using UnityEngine.Rendering;

// Animated backdrop behind the board: a pixelated swirling-paint galaxy with twinkling pixel stars.
// Drawn by the "Stratafield/Space Background" shader on a quad that fills the camera's view, below the board.
// CombatManager adds it automatically. Change colours and speeds on Assets/Resources/VFX/SpaceBackground (the material).
public class SpaceBackground : MonoBehaviour
{
    [SerializeField] private Material material;            // Empty = Resources/VFX/SpaceBackground
    [SerializeField] private float depthBelowBoard = 4f;   // How far under the board it sits
    [SerializeField] private float overscan = 1.15f;       // A bit bigger than the view, so screen shake never shows an edge
    [SerializeField] private bool hideBoardSurface = true; // Hides the board under the grid so the slots float over space
    [Tooltip("Works the swirl out once per background pixel into a small texture instead of once per screen pixel. " +
             "Looks the same, roughly 5-10x cheaper on the GPU (important for WebGL). Turn off if the background looks wrong")]
    [SerializeField] private bool lowResRender = true;
    [SerializeField] private float lowResFps = 30f;        // How often the small texture is redrawn (the swirl moves slowly)

    private Camera cam;
    private Transform quad;
    private MaterialPropertyBlock block;
    private float fittedAspect;
    private Material renderMaterial; // A copy, so setting the aspect doesn't change the material asset
    private RenderTexture lowRes;
    private float nextRender;

    void Start()
    {
        cam = Camera.main;
        if (cam == null) return;

        if (material == null) material = Resources.Load<Material>("VFX/SpaceBackground");
        if (material == null)
        {
            Debug.LogWarning("SpaceBackground: no material found at Resources/VFX/SpaceBackground");
            return;
        }

        // Nothing should show behind the background
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        GameObject quadObject = new GameObject("Space Background", typeof(MeshFilter), typeof(MeshRenderer));
        quadObject.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        MeshRenderer meshRenderer = quadObject.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        quad = quadObject.transform;
        block = new MaterialPropertyBlock();
        Fit();

        if (hideBoardSurface) HideBoardSurface();
    }

    // Turns off the visible surface of whatever the grid sits on (the "Board" plane). Its collider and the slots stay
    void HideBoardSurface()
    {
        CardGrid grid = FindAnyObjectByType<CardGrid>();
        if (grid == null) return;

        for (Transform parent = grid.transform.parent; parent != null; parent = parent.parent)
        {
            MeshRenderer surface = parent.GetComponent<MeshRenderer>();
            if (surface == null) continue;
            surface.enabled = false;
            return;
        }
    }

    // Re-fits if the window is resized, and redraws the small texture
    void LateUpdate()
    {
        if (quad == null) return;
        if (!Mathf.Approximately(cam.aspect, fittedAspect)) Fit();
        if (lowRes != null && Time.unscaledTime >= nextRender)
        {
            nextRender = Time.unscaledTime + 1f / Mathf.Max(1f, lowResFps);
            Graphics.Blit(null, lowRes, renderMaterial, 0);
        }
    }

    // One texture pixel per background pixel (the shader's Pixels Across grid)
    void MakeLowRes(float aspect)
    {
        if (!lowResRender) return;
        if (renderMaterial == null) renderMaterial = new Material(material);
        renderMaterial.SetFloat("_Aspect", aspect);
        renderMaterial.SetFloat("_UseLowRes", 0f);

        float across = material.HasProperty("_PixelsAcross") ? material.GetFloat("_PixelsAcross") : 700f;
        int width = Mathf.Max(16, Mathf.RoundToInt(across));
        int height = Mathf.Max(16, Mathf.RoundToInt(across / aspect));
        if (lowRes != null && lowRes.width == width && lowRes.height == height) return;

        if (lowRes != null) { lowRes.Release(); Destroy(lowRes); }
        lowRes = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        lowRes.Create();
        nextRender = 0f;
    }

    // Call after moving the camera somewhere new (CameraRig does)
    public void Refit()
    {
        if (quad != null) Fit();
    }

    // Faces the camera and fills its view at the background's depth.
    // A child of the camera, so it stays put on screen while the camera sways (it's infinitely far away)
    void Fit()
    {
        fittedAspect = cam.aspect;
        Vector3 forward = cam.transform.forward;

        // Distance along the view to the background, which sits depthBelowBoard under the board (y = 0)
        float distance = forward.y < -0.01f ? (cam.transform.position.y + depthBelowBoard) / -forward.y : 20f;
        float height = cam.orthographic ? cam.orthographicSize * 2f : 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        height *= overscan;
        float width = height * cam.aspect;

        quad.SetParent(cam.transform, false);
        quad.localPosition = Vector3.forward * distance;
        quad.localRotation = Quaternion.identity;
        quad.localScale = new Vector3(width, height, 1f);

        // So the pixels stay square and the swirl stays round on any screen shape
        MeshRenderer meshRenderer = quad.GetComponent<MeshRenderer>();
        meshRenderer.GetPropertyBlock(block);
        block.SetFloat("_Aspect", width / height);
        MakeLowRes(width / height);
        if (lowRes != null)
        {
            block.SetTexture("_LowResTex", lowRes);
            block.SetFloat("_UseLowRes", 1f);
        }
        meshRenderer.SetPropertyBlock(block);
    }

    void OnDestroy()
    {
        if (quad != null) Destroy(quad.gameObject);
        if (lowRes != null) { lowRes.Release(); Destroy(lowRes); }
        if (renderMaterial != null) Destroy(renderMaterial);
    }
}
