using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A textured UI ribbon through the level markers; rebuilt only on layout changes.
[RequireComponent(typeof(CanvasRenderer))]
public class SelectionTrailGraphic : MaskableGraphic
{
    public List<Vector2> points;
    public float width = 30;
    public int completedSegments;
    public Color completedColor = new Color(.75f,.85f,0f);
    public Texture texture;
    public override Texture mainTexture => texture != null ? texture : Texture2D.whiteTexture;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if(points == null || points.Count < 2) return;
        var samples = new List<Vector2>();
        Rect rect = rectTransform.rect;
        for(int i=0;i<points.Count-1;i++)
        {
            Vector2 a=points[Mathf.Max(0,i-1)], b=points[i], c=points[i+1], d=points[Mathf.Min(points.Count-1,i+2)];
            for(int j=0;j<20;j++)
            {
                float t=j/20f;
                Vector2 p=.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
                samples.Add(rect.min+Vector2.Scale(p,rect.size));
            }
        }
        samples.Add(rect.min+Vector2.Scale(points[points.Count-1],rect.size));
        float distance=0;
        for(int i=0;i<samples.Count;i++)
        {
            Vector2 direction=(samples[Mathf.Min(i+1,samples.Count-1)]-samples[Mathf.Max(0,i-1)]).normalized;
            Vector2 normal=new Vector2(-direction.y,direction.x)*width*.5f;
            if(i>0) distance+=Vector2.Distance(samples[i],samples[i-1]);
            Color tint = i<=completedSegments*20 && completedSegments>0 ? completedColor : color;
            // Transparent shoulders soften the trail into the terrain without an
            // extra material, texture or per-frame work. UVs retain the real grain.
            Color shoulder = tint; shoulder.a = 0;
            Vector2 inset = normal * Mathf.Max(0, 1f - 4f / width);
            vh.AddVert(samples[i]-normal,shoulder,new Vector2(distance/100,0));
            vh.AddVert(samples[i]-inset,tint,new Vector2(distance/100,.12f));
            vh.AddVert(samples[i]+inset,tint,new Vector2(distance/100,.88f));
            vh.AddVert(samples[i]+normal,shoulder,new Vector2(distance/100,1));
            if(i==0) continue;
            int n=i*4;
            for(int strip=0;strip<3;strip++)
            {
                vh.AddTriangle(n-4+strip,n-3+strip,n+strip);
                vh.AddTriangle(n+strip,n-3+strip,n+strip+1);
            }
        }
    }
}
