using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelSelectionScreen : MonoBehaviour
{
    [SerializeField] private GameObject levelCardPrefab;
    [SerializeField] private Transform cardsContainer;
    [SerializeField] private Button backButton;
    SelectionScreenView view;
    List<LevelConfig> levels;
    RectTransform route;
    TMP_Text selection, description, pageLabel;
    Button play, previous, next;
    int selectedIndex, page, columns, nextIndex;
    float lastWidth;
    readonly Dictionary<int,Button> levelButtons = new Dictionary<int,Button>();
    RectTransform selectionRing, currentBase;
    SelectionIslandPreview basePreview, sceneryPreview;
    readonly List<GameObject> nodes = new List<GameObject>();
    public int PageSize => columns * 3;

    void Start()
    {
        if (view != null) return;
        levels = LevelRepository.GetLevelsForEnvironment(GameSession.SelectedEnvironment);
        view = new SelectionScreenView(transform, EnvironmentInfo.DisplayName(GameSession.SelectedEnvironment).ToUpperInvariant(), OnBack);
        view.Theme(GameSession.SelectedEnvironment);
        view.MapTexture(GameSession.SelectedEnvironment);
        view.Title.rectTransform.anchorMax = new Vector2(.70f, .98f);
        view.ProgressBadge.anchorMin = new Vector2(.70f, .90f);
        view.ProgressBadge.anchorMax = new Vector2(.87f, .975f);
        SelectionScreenView.ButtonIcon(SelectionScreenView.Button(SelectionScreenView.Rect("Home", view.Root, new Vector2(.89f, .89f), new Vector2(.975f, .975f)), "HOME", UiSkin.Neutral, OnHome), UiSprites.Home(), 0, true);
        int stars = 0;
        foreach (var level in levels) stars += Mathf.Clamp(LevelProgress.GetStars(level.environmentName, level.levelNumber), 0, 3);
        view.Progress.text = $"{stars} / {levels.Count * 3}";
        route = SelectionScreenView.Rect("LevelTrail", view.Root, new Vector2(.07f, .22f), new Vector2(.93f, .83f));
        var footer = SelectionScreenView.Rect("SelectionFooter", view.Root, new Vector2(.025f, .025f), new Vector2(.975f, .145f));
        UiSkin.Panel(footer.gameObject.AddComponent<Image>(), new Color(.055f, .09f, .10f, .94f), 20);
        selection = SelectionScreenView.Label(SelectionScreenView.Rect("SelectedLevel", footer, new Vector2(.025f, .46f), new Vector2(.30f, .94f)), "", 27, true);
        description = SelectionScreenView.Label(SelectionScreenView.Rect("Description", footer, new Vector2(.025f, .04f), new Vector2(.48f, .47f)), "", 17);
        play = SelectionScreenView.Button(SelectionScreenView.Rect("Play", footer, new Vector2(.72f, .15f), new Vector2(.98f, .85f)), "PLAY", UiSkin.Primary, Play);
        previous = SelectionScreenView.Button(SelectionScreenView.Rect("PreviousPage", footer, new Vector2(.49f, .22f), new Vector2(.55f, .78f)), "", UiSkin.Neutral, () => ChangePage(-1));
        next = SelectionScreenView.Button(SelectionScreenView.Rect("NextPage", footer, new Vector2(.64f, .22f), new Vector2(.70f, .78f)), "", UiSkin.Neutral, () => ChangePage(1));
        EnvironmentsScreen.Arrow(previous, false); EnvironmentsScreen.Arrow(next, true);
        pageLabel = SelectionScreenView.Label(SelectionScreenView.Rect("Page", footer, new Vector2(.55f, .15f), new Vector2(.64f, .85f)), "", 16);
        pageLabel.alignment = TextAlignmentOptions.Center;
        selectedIndex = levels.FindIndex(l => LevelProgress.IsLevelUnlocked(l.environmentName, l.levelNumber) && l.levelNumber > LevelProgress.GetHighestCompletedLevel(l.environmentName));
        nextIndex = selectedIndex < 0 ? Mathf.Max(0,levels.Count-1) : selectedIndex;
        if (selectedIndex < 0) selectedIndex = Mathf.Max(0, levels.Count - 1);
        Relayout();
        Select(selectedIndex);
        ScreenFade.In(transform);
    }
    void Update()
    {
        if (view == null) return;
        float width = ScreenTheme.LayoutWidth(view.Root);
        if (Mathf.Abs(width - lastWidth) > 1f) Relayout();
    }
    void Relayout()
    {
        lastWidth = ScreenTheme.LayoutWidth(view.Root);
        columns = SelectionRouteLayout.Columns(lastWidth);
        page = SelectionRouteLayout.PageForIndex(selectedIndex, PageSize);
        BuildRoute();
    }
    void ChangePage(int direction)
    {
        int count = SelectionRouteLayout.PageCount(levels.Count, PageSize);
        page = Mathf.Clamp(page + direction, 0, count - 1);
        selectedIndex = page * PageSize;
        BuildRoute(); Select(selectedIndex);
    }
    void BuildRoute()
    {
        foreach (GameObject go in nodes)
        {
            go.SetActive(false);
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
        nodes.Clear();
        int start = page * PageSize, count = SelectionRouteLayout.VisibleCount(levels.Count, page, PageSize);
        levelButtons.Clear();
        selectionRing=null;
        var positions = new List<Vector2>();
        for (int slot = 0; slot < count; slot++)
        {
            SelectionRouteLayout.Position(slot, columns, out float x, out float y, page * 31 + EnvironmentInfo.DisplayName(GameSession.SelectedEnvironment).Length * 17, count);
            positions.Add(new Vector2(x, y));
        }
        var palette = EnvironmentTheme.PaletteFor(GameSession.SelectedEnvironment);
        for(int layer=0;layer<2;layer++)
        {
            var r = SelectionScreenView.Rect(layer==0 ? "TrailEdge" : "TrailSurface", route, Vector2.zero, Vector2.one);
            var trail = r.gameObject.AddComponent<SelectionTrailGraphic>();
            trail.points=positions;
            trail.width=layer==0 ? 36 : 34;
            trail.color=layer==0 ? PathSurface.Edge(palette.pathColor) : palette.pathColor;
            if(layer==1)
            {
                int completed=LevelProgress.GetHighestCompletedLevel(GameSession.SelectedEnvironment);
                int traversed=0;
                for(int i=0;i<count-1;i++) if(levels[start+i].levelNumber<=completed) traversed++;
                trail.completedSegments=traversed;
                trail.completedColor=UiSkin.Primary;
            }
            trail.texture=PathSurface.Grain;
            trail.raycastTarget=false;
            trail.SetAllDirty();
            nodes.Add(r.gameObject);
        }
        ScatterScenery(positions);
        for (int slot = 0; slot < count; slot++)
        {
            int index = start + slot; LevelConfig level = levels[index];
            var r = SelectionScreenView.Rect("Level_" + level.levelNumber, route, positions[slot], positions[slot]);
            r.sizeDelta = new Vector2(84, 84);
            bool unlocked = LevelProgress.IsLevelUnlocked(level.environmentName, level.levelNumber);
            Color fill = index == selectedIndex ? UiSkin.Primary : UiSkin.Neutral;
            if (!unlocked) fill = UiSkin.PanelRaised;
            fill.a=1;
            Button b = SelectionScreenView.Button(r, level.levelNumber.ToString(), fill, () => Select(index));
            levelButtons[index]=b;
            int earned = Mathf.Clamp(LevelProgress.GetStars(level.environmentName, level.levelNumber), 0, 3);
            if(unlocked)
            {
                var stars = SelectionScreenView.Rect("Stars", r, new Vector2(.5f,-.25f),new Vector2(.5f,-.25f));
                StarSprite.BuildRow(stars,earned,16);
            }
            if (!unlocked)
            {
                var badge = SelectionScreenView.Rect("Lock", r, new Vector2(.38f, -.32f), new Vector2(.62f, -.08f));
                var icon = badge.gameObject.AddComponent<Image>();
                icon.sprite = UiSprites.Lock(); icon.color = SelectionScreenView.Paper; icon.raycastTarget = false;
            }
            nodes.Add(r.gameObject);
        }
        if(currentBase==null)
        {
            currentBase=SelectionScreenView.Rect("CurrentLevelBase",route,Vector2.zero,Vector2.zero);
            currentBase.sizeDelta=new Vector2(88,88);
            basePreview=SelectionIslandPreview.Create(currentBase);
        }
        bool currentVisible=nextIndex>=start && nextIndex<start+count;
        currentBase.gameObject.SetActive(currentVisible);
        if(currentVisible)
        {
            currentBase.anchorMin=currentBase.anchorMax=positions[nextIndex-start];
            currentBase.anchoredPosition=new Vector2(0,66);
            currentBase.SetAsLastSibling();
            basePreview.ShowBase(levels[nextIndex]);
        }
        UpdateSelection();
        int pages = SelectionRouteLayout.PageCount(levels.Count, PageSize);
        previous.gameObject.SetActive(pages > 1);
        next.gameObject.SetActive(pages > 1);
        pageLabel.gameObject.SetActive(pages > 1);
        pageLabel.text = $"{page + 1}/{pages}";
        previous.interactable = page > 0; next.interactable = page < pages - 1;
    }
    void UpdateSelection()
    {
        foreach(var entry in levelButtons)
        {
            var level=levels[entry.Key];
            bool unlocked=LevelProgress.IsLevelUnlocked(level.environmentName,level.levelNumber);
            Color fill=unlocked ? (entry.Key==selectedIndex ? UiSkin.Primary : UiSkin.Neutral) : UiSkin.PanelRaised;
            fill.a=1;
            entry.Value.GetComponent<Image>().color=fill;
            entry.Value.GetComponentInChildren<TMP_Text>().color=unlocked && entry.Key==selectedIndex ? UiSkin.TextDark : UiSkin.TextPrimary;
        }
        if(!levelButtons.TryGetValue(selectedIndex,out var selected)) return;
        if(selectionRing==null)
        {
            var ring=UiSkin.AddBorder((RectTransform)selected.transform,20,3);
            ring.color=SelectionScreenView.Paper; selectionRing=ring.rectTransform;
        }
        selectionRing.SetParent(selected.transform,false);
        selectionRing.anchorMin=Vector2.zero; selectionRing.anchorMax=Vector2.one;
        selectionRing.offsetMin=new Vector2(-5,-5); selectionRing.offsetMax=new Vector2(5,5);
    }
    void ScatterScenery(List<Vector2> positions)
    {
        if(levels.Count==0) return;
        if(sceneryPreview==null)
        {
            var source=SelectionScreenView.Rect("ScenerySource",route,Vector2.zero,Vector2.zero);
            sceneryPreview=SelectionIslandPreview.Create(source);
        }
        sceneryPreview.ShowScenery(levels[0]);
        var sourceImage=sceneryPreview.GetComponent<RawImage>();
        sourceImage.enabled=false;
        Canvas.ForceUpdateCanvases();
        Vector2 size=route.rect.size;
        var rng=new System.Random(page*31+GameSession.SelectedEnvironment.Length*71+columns);
        var placed=new List<Vector2>();
        for(int attempt=0;attempt<120 && placed.Count<6;attempt++)
        {
            Vector2 p=new Vector2(.04f+(float)rng.NextDouble()*.92f,.05f+(float)rng.NextDouble()*.90f);
            bool clear=true;
            foreach(var node in positions) if(Vector2.Scale(p-node,size).magnitude<118) clear=false;
            foreach(var other in placed) if(Vector2.Scale(p-other,size).magnitude<100) clear=false;
            for(int i=1;i<positions.Count;i++)
            {
                Vector2 a=Vector2.Scale(positions[i-1],size), b=Vector2.Scale(positions[i],size), v=Vector2.Scale(p,size);
                Vector2 q=a+Vector2.ClampMagnitude(b-a,Vector2.Distance(a,b)*Mathf.Clamp01(Vector2.Dot(v-a,b-a)/(b-a).sqrMagnitude));
                if(Vector2.Distance(v,q)<60) clear=false;
            }
            if(!clear) continue;
            var icon=SelectionScreenView.Rect("GroundScenery",route,p,p);
            float scale=58+(float)rng.NextDouble()*20;
            icon.sizeDelta=new Vector2(scale,scale);
            var image=icon.gameObject.AddComponent<RawImage>();
            image.texture=sourceImage.texture; image.raycastTarget=false;
            if(rng.Next(2)==0) image.uvRect=new Rect(1,0,-1,1);
            nodes.Add(icon.gameObject); placed.Add(p);
        }
    }
    void Select(int index)
    {
        if (index < 0 || index >= levels.Count)
        {
            selection.text = "No levels yet"; description.text = "More adventures coming soon"; play.interactable = false; return;
        }
        bool changed = selectedIndex != index;
        selectedIndex = index;
        LevelConfig level = levels[index];
        bool unlocked = LevelProgress.IsLevelUnlocked(level.environmentName, level.levelNumber);
        selection.text = $"LEVEL {level.levelNumber}";
        int waves = level.waveConfig != null && level.waveConfig.waves != null ? level.waveConfig.waves.Length : 0;
        description.text = unlocked ? $"{waves} waves  /  Best: {LevelProgress.GetStars(level.environmentName, level.levelNumber)}/3"
          : index > 0 ? $"Complete level {levels[index - 1].levelNumber} to unlock" : "Locked";
        play.interactable = unlocked;
        SelectionScreenView.ButtonText(play, unlocked ? "PLAY LEVEL" : "LOCKED");
        SelectionScreenView.ButtonIcon(play, unlocked ? UiSprites.Play() : UiSprites.Lock());
        if (changed) UpdateSelection();
    }
    void Play()
    {
        if (selectedIndex < 0 || selectedIndex >= levels.Count) return;
        LevelConfig level = levels[selectedIndex];
        if (!LevelProgress.IsLevelUnlocked(level.environmentName, level.levelNumber)) return;
        GameSession.SelectedLevel = level;
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.LevelPicked);
        SceneController.Instance.LoadScene(SceneController.GameScene.MainGame);
        gameObject.SetActive(false);
    }
    void OnHome()
    {
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
        if (EnvironmentsScreen.Instance != null) EnvironmentsScreen.Instance.ReturnToMenu();
        Destroy(gameObject);
    }
    void OnBack()
    {
        if (EnvironmentsScreen.Instance != null) EnvironmentsScreen.Instance.gameObject.SetActive(true);
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
        Destroy(gameObject);
    }
    void OnDestroy() { view?.Dispose(); }
}
