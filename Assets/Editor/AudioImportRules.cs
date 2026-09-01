// AudioImportRules.cs
//
// Import settings for the project's footstep clips, applied automatically so a
// reimport cannot silently revert them to Unity's defaults.
//
// These are sub-half-second one-shots played at unpredictable moments, so the
// priority is that they start INSTANTLY and cost nothing to begin.
//
//   DecompressOnLoad  decodes once at load. The alternative, CompressedInMemory,
//                     decodes on every play -- a spike on the audio thread at
//                     exactly the moment a step should land.
//   PCM               no decode at all. Nine clips of 0.4 s is ~300 KB, not
//                     worth compressing on a device with this much RAM.
//   ForceToMono       own footsteps are positioned by the AudioSource's pan,
//                     not by the file, so a stereo file is wasted memory.
//
// preloadAudioData lives on the per-platform sample settings in Unity 6, not on
// the importer -- setting it on the importer is an obsolete-API error.

using UnityEditor;
using UnityEngine;

public class AudioImportRules : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Audio/"))
            return;

        AudioImporter importer = (AudioImporter)assetImporter;
        importer.forceToMono = true;
        importer.loadInBackground = false;

        AudioImporterSampleSettings s = importer.defaultSampleSettings;
        s.loadType = AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = AudioCompressionFormat.PCM;
        s.preloadAudioData = true;
        importer.defaultSampleSettings = s;
    }
}
