using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class SceneController : MonoBehaviour
{
  public static SceneController Instance { get; private set; }

  [SerializeField] private GameObject loadingScreenPrefab;
  private GameObject activeLoadingScreen;
  private LoadingScreen loadingScreenComponent;

  public enum GameScene
  {
    MainMenu,
    MainGame,
    Settings
  }

  private void Awake()
  {
    if (Instance == null)
    {
      Instance = this;
      DontDestroyOnLoad(gameObject);
    }
    else
    {
      Destroy(gameObject);
    }
  }

  public async void LoadScene(GameScene scene)
  {
    if (activeLoadingScreen == null)
    {
      activeLoadingScreen = Instantiate(loadingScreenPrefab);
      loadingScreenComponent = activeLoadingScreen.GetComponent<LoadingScreen>();
      DontDestroyOnLoad(activeLoadingScreen);
    }

    activeLoadingScreen.SetActive(true);

    var sceneLoad = SceneManager.LoadSceneAsync(scene.ToString());

    while (!sceneLoad.isDone)
    {
      float progress = sceneLoad.progress;
      if (loadingScreenComponent != null)
      {
        loadingScreenComponent.UpdateProgress(progress);
        Debug.Log("Scene loaded " + progress * 100 + " %");
      }

      await Task.Yield();
    }

    if (activeLoadingScreen != null)
    {
      activeLoadingScreen.SetActive(false);
    }
  }

  private void OnDestroy()
  {
    if (activeLoadingScreen != null)
    {
      Destroy(activeLoadingScreen);
    }
  }
}