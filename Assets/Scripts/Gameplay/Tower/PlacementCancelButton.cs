using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A touch-friendly "Cancel" button shown while placing a tower, so players on a
// touchscreen (who have no right-click) can back out. Built at runtime under the
// HUD canvas; no prefab wiring required.
public class PlacementCancelButton : MonoBehaviour
{
  private static PlacementCancelButton instance;

  public static void Show(Action onCancel)
  {
    if (instance == null)
    {
      Canvas canvas = FindHudCanvas();
      if (canvas == null) return;

      var go = new GameObject("PlacementCancelButton", typeof(RectTransform));
      go.transform.SetParent(canvas.transform, false);
      instance = go.AddComponent<PlacementCancelButton>();
      instance.Build();
    }

    instance.onCancel = onCancel;
    instance.gameObject.SetActive(true);
    instance.transform.SetAsLastSibling();
  }

  public static void Hide()
  {
    if (instance != null) instance.gameObject.SetActive(false);
  }

  private Action onCancel;

  private void Build()
  {
    var rect = (RectTransform)transform;
    rect.anchorMin = new Vector2(0.5f, 0f);
    rect.anchorMax = new Vector2(0.5f, 0f);
    rect.pivot = new Vector2(0.5f, 0f);
    rect.anchoredPosition = new Vector2(0f, 40f);
    rect.sizeDelta = new Vector2(360f, 110f);

    var image = gameObject.AddComponent<Image>();
    image.color = new Color(0.7f, 0.12f, 0.14f, 0.92f);

    var button = gameObject.AddComponent<Button>();
    button.targetGraphic = image;
    button.onClick.AddListener(() =>
    {
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      onCancel?.Invoke();
    });

    var textGo = new GameObject("Label", typeof(RectTransform));
    textGo.transform.SetParent(transform, false);
    var textRect = (RectTransform)textGo.transform;
    textRect.anchorMin = Vector2.zero;
    textRect.anchorMax = Vector2.one;
    textRect.offsetMin = Vector2.zero;
    textRect.offsetMax = Vector2.zero;

    var label = textGo.AddComponent<TextMeshProUGUI>();
    UiFont.Apply(label);
    label.text = "✕  CANCEL";
    label.fontSize = 44f;
    label.enableAutoSizing = true;
    label.fontSizeMin = 20f;
    label.fontSizeMax = 44f;
    label.alignment = TextAlignmentOptions.Center;
    label.fontStyle = FontStyles.Bold;
    label.color = Color.white;
    label.raycastTarget = false;
  }

  private static Canvas FindHudCanvas()
  {
    Canvas best = null;
    foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
    {
      if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
      // Prefer the lowest-sorting overlay canvas (the main HUD, not popups)
      if (best == null || canvas.sortingOrder < best.sortingOrder) best = canvas;
    }
    return best;
  }
}
