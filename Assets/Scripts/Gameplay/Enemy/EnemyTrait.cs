using UnityEngine;

// Marks and animates the extra geometry that gives a variety enemy type its own
// silhouette. See EnemyArtSetup for how the parts are assembled.
//
// The component is a MARKER first. Enemy, EnemyHealthBar and EnemySpawner all
// need to find "the body", and the bare GetComponentInChildren<MeshRenderer>()
// they used to call returns the FIRST renderer in depth-first order - which
// after composition can be a shield bubble or a daughter cell. That would tint
// the part instead of the body and hang the health bar off the wrong thing.
// Enemy.FindBodyRenderer skips anything under an EnemyTrait; nothing else may
// assume sibling order.
//
// It is also where a part's motion lives, but it deliberately has NO Update.
// A splitter carries six orbs and a late wave holds 30+ enemies, so per-part
// Update calls would be ~200 messages a frame for decoration. Enemy already
// caches its traits and drives them from its own Update by calling Animate.
[DisallowMultipleComponent]
public class EnemyTrait : MonoBehaviour
{
  public enum Motion
  {
    None,
    Orbit,    // circles the host's vertical axis, with a slow vertical drift
    Pulse,    // swells and shrinks, for an aura that should look like it acts
    Breathe,  // a gentler swell, for a membrane that should not look rigid
  }

  [Tooltip("Accent colour for the part itself. Kept separate from the body " +
           "colour so the part stays legible against a body the biome has " +
           "pushed towards the same hue.")]
  public Color accentColor = Color.white;

  [Tooltip("Multiplies the biome enemy tint into the accent. Off keeps the " +
           "part a fixed colour everywhere, which is what makes a type read " +
           "the same in all seven biomes.")]
  public bool tintWithBiome = true;

  [Tooltip("Hides the part while the enemy's shield pool is empty. This is " +
           "the shield bubble: a shielded enemy with no shield left should " +
           "look like it lost something, and it is the only in-world cue " +
           "that the regen delay is running.")]
  public bool hideWhileShieldDown = false;

  [Header("Motion")]
  public Motion motion = Motion.None;
  [Tooltip("Cycles per second.")]
  public float motionSpeed = 1f;
  [Tooltip("Scales the whole effect. 1 is the tuned default.")]
  public float motionAmount = 1f;

  private MeshRenderer[] renderers;
  private MaterialPropertyBlock block;
  private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
  private static readonly int ColorId = Shader.PropertyToID("_Color");

  // Rest pose, captured once. Orbit works in polar coordinates around the
  // host's axis so a part keeps the radius and the bearing it was authored at
  // and simply travels around - which preserves the hand-spread arrangement of
  // the splitter's orbs instead of snapping them onto an even ring.
  private Vector3 restPosition;
  private Vector3 restScale;
  private float restRadius;
  private float restAngle;
  private float phase;
  private bool captured;

  private void Awake()
  {
    renderers = GetComponentsInChildren<MeshRenderer>(true);
    Capture();
  }

  private void Capture()
  {
    if (captured) return;
    restPosition = transform.localPosition;
    restScale = transform.localScale;
    restRadius = new Vector2(restPosition.x, restPosition.z).magnitude;
    restAngle = Mathf.Atan2(restPosition.z, restPosition.x);
    // Per-part offset so parts on one enemy do not pulse in lockstep, derived
    // from the rest bearing rather than from Random so a pooled part animates
    // identically every time it is reused.
    phase = restAngle + restPosition.y;
    captured = true;
  }

  // Driven by Enemy.Update. `time` is passed in rather than read from Time.time
  // so a preview can render the same part at several points in its cycle - the
  // amplitude is the only thing that can look wrong here, and a still frame at
  // one phase cannot show it.
  public void Animate(float time)
  {
    if (motion == Motion.None) return;
    Capture();

    float t = time * motionSpeed * Mathf.PI * 2f + phase;

    switch (motion)
    {
      case Motion.Orbit:
      {
        float angle = restAngle + time * motionSpeed * Mathf.PI * 2f;
        float lift = Mathf.Sin(t * 0.7f) * 0.05f * motionAmount * Mathf.Max(0.001f, restRadius);
        transform.localPosition = new Vector3(
          Mathf.Cos(angle) * restRadius,
          restPosition.y + lift,
          Mathf.Sin(angle) * restRadius);
        break;
      }
      case Motion.Pulse:
        transform.localScale = restScale * (1f + Mathf.Sin(t) * 0.10f * motionAmount);
        break;
      case Motion.Breathe:
        transform.localScale = restScale * (1f + Mathf.Sin(t) * 0.035f * motionAmount);
        break;
    }
  }

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
