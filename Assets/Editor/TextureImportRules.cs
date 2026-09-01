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

        importer.maxTextureSize = MaxSize;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.mipmapEnabled = true;
    }
}
