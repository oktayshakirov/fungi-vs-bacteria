using System.IO;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
public static class EnemyMeshOptimization {
 [MenuItem("Tools/Enemies/Export Original Bodies for Optimization")]
 public static void Export(){
  Directory.CreateDirectory("Builds/MeshOptimization");
  foreach(string name in new[]{"BasicEnemy","ArmoredEnemy"}){
   var root=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/{name}.prefab");
   var filter=Enemy.FindBodyRenderer(root).GetComponent<MeshFilter>();
   var original=PrefabUtility.GetCorrespondingObjectFromOriginalSource(filter);
   var mesh=original!=null?original.sharedMesh:filter.sharedMesh;var b=new StringBuilder();
   foreach(var p in mesh.vertices)b.AppendFormat(CultureInfo.InvariantCulture,"v {0:R} {1:R} {2:R}\n",p.x,p.y,p.z);
   foreach(var uv in mesh.uv)b.AppendFormat(CultureInfo.InvariantCulture,"vt {0:R} {1:R}\n",uv.x,uv.y);
   var tri=mesh.triangles;for(int i=0;i<tri.Length;i+=3)b.Append($"f {tri[i]+1}/{tri[i]+1} {tri[i+1]+1}/{tri[i+1]+1} {tri[i+2]+1}/{tri[i+2]+1}\n");
   File.WriteAllText($"Builds/MeshOptimization/{name}.obj",b.ToString());Debug.Log($"MESH EXPORT {name}: {mesh.vertexCount} vertices, {tri.Length/3} triangles");
  }
 }

 [System.Serializable] class MeshData {public Vector3[] vertices;public Vector3[] normals;public Vector2[] uv;public int[] triangles;}
 [MenuItem("Tools/Enemies/Import Optimized Bodies")]
 public static void Import(){
  Directory.CreateDirectory("Assets/Models/OptimizedEnemies");AssetDatabase.Refresh();
  foreach(string name in new[]{"BasicEnemy","ArmoredEnemy"}){
   var data=JsonUtility.FromJson<MeshData>(File.ReadAllText($"Builds/MeshOptimization/{name}.json"));
   string path=$"Assets/Models/OptimizedEnemies/{name}.asset";
   var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
   if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
   mesh.name=name+" Mobile Body";mesh.vertices=data.vertices;mesh.normals=data.normals;mesh.uv=data.uv;mesh.triangles=data.triangles;
   mesh.RecalculateBounds();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);
   string prefabPath=$"Assets/Prefabs/Enemies/{name}.prefab";
   var root=PrefabUtility.LoadPrefabContents(prefabPath);
   try {Enemy.FindBodyRenderer(root).GetComponent<MeshFilter>().sharedMesh=mesh;PrefabUtility.SaveAsPrefabAsset(root,prefabPath);}
   finally {PrefabUtility.UnloadPrefabContents(root);}
   Debug.Log($"OPTIMIZED {name}: {mesh.vertexCount} vertices, {mesh.triangles.Length/3} triangles");
  }
  AssetDatabase.SaveAssets();
 }
}
