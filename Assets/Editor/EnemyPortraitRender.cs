using System.IO;
using UnityEditor;
using UnityEngine;

public static class EnemyPortraitRender
{
  public static void Render()
  {
    bool previous = ShaderUtil.allowAsyncCompilation;
    ShaderUtil.allowAsyncCompilation = false;
    const string folder = "Assets/Resources/EnemyPortraits";
    Directory.CreateDirectory(folder);
    try
    {
      foreach (string name in FullCastReview.Enemies)
      {
        Texture2D image = VisualSlicePreview.Portrait(name, "", false, false);
        // RenderTexture scaling is performed by Unity, so this remains a
        // reproducible prefab render rather than a separate art variant.
        var small = RenderTexture.GetTemporary(128,128,0);
        Graphics.Blit(image, small);
        var previousTarget = RenderTexture.active;
        RenderTexture.active = small;
        var icon = new Texture2D(128,128,TextureFormat.RGB24,false);
        icon.ReadPixels(new Rect(0,0,128,128),0,0); icon.Apply();
        RenderTexture.active = previousTarget;
        File.WriteAllBytes($"{folder}/{name}.png", icon.EncodeToPNG());
        RenderTexture.ReleaseTemporary(small);
        Object.DestroyImmediate(icon); Object.DestroyImmediate(image);
      }
      AssetDatabase.Refresh();
      foreach (string name in FullCastReview.Enemies)
      {
        var importer = (TextureImporter)AssetImporter.GetAtPath($"{folder}/{name}.png");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
      }
      Debug.Log("ENEMY PORTRAITS: eight original-model scout portraits rendered.");
    }
    finally { ShaderUtil.allowAsyncCompilation = previous; }
  }
}
