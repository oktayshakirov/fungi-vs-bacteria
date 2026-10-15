using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

// Sets the game's app icon from Assets/Sprites/Icons/AppIcon.png.
//
// Only the DEFAULT icon is set. Every platform-specific slot (iPhone, Android)
// is left empty in ProjectSettings, and Unity scales the default into each of
// them at build time - so one 1024x1024 source covers every size either store
// asks for. The source is saved opaque: the App Store rejects an icon with an
// alpha channel.
//
// Android 8+ is the exception: with no adaptive icon the launcher shrinks the
// square into a white disc. The adaptive slots get two 432px layers cut from
// the same art (committed PNGs next to AppIcon.png): the whole
// artwork spans the 72dp viewport of the 108dp foreground, so any mask shape
// is filled edge to edge, and a blurred full-bleed copy sits behind it for
// the launcher's parallax.
public static class AppIconSetup
{
  private const string IconPath = "Assets/Sprites/Icons/AppIcon.png";
  private const string AdaptiveBackgroundPath = "Assets/Sprites/Icons/AppIconAndroidBackground.png";
  private const string AdaptiveForegroundPath = "Assets/Sprites/Icons/AppIconAndroidForeground.png";

  [MenuItem("Tools/App Icon/Apply")]
  public static void Apply()
  {
    var icon = ImportIcon(IconPath, TextureImporterAlphaSource.None);
    var background = ImportIcon(AdaptiveBackgroundPath, TextureImporterAlphaSource.None);
    var foreground = ImportIcon(AdaptiveForegroundPath, TextureImporterAlphaSource.FromInput);
    if (icon == null || background == null || foreground == null)
    {
      if (Application.isBatchMode) EditorApplication.Exit(1);
      return;
    }

    PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

    // Every adaptive size gets the same pair; Unity scales them per density.
    // Layer 0 is the background, layer 1 the foreground.
    var adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
    foreach (var slot in adaptive) slot.SetTextures(background, foreground);
    PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive, adaptive);

    AssetDatabase.SaveAssets();
    Debug.Log("APP ICON OK");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  private static Texture2D ImportIcon(string path, TextureImporterAlphaSource alpha)
  {
    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
    if (importer != null)
    {
      // An icon is sampled down to many sizes, so it must not be squeezed on
      // import: full resolution, no compression artefacts, no mip bias.
      importer.textureType = TextureImporterType.Default;
      importer.npotScale = TextureImporterNPOTScale.None;
      importer.maxTextureSize = 1024;
      importer.textureCompression = TextureImporterCompression.Uncompressed;
      importer.alphaSource = alpha;
      importer.mipmapEnabled = false;
      importer.SaveAndReimport();
    }

    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    if (texture == null) Debug.LogError("APP ICON: missing " + path);
    return texture;
  }
}
