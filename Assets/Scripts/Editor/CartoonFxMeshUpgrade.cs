using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

// The Cartoon FX pack ships some meshes saved by an old Unity (Mesh version 9), which Unity 6 complains about on every
// import ("contains a Mesh object at version 9, below the supported minimum"). This re-saves just those files once,
// which upgrades them. It checks the files themselves, so it does nothing once they're upgraded (also on teammates' PCs)
[InitializeOnLoad]
public static class CartoonFxMeshUpgrade
{
    const string Folder = "Assets/VFX/JMO Assets";

    static CartoonFxMeshUpgrade() => EditorApplication.delayCall += Upgrade;

    [MenuItem("Stratafield/Tools/Upgrade Old Cartoon FX Meshes")]
    static void Upgrade()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !Directory.Exists(Folder)) return;

        Regex oldMesh = new Regex(@"^Mesh:\s*\n(?:.*\n){0,8}?\s*serializedVersion: ([0-9]+)", RegexOptions.Multiline);
        List<string> old = new List<string>();
        foreach (string path in Directory.GetFiles(Folder, "*.asset", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (!text.Contains("Mesh:")) continue;
            Match match = oldMesh.Match(text.Replace("\r\n", "\n"));
            if (match.Success && int.Parse(match.Groups[1].Value) < 10) old.Add(path.Replace('\\', '/'));
        }
        if (old.Count == 0) return;

        AssetDatabase.ForceReserializeAssets(old, ForceReserializeAssetsOptions.ReserializeAssets);
        UnityEngine.Debug.Log($"Upgraded {old.Count} old Cartoon FX meshes to the current format");
    }
}
