using UnityEngine;
using UnityEngine.UI;

// Fades a screen up from black when it opens. Built at runtime and destroyed
// when it finishes, so nothing has to be wired in the prefab.
public class ScreenFade : MonoBehaviour
{
  private Image sheet;
  private float duration;
  private float age;

  // Covers `parent` in black and dissolves it away.
  public static void In(Transform parent, float duration = 0.4f)
  {
    if (parent == null) return;

    // Anchored to the canvas, not the caller, so the fade covers the notch too
    Canvas canvas = parent.GetComponentInParent<Canvas>();
    Transform host = canvas != null ? canvas.transform : parent;

    var go = new GameObject("ScreenFade", typeof(RectTransform));
    go.transform.SetParent(host, false);
    go.transform.SetAsLastSibling();   // above everything it is hiding
    UiSkin.Stretch((RectTransform)go.transform);

    var fade = go.AddComponent<ScreenFade>();
    fade.duration = Mathf.Max(0.01f, duration);
    fade.sheet = go.AddComponent<Image>();
    fade.sheet.color = Color.black;
    fade.sheet.raycastTarget = true;   // swallow taps until the screen settles
  }

  private void Update()
  {
    // Unscaled: menus can be reached while the game sits at timeScale 0
    age += Time.unscaledDeltaTime;
    float t = Mathf.Clamp01(age / duration);

    Color c = sheet.color;
    c.a = 1f - t;
    sheet.color = c;

    if (t >= 1f) Destroy(gameObject);
  }
}
