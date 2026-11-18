using UnityEngine;

// The Cartoon FX Remaster FREE effects (Jean Moreno, Asset Store) that ThemedEffects layers onto its ink effects.
// Lives at Resources/VFX/CartoonFxSet. Filled in automatically in the editor (Scripts/Editor/CartoonFxSetup);
// swap any slot for another effect by dragging a prefab in. Empty slots are simply skipped
[CreateAssetMenu(fileName = "CartoonFxSet", menuName = "Stratafield/Cartoon FX Set")]
public class CartoonFxSet : ScriptableObject
{
    [Header("Size")]
    [Tooltip("The pack's effects are made for characters about 2 units tall; tokens are about 1")]
    public float scale = 0.45f;
    public bool useCartoonFx = true;

    [Header("Hits")]
    public GameObject slashCross;
    public GameObject iceCross;
    public GameObject redHit;
    public GameObject yellowHit;
    public GameObject groundHit;
    public GameObject glowingImpact;
    public GameObject iceHit;
    public GameObject waterSplash;
    public GameObject waterRipples;
    public GameObject lightHit;
    public GameObject leavesHit;
    public GameObject electricHit;
    public GameObject sparksRain;
    public GameObject magicPoof;
    public GameObject miscHit;
    public GameObject poisonCloud;
    public GameObject fireHit;

    [Header("Big moments")]
    public GameObject fallingStars;
    public GameObject defeatPoof;
    public GameObject firework;

    private static CartoonFxSet loaded;
    private static bool looked;

    // The set in Resources, or null if there isn't one (the ink effects still play)
    public static CartoonFxSet Instance
    {
        get
        {
            if (!looked)
            {
                looked = true;
                loaded = Resources.Load<CartoonFxSet>("VFX/CartoonFxSet");
                DisablePackCameraShake();
            }
            return loaded != null && loaded.useCartoonFx ? loaded : null;
        }
    }

    // Plays one of the pack's effects at a point, sized for the tokens, and removes it after a few seconds
    public static void Play(GameObject prefab, Vector3 position, float size = 1f, float lifetime = 3f)
    {
        CartoonFxSet set = Instance;
        if (set == null || prefab == null) return;

        GameObject effect = Object.Instantiate(prefab, position, prefab.transform.rotation);
        effect.transform.localScale = prefab.transform.localScale * set.scale * size;
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy; // Every part follows the size
        }
        foreach (Renderer renderer in effect.GetComponentsInChildren<Renderer>(true))
            renderer.sortingOrder = InkFx.AboveUnits - 1; // Just under the ink layers, above the tokens
        Object.Destroy(effect, lifetime);
    }

    // The pack can shake the camera itself; CombatEffects already does that, so switch the pack's off
    // (Found by name, so this still compiles if the pack is removed)
    static void DisablePackCameraShake()
    {
        System.Type type = System.Type.GetType("CartoonFX.CFXR_Effect, CFXRRuntime");
        System.Reflection.FieldInfo field = type != null ? type.GetField("GlobalDisableCameraShake") : null;
        if (field != null) field.SetValue(null, true);
    }
}
