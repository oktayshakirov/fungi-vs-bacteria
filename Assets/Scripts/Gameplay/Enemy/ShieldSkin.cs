using UnityEngine;

// Which skin the Shielded enemy's body wears: the plated carapace while its
// absorb pool holds, the bare body once that pool is empty.
//
// This replaces the translucent orb that used to sit around the whole enemy.
// The orb was removed because a transparent sphere over a detailed body read as
// a lump of glass rather than as armour - but removing it took the ONLY
// in-world sign that the shield was down with it, and with it the only way to
// see that the 4-second regen delay was running. The health bar's cyan strip
// shows how MUCH shield is left; this shows whether there is any at all, which
// is the part that changes how the enemy should be shot at.
//
// A material swap rather than a second mesh: the plating is a baked texture on
// the body, so there is no shell to hide, and two shared materials cost nothing
// per enemy. Both are authored by EnemySurfaceSetup.
//
// The body's colour comes from Enemy's MaterialPropertyBlock, which is per
// RENDERER and not per material, so it survives the swap untouched - the enemy
// keeps its type colour and its biome tint in both states.
public class ShieldSkin : MonoBehaviour
{
  [SerializeField] private Material plated;
  [SerializeField] private Material bare;

  private MeshRenderer body;
  private bool applied, hasState;

  public void Bind(Material platedSkin, Material bareSkin)
  {
    plated = platedSkin;
    bare = bareSkin;
  }

  public void Apply(bool shieldUp)
  {
    if (plated == null || bare == null) return;
    if (hasState && applied == shieldUp) return;

    if (body == null) body = Enemy.FindBodyRenderer(gameObject);
    if (body == null) return;

    body.sharedMaterial = shieldUp ? plated : bare;
    applied = shieldUp;
    hasState = true;
  }
}
