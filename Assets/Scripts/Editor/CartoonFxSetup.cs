using UnityEditor;
using UnityEngine;

// Makes Resources/VFX/CartoonFxSet and fills its empty slots with the Cartoon FX Remaster FREE prefabs, whenever
// scripts reload in the editor. Slots you've changed by hand are left alone. Does nothing if the pack isn't imported
[InitializeOnLoad]
public static class CartoonFxSetup
{
    const string AssetPath = "Assets/Resources/VFX/CartoonFxSet.asset";
    const string Prefabs = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/";

    // Slot name -> prefab, relative to the pack's prefab folder
    static readonly (string field, string path)[] Slots =
    {
        ("slashCross", "Sword Trails/Plain/CFXR4 Sword Hit PLAIN (Cross).prefab"),
        ("iceCross", "Sword Trails/Ice/CFXR4 Sword Hit ICE (Cross).prefab"),
        ("redHit", "Impacts/CFXR Hit A (Red).prefab"),
        ("yellowHit", "Impacts/CFXR Hit D 3D (Yellow).prefab"),
        ("groundHit", "Impacts/CFXR2 Ground Hit.prefab"),
        ("glowingImpact", "Impacts/CFXR Impact Glowing HDR (Blue).prefab"),
        ("iceHit", "Ice/CFXR3 Hit Ice B (Air).prefab"),
        ("waterSplash", "Liquids/CFXR Water Splash (Smaller).prefab"),
        ("waterRipples", "Liquids/CFXR Water Ripples.prefab"),
        ("lightHit", "Light/CFXR3 Hit Light B (Air).prefab"),
        ("leavesHit", "Nature/CFXR3 Hit Leaves A (Lit).prefab"),
        ("electricHit", "Electric/CFXR3 Hit Electric C (Air).prefab"),
        ("sparksRain", "Electric/CFXR2 Sparks Rain.prefab"),
        ("magicPoof", "Misc/CFXR Magic Poof.prefab"),
        ("miscHit", "Misc/CFXR3 Hit Misc A.prefab"),
        ("poisonCloud", "Misc/CFXR2 Poison Cloud.prefab"),
        ("fireHit", "Fire/CFXR3 Hit Fire B (Air).prefab"),
        ("fallingStars", "Magic Misc/CFXR4 Falling Stars.prefab"),
        ("defeatPoof", "Eerie/CFXR2 WW Enemy Explosion.prefab"),
        ("firework", "Explosions/CFXR4 Firework 1 Cyan-Purple (HDR).prefab"),
    };

    static CartoonFxSetup()
    {
        EditorApplication.delayCall += Fill;
    }

    // Adds the VFX Tester panel (Dev/VfxTester) to the open scene. It shows in Play mode
    [MenuItem("Stratafield/VFX/Add VFX Tester")]
    static void AddTester()
    {
        VfxTester existing = Object.FindAnyObjectByType<VfxTester>(FindObjectsInactive.Include);
        if (existing != null) { Selection.activeObject = existing.gameObject; return; }
        GameObject tester = new GameObject("VFX Tester");
        Undo.RegisterCreatedObjectUndo(tester, "Add VFX Tester");
        tester.AddComponent<VfxTester>();
        Selection.activeObject = tester;
    }

    [MenuItem("Stratafield/VFX/Link Cartoon FX")]
    static void Fill()
    {
        if (!AssetDatabase.IsValidFolder("Assets/JMO Assets/Cartoon FX Remaster")) return; // Pack not imported

        CartoonFxSet set = AssetDatabase.LoadAssetAtPath<CartoonFxSet>(AssetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<CartoonFxSet>();
            AssetDatabase.CreateAsset(set, AssetPath);
        }

        SerializedObject serialized = new SerializedObject(set);
        bool changed = false;
        foreach ((string field, string path) in Slots)
        {
            SerializedProperty slot = serialized.FindProperty(field);
            if (slot == null || slot.objectReferenceValue != null) continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + path);
            if (prefab == null) continue;
            slot.objectReferenceValue = prefab;
            changed = true;
        }

        if (!changed) return;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Debug.Log("Cartoon FX linked into Resources/VFX/CartoonFxSet");
    }
}
