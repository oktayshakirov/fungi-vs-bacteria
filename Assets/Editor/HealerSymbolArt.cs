using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// A small effect symbol, rather than a replacement for the organic enemy art.
public static class HealerSymbolArt
{
  const string MeshPath="Assets/Meshes/Enemies/HealingPlus.asset";
  static readonly Color SignColor=new Color(.76f,.055f,.08f,1);

  public static void Refine(GameObject healer)
  {
    // Replace only the old paired capsule bars; preserve the body and aura.
    foreach(var trait in healer.GetComponentsInChildren<EnemyTrait>(true))
      if(trait.name.StartsWith("HealSign"))Object.DestroyImmediate(trait.gameObject);
    Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
    if(mesh==null)
    {
      mesh=BuildMesh();AssetDatabase.CreateAsset(mesh,MeshPath);
    }
    Material material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Enemies/HealSign.mat");
    material.SetColor("_BaseColor",SignColor);material.SetColor("_Color",SignColor);
    material.SetFloat("_Surface",0);material.SetFloat("_SrcBlend",1);material.SetFloat("_DstBlend",0);
    material.SetFloat("_SrcBlendAlpha",1);material.SetFloat("_DstBlendAlpha",0);material.SetFloat("_ZWrite",1);
    material.SetFloat("_Smoothness",.24f);material.SetFloat("_Metallic",0);
    material.SetColor("_EmissionColor",SignColor*.12f);
    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    material.EnableKeyword("_EMISSION");material.SetOverrideTag("RenderType","Opaque");
    material.renderQueue=-1;material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
    EditorUtility.SetDirty(material);

    // Authored body bounds transformed into the prefab root's coordinates.
    var body=Enemy.FindBodyRenderer(healer);var local=body.GetComponent<MeshFilter>().sharedMesh.bounds;
    Matrix4x4 matrix=healer.transform.worldToLocalMatrix*body.transform.localToWorldMatrix;
    Bounds bounds=new Bounds(matrix.MultiplyPoint3x4(local.center),Vector3.zero);
    for(int i=0;i<8;i++)bounds.Encapsulate(matrix.MultiplyPoint3x4(local.center+new Vector3(
      (i&1)==0?-local.extents.x:local.extents.x,(i&2)==0?-local.extents.y:local.extents.y,(i&4)==0?-local.extents.z:local.extents.z)));
    float width=(bounds.size.x+bounds.size.z)*.5f;
    Vector3[] offsets={new Vector3(.70f,1.20f,.12f),new Vector3(-.47f,1.18f,-.63f),new Vector3(-.16f,1.55f,.55f)};
    for(int i=0;i<offsets.Length;i++)
    {
      var sign=new GameObject("HealSign"+(i+1));sign.transform.SetParent(healer.transform,false);
      sign.transform.localPosition=new Vector3(bounds.center.x+offsets[i].x*bounds.size.x,
        bounds.min.y+offsets[i].y*bounds.size.y,bounds.center.z+offsets[i].z*bounds.size.z);
      sign.transform.localScale=Vector3.one*width*(i==2?.18f:.21f);
      sign.AddComponent<MeshFilter>().sharedMesh=mesh;
      var renderer=sign.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
      renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
      var trait=sign.AddComponent<EnemyTrait>();trait.accentColor=SignColor;trait.tintWithBiome=false;
      trait.motion=EnemyTrait.Motion.Orbit;trait.motionSpeed=.07f;trait.motionAmount=.7f;trait.faceCamera=true;trait.includeInHealthBarBounds=true;
    }
  }

  static Mesh BuildMesh()
  {
    // Equal-length arms with a narrow bevel and genuine thickness. The single
    // mesh avoids intersecting bars and remains square in the camera plane.
    Vector2[] Ring(float h,float end)=>new[]{new Vector2(-h,end),new Vector2(h,end),new Vector2(h,h),
      new Vector2(end,h),new Vector2(end,-h),new Vector2(h,-h),new Vector2(h,-end),new Vector2(-h,-end),
      new Vector2(-h,-h),new Vector2(-end,-h),new Vector2(-end,h),new Vector2(-h,h)};
    var outside=Ring(.16f,.5f);var inside=Ring(.135f,.475f);
    var vertices=new List<Vector3>();var triangles=new List<int>();
    void Triangle(Vector3 a,Vector3 b,Vector3 c)
    {int k=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(k);triangles.Add(k+1);triangles.Add(k+2);}
    void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
    Vector3 At(Vector2 p,float z)=>new Vector3(p.x,p.y,z);
    for(int i=0;i<12;i++)
    {
      int j=(i+1)%12;
      Triangle(new Vector3(0,0,-.05f),At(inside[i],-.05f),At(inside[j],-.05f));
      Triangle(new Vector3(0,0,.05f),At(outside[j],.05f),At(outside[i],.05f));
      Quad(At(inside[i],-.05f),At(outside[i],-.02f),At(outside[j],-.02f),At(inside[j],-.05f));
      Quad(At(outside[i],-.02f),At(outside[i],.05f),At(outside[j],.05f),At(outside[j],-.02f));
    }
    var mesh=new Mesh{name="Healing Plus · Beveled"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
    mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
  }
}
