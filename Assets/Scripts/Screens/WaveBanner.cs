using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// One compact, non-blocking announcement at a time. No per-enemy UI objects.
public class WaveBanner : MonoBehaviour
{
  private CanvasGroup group;
  private float age;
  public static void Show(Transform parent, string message, string detail = "")
  {
    // A clear-path message cannot pile up underneath the next wave announcement.
    foreach(var old in parent.GetComponentsInChildren<WaveBanner>())
    { old.gameObject.SetActive(false); Destroy(old.gameObject); }
    var rect=SelectionScreenView.Rect("WaveBanner",parent,new Vector2(.5f,.54f),new Vector2(.5f,.54f));
    rect.sizeDelta=new Vector2(Mathf.Min(580,ScreenTheme.LayoutWidth((RectTransform)parent)-260),106);
    rect.gameObject.AddComponent<WaveBanner>().Setup(message,detail);
  }
  private void Setup(string message,string detail)
  {
    group=gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts=false; group.interactable=false;
    UiSkin.Panel(gameObject.AddComponent<Image>(),new Color(.06f,.09f,.12f,.88f));
    var title=SelectionScreenView.Label(SelectionScreenView.Rect("Title",transform,new Vector2(.04f,.59f),new Vector2(.96f,.96f)),message,30,true);
    title.color=UiSkin.Gold; title.alignment=TextAlignmentOptions.Center;
    var caption=SelectionScreenView.Label(SelectionScreenView.Rect("Detail",transform,new Vector2(.04f,.07f),new Vector2(.96f,.57f)),detail,16);
    caption.alignment=TextAlignmentOptions.Center;
  }
  public static string Describe(WaveConfig.Wave wave)
  {
    return $"{WaveIntel.Count(wave)} incoming\n{WaveIntel.Hint(wave)}";
  }
  private void Update()
  {
    age+=Time.deltaTime;
    group.alpha=Mathf.Min(Mathf.Clamp01(age/.15f),Mathf.Clamp01((3.5f-age)/.5f));
    if(age>=3.5f) Destroy(gameObject);
  }
}
