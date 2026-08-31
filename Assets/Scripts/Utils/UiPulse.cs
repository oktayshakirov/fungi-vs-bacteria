using UnityEngine;
using UnityEngine.UI;

// A slow breathing pulse on a graphic's alpha.
//
// Used for the neon cue on the level you are meant to play next: a static glow
// reads as decoration, while one that breathes reads as "here". Deliberately
// slow and shallow — a fast or high-contrast pulse on a menu is fatiguing.
//
// Driven by unscaledDeltaTime so it keeps moving on screens shown while the
// game is paused (timeScale 0), which is every menu in this project.
[RequireComponent(typeof(Graphic))]
public class UiPulse : MonoBehaviour
{
  [SerializeField] private float minAlpha = 0.30f;
  [SerializeField] private float maxAlpha = 0.85f;
  [SerializeField] private float speed = 2.0f;

  private Graphic target;
  private float phase;

  public static UiPulse Attach(Graphic graphic, float minAlpha, float maxAlpha, float speed)
  {
    if (graphic == null) return null;

    var pulse = graphic.gameObject.AddComponent<UiPulse>();
    pulse.minAlpha = minAlpha;
    pulse.maxAlpha = maxAlpha;
    pulse.speed = speed;

    // Start at full brightness rather than at the bottom of the cycle: batch
    // mode captures a single frame with no Update, so a preview render would
    // otherwise show the glow at its dimmest.
    Color c = graphic.color;
    c.a = maxAlpha;
    graphic.color = c;
    return pulse;
  }

  private void Awake()
  {
    target = GetComponent<Graphic>();
  }

  private void Update()
  {
    if (target == null) return;

    phase += Time.unscaledDeltaTime * speed;
    float k = (Mathf.Sin(phase) + 1f) * 0.5f;

    Color c = target.color;
    c.a = Mathf.Lerp(minAlpha, maxAlpha, k);
    target.color = c;
  }
}
