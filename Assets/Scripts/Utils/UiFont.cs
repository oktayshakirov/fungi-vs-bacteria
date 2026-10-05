using TMPro;
using UnityEngine;

// Shared, bundled text faces: readable regular/medium/semibold/bold for detailed UI,
// with the original display face reserved for prominent counters and play.
public static class UiFont
{
  private static TMP_FontAsset body, medium, emphasis, action, title;
  private static bool loaded;
  public static TMP_FontAsset Body { get { EnsureLoaded(); return body; } }
  public static TMP_FontAsset Medium { get { EnsureLoaded(); return medium != null ? medium : Body; } }
  public static TMP_FontAsset Emphasis { get { EnsureLoaded(); return emphasis != null ? emphasis : body; } }
  public static TMP_FontAsset Action { get { EnsureLoaded(); return action != null ? action : Emphasis; } }
  public static TMP_FontAsset Title { get { EnsureLoaded(); return title != null ? title : body; } }
  private static void EnsureLoaded()
  {
    if(loaded)return;loaded=true;
    body=Resources.Load<TMP_FontAsset>("Fonts/NunitoSans-Regular")
      ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/Text");
    medium=Resources.Load<TMP_FontAsset>("Fonts/NunitoSans-Medium");
    emphasis=Resources.Load<TMP_FontAsset>("Fonts/NunitoSans-SemiBold");
    action=Resources.Load<TMP_FontAsset>("Fonts/NunitoSans-Bold");
    title=Resources.Load<TMP_FontAsset>("Fonts & Materials/Title");
  }
  public static void Apply(TMP_Text label,bool useTitle=false)
  {
    var font=useTitle?Title:Body;if(label!=null && font!=null)label.font=font;
  }
  public static void ApplyReadable(TMP_Text label,bool strong=false)
  {
    var font=strong?Emphasis:Body;
    if(label==null || font==null)return;
    SetFace(label,font);
  }
  // Use real glyph weights; clear inherited prefab bold/weight overrides.
  public static void ApplyControl(TMP_Text label,bool emphasized=false)
  {
    SetFace(label,emphasized?Action:Medium);
  }
  private static void SetFace(TMP_Text label,TMP_FontAsset font)
  {
    if(label==null || font==null)return;
    label.font=font;label.fontWeight=FontWeight.Regular;
    label.fontStyle=FontStyles.Normal;label.characterSpacing=0;
  }
}
