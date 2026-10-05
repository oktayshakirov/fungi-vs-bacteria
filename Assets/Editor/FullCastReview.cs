using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class FullCastReview
{
  public static readonly string[] Towers={"ArcherTower","IceTower","PoisonTower","InfernoTower","SniperTower","ShockTower","AuraTower","DefenseTower"};
  public static readonly string[] Enemies={"BasicEnemy","FastEnemy","HealerEnemy","ArmoredEnemy","SplitterEnemy","SwarmEnemy","ShieldedEnemy","BossEnemy"};
  const string Output="Builds/FullCastReview";
  [MenuItem("Tools/Art/Review Complete Cast")]
  public static void Render()
  {
    Directory.CreateDirectory(Output);
    bool previous=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
    try
    {
      Gallery(Towers,true,"towers",0);Gallery(Enemies,false,"enemies",0);
      Gallery(Towers,true,"towers-side",90);Gallery(Enemies,false,"enemies-side",90);
      var shield=VisualSlicePreview.Portrait("ShieldedEnemy","SHIELDED · SHIELD DEPLETED",false,false,0,true);
      File.WriteAllBytes(Output+"/shield-depleted.png",shield.EncodeToPNG());Object.DestroyImmediate(shield);
      Debug.Log("FULL CAST REVIEW: all eight towers and eight enemies rendered at two headings, plus depleted shield state.");
    }
    finally {ShaderUtil.allowAsyncCompilation=previous;}
  }
  static void Gallery(string[] names,bool tower,string label,float viewYaw)
  {
    var tiles=new Color[names.Length][];
    for(int i=0;i<names.Length;i++)
    {
      string title=names[i].Replace("Tower","").Replace("Enemy","").ToUpperInvariant();
      var tile=VisualSlicePreview.Portrait(names[i],title,tower,false,viewYaw);
      File.WriteAllBytes(Output+"/"+names[i]+(viewYaw==0?"":"-side")+".png",tile.EncodeToPNG());
      tiles[i]=tile.GetPixels();Object.DestroyImmediate(tile);
    }
    var gallery=new Texture2D(2560,1280,TextureFormat.RGB24,false);
    for(int i=0;i<names.Length;i++)gallery.SetPixels((i%4)*640,i<4?640:0,640,640,tiles[i]);
    gallery.Apply();File.WriteAllBytes(Output+"/"+label+".png",gallery.EncodeToPNG());Object.DestroyImmediate(gallery);
  }
  public static void RunBatch()
  {
    try {Render();EditorApplication.Exit(0);}
    catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
}
