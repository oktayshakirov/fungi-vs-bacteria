using UnityEngine;
using UnityEngine.UI;

// Turns a stock Toggle into an on/off switch: a pill track that goes from
// neutral to the call-to-action colour, and a knob that slides across. The
// settings rows were checkboxes under display-font labels, which read as a web
// form rather than a game's options.
//
// Realtime, because settings can open over the paused game (timeScale 0).
public class ToggleSwitch : MonoBehaviour
{
  private const float Width = 104f;
  private const float Height = 56f;
  private const float Pad = 6f;

  private Toggle toggle;
  private Image track;
  private RectTransform knob;
  private float position;   // 0 = off, 1 = on

  public static ToggleSwitch Build(Toggle toggle, RectTransform host)
  {
    var go = new GameObject("Switch", typeof(RectTransform));
    go.transform.SetParent(host, false);
    var rect = (RectTransform)go.transform;
    rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
    rect.pivot = new Vector2(1f, 0.5f);
    rect.anchoredPosition = new Vector2(-24f, 0f);
    rect.sizeDelta = new Vector2(Width, Height);
    go.AddComponent<LayoutElement>().ignoreLayout = true;

    var sw = go.AddComponent<ToggleSwitch>();
    sw.toggle = toggle;
    sw.track = go.AddComponent<Image>();
    UiSkin.Panel(sw.track, UiSkin.Neutral, 28);

    var knobGo = new GameObject("Knob", typeof(RectTransform));
    knobGo.transform.SetParent(go.transform, false);
    sw.knob = (RectTransform)knobGo.transform;
    sw.knob.anchorMin = sw.knob.anchorMax = new Vector2(0f, 0.5f);
    sw.knob.pivot = new Vector2(0f, 0.5f);
    sw.knob.sizeDelta = new Vector2(Height - Pad * 2f, Height - Pad * 2f);
    var knobImage = knobGo.AddComponent<Image>();
    knobImage.sprite = UiSprites.Circle(128);
    knobImage.color = UiSkin.TextPrimary;
    knobImage.raycastTarget = false;

    // The switch is what gets tapped and tinted now; the stock checkbox art is
    // hidden rather than destroyed, since the prefab still references it.
    toggle.targetGraphic = sw.track;
    if (toggle.graphic != null) toggle.graphic.enabled = false;
    toggle.graphic = null;

    sw.position = toggle.isOn ? 1f : 0f;
    sw.Apply();
    return sw;
  }

  private void Update()
  {
    if (toggle == null) return;
    float target = toggle.isOn ? 1f : 0f;
    if (Mathf.Approximately(position, target)) return;
    position = Mathf.MoveTowards(position, target, Time.unscaledDeltaTime * 7f);
    Apply();
  }

  private void Apply()
  {
    float eased = position * position * (3f - 2f * position);
    knob.anchoredPosition = new Vector2(Pad + eased * (Width - Height), 0f);
    track.color = Color.Lerp(UiSkin.Neutral, UiSkin.Primary, eased);
  }
}
