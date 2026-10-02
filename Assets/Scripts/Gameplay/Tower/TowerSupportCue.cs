using UnityEngine;
using UnityEngine.Rendering;

// Two quiet arcs under the mushroom: gold = damage, violet = fire rate.
// Rebuilt only when the support graph changes; no Update or particle systems.
public class TowerSupportCue : MonoBehaviour
{
    static Material material;
    LineRenderer damageArc, speedArc;
    public void Set(bool damage, bool speed)
    {
        if(damage && damageArc==null) damageArc=Arc("Damage support",new Color(.95f,.69f,.27f),0);
        if(speed && speedArc==null) speedArc=Arc("Speed support",new Color(.65f,.57f,.95f),180);
        if(damageArc!=null) damageArc.enabled=damage;
        if(speedArc!=null) speedArc.enabled=speed;
    }
    LineRenderer Arc(string name,Color color,float start)
    {
        if(material==null) material=new Material(Shader.Find("Universal Render Pipeline/Unlit")) {name="Shared support cue"};
        var go=new GameObject(name);go.transform.SetParent(transform,false);
        var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;
        line.useWorldSpace=true;line.alignment=LineAlignment.TransformZ;
        go.transform.rotation=Quaternion.Euler(90,0,0);
        // Prefab scales differ widely; keep the marker stroke in world units.
        Vector3 scale=transform.lossyScale;
        go.transform.localScale=new Vector3(1/Mathf.Max(.001f,Mathf.Abs(scale.x)),1/Mathf.Max(.001f,Mathf.Abs(scale.y)),1/Mathf.Max(.001f,Mathf.Abs(scale.z)));
        line.widthMultiplier=.075f;line.numCapVertices=3;line.positionCount=19;
        float radius=(GridManager.Instance!=null?GridManager.Instance.cellSize:5f)*.25f;
        for(int i=0;i<19;i++)
        {
            float angle=(start+15+i*150f/18)*Mathf.Deg2Rad;
            line.SetPosition(i,transform.position+new Vector3(Mathf.Cos(angle)*radius,.075f,Mathf.Sin(angle)*radius));
        }
        var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);line.SetPropertyBlock(block);
        line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
        return line;
    }
}
