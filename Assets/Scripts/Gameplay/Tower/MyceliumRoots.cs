using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Selection reveals the actual adjacent support connections. Quiet at rest,
// no Update, colliders, particles, or links across unoccupied/path cells.
public class MyceliumRoots : MonoBehaviour
{
  private readonly List<LineRenderer> lines = new List<LineRenderer>();
  private readonly List<Tower> targets = new List<Tower>();
  private Tower source;
  private Color tint;
  private static Material material;

  public void Begin(Tower tower)
  {
    source = tower;
    tint = tower.EffectiveDamageBoost > 0 ? new Color(.80f,.59f,.26f) : new Color(.61f,.52f,.80f);
    targets.Clear();
    foreach(var line in lines) line.enabled = false;
  }
  public void Connect(Tower target)
  {
    int index = targets.Count;
    targets.Add(target);
    while(lines.Count < (index+1)*3) lines.Add(BuildLine());
    Vector3 from=source.transform.position, to=target.transform.position;
    from.y=to.y=.11f;
    Vector3 direction=to-from;
    Vector3 side=Vector3.Cross(direction.normalized,Vector3.up);
    for(int branch=0;branch<3;branch++)
    {
      LineRenderer line=lines[index*3+branch];
      for(int i=0;i<7;i++)
      {
        float t=i/6f;
        float bend=Mathf.Sin(t*Mathf.PI)*(branch-1)*.3f + Mathf.Sin(t*Mathf.PI*2)*.09f;
        line.SetPosition(i,Vector3.Lerp(from,to,t)+side*bend);
      }
      var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",tint);line.SetPropertyBlock(block);
      line.widthMultiplier=branch==1?.085f:.035f;
    }
  }
  public void ShowFor(Tower selected)
  {
    for(int i=0;i<lines.Count;i++)
    {
      int edge=i/3;
      lines[i].enabled=edge<targets.Count && selected!=null && (selected==source || selected==targets[edge]);
    }
  }
  private LineRenderer BuildLine()
  {
    if(material==null) material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Shared mycelium roots"};
    var child=new GameObject("Mycelium strand");child.transform.SetParent(transform,false);
    var line=child.AddComponent<LineRenderer>();line.sharedMaterial=material;
    line.useWorldSpace=true; line.positionCount=7; line.numCapVertices=2;
    line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
    // View alignment keeps fine ground strands visible in all camera presets.
    line.alignment=LineAlignment.View;line.enabled=false;
    return line;
  }
}
