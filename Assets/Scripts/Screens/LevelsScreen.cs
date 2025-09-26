using UnityEngine;
using UnityEngine.UI;

public class LevelSelectionScreen : MonoBehaviour
{
  [Header("Level Card Setup")]
  [SerializeField] private GameObject levelCardPrefab;
  [SerializeField] private Transform cardsContainer;

  [Header("Level Data")]
  [SerializeField] private int numberOfLevels = 10;
  [SerializeField] private int unlockedLevels = 5;

  [Header("Back Button")]
  [SerializeField] private Button backButton;

  private void Start()
  {
    PopulateLevelCards();

    if (backButton != null)
    {
      backButton.onClick.AddListener(OnBack);
    }
  }

  private void PopulateLevelCards()
  {
    for (int i = 1; i <= numberOfLevels; i++)
    {
      GameObject cardGO = Instantiate(levelCardPrefab, cardsContainer);
      LevelCard card = cardGO.GetComponent<LevelCard>();
      if (card != null)
      {
        bool isLocked = (i > unlockedLevels);
        card.Setup(i, isLocked, OnLevelSelected);
      }
      else
      {
        Debug.LogWarning("LevelCard component not found on the levelCardPrefab.");
      }
    }
  }

  private void OnLevelSelected(int levelNumber)
  {
    Debug.Log("Selected Level: " + levelNumber);
    AudioManager.Instance.PlaySound(AudioManager.SoundType.LevelPicked);
    SceneController.Instance.LoadScene(SceneController.GameScene.MainGame);
    gameObject.SetActive(false);
  }

  private void OnBack()
  {
    Destroy(gameObject);
    if (EnvironmentsScreen.Instance != null)
    {
      AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);
      EnvironmentsScreen.Instance.gameObject.SetActive(true);
    }
  }
}
