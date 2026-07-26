using UnityEngine;
using TMPro;

// A transient "WAVE N" banner that scales and fades in the upper-middle of the
// screen when a wave starts. Built at runtime under the HUD canvas.
public class WaveBanner : MonoBehaviour
{
  private const float Lifetime = 1.8f;

  private TextMeshProUGUI label;
  private CanvasGroup group;
  private RectTransform rect;
  private float age;

  public static void Show(Transform canvasParent, string message)
  {
    var go = new GameObject("WaveBanner", typeof(RectTransform));
    go.transform.SetParent(canvasParent, false);
    go.AddComponent<WaveBanner>().Setup(message);
  }

  private void Setup(string message)
  {
    rect = (RectTransform)transform;
    rect.anchorMin = new Vector2(0.5f, 0.72f);
    rect.anchorMax = new Vector2(0.5f, 0.72f);
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = Vector2.zero;
    rect.sizeDelta = new Vector2(1200f, 240f);
    transform.SetAsLastSibling();

    group = gameObject.AddComponent<CanvasGroup>();
    group.blocksRaycasts = false;
    group.interactable = false;

    label = gameObject.AddComponent<TextMeshProUGUI>();
    UiFont.Apply(label, useTitle: true);
    label.text = message;
    label.fontSize = 120f;
    label.enableAutoSizing = true;
    label.fontSizeMin = 40f;
    label.fontSizeMax = 120f;
    label.alignment = TextAlignmentOptions.Center;
    label.fontStyle = FontStyles.Bold;
    label.color = new Color(1f, 0.95f, 0.7f);
    label.outlineWidth = 0.2f;
    label.outlineColor = new Color(0f, 0f, 0f, 0.85f);
    label.raycastTarget = false;
  }

  private void Update()
  {
    // Runs during normal play (timeScale 1); unscaled keeps it steady regardless
    age += Time.unscaledDeltaTime;
    float t = age / Lifetime;

    if (t >= 1f)
    {
      Destroy(gameObject);
      return;
    }

    // Pop in over the first 25%, hold, then fade out over the last 35%
    float alpha = 1f;
    if (t < 0.25f) alpha = t / 0.25f;
    else if (t > 0.65f) alpha = 1f - (t - 0.65f) / 0.35f;
    group.alpha = alpha;

    float scale = Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(t / 0.25f));
    rect.localScale = Vector3.one * scale;
  }
}
