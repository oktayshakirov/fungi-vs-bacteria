using UnityEngine;

// Automatically synchronized from MainGame by SelectionBoardSync. Keeping the
// scene's dimensions here lets menus build real boards without loading gameplay.
public class SelectionBoardSettings : ScriptableObject
{
  public Vector2Int gridSize;
  public float cellSize;
  public Vector3 origin;
  public float groundHeight;
  public Mesh groundMesh;
  public Quaternion groundRotation;
  public Vector3 groundScale;
  public Vector3 groundPosition;
  public Mesh soilMesh;
  public Quaternion soilRotation;
  public Vector3 soilScale;
  public Vector3 soilPosition;
}
