using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared, safe-area layout for the two result screens. Existing actions keep
// their listeners and purchase/ad behavior; this component presents run data.
public sealed class BattleDebrief : MonoBehaviour
{
  private RectTransform actions, card, host;
  private bool victory;
  private float lastWidth, lastHeight;

  public static void Show(Transform screen, RectTransform actions, bool victory, int stars,
    BattleReport report = null, int health = -1, int wave = -1)
  {
    if (actions == null) return;
    var view = screen.GetComponent<BattleDebrief>();
    if (view == null) view = screen.gameObject.AddComponent<BattleDebrief>();
    view.actions = actions;
    view.host = actions.parent as RectTransform;
    view.victory = victory;
    var manager = GameManager.Instance;
    var level = GameSession.SelectedLevel;
    report = report ?? manager?.Report ?? new BattleReport(level != null ? level.startingHealth : 100, 0);
    if (health < 0) health = manager != null ? manager.currentHealth : victory ? report.StartingHealth : 0;
    if (wave < 0) wave = EnemySpawner.Instance != null ? EnemySpawner.Instance.WavesStarted : 0;
    int total = level?.waveConfig?.waves?.Length ?? 1;
    view.Build(report, health, Mathf.Clamp(wave,0,total), total, stars);
    view.Layout();
  }

  private void Build(BattleReport report, int health, int wave, int total, int stars)
  {
    if (card != null)
    {
      card.gameObject.SetActive(false);
      if (Application.isPlaying) Destroy(card.gameObject); else DestroyImmediate(card.gameObject);
    }
    card = Rect("BattleDebrief",host,Vector2.zero,Vector2.one);
    UiSkin.Panel(card.gameObject.AddComponent<Image>(),UiSkin.PanelDark);
    UiSkin.AddBorder(card);
    Text("DebriefHeading",card,new Vector2(.05f,.875f),new Vector2(.95f,.965f),
      report.ReachedColony == 0 ? "COLONY HELD" : "REACHED THE COLONY",24,true,UiSkin.TextPrimary);
    Text("RunHealth",card,new Vector2(.05f,.80f),new Vector2(.95f,.875f),
      $"Level {GameSession.SelectedLevel?.levelNumber ?? 1}  /  Health lost {report.HealthLost}  /  Wave {wave}/{total}",19,false,UiSkin.TextMuted);
    var entries = report.RankedEscapes();
    if (entries.Count == 0)
    {
      Text("NoEscapes",card,new Vector2(.05f,.50f),new Vector2(.95f,.76f),
        "No enemies reached the colony.",22,false,UiSkin.TextPrimary);
    }
    else
    {
      for (int i=0;i<Mathf.Min(3,entries.Count);i++)
      {
        var entry = entries[i];
        float top = .79f - i * .139f;
        var row = Rect("Escape"+i,card,new Vector2(.05f,top-.13f),new Vector2(.95f,top));
        UiSkin.Panel(row.gameObject.AddComponent<Image>(),UiSkin.PanelRaised,10);
        var portrait = Rect("Portrait",row,new Vector2(0,0),new Vector2(0,1));
        portrait.pivot=new Vector2(0,.5f);portrait.offsetMin=new Vector2(7,3);portrait.offsetMax=new Vector2(53,-3);
        var image = portrait.gameObject.AddComponent<Image>();
        image.sprite = entry.Config != null ? WaveIntel.Portrait(entry.Config) : null;
        image.preserveAspect = true; image.raycastTarget=false;image.enabled=image.sprite!=null;
        var name = Text("EnemyName",row,new Vector2(0,.5f),new Vector2(1,1f),entry.Name+"  x"+entry.Count,21,true,UiSkin.TextPrimary);
        name.rectTransform.offsetMin=new Vector2(65,0);name.rectTransform.offsetMax=new Vector2(-10,0);
        var damage = Text("EnemyDamage",row,new Vector2(0,0),new Vector2(1,.5f),
          entry.HealthLost > 0 ? entry.HealthLost+" total health lost" : "Blocked by Colony Shield",17,false,UiSkin.TextMuted);
        damage.rectTransform.offsetMin=new Vector2(65,0);damage.rectTransform.offsetMax=new Vector2(-10,0);
      }
    }
    int other = 0;
    for (int i=3;i<entries.Count;i++) other += entries[i].Count;
    string escapeSummary = other>0 ? $"+{other} other escapes  /  {report.HealthLost} total health lost" :
      report.ReachedColony>0 ? $"{report.ReachedColony} reached  /  {report.HealthLost} total health lost" : "All threats stopped along the path";
    Text("EscapeSummary",card,new Vector2(.05f,.305f),new Vector2(.95f,.382f),escapeSummary,17,false,UiSkin.TextMuted);
    Text("ReplayGoal",card,new Vector2(.05f,.165f),new Vector2(.95f,.30f),
      BattleReport.Goal(stars,report.PreviousBest,report.StartingHealth,victory,total),20,true,UiSkin.Gold);
    Text("CounterLesson",card,new Vector2(.05f,.018f),new Vector2(.95f,.16f),
      report.Lesson(),19,false,UiSkin.TextPrimary);
    // Secondary navigation is medium weight; highlighted actions stay bold.
    foreach (var button in actions.GetComponentsInChildren<Button>(true))
    {
      var element=button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
      element.minHeight=56; element.preferredHeight=56;
      var label=button.GetComponentInChildren<TMP_Text>(true);
      if (label != null) { label.fontSizeMin=18;label.fontSizeMax=24; }
    }
    var layout=actions.GetComponent<VerticalLayoutGroup>();
    if(layout!=null) {layout.padding=new RectOffset(18,18,18,18);layout.spacing=10;}
    // Collapse the status row when it is empty, including its layout spacing.
    var status=actions.Find("Status");
    if(status!=null) status.gameObject.SetActive(!string.IsNullOrEmpty(status.GetComponent<TMP_Text>()?.text));
  }

  private void LateUpdate()
  {
    if(host!=null && (Mathf.Abs(host.rect.width-lastWidth)>.5f || Mathf.Abs(host.rect.height-lastHeight)>.5f)) Layout();
  }
  private void Layout()
  {
    if(host==null || card==null)return;
    Canvas.ForceUpdateCanvases();
    lastWidth=host.rect.width; lastHeight=host.rect.height;
    float totalWidth=Mathf.Min(1120,Mathf.Max(760,lastWidth-48));
    float gap=20, actionWidth=Mathf.Clamp(totalWidth*.37f,300,390);
    float cardWidth=totalWidth-gap-actionWidth;
    float top=victory ? .57f : .73f, bottom=victory ? .06f : .17f;
    float height=lastHeight*(top-bottom);
    card.anchorMin=card.anchorMax=new Vector2(.5f,(top+bottom)*.5f);
    card.pivot=new Vector2(.5f,.5f);card.anchoredPosition=new Vector2(-(actionWidth+gap)*.5f,0);
    card.sizeDelta=new Vector2(cardWidth,height);
    actions.anchorMin=actions.anchorMax=card.anchorMin;actions.pivot=new Vector2(.5f,.5f);
    actions.anchoredPosition=new Vector2((cardWidth+gap)*.5f,0);actions.sizeDelta=new Vector2(actionWidth,actions.sizeDelta.y);
    LayoutRebuilder.ForceRebuildLayoutImmediate(actions);
    if(victory)
    {
      Move("StarsRow",.765f,new Vector2(300,74));
      var stars=host.Find("StarsRow");
      if(stars!=null) for(int i=0;i<stars.childCount;i++) { var r=stars.GetChild(i) as RectTransform;if(r!=null) {r.sizeDelta=new Vector2(68,68);r.anchoredPosition=new Vector2((i-1)*82,0);} }
      var starLayout=stars!=null?stars.GetComponent<HorizontalLayoutGroup>():null;
      if(starLayout!=null)starLayout.spacing=14;
      Move("CoinPayout",.665f,new Vector2(260,50));
      Move("ProgressSummary",.607f,new Vector2(Mathf.Min(900,lastWidth-64),28));
    }
  }
  private void Move(string name,float y,Vector2 size)
  {
    var r=(transform.Find(name) ?? host.Find(name)) as RectTransform;
    if(r==null)return;
    // Match the safe-area space used by the buttons and the debrief.
    r.SetParent(host,false);r.anchorMin=r.anchorMax=new Vector2(.5f,y);r.anchoredPosition=Vector2.zero;r.sizeDelta=size;
  }
  private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
  {
    var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
    var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
    go.AddComponent<LayoutElement>().ignoreLayout=true;return r;
  }
  private static TMP_Text Text(string name,Transform parent,Vector2 min,Vector2 max,string value,float size,bool bold,Color color)
  {
    var r=Rect(name,parent,min,max);var label=r.gameObject.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label,bold?UiSkin.Role.Heading:UiSkin.Role.Body,color);
    label.fontSizeMin=Mathf.Min(16,size);label.fontSizeMax=size;label.text=value;
    label.alignment=TextAlignmentOptions.MidlineLeft;label.raycastTarget=false;
    return label;
  }
}
