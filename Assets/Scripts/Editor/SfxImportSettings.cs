using UnityEditor;
using UnityEngine;

// Any sound added under Assets/Audio/SFX is preloaded and decompressed when the scene loads, so it plays the instant
// it's asked for (otherwise the first play of each clip loads from disk and comes in late)
public class SfxImportSettings : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Audio/SFX")) return;
        AudioImporter importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
        importer.loadInBackground = false;
    }
}
