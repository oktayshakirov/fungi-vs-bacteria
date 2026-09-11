using UnityEngine;

// Marks the extra geometry that gives a variety enemy type its own silhouette.
//
// Why this exists at all: Swarm / Shielded / Splitter / Healer all reuse one of
// the four authored base bodies, and until now the only thing telling them
// apart was a colour tint and a scale factor. A tint does not read at phone
// size against seven different biome palettes, and two types that differ only
// in size read as the same type at two distances. So each of them now carries
// one small authored mesh - a fringe, a carapace, budding lobes, a spore crown
// - parented under the base body. See Tools/Blender/enemy_traits.py.
//
// The component is a MARKER first and a behaviour second. Enemy,
// EnemyHealthBar and EnemySpawner all resolve "the body" with
// GetComponentInChildren<MeshRenderer>(), which returns the FIRST renderer in
// depth-first order - so without a way to recognise a trait, adding one could
// silently make the trait the thing that gets tinted, and make the health bar
// hover at the trait's height instead of the body's. Enemy.FindBodyRenderer
// skips anything under an EnemyTrait; nothing else may assume sibling order.
[DisallowMultipleComponent]
public class EnemyTrait : MonoBehaviour
{
  [Tooltip("Accent colour for the trait itself, before the biome tint. Kept " +
           "separate from the body colour so the trait stays legible against " +
           "a body that the biome has pushed towards the same hue.")]
  public Color accentColor = Color.white;

  [Tooltip("Multiplies the biome enemy tint into the accent. Off keeps the " +
           "trait a fixed colour everywhere, which is what makes a shielded " +
           "enemy read blue in all seven biomes.")]
  public bool tintWithBiome = true;

  [Tooltip("Hides the trait while the enemy's shield pool is empty. This is " +
           "the carapace: a shielded enemy with no shield left should look " +
           "like it lost something, and it is the only in-world cue that the " +
           "regen delay is running.")]
  public bool hideWhileShieldDown = false;

  private MeshRenderer[] renderers;
  private MaterialPropertyBlock block;
  private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
  private static readonly int ColorId = Shader.PropertyToID("_Color");

  private void Awake()
  {
    renderers = GetComponentsInChildren<MeshRenderer>(true);
  }

  // Called by Enemy whenever it applies its own appearance.
  public void ApplyTint(Color biomeTint)
  {
    if (renderers == null || renderers.Length == 0) return;
    Color c = tintWithBiome
      ? new Color(accentColor.r * biomeTint.r, accentColor.g * biomeTint.g,
                  accentColor.b * biomeTint.b, accentColor.a)
      : accentColor;

    block ??= new MaterialPropertyBlock();
    foreach (MeshRenderer r in renderers)
    {
      if (r == null) continue;
      r.GetPropertyBlock(block);
      Material shared = r.sharedMaterial;
      if (shared != null && shared.HasProperty("_BaseColor")) block.SetColor(BaseColorId, c);
      if (shared != null && shared.HasProperty("_Color")) block.SetColor(ColorId, c);
      r.SetPropertyBlock(block);
    }
  }

  // Called by Enemy when the shield pool crosses empty, not every frame.
  public void SetShieldUp(bool up)
  {
    if (!hideWhileShieldDown || renderers == null) return;
    foreach (MeshRenderer r in renderers)
    {
      if (r != null) r.enabled = up;
    }
  }
}
