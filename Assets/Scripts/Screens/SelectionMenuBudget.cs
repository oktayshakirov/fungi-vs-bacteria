using UnityEngine;

// A scoped menu cap: restore the previous settings before returning to gameplay.
public class SelectionMenuBudget : MonoBehaviour
{
    static int users, previousRate, previousSync;
    bool acquired;
    void OnEnable()
    {
        if(!Application.isPlaying) return;
        if(users++==0)
        {
            previousRate=Application.targetFrameRate; previousSync=QualitySettings.vSyncCount;
            QualitySettings.vSyncCount=0; Application.targetFrameRate=30;
        }
        acquired=true;
    }
    void OnDisable()
    {
        if(!acquired) return;
        acquired=false;
        if(--users==0) { Application.targetFrameRate=previousRate; QualitySettings.vSyncCount=previousSync; }
    }
}
