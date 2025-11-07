using UnityEngine;
using System.Collections.Generic;
[CreateAssetMenu(fileName = "NewPathConfig", menuName = "Tower Defense/Path Config")]
public class PathConfig : ScriptableObject
{
  [Header("Path Metadata")]
  public string pathName = "New Path";
  [TextArea(3, 5)]
  public string description;

  [Header("Path Definition")]
  public List<Vector2Int> pathGridCoordinates = new List<Vector2Int>();
}