// How large towers and enemies are drawn relative to their tile.
//
// The prefabs were authored when the camera framed 75 world units across; the
// board is now 10x5 framed at ~54, and the models still filled only about a
// third of a 5-unit cell. Scaling them here keeps all eight tower prefabs and
// the enemy prefabs untouched, and keeps the two figures side by side.
public static class UnitScale
{
  public const float Tower = 1.5f;
  public const float Enemy = 1.35f;
}
