using UnityEngine;
using UnityEngine.UI;

// Guarantees a background image always covers the whole screen, even when the
// screen aspect ratio differs from the image: it scales the image up uniformly
// and crops the overflow (like CSS `background-size: cover`) instead of
// letterboxing or stretching. Sits outside any SafeArea so it also fills the
// notch and rounded-corner regions.
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class BackgroundFill : MonoBehaviour
{
  private RectTransform rectTransform;
  private Image image;
  private Vector2 lastCanvasSize = Vector2.zero;

  private void OnEnable()
  {
    rectTransform = GetComponent<RectTransform>();
    image = GetComponent<Image>();
    Fill();
  }

  private void OnRectTransformDimensionsChange()
  {
    if (isActiveAndEnabled) Fill();
  }

  private void Update()
  {
    // The canvas can resize without our own rect changing (e.g. rotation)
    RectTransform canvasRect = GetCanvasRect();
    if (canvasRect != null && canvasRect.rect.size != lastCanvasSize)
    {
      Fill();
    }
  }

  private void Fill()
  {
    if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
    RectTransform canvasRect = GetCanvasRect();
    if (canvasRect == null) return;

    Vector2 canvasSize = canvasRect.rect.size;
    lastCanvasSize = canvasSize;

    // Anchor to the canvas centre so the crop is symmetric
    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
    rectTransform.pivot = new Vector2(0.5f, 0.5f);
    rectTransform.anchoredPosition = Vector2.zero;

    if (image == null) image = GetComponent<Image>();
    Sprite sprite = image != null ? image.sprite : null;

    if (sprite == null)
    {
      // Solid-colour fill: just cover the canvas exactly
      rectTransform.sizeDelta = canvasSize;
      return;
    }

    // Cover: scale the sprite so both axes are at least the canvas size
    Vector2 spriteSize = sprite.rect.size;
    if (spriteSize.x < 1f || spriteSize.y < 1f)
    {
      rectTransform.sizeDelta = canvasSize;
      return;
    }

    float scale = Mathf.Max(canvasSize.x / spriteSize.x, canvasSize.y / spriteSize.y);
    rectTransform.sizeDelta = spriteSize * scale;
  }

  private RectTransform GetCanvasRect()
  {
    Canvas canvas = GetComponentInParent<Canvas>();
    if (canvas == null) return null;
    return canvas.rootCanvas.GetComponent<RectTransform>();
  }
}
