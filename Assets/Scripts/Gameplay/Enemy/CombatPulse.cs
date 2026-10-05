using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Small local cues with a hard live budget. Ordinary shots/heals cannot use
// the reserved slots for shield breaks, split births, and boss warnings.
public class CombatPulse : MonoBehaviour
{
  public enum Shape { Ring, BrokenShield, Shot, Warning }
  public const int MinorLimit=12, TotalLimit=24;
  private static readonly List<CombatPulse> live=new List<CombatPulse>();
  private static readonly Stack<CombatPulse> pool=new Stack<CombatPulse>();
  private static Material material;
  private LineRenderer line;
  private MaterialPropertyBlock block;
  private float radius,age,duration;
  private Color tint;
  private Shape shape;
  private Transform follow;
  private Enemy followEnemy;
  private uint followSpawn;
  private bool important;
  public static int ActiveCount=>live.Count;
  public uint Revision { get; private set; }

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void Reset() { live.Clear();pool.Clear(); }
  public static CombatPulse Emit(Vector3 position,float radius,Color color,float duration,Shape shape=Shape.Ring,bool important=false,Transform follow=null)
  {
    live.RemoveAll(e=>e==null);
    int minor=0;
    foreach(var pulse in live) if(!pulse.important) minor++;
    if(!important && (minor>=MinorLimit || live.Count>=TotalLimit)) return null;
    if(live.Count>=TotalLimit)
    {
      CombatPulse replace=live.Find(e=>!e.important);
      if(replace==null) return null;
      replace.Release();
    }
    CombatPulse effect=null;
    while(pool.Count>0 && effect==null) effect=pool.Pop();
    if(effect==null)
    {
      effect=new GameObject("Combat pulse").AddComponent<CombatPulse>();
      effect.Build();
    }
    unchecked { effect.Revision++; }
    effect.radius=Mathf.Clamp(radius,.12f,6);
    effect.tint=color;effect.duration=Mathf.Max(.05f,duration);effect.age=0;
    effect.shape=shape;effect.important=important;effect.follow=follow;
    effect.followEnemy=follow!=null?follow.GetComponent<Enemy>():null;
    effect.followSpawn=effect.followEnemy!=null?effect.followEnemy.SpawnVersion:0;
    effect.transform.position=position;
    effect.gameObject.SetActive(true);live.Add(effect);
    effect.Pose(0);
    return effect;
  }
  public static Material SharedMaterial()
  {
    if(material!=null) return material;
    material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Shared soft combat cue"};
    material.SetOverrideTag("RenderType","Transparent");material.renderQueue=(int)RenderQueue.Transparent;
    material.SetFloat("_Surface",1); material.SetFloat("_Blend",0);
    material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);
    material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
    material.SetInt("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    return material;
  }
  private void Build()
  {
    line=gameObject.AddComponent<LineRenderer>();line.sharedMaterial=SharedMaterial();
    line.positionCount=33;line.useWorldSpace=true;line.alignment=LineAlignment.View;
    line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.numCapVertices=1;
    block=new MaterialPropertyBlock();
  }
  public void Pose(float normalizedAge)
  {
    float t=Mathf.Clamp01(normalizedAge);
    if(follow!=null) transform.position=new Vector3(follow.position.x,.15f,follow.position.z);
    Vector3 center=transform.position;
    float expansion=shape==Shape.Warning?1+.04f*Mathf.Sin(t*Mathf.PI*6):Mathf.Lerp(.55f,1.35f,t);
    float r=radius*expansion;
    for(int i=0;i<33;i++)
    {
      float angle=i*Mathf.PI*2/32;
      Vector3 point=center+new Vector3(Mathf.Cos(angle)*r,0,Mathf.Sin(angle)*r);
      if(shape==Shape.Shot)
        point=center+new Vector3(Mathf.Cos(angle)*r,Mathf.Sin(angle)*r,0);
      if(shape==Shape.BrokenShield) point.y+=Mathf.Sin(angle*3)*.22f*(1-t);
      line.SetPosition(i,point);
    }
    float fade=shape==Shape.Warning?.65f:Mathf.Pow(1-t,1.3f)*.85f;
    Color c=tint;c.a*=fade;
    block.SetColor("_BaseColor",c);line.SetPropertyBlock(block);
    line.widthMultiplier=(shape==Shape.Shot?.055f:.085f)*(shape==Shape.Warning?1:1-t*.65f);
  }
  private void Update()
  {
    if(followEnemy!=null && (!followEnemy.IsTargetable || followEnemy.SpawnVersion!=followSpawn)) { Release();return; }
    age+=Time.deltaTime;
    if(age>=duration) { Release();return; }
    Pose(age/duration);
  }
  public void Cancel(uint revision) { if(Revision==revision && gameObject.activeSelf && live.Contains(this)) Release(); }
  private void Release() { gameObject.SetActive(false); pool.Push(this); }
  private void OnDisable() { live.Remove(this); }
}
