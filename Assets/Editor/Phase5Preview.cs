using System;
using System.IO;
using System.Collections.Generic;
using TMPro;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class Phase5Preview
{
  public static void RunBeforeBatch() => Render("phase5-before");
  public static void RunBatch() => Render("phase5-after");
  public static Tower PrepareArcher(List<GameObject> placed,int mode)
  {
    TowerBuffs.Clear();
    var cfg=AssetDatabase.LoadAssetAtPath<TowerConfig>("Assets/Settings/Towers/ArcherTower.asset");
    var go=placed.Find(unit=>unit!=null && unit.GetComponent<Tower>()!=null && unit.name==cfg.towerPrefab.name);
    var tower=go.GetComponent<Tower>();
    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
    typeof(Tower).GetMethod("Awake",flags).Invoke(tower,null);tower.Initialize(cfg);
    if(mode<=4) typeof(Tower).GetField("<Level>k__BackingField",flags).SetValue(tower,2);
    typeof(Tower).GetMethod("ApplyTierVisuals",flags).Invoke(tower,null);
    if((mode==2 || mode==3) && !tower.Specialize(mode==2?ArcherSpecialization.Flurry:ArcherSpecialization.Longshot))throw new InvalidOperationException("Specialization preview failed");
    return tower;
  }
  public static void ValidateLabels(Canvas canvas)
  {
    foreach(var text in canvas.GetComponentsInChildren<TMP_Text>())
    {
      if(!text.transform.IsChildOf(canvas.GetComponentInChildren<TowerDefense.UI.TowerActions>().transform))continue;
      text.ForceMeshUpdate();
      // Allow the SDF font's small glyph overhang; reject meaningful label spill.
      if(text.isTextOverflowing || text.textBounds.size.x>text.rectTransform.rect.width+3 || text.textBounds.size.y>text.rectTransform.rect.height+3)throw new InvalidOperationException("Archer label overflows: "+text.name+" / "+text.text+" bounds="+text.textBounds.size+" rect="+text.rectTransform.rect.size);
    }
  }
  public static void RunSpecializationBatch()
  {
    try
    {
      ShaderUtil.allowAsyncCompilation=false;
      Directory.CreateDirectory("Builds/BattlefieldPreview");Directory.CreateDirectory("Builds/phase5-specializations");
      var shoot=typeof(BattlefieldPreview).GetMethod("Shoot",BindingFlags.Static|BindingFlags.NonPublic);
      foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(2340,1080),new Vector2Int(2400,1080),new Vector2Int(1440,1080)})
        for(int mode=1;mode<=5;mode++)
        {
          shoot.Invoke(null,new object[]{1,size,42f,false,false,true,0,0,mode,false,0});
          string name=$"env1-{size.x}x{size.y}-p42-full-cast-specialty{mode}.png";
          File.Copy("Builds/BattlefieldPreview/"+name,"Builds/phase5-specializations/"+name,true);
        }
      Debug.Log("SPECIALIZATION PREVIEW: twenty Archer unlock/inspection/choice/Flurry/Longshot frames passed across four aspect ratios.");EditorApplication.Exit(0);
    }
    catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
  public static void RunForecastBatch()
  {
    try
    {
      ShaderUtil.allowAsyncCompilation=false;
      Directory.CreateDirectory("Builds/BattlefieldPreview");Directory.CreateDirectory("Builds/phase5-forecasts");
      var shoot=typeof(BattlefieldPreview).GetMethod("Shoot",BindingFlags.Static|BindingFlags.NonPublic);
      foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1440,1080)})
        for(int biome=2;biome<=7;biome++)
        {
          shoot.Invoke(null,new object[]{biome,size,42f,false,false,true,1,0,0,true,0});
          string name=$"env{biome}-{size.x}x{size.y}-p42-full-cast-tactical1-challenge.png";
          File.Copy("Builds/BattlefieldPreview/"+name,"Builds/phase5-forecasts/"+name,true);
        }
      Debug.Log("BIOME FORECAST PREVIEW: twelve final-wave forecasts passed on phone/tablet.");EditorApplication.Exit(0);
    }
    catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
  private static void Render(string folder)
  {
    try
    {
      ShaderUtil.allowAsyncCompilation=false;
      Directory.CreateDirectory("Builds/BattlefieldPreview");Directory.CreateDirectory("Builds/"+folder);
      var shoot=typeof(BattlefieldPreview).GetMethod("Shoot",BindingFlags.Static|BindingFlags.NonPublic);
      foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1440,1080)})
        for(int biome=1;biome<=7;biome++)
        {
          shoot.Invoke(null,new object[]{biome,size,42f,false,false,true,0,0,0,false,0});
          string name=$"env{biome}-{size.x}x{size.y}-p42-full-cast.png";
          File.Copy("Builds/BattlefieldPreview/"+name,"Builds/"+folder+"/"+name,true);
        }
      Debug.Log("PHASE 5 PREVIEW: fourteen real battlefield/HUD frames passed across all seven biomes.");
      EditorApplication.Exit(0);
    }
    catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
  }
}
