using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A non-blocking first-run coach. Progress follows successful gameplay actions,
// never taps on an overlay; skipped/completed tutorials retain the existing key.
public class TutorialOverlay : MonoBehaviour
{
  private const string CompletedKey = "TutorialCompleted";
  private TMP_Text stepText, counterText;
  private bool placed, started, finished;
  private RectTransform startButton;
  private Image highlight;

  public static bool ShouldShow() => PlayerPrefs.GetInt(CompletedKey, 0) == 0;

  public static void Show(Transform canvasParent, RectTransform towerPanel = null, RectTransform startButton = null)
  {
    var root = new GameObject("TutorialOverlay", typeof(RectTransform));
    root.transform.SetParent(canvasParent, false);
    var coach=root.AddComponent<TutorialOverlay>();
    coach.startButton=startButton;
    coach.Highlight(towerPanel);
  }

  private void Awake()
  {
    UiSkin.Stretch((RectTransform)transform);
    // No scrim or full-screen raycast target: tower dragging and board taps pass through.
    var card = SelectionScreenView.Rect("Coach", transform, new Vector2(.5f,.74f),new Vector2(.5f,.74f));
    card.sizeDelta = new Vector2(Mathf.Min(440, ScreenTheme.LayoutWidth((RectTransform)transform)-300),112);
    UiSkin.Panel(card.gameObject.AddComponent<Image>(),UiSkin.PanelDark);
    counterText = SelectionScreenView.Label(SelectionScreenView.Rect("Step",card,new Vector2(.04f,.68f),new Vector2(.74f,.94f)),"",16,true);
    counterText.color=UiSkin.Primary;
    stepText = SelectionScreenView.Label(SelectionScreenView.Rect("Instruction",card,new Vector2(.04f,.08f),new Vector2(.96f,.65f)),"",18);
    var skip=SelectionScreenView.Button(SelectionScreenView.Rect("Skip",card,new Vector2(.76f,.69f),new Vector2(.96f,.95f)),"SKIP",UiSkin.Neutral,Skip);
    skip.GetComponentInChildren<TMP_Text>().fontSizeMax=14;
    started=EnemySpawner.Instance!=null && EnemySpawner.Instance.WavesStarted>0;
    Refresh();
  }

  private void OnEnable()
  {
    TowerPlacement.OnTowerPlaced += Placed;
    EnemySpawner.OnWaveStarted += Started;
  }
  private void OnDisable()
  {
    TowerPlacement.OnTowerPlaced -= Placed;
    EnemySpawner.OnWaveStarted -= Started;
    if(highlight!=null) Destroy(highlight.gameObject);
  }
  private void Placed(TowerConfig config) { placed=true; Refresh(); }
  private void Started(int wave) { started=true; Refresh(); }

  private void Refresh()
  {
    if(finished || stepText==null) return;
    if(placed && started)
    {
      finished=true;
      counterText.text="YOU'RE READY";
      stepText.text="Your fungus attacks automatically. Earn coins from enemies and build more defenses.";
      Highlight(null);
      Complete();
      StartCoroutine(Dismiss());
    }
    else if(placed)
    {
      Highlight(startButton);
      counterText.text="2 / 2   START YOUR FIRST WAVE";
      stepText.text="Tap START WAVE when you're ready. Keep bacteria away from your base.";
    }
    else
    {
      counterText.text="1 / 2   PLACE YOUR FIRST FUNGUS";
      stepText.text="Drag a fungus from the right panel onto a free tile beside the path. Or tap the fungus, then the tile.";
    }
  }
  private void Highlight(RectTransform target)
  {
    if(highlight!=null) { highlight.gameObject.SetActive(false); Destroy(highlight.gameObject); }
    if(target==null) return;
    highlight=UiSkin.AddBorder(target,UiSkin.RadiusButton,3);
    highlight.color=UiSkin.Primary;
    highlight.rectTransform.offsetMin=new Vector2(-4,-4);
    highlight.rectTransform.offsetMax=new Vector2(4,4);
  }
  private IEnumerator Dismiss() { yield return new WaitForSecondsRealtime(3); Destroy(gameObject); }
  private static void Complete() { PlayerPrefs.SetInt(CompletedKey,1); PlayerPrefs.Save(); }
  private void Skip() { Complete(); Destroy(gameObject); }
}
