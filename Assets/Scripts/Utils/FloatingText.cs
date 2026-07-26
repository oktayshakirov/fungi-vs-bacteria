using UnityEngine;
using TMPro;

// Short-lived world-space number (damage taken, gold earned) that rises and
// fades. Built entirely at runtime, billboarded to the camera. No prefab needed.
public class FloatingText : MonoBehaviour
{
  private const float Lifetime = 0.75f;
  private const float RiseSpeed = 2.2f;

  private TextMeshPro label;
  private Camera cam;
  private float age;
  private Color startColor;

  public static void Spawn(Vector3 worldPosition, string text, Color color, float fontSize = 5f)
  {
    var go = new GameObject("FloatingText");
    go.transform.position = worldPosition;
    var floating = go.AddComponent<FloatingText>();
    floating.Setup(text, color, fontSize);
  }

  private void Setup(string text, Color color, float fontSize)
  {
    cam = Camera.main;

    label = gameObject.AddComponent<TextMeshPro>();
    UiFont.Apply(label);
    label.text = text;
    label.color = color;
    label.fontSize = fontSize;
    label.alignment = TextAlignmentOptions.Center;
    label.fontStyle = FontStyles.Bold;
    label.outlineWidth = 0.25f;
    label.outlineColor = new Color(0f, 0f, 0f, 0.9f);

    // Keep the text small and readable in world space
    var rect = label.rectTransform;
    rect.sizeDelta = new Vector2(6f, 2f);

    startColor = color;

    // A little horizontal scatter so stacked hits don't overlap exactly
    transform.position += new Vector3(Random.Range(-0.4f, 0.4f), 0f, 0f);
  }

  private void Update()
  {
    age += Time.deltaTime;
    if (age >= Lifetime)
    {
      Destroy(gameObject);
      return;
    }

    transform.position += Vector3.up * RiseSpeed * Time.deltaTime;

    float t = age / Lifetime;
    Color c = startColor;
    c.a = 1f - t * t; // fade out, slow at first
    if (label != null) label.color = c;
  }

  private void LateUpdate()
  {
    if (cam == null) cam = Camera.main;
    if (cam != null)
    {
      transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
  }
}
