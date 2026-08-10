// Shared decorative margin (world units) around the playable grid. The visual
// ground and the camera framing both extend by this much so there is a border
// ring of grass to place scenery props in, without touching the play grid.
public static class BoardDecor
{
  public const float Margin = 6f;

  // Thickness of the soil rim under the grass plane. Kept thin: it is only the
  // dirt layer directly beneath the turf, and the sculpted cliff below carries
  // the rest of the island's depth. A thick slab hides the cliff entirely at the
  // game's low camera angle and the island reads as a flat brown band.
  public const float SoilThickness = 1.2f;

  // Grass sits at y=0, the slab hangs just under it, and the cliff starts where
  // the slab ends (overlapping slightly so no seam shows).
  public const float SoilTop = -0.1f;
  public static float CliffTop => SoilTop - SoilThickness + 0.05f;
}
