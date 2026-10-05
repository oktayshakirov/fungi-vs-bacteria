using UnityEngine;
using UnityEngine.Rendering;

// Two restrained ground arcs identify the active phase without changing the
// original boss mesh. Enemy drives this; there is no extra per-part Update.
public class BossCue : MonoBehaviour
{
  private LineRenderer[] arcs;
  private MaterialPropertyBlock block;
  public void Pose(float radius,Enemy.BossStage stage,bool warning)
  {
    if(arcs==null)
    {
      arcs=new LineRenderer[2];block=new MaterialPropertyBlock();
      for(int j=0;j<2;j++)
      {
        var go=new GameObject("Boss phase arc");go.transform.SetParent(transform,false);
        var line=go.AddComponent<LineRenderer>();line.sharedMaterial=CombatPulse.SharedMaterial();
        line.positionCount=13;line.useWorldSpace=true;line.alignment=LineAlignment.View;
        line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.widthMultiplier=.09f;
        arcs[j]=line;
      }
    }
    bool show=stage!=Enemy.BossStage.Stable || warning;
    Color c=warning?new Color(.93f,.72f,.28f,.65f):stage==Enemy.BossStage.Fortified
      ?new Color(.76f,.57f,.28f,.6f):new Color(.87f,.31f,.24f,.6f);
    block.SetColor("_BaseColor",c);
    for(int j=0;j<2;j++)
    {
      var line=arcs[j];line.enabled=show;
      if(!show) continue;
      line.SetPropertyBlock(block);
      for(int i=0;i<13;i++)
      {
        float angle=(j*180+25+i*130/12f)*Mathf.Deg2Rad;
        line.SetPosition(i,new Vector3(transform.position.x+Mathf.Cos(angle)*radius,.14f,transform.position.z+Mathf.Sin(angle)*radius));
      }
    }
  }
}
