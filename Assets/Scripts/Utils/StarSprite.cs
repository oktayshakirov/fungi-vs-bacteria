using UnityEngine;
using UnityEngine.UI;

// Generates a 5-pointed star sprite procedurally and builds star rows, so the
// UI does not depend on a ★ glyph (the project's TMP font is ASCII-only).
public static class StarSprite
{
  private static readonly Color Earned = new Color(1f, 0.82f, 0.29f);
  private static readonly Color Empty = new Color(0.32f, 0.32f, 0.34f);

  private static Sprite sprite;

  public static Sprite Star
  {
    get
    {
      if (sprite == null) sprite = Generate(64);
      return sprite;
    }
  }

  // Adds a horizontal row of 3 stars to the container; the first `filled` are gold.
  public static void BuildRow(RectTransform container, int filled, float starSize)
  {
    float spacing = starSize * 0.15f;
    float totalWidth = starSize * 3f + spacing * 2f;
    float startX = -totalWidth * 0.5f + starSize * 0.5f;

    for (int i = 0; i < 3; i++)
    {
      var go = new GameObject($"Star{i}", typeof(RectTransform));
      go.transform.SetParent(container, false);
      var rect = (RectTransform)go.transform;
      rect.anchorMin = new Vector2(0.5f, 0.5f);
      rect.anchorMax = new Vector2(0.5f, 0.5f);
      rect.pivot = new Vector2(0.5f, 0.5f);
      rect.sizeDelta = new Vector2(starSize, starSize);
      rect.anchoredPosition = new Vector2(startX + i * (starSize + spacing), 0f);

      var image = go.AddComponent<Image>();
      image.sprite = Star;
      image.color = i < filled ? Earned : Empty;
      image.raycastTarget = false;
    }
  }

  private static Sprite Generate(int size)
  {
    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
    float outer = size * 0.48f;
    float inner = outer * 0.42f;

    // 10 alternating vertices, first point up
    var pts = new Vector2[10];
    for (int i = 0; i < 10; i++)
    {
      float r = (i % 2 == 0) ? outer : inner;
      float a = Mathf.Deg2Rad * (-90f + i * 36f);
      pts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
    }

    var pixels = new Color32[size * size];
    for (int y = 0; y < size; y++)
    {
      for (int x = 0; x < size; x++)
      {
        bool inside = PointInPolygon(new Vector2(x + 0.5f, y + 0.5f), pts);
        pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
      }
    }
    tex.SetPixels32(pixels);
    tex.Apply();
    tex.wrapMode = TextureWrapMode.Clamp;

    return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
  }

  private static bool PointInPolygon(Vector2 p, Vector2[] poly)
  {
    bool inside = false;
    for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
    {
      if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
          (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
      {
        inside = !inside;
      }
    }
    return inside;
  }
}
