using TMPro;
using UnityEngine;

// Supplies a guaranteed-present TMP font for text created at runtime. The
// project's TMP default-font reference does not resolve inside Assets, so
// relying on it would render runtime text invisible; these font assets live in
// a Resources folder and always load.
public static class UiFont
{
  private static TMP_FontAsset body;
  private static TMP_FontAsset title;
  private static bool loaded;

  public static TMP_FontAsset Body { get { EnsureLoaded(); return body; } }
  public static TMP_FontAsset Title { get { EnsureLoaded(); return title != null ? title : body; } }

  private static void EnsureLoaded()
  {
    if (loaded) return;
    loaded = true;
    body = Resources.Load<TMP_FontAsset>("Fonts & Materials/Text");
    title = Resources.Load<TMP_FontAsset>("Fonts & Materials/Title");
  }

  // Assigns a font only if one resolved, so TMP's own fallback still applies
  // if the assets were ever moved.
  public static void Apply(TMP_Text label, bool useTitle = false)
  {
    TMP_FontAsset font = useTitle ? Title : Body;
    if (font != null) label.font = font;
  }
}
