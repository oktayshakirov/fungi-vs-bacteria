using UnityEngine;

// The mushroom house squashes and springs back when an enemy reaches it, so a
// leak is visible where it happened and not only in the health number in the
// corner. Driven by GameManager.OnBaseDamaged; unscaled time, so it still plays
// if the hit ends the run and the game pauses on the game-over screen.
public class BaseFlinch : MonoBehaviour
{
  private const float Duration = 0.45f;

  private Vector3 restScale;
  private float startedAt = -1f;

  private void Awake() => restScale = transform.localScale;

  private void OnEnable() => GameManager.OnBaseDamaged += Flinch;
  private void OnDisable() => GameManager.OnBaseDamaged -= Flinch;

  private void Flinch(int damage)
  {
    startedAt = Time.unscaledTime;
  }

  private void Update()
  {
    if (startedAt < 0f) return;
    float t = (Time.unscaledTime - startedAt) / Duration;
    if (t >= 1f)
    {
      transform.localScale = restScale;
      startedAt = -1f;
      return;
    }

    // A damped squash: flattened and widened on impact, overshooting once.
    float wave = Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t);
    transform.localScale = new Vector3(
      restScale.x * (1f + 0.12f * wave),
      restScale.y * (1f - 0.18f * wave),
      restScale.z * (1f + 0.12f * wave));
  }
}
