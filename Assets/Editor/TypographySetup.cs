using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Builds bundled static SDF atlases; no runtime OS-font or network dependency.
public static class TypographySetup
{
  [MenuItem("Tools/UI/Build Readable Fonts")]
  public static void Build()
  {
    Directory.CreateDirectory("Assets/Resources/Fonts");AssetDatabase.Refresh();
    var regular=Create("Regular");var medium=Create("Medium");var semibold=Create("SemiBold");Create("Bold");
    var weights=regular.fontWeightTable;weights[5].regularTypeface=medium;weights[6].regularTypeface=semibold;weights[7].regularTypeface=semibold;
    EditorUtility.SetDirty(regular);AssetDatabase.SaveAssets();
    Debug.Log("TYPOGRAPHY FONT BUILD: Nunito Sans regular/medium/semibold/bold static atlases saved.");
  }
  private static TMP_FontAsset Create(string style)
  {
    string assetPath=$"Assets/Resources/Fonts/NunitoSans-{style}.asset";
    var existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);if(existing!=null)return existing;
    var source=AssetDatabase.LoadAssetAtPath<Font>($"Assets/Fonts/NunitoSans/NunitoSans-{style}.ttf");
    if(source==null)throw new InvalidOperationException("Missing Nunito Sans "+style);
    var asset=TMP_FontAsset.CreateFontAsset(source,64,8,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,false);
    asset.name="NunitoSans-"+style;
    // Include Latin text/numbers and punctuation used by this English UI.
    string characters="";for(int code=32;code<=126;code++)characters+=(char)code;
    for(int code=160;code<=255;code++)characters+=(char)code;
    characters+="\u2013\u2014\u2018\u2019\u201c\u201d\u2022\u2026\u20ac";
    if(!asset.TryAddCharacters(characters,out string missing))throw new InvalidOperationException("Missing glyphs: "+missing);
    asset.atlasPopulationMode=AtlasPopulationMode.Static;
    AssetDatabase.CreateAsset(asset,assetPath);
    AssetDatabase.AddObjectToAsset(asset.material,asset);
    foreach(var texture in asset.atlasTextures)AssetDatabase.AddObjectToAsset(texture,asset);
    EditorUtility.SetDirty(asset);return asset;
  }
}
