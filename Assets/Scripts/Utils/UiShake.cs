using UnityEngine;

// The "no" gesture: a short horizontal wobble, used when a tap lands on
// something that cannot be opened yet (a locked biome, a locked level).
//
// A disabled button swallows the tap silently, which reads as a dead screen
// rather than as a locked door - so the locked cards stay tappable and answer
// with this instead. Paired with AudioManager.PlayLocked at every call site.
//
// Driven by unscaledDeltaTime: every screen that uses it is shown while the
// game is paused (timeScale 0).
public class UiShake : MonoBehaviour
{
  private const float Duration = 0.32f;
  private const float Frequency = 22f;

  private RectTransform rect;
  private Vector2 home;
  private float amplitude;
  private float elapsed;
  private bool running;

  // A sideways wobble on `target`, restarted if one is already in flight.
  public static void Nudge(RectTransform target, float amplitude = 9f)
  {
    if (target == null) return;

    var shake = target.GetComponent<UiShake>();
    if (shake == null) shake = target.gameObject.AddComponent<UiShake>();
    shake.Begin(target, amplitude);
  }

  // A quick scale punch, for the padlock badge itself: the wobble says "no",
  // this says what said it.
  public static void Punch(Transform target, float scale = 1.35f)
  {
    if (target == null) return;

    var punch = target.GetComponent<UiPunch>();
    if (punch == null) punch = target.gameObject.AddComponent<UiPunch>();
    punch.Begin(scale);
  }

  private void Begin(RectTransform target, float size)
  {
    // Captured on the first run only. Re-reading it mid-shake would bake the
    // current offset into the rest position and walk the card off its cell.
    if (!running)
    {
      rect = target;
      home = target.anchoredPosition;
    }

    amplitude = size;
    elapsed = 0f;
    running = true;
  }

  private void Update()
  {
    if (!running || rect == null) return;

    elapsed += Time.unscaledDeltaTime;
    if (elapsed >= Duration)
    {
      rect.anchoredPosition = home;
      running = false;
      return;
    }

    // Decaying sine, so it settles instead of stopping mid-swing.
    float decay = 1f - elapsed / Duration;
    float offset = Mathf.Sin(elapsed * Frequency) * amplitude * decay * decay;
    rect.anchoredPosition = home + new Vector2(offset, 0f);
  }
}

// Kept in the same file because it exists only to serve UiShake.Punch.
public class UiPunch : MonoBehaviour
{
  private const float Duration = 0.30f;

  private float peak = 1.35f;
  private float elapsed;
  private bool running;

  public void Begin(float scale)
  {
    peak = scale;
    elapsed = 0f;
    running = true;
  }

  private void Update()
  {
    if (!running) return;

    elapsed += Time.unscaledDeltaTime;
    float t = Mathf.Clamp01(elapsed / Duration);

    // Out and back, on a single arch.
    float k = Mathf.Sin(t * Mathf.PI);
    transform.localScale = Vector3.one * Mathf.Lerp(1f, peak, k);

    if (t >= 1f)
    {
      transform.localScale = Vector3.one;
      running = false;
    }
  }
}
