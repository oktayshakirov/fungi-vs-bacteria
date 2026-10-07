using UnityEngine;

// Makes the base tappable: tapping the mushroom house opens BasePanel, where
// health can be bought back or the ceiling raised (see BaseUpgrades).
//
// The house is built from merged model pieces with no collider of its own, so
// one sphere is fitted round all of them - generous on purpose, since it is a
// thumb target on a phone - on the Tower layer, which is what HUDManager's
// selection raycast already looks at.
public class BaseHouse : MonoBehaviour
{
  public static BaseHouse Current { get; private set; }

  // World-space bounds of the house's renderers, measured once at start.
  public Bounds Bounds { get; private set; }

  private void Start()
  {
    Current = this;

    Renderer[] renderers = GetComponentsInChildren<Renderer>();
    if (renderers.Length == 0) return;
    Bounds bounds = renderers[0].bounds;
    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
    Bounds = bounds;

    var collider = gameObject.AddComponent<SphereCollider>();
    collider.center = transform.InverseTransformPoint(bounds.center);
    float scale = Mathf.Max(0.0001f, transform.lossyScale.x);
    collider.radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.15f / scale;

    int layer = LayerMask.NameToLayer("Tower");
    if (layer >= 0) gameObject.layer = layer;
  }

  private void OnEnable() => GameManager.OnBaseRepaired += Repaired;
  private void OnDisable() => GameManager.OnBaseRepaired -= Repaired;

  // A heal or reinforcement shows on the board, not only in the corner chip.
  private void Repaired()
  {
    if (Bounds.size == Vector3.zero) return;
    Vector3 foot = new Vector3(Bounds.center.x, Bounds.min.y + 0.1f, Bounds.center.z);
    float radius = Mathf.Max(Bounds.extents.x, Bounds.extents.z) * 1.4f;
    CombatPulse.Emit(foot, radius, UiSkin.Health, 0.6f, CombatPulse.Shape.Ring, important: true);
  }

  private void OnDestroy()
  {
    if (Current == this) Current = null;
  }
}
