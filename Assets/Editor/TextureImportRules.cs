// TextureImportRules.cs
//
// Import settings for the project's few real textures, applied automatically so
// they cannot be forgotten or lost when a texture is reimported.
//
// TWO THINGS THAT ARE INVISIBLE UNTIL THEY ARE WRONG
// --------------------------------------------------
// 1. NORMAL MAPS must be marked as such. Imported as a plain colour texture a
//    normal map still renders -- it just lights the surface completely wrong,
//    which reads as "the model looks a bit off" rather than as an error.
//
// 2. SOURCE TEXTURES ARE 2048px. On a Quest that is several megabytes of GPU
//    memory each for props a few centimetres across. Capping at 1024 costs
//    nothing visible at arm's length and quarters the memory.
//
// Doing this here rather than by hand means a re-download or a reimport gets
// the same treatment, instead of silently reverting to Unity's defaults.

using UnityEditor;

public class TextureImportRules : AssetPostprocessor
{
    const int MaxSize = 1024;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Textures/"))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;

        // Name-based, because that is the only signal the file carries. The
        // Blender scripts copy the OpenGL-convention normal map under this
        // name -- Unity expects OpenGL, and the DirectX variant would invert
        // the green channel and light every facet backwards.
        if (assetPath.Contains("_Normal"))
            importer.textureType = TextureImporterType.NormalMap;

        // A 360 panorama wraps the entire horizon, so it needs far more pixels
        // than a label on a bottle -- capping it at 1024 spreads about three
        // pixels across each degree of arc and the sky turns to mush. Keep the
        // source resolution instead.
        bool isSky = assetPath.Contains("Sky_");

        importer.maxTextureSize = isSky ? 2048 : MaxSize;

        // A 360 sky is a large, smooth gradient, which is the worst case for
        // block compression: standard ASTC bands it into visible tiles across
        // exactly the flat areas that make up most of an overcast sky. HQ picks
        // a finer block size, roughly doubling the memory for this one texture
        // and removing the banding. Everything else is detailed enough that
        // standard compression is invisible.
        importer.textureCompression = isSky
            ? TextureImporterCompression.CompressedHQ
            : TextureImporterCompression.Compressed;

        // No mipmaps on the sky: it is only ever drawn at a fixed distance, so
        // mips are memory spent on levels that never get sampled, and their
        // filtering softens it further.
        importer.mipmapEnabled = !isSky;
    }
}
