using UnityEngine;

// Springs a freshly placed tower up to full size with a slight overshoot, so it
// lands on the board with some weight instead of blinking into existence.
// Removes itself when finished.
public class TowerPopIn : MonoBehaviour
{
  private const float Duration = 0.32f;
  private const float Overshoot = 1.18f;

  private Vector3 targetScale;
  private float age;

  public static void Play(GameObject target)
  {
    if (target == null) return;
    // Restarting on an existing component would fight the one already running
    if (target.GetComponent<TowerPopIn>() != null) return;
    target.AddComponent<TowerPopIn>();
  }

  private void Awake()
  {
    targetScale = transform.localScale;
    transform.localScale = targetScale * 0.35f;
  }

  private void Update()
  {
    age += Time.deltaTime;
    float t = Mathf.Clamp01(age / Duration);

    // Rise past full size, then settle back
    float scale = t < 0.65f
      ? Mathf.Lerp(0.35f, Overshoot, Mathf.SmoothStep(0f, 1f, t / 0.65f))
      : Mathf.Lerp(Overshoot, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.65f) / 0.35f));

    transform.localScale = targetScale * scale;

    if (t >= 1f)
    {
      transform.localScale = targetScale;
      Destroy(this);
    }
  }
}
