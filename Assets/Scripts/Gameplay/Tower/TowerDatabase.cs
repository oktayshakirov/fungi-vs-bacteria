using UnityEngine;

[CreateAssetMenu(fileName = "TowerDatabase", menuName = "Tower Defense/Tower Database")]
public class TowerDatabase : ScriptableObject
{
  [Header("Available Towers")]
  public TowerConfig[] availableTowers;

  [Header("Settings")]
  public bool sortByPrice = true;

  private void OnValidate()
  {
    if (sortByPrice && availableTowers != null)
    {
      System.Array.Sort(availableTowers, (a, b) => a.cost.CompareTo(b.cost));
    }
  }
}