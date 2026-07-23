using UnityEngine;

// Builds a small billboarded health bar above the enemy at runtime,
// so no prefab or scene wiring is required.
public class EnemyHealthBar : MonoBehaviour
{
  private const float BarWidth = 1.6f;
  private const float BarHeight = 0.22f;
  private const float HeightMargin = 0.5f;

  private static Material backgroundMaterial;

  private Transform barRoot;
  private Transform fill;
  private Material fillMaterial;
  private Camera mainCamera;
  private float verticalOffset;

  private void Awake()
  {
    mainCamera = Camera.main;
    CreateBar();
  }

  private void CreateBar()
  {
    // Measure the body before the bar quads add their own renderers
    Renderer bodyRenderer = GetComponentInChildren<MeshRenderer>();
    verticalOffset = (bodyRenderer != null ? bodyRenderer.bounds.size.y : 1f) + HeightMargin;

    barRoot = new GameObject("HealthBar").transform;
    barRoot.SetParent(transform, false);

    Transform background = CreateQuad("Background", barRoot);
    background.localScale = new Vector3(BarWidth, BarHeight, 1f);
    background.GetComponent<MeshRenderer>().sharedMaterial = GetBackgroundMaterial();

    fill = CreateQuad("Fill", barRoot);
    fillMaterial = new Material(GetBackgroundMaterial()) { color = Color.green };
    fill.GetComponent<MeshRenderer>().sharedMaterial = fillMaterial;
    fill.localPosition = new Vector3(0f, 0f, -0.01f);
    fill.localScale = new Vector3(BarWidth, BarHeight * 0.7f, 1f);
  }

  private static Material GetBackgroundMaterial()
  {
    if (backgroundMaterial == null)
    {
      Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
      if (shader == null) shader = Shader.Find("Unlit/Color");
      backgroundMaterial = new Material(shader) { color = new Color(0.1f, 0.1f, 0.1f, 1f) };
    }
    return backgroundMaterial;
  }

  private static Transform CreateQuad(string name, Transform parent)
  {
    GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
    quad.name = name;
    Destroy(quad.GetComponent<Collider>());
    quad.transform.SetParent(parent, false);
    MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    renderer.receiveShadows = false;
    return quad.transform;
  }

  public void SetHealth(float percent)
  {
    if (fill == null) return;

    percent = Mathf.Clamp01(percent);
    fill.localScale = new Vector3(BarWidth * percent, BarHeight * 0.7f, 1f);
    fill.localPosition = new Vector3(-BarWidth * (1f - percent) * 0.5f, 0f, -0.01f);
    fillMaterial.color = Color.Lerp(Color.red, Color.green, percent);

    // Only show the bar once the enemy has taken damage
    barRoot.gameObject.SetActive(percent < 1f);
  }

  private void LateUpdate()
  {
    if (barRoot == null || mainCamera == null) return;

    barRoot.position = transform.position + Vector3.up * verticalOffset;
    barRoot.rotation = Quaternion.LookRotation(barRoot.position - mainCamera.transform.position);
  }

  private void OnDestroy()
  {
    if (fillMaterial != null) Destroy(fillMaterial);
  }
}
