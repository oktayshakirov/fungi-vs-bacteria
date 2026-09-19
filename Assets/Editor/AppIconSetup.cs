using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Sets the game's app icon from Assets/Sprites/Icons/AppIcon.png.
//
// Only the DEFAULT icon is set. Every platform-specific slot (iPhone, Android)
// is left empty in ProjectSettings, and Unity scales the default into each of
// them at build time - so one 1024x1024 source covers every size either store
// asks for. The source is saved opaque: the App Store rejects an icon with an
// alpha channel.
public static class AppIconSetup
{
  private const string IconPath = "Assets/Sprites/Icons/AppIcon.png";

  [MenuItem("Tools/App Icon/Apply")]
  public static void Apply()
  {
    var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
    if (importer != null)
    {
      // An icon is sampled down to many sizes, so it must not be squeezed on
      // import: full resolution, no compression artefacts, no mip bias.
      importer.textureType = TextureImporterType.Default;
      importer.npotScale = TextureImporterNPOTScale.None;
      importer.maxTextureSize = 1024;
      importer.textureCompression = TextureImporterCompression.Uncompressed;
      importer.alphaSource = TextureImporterAlphaSource.None;
      importer.mipmapEnabled = false;
      importer.SaveAndReimport();
    }

    var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
    if (icon == null)
    {
      Debug.LogError("APP ICON: missing " + IconPath);
      if (Application.isBatchMode) EditorApplication.Exit(1);
      return;
    }

    PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
    AssetDatabase.SaveAssets();
    Debug.Log("APP ICON OK");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }
}
