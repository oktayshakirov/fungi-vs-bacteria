using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Short-lived world-space number (damage taken, gold earned) that rises and
// fades. Built entirely at runtime, billboarded to the camera. No prefab needed.
public class FloatingText : MonoBehaviour
{
  private const float Lifetime = 0.75f;
  private const float RiseSpeed = 2.2f;

  private TextMeshPro label;
  private Camera cam;
  private float age, duration;
  private Transform follow;
  private Enemy followEnemy;
  private uint followSpawn;
  private Vector3 followOffset;
  private Color startColor;
  public const int MinorLimit=24, TotalLimit=36;
  private static int activeCount, minorCount;
  private bool counted, critical;
  public static int ActiveCount=>activeCount;
  public uint Revision { get; private set; }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void ResetBudget() { activeCount=minorCount=0;pool.Clear(); }

  // Reused rather than allocated per hit. Creating a GameObject and a
  // TextMeshPro for every damage number was the main source of the frame dips
  // once a wave got large.
  private static readonly Stack<FloatingText> pool = new Stack<FloatingText>();

  public static FloatingText Spawn(Vector3 worldPosition, string text, Color color, float fontSize = 5f, bool critical = false, float lifetime = Lifetime, Transform follow = null)
  {
    if(activeCount>=TotalLimit || (!critical && minorCount>=MinorLimit)) return null;
    FloatingText floating = null;
    while (pool.Count > 0 && floating == null)
    {
      floating = pool.Pop();   // entries go null across a scene load
    }

    if (floating == null)
    {
      var go = new GameObject("FloatingText");
      floating = go.AddComponent<FloatingText>();
      floating.Build();
    }

    floating.transform.position = worldPosition;
    floating.gameObject.SetActive(true);
    floating.Setup(text, color, fontSize);
    floating.label.rectTransform.sizeDelta=new Vector2(critical?18:6,3);
    floating.duration=Mathf.Max(.05f,lifetime);floating.follow=follow;
    floating.followEnemy=follow!=null?follow.GetComponent<Enemy>():null;
    floating.followSpawn=floating.followEnemy!=null?floating.followEnemy.SpawnVersion:0;
    floating.followOffset=floating.transform.position-(follow!=null?follow.position:Vector3.zero);
    unchecked { floating.Revision++; }
    floating.critical=critical;floating.counted=true;
    activeCount++;if(!critical) minorCount++;
    return floating;
  }

  // One-time construction; only the parts that vary are set in Setup.
  private void Build()
  {
    label = gameObject.AddComponent<TextMeshPro>();
    UiFont.Apply(label);
    label.alignment = TextAlignmentOptions.Center;
    label.fontStyle = FontStyles.Bold;
    label.outlineWidth = 0.25f;
    label.outlineColor = new Color(0f, 0f, 0f, 0.9f);
    label.rectTransform.sizeDelta = new Vector2(6f, 2f);
  }

  private void Setup(string text, Color color, float fontSize)
  {
    cam = Camera.main;
    if (label == null) Build();

    label.text = text;
    label.color = color;
    label.fontSize = fontSize;

    startColor = color;
    age = 0f;

    // A little horizontal scatter so stacked hits don't overlap exactly
    transform.position += new Vector3(Random.Range(-0.4f, 0.4f), 0f, 0f);
  }

  private void Update()
  {
    if(followEnemy!=null && (!followEnemy.IsTargetable || followEnemy.SpawnVersion!=followSpawn)) { Cancel(Revision);return; }
    age += Time.deltaTime;
    if (age >= duration)
    {
      gameObject.SetActive(false);
      pool.Push(this);
      return;
    }

    if(follow!=null) transform.position=follow.position+followOffset;
    else transform.position += Vector3.up * RiseSpeed * Time.deltaTime;

    float t = age / duration;
    Color c = startColor;
    c.a = 1f - t * t; // fade out, slow at first
    if (label != null) label.color = c;
  }

  public void Cancel(uint revision)
  {
    if(Revision!=revision || !counted || !gameObject.activeSelf) return;
    gameObject.SetActive(false);pool.Push(this);
  }
  private void OnDisable()
  {
    if(!counted) return;
    activeCount=Mathf.Max(0,activeCount-1);
    if(!critical) minorCount=Mathf.Max(0,minorCount-1);
    counted=false;
  }

  private void LateUpdate()
  {
    if (cam == null) cam = Camera.main;
    if (cam != null)
    {
      transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
  }
}
