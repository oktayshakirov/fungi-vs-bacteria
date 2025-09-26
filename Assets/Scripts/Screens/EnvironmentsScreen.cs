using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EnvironmentsScreen : MonoBehaviour
{
  public static EnvironmentsScreen Instance;

  [Header("Environment Card Setup")]
  [SerializeField] private GameObject environmentCardPrefab;
  [SerializeField] private Transform cardsContainer;

  [Header("Next Screen")]
  [SerializeField] private GameObject levelsScreenPrefab;

  [System.Serializable]
  public class EnvironmentData
  {
    public Sprite environmentSprite;
    public string environmentName;
    public bool isLocked;
  }

  [Header("Environments Data")]
  [SerializeField] private List<EnvironmentData> environments = new List<EnvironmentData>();

  private void Awake()
  {
    Instance = this;
  }

  private void Start()
  {
    PopulateEnvironmentCards();
  }

  private void PopulateEnvironmentCards()
  {
    foreach (var envData in environments)
    {
      GameObject cardGO = Instantiate(environmentCardPrefab, cardsContainer);
      EnvironmentCard card = cardGO.GetComponent<EnvironmentCard>();
      if (card != null)
      {
        card.Setup(envData.environmentSprite, envData.environmentName, envData.isLocked);
      }

      Button cardButton = cardGO.GetComponent<Button>();
      if (cardButton != null)
      {
        string envName = envData.environmentName;
        cardButton.onClick.AddListener(() => OnEnvironmentSelected(envName));
        if (envData.isLocked)
        {
          cardButton.interactable = false;
        }
      }
    }
  }

  public void SetLevelSelectionPrefab(GameObject prefab)
  {
    levelsScreenPrefab = prefab;
  }

  private void OnEnvironmentSelected(string environmentName)
  {
    Debug.Log("Selected Environment: " + environmentName);
    AudioManager.Instance.PlaySound(AudioManager.SoundType.EnvironmentPicked);
    if (levelsScreenPrefab != null)
    {
      Instantiate(levelsScreenPrefab, transform.parent);
    }
    gameObject.SetActive(false);
  }
}
