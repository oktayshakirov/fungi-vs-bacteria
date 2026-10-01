using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class EnvironmentsScreen : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static EnvironmentsScreen Instance;
    // Retained so existing prefab GUIDs, serialized fields and editor tools stay valid.
    [SerializeField] private GameObject environmentCardPrefab;
    [SerializeField] private Transform cardsContainer;
    [SerializeField] private GameObject levelsScreenPrefab;
    [System.Serializable]
    public class EnvironmentData
    {
        public Sprite environmentSprite;
        public string environmentName;
    }
    [SerializeField] private List<EnvironmentData> environments = new List<EnvironmentData>();
    GameObject returnTarget;
    SelectionScreenView view;
    SelectionIslandPreview island;
    TMP_Text nameLabel, details, requirement, position;
    Button explore, previous, next;
    int selected;
    bool started;
    Coroutine transition;
    CanvasGroup fade;

    void Awake() { Instance = this; }
    public void SetReturnTarget(GameObject menu) { returnTarget = menu; }
    public void SetLevelSelectionPrefab(GameObject prefab) { levelsScreenPrefab = prefab; }
    void Start()
    {
        if (started) return;
        started = true;
        view = new SelectionScreenView(transform, "EXPLORE THE ENVIRONMENTS", OnBack);
        fade=view.Root.gameObject.AddComponent<CanvasGroup>();
        island = SelectionScreenView.Island(view.Root, new Vector2(.015f, .16f), new Vector2(.64f, .88f));

        island.transform.parent.gameObject.AddComponent<SelectionIslandFloat>();
        nameLabel = SelectionScreenView.Label(SelectionScreenView.Rect("Biome", view.Root, new Vector2(.65f, .53f), new Vector2(.96f, .70f)), "", 44, true);
        details = SelectionScreenView.Label(SelectionScreenView.Rect("Completion", view.Root, new Vector2(.65f, .44f), new Vector2(.96f, .51f)), "", 22);
        requirement = SelectionScreenView.Label(SelectionScreenView.Rect("Unlock", view.Root, new Vector2(.65f, .30f), new Vector2(.96f, .43f)), "", 19);
        explore = SelectionScreenView.Button(SelectionScreenView.Rect("Explore", view.Root, new Vector2(.65f, .18f), new Vector2(.96f, .28f)), "EXPLORE", UiSkin.Primary, OpenSelected);
        previous = SelectionScreenView.Button(SelectionScreenView.Rect("Previous", view.Root, new Vector2(.27f, .035f), new Vector2(.34f, .12f)), "", UiSkin.Neutral, () => Browse(-1));
        next = SelectionScreenView.Button(SelectionScreenView.Rect("Next", view.Root, new Vector2(.66f, .035f), new Vector2(.73f, .12f)), "", UiSkin.Neutral, () => Browse(1));
        Arrow(previous, false); Arrow(next, true);
        position = SelectionScreenView.Label(SelectionScreenView.Rect("Position", view.Root, new Vector2(.36f, .035f), new Vector2(.64f, .12f)), "", 20);
        position.alignment = TextAlignmentOptions.Center;
        for (int i = 0; i < environments.Count; i++) if (IsUnlocked(i)) selected = i;
        Refresh();
        ScreenFade.In(transform);
    }
    void OnEnable() { if (started) Refresh(); }
    bool IsUnlocked(int index)
    {
        if (index < 0 || index >= environments.Count) return false;
        if (LevelRepository.GetLevelsForEnvironment(environments[index].environmentName).Count == 0) return false;
        string prior = index > 0 ? environments[index - 1].environmentName : null;
        int count = prior != null ? LevelRepository.GetLevelsForEnvironment(prior).Count : 0;
        return LevelProgress.IsEnvironmentUnlocked(prior, count);
    }
    void Browse(int step)
    {
        int destination=Mathf.Clamp(selected+step,0,Mathf.Max(0,environments.Count-1));
        if(destination==selected || transition!=null) return;
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
        if(!Application.isPlaying) { selected=destination; Refresh(); return; }
        transition=StartCoroutine(SwitchEnvironment(destination));
    }
    IEnumerator SwitchEnvironment(int destination)
    {
        fade.interactable=false;
        for(float t=0;t<.12f;t+=Time.unscaledDeltaTime)
        { fade.alpha=1-.65f*t/.12f; yield return null; }
        selected=destination; Refresh();
        for(float t=0;t<.20f;t+=Time.unscaledDeltaTime)
        { fade.alpha=.35f+.65f*Mathf.SmoothStep(0,1,t/.20f); yield return null; }
        fade.alpha=1; fade.interactable=true; transition=null;
    }
    void OnDisable()
    {
        if(transition!=null) StopCoroutine(transition);
        transition=null;
        if(fade!=null) { fade.alpha=1; fade.interactable=true; }
    }
    void Refresh()
    {
        if (environments.Count == 0)
        {
            nameLabel.text = "No biomes yet"; explore.interactable = previous.interactable = next.interactable = false; return;
        }
        string env = environments[selected].environmentName;
        var levels = LevelRepository.GetLevelsForEnvironment(env);
        int done = 0, stars = 0;
        foreach (var level in levels)
        {
            if (level.levelNumber <= LevelProgress.GetHighestCompletedLevel(env)) done++;
            stars += Mathf.Clamp(LevelProgress.GetStars(env, level.levelNumber), 0, 3);
        }
        view.Theme(env);
        view.Progress.text = $"{stars} / {levels.Count * 3}";
        nameLabel.text = EnvironmentInfo.DisplayName(env).ToUpperInvariant();
        details.text = $"{done} / {levels.Count} levels cleared";
        bool unlocked = IsUnlocked(selected);
        requirement.text = levels.Count == 0 ? "Levels coming soon" : unlocked
          ? "Follow the trail. Protect your fungi." : $"Complete {EnvironmentInfo.DisplayName(environments[selected - 1].environmentName)} to unlock.";
        SelectionScreenView.ButtonText(explore, unlocked ? "EXPLORE" : "LOCKED");
        UiSkin.StyleButton(explore, unlocked ? UiSkin.Primary : UiSkin.Neutral);
        SelectionScreenView.ButtonIcon(explore, unlocked ? UiSprites.Play() : UiSprites.Lock());
        explore.interactable = levels.Count > 0;
        previous.interactable = selected > 0;
        next.interactable = selected < environments.Count - 1;
        position.text = $"{selected + 1:00}  /  {environments.Count:00}";
        // The first actual level defines the biome's preview, never a fabricated map.
        island.Show(levels.Count > 0 ? levels[0] : null);
    }
    void OpenSelected()
    {
        if (!IsUnlocked(selected))
        {
            AudioManager.Instance?.PlayLocked(); UiShake.Nudge((RectTransform)explore.transform); return;
        }
        if (levelsScreenPrefab == null) { Debug.LogError("Levels screen prefab is not assigned."); return; }
        GameSession.SelectedEnvironment = environments[selected].environmentName;
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.EnvironmentPicked);
        Instantiate(levelsScreenPrefab, transform.parent);
        gameObject.SetActive(false);
    }
    public void ReturnToMenu()
    {
        if (returnTarget != null) returnTarget.SetActive(true);
        Destroy(gameObject);
    }
    void OnBack() { AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick); ReturnToMenu(); }
    void OnDestroy() { view?.Dispose(); if (Instance == this) Instance = null; }
    public static void Arrow(Button button, bool right)
    {
        SelectionScreenView.ButtonIcon(button, UiSprites.Chevron(), right ? 90 : -90, true);
    }
    public void OnBeginDrag(PointerEventData data) { }
    public void OnDrag(PointerEventData data) { }
    public void OnEndDrag(PointerEventData data)
    {
        Vector2 delta = data.position - data.pressPosition;
        if(Mathf.Abs(delta.x) < Screen.width * .06f || Mathf.Abs(delta.x) < Mathf.Abs(delta.y)) return;
        data.eligibleForClick = false;
        Browse(delta.x < 0 ? 1 : -1);
    }
}
