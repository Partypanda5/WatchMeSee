using UnityEditor;

// New images come in as single sprites at 140 pixels per unit, matching the character art.
// Only applies the first time a file is imported, so changes made in the inspector stick.
public sealed class SpriteImportDefaults : AssetPostprocessor
{
    private const float PixelsPerUnit = 140f;

    private void OnPreprocessTexture()
    {
        if (!assetImporter.importSettingsMissing) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
    }
}
