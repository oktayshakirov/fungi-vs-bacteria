using TMPro;
using UnityEngine;

// A small on-screen readout of frame rate and live object counts.
//
// Frame timing is the one thing that cannot be measured from the editor in
// batch mode — it needs the real device. This shows smoothed FPS, the worst
// frame in the last few seconds (stutter matters more than the average), and
// how many enemies are alive, which is what drives the late-game cost.
//
// Set Enabled = false before shipping.
public class PerfOverlay : MonoBehaviour
{
  public static bool Enabled = true;

  private const float Window = 3f;      // seconds the worst-frame figure covers
  private const float Interval = 0.25f; // how often the text is rewritten

  private TMP_Text label;
  private float accumulated;
  private int frames;
  private float sinceRefresh;
  private float worstFrameTime;
  private float windowAge;

  public static void Create(Transform parent)
  {
    if (!Enabled || parent == null) return;

    var go = new GameObject("PerfOverlay", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    go.AddComponent<PerfOverlay>().Build();
  }

  private void Build()
  {
    transform.SetAsLastSibling();

    var rect = (RectTransform)transform;
    rect.anchorMin = new Vector2(0.5f, 0f);
    rect.anchorMax = new Vector2(0.5f, 0f);
    rect.pivot = new Vector2(0.5f, 0f);
    rect.anchoredPosition = new Vector2(0f, 12f);
    rect.sizeDelta = new Vector2(560f, 34f);

    label = gameObject.AddComponent<TextMeshProUGUI>();
    UiFont.Apply(label);
    label.fontSize = 22f;
    label.alignment = TextAlignmentOptions.Center;
    label.color = new Color(1f, 1f, 1f, 0.75f);
    label.outlineWidth = 0.2f;
    label.outlineColor = new Color32(0, 0, 0, 200);
    label.raycastTarget = false;
  }

  private void Update()
  {
    // Unscaled, so the 2x speed toggle and pausing do not skew the reading
    float dt = Time.unscaledDeltaTime;
    accumulated += dt;
    frames++;
    worstFrameTime = Mathf.Max(worstFrameTime, dt);

    windowAge += dt;
    if (windowAge >= Window)
    {
      windowAge = 0f;
      worstFrameTime = dt;
    }

    sinceRefresh += dt;
    if (sinceRefresh < Interval) return;

    float fps = frames / Mathf.Max(accumulated, 0.0001f);
    float worstFps = 1f / Mathf.Max(worstFrameTime, 0.0001f);
    int enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None).Length;

    label.text = $"{fps:0} fps   low {worstFps:0}   enemies {enemies}";
    label.color = fps < 30f
      ? new Color(1f, 0.45f, 0.45f, 0.9f)
      : new Color(1f, 1f, 1f, 0.75f);

    accumulated = 0f;
    frames = 0;
    sinceRefresh = 0f;
  }
}
