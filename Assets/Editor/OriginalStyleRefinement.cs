using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Polishes the authored cast without replacing creature meshes, faces or maps.
// Absolute values make repeated application safe for prefab scale and color.
public static class OriginalStyleRefinement
{
  [MenuItem("Tools/Art/Refine Original Character Style")]
  public static void Apply()
  {
    var materials=new HashSet<Material>();
    int stems=0;
    foreach(string name in FullCastReview.Towers)
    {
      var config=AssetDatabase.LoadAssetAtPath<TowerConfig>($"Assets/Settings/Towers/{name}.asset");
      string path=AssetDatabase.GetAssetPath(config.towerPrefab);
      var root=PrefabUtility.LoadPrefabContents(path);
      try
      {
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
          string part=renderer.name;
          if(part=="Root" || part.StartsWith("Root_"))
          {
            Vector3 scale=renderer.transform.localScale;
            renderer.transform.localScale=new Vector3(1.12f,scale.y,1.12f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.transform);
            stems++;
          }
          foreach(Material material in renderer.sharedMaterials)
          {
            if(material==null || !materials.Add(material))continue;
            switch(material.name)
            {
              case "Root": case "Head": Finish(material,.22f);break;
              case "Iris":
                Finish(material,.36f);StopEmission(material);break;
              case "EyeWhite": Finish(material,.42f);break;
              case "Mouth": Finish(material,.25f);break;
              case "Dots": Finish(material,.36f);break;
              case "IceDrops": Finish(material,.55f);break;
            }
          }
        }
        PrefabUtility.SaveAsPrefabAsset(root,path);
      }
      finally {PrefabUtility.UnloadPrefabContents(root);}
    }
    // Shock's original pale yellow disappeared against the white of the eye.
    // Keep the gold family, with enough contrast to read at gameplay distance.
    Material shockIris=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Towers/ShockTower/Iris.mat");
    shockIris.SetColor("_BaseColor",new Color(.50f,.37f,.015f,1));
    shockIris.SetColor("_Color",new Color(.50f,.37f,.015f,1));EditorUtility.SetDirty(shockIris);

    foreach(string name in FullCastReview.Enemies)
    {
      var config=AssetDatabase.LoadAssetAtPath<EnemyConfig>($"Assets/Settings/Enemies/{name}.asset");
      foreach(var renderer in config.prefab.GetComponentsInChildren<MeshRenderer>(true))
        foreach(Material material in renderer.sharedMaterials)
        {
          if(material==null || !materials.Add(material))continue;
          switch(material.name)
          {
            case "BodySkin": case "BossMatteSkin": case "ShieldCarapace": Finish(material,.24f);break;
            case "Iris": Finish(material,.36f);StopEmission(material);break;
            case "EyeWhite": Finish(material,.42f);break;
            case "Pupils": Finish(material,.26f);break;
            case "Hair": case "Tail": case "HealerAura": Finish(material,.24f);break;
            case "ColonyRod": Finish(material,.26f);break;
          }
        }
    }
    Finish(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Enemies/ShieldBare.mat"),.24f);
    RefineRole("HealerEnemy",HealerSymbolArt.Refine);
    RefineRole("SplitterEnemy",SplitterCellArt.Refine);
    AssetDatabase.SaveAssets();
    Debug.Log($"ORIGINAL STYLE: all eight towers and eight enemies refined; {stems} authored stems widened 12%; colored pupils restored; closed amber daughter cells and polished healer symbols. Creature meshes and texture maps retained.");
  }

  static void RefineRole(string name,Action<GameObject> refine)
  {
    string path=$"Assets/Prefabs/Enemies/{name}.prefab";
    var root=PrefabUtility.LoadPrefabContents(path);
    try {refine(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
    finally {PrefabUtility.UnloadPrefabContents(root);}
  }
  static void StopEmission(Material material)
  {
    material.SetColor("_EmissionColor",Color.black);material.DisableKeyword("_EMISSION");
    material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
    EditorUtility.SetDirty(material);
  }
  static void Finish(Material material,float smoothness)
  {
    if(material==null)throw new InvalidOperationException("Missing authored cast material.");
    if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smoothness);
    if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",0);
    EditorUtility.SetDirty(material);
  }
  public static void RunBatch()
  {
    try {Apply();EditorApplication.Exit(0);}
    catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
}
