using UnityEngine;

// Animate the cached image on its own canvas, never re-render the 3D island.
public class SelectionIslandFloat : MonoBehaviour
{
    RectTransform rect;
    Vector2 rest;
    void Awake()
    {
        rect=(RectTransform)transform; rest=rect.anchoredPosition;
        gameObject.AddComponent<Canvas>();
    }
    void Update()
    {
        if(Application.isPlaying) rect.anchoredPosition=rest+Vector2.up*(Mathf.Sin(Time.unscaledTime*.85f)*4f);
    }
    void OnDisable() { if(rect!=null) rect.anchoredPosition=rest; }
}
