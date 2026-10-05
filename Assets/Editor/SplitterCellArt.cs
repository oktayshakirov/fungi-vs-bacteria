using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Floating buds are effects. The spiked, textured parent remains authored art.
public static class SplitterCellArt
{
  const string MeshPath="Assets/Meshes/Enemies/DaughterCell.asset";
  public static void Refine(GameObject splitter)
  {
    foreach(var trait in splitter.GetComponentsInChildren<EnemyTrait>(true))
      if(trait.name.StartsWith("DaughterCell"))Object.DestroyImmediate(trait.gameObject);
    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
    if(mesh==null)
    {
      // Eye-white meshes have an iris opening and produce hollow rings when
      // reused as cells. Bake a closed Unity sphere once; no runtime allocation.
      var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);
      mesh=Object.Instantiate(sphere.GetComponent<MeshFilter>().sharedMesh);
      mesh.name="Closed daughter cell";Object.DestroyImmediate(sphere);
      AssetDatabase.CreateAsset(mesh,MeshPath);
    }
    var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Enemies/DaughterCell.mat");
    var amber=new Color(1,.58f,.12f,1);
    material.SetFloat("_Smoothness",.27f);material.SetFloat("_Metallic",0);
    material.SetColor("_EmissionColor",amber*.15f);material.EnableKeyword("_EMISSION");
    material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
    EditorUtility.SetDirty(material);
    var body=Enemy.FindBodyRenderer(splitter);var source=body.GetComponent<MeshFilter>().sharedMesh.bounds;
    Matrix4x4 matrix=splitter.transform.worldToLocalMatrix*body.transform.localToWorldMatrix;
    Bounds bounds=new Bounds(matrix.MultiplyPoint3x4(source.center),Vector3.zero);
    for(int i=0;i<8;i++)bounds.Encapsulate(matrix.MultiplyPoint3x4(source.center+new Vector3(
      (i&1)==0?-source.extents.x:source.extents.x,(i&2)==0?-source.extents.y:source.extents.y,(i&4)==0?-source.extents.z:source.extents.z)));
    float width=(bounds.size.x+bounds.size.z)*.5f;
    Vector3[] offsets={new Vector3(.54f,.52f,.14f),new Vector3(-.32f,.26f,-.50f),new Vector3(.16f,.18f,.55f),
      new Vector3(-.50f,.62f,.22f),new Vector3(.30f,.78f,-.34f),new Vector3(-.14f,.86f,.12f)};
    float[] sizes={.24f,.192f,.168f,.144f,.128f,.104f};
    for(int i=0;i<offsets.Length;i++)
    {
      var cell=new GameObject("DaughterCell"+(i+1));cell.transform.SetParent(splitter.transform,false);
      cell.transform.localPosition=new Vector3(bounds.center.x+offsets[i].x*bounds.size.x,
        bounds.min.y+offsets[i].y*bounds.size.y,bounds.center.z+offsets[i].z*bounds.size.z);
      cell.transform.localScale=Vector3.one*width*sizes[i];
      cell.AddComponent<MeshFilter>().sharedMesh=mesh;
      var renderer=cell.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
      renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
      var trait=cell.AddComponent<EnemyTrait>();trait.accentColor=amber;trait.tintWithBiome=false;
      trait.motion=EnemyTrait.Motion.Orbit;trait.motionSpeed=.12f;trait.motionAmount=.6f;
    }
  }
}
