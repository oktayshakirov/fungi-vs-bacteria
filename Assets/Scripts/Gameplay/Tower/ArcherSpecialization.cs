public enum ArcherSpecialization { Balanced, Flurry, Longshot }

// A one-time choice for an individual upgraded Archer. The original model and
// config are shared; choosing a branch never mutates the tower asset.
public static class ArcherBranches
{
  public static float Damage(ArcherSpecialization branch) => branch==ArcherSpecialization.Flurry?.8f:branch==ArcherSpecialization.Longshot?1.35f:1;
  public static float Rate(ArcherSpecialization branch) => branch==ArcherSpecialization.Flurry?1.4f:branch==ArcherSpecialization.Longshot?.75f:1;
  public static float Reach(ArcherSpecialization branch) => branch==ArcherSpecialization.Flurry?.85f:branch==ArcherSpecialization.Longshot?1.25f:1;
  public static string Description(ArcherSpecialization branch) => branch==ArcherSpecialization.Flurry
    ? "Flurry: rapid shots, less damage and shorter reach."
    : "Longshot: heavier hits and more reach, slower shots.";
}
