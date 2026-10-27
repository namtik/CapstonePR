using UnityEngine;

/// <summary>Limits this boss stage to its selected lighting presets.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(StageLightingManager))]
[DefaultExecutionOrder(11000)]
public sealed class BossStageTimePresets : MonoBehaviour, IStageTimePresetFilter
{
    [Header("허용할 시간대")]
    [SerializeField, InspectorName("새벽 · Dawn")] private bool allowDawn = true;
    [SerializeField, InspectorName("아침 · Morning")] private bool allowMorning = true;
    [SerializeField, InspectorName("오후 1~2시 · Afternoon")] private bool allowAfternoon = true;
    [SerializeField, InspectorName("노을 · Sunset")] private bool allowSunset = true;
    [SerializeField, InspectorName("밤 · Evening")] private bool allowEvening = true;
    [SerializeField, InspectorName("흐린 날 · Overcast")] private bool allowOvercast = true;
    [SerializeField, InspectorName("늦은 오후 · LateAfternoon")] private bool allowLateAfternoon = true;
    [SerializeField, InspectorName("푸른 저녁 · BlueHour")] private bool allowBlueHour = true;

    [Header("선택이 해제됐을 때")]
    [Tooltip("현재 시간대가 허용되지 않으면 마지막으로 허용된 시간을 유지합니다. 그것도 해제됐으면 이 시간대, 그다음 첫 허용 시간대를 사용합니다. 모두 해제하면 이 시간대 하나를 자동 허용합니다.")]
    [SerializeField, InspectorName("대체 시간대")]
    private StageLightingManager.StageTime fallbackTime = StageLightingManager.StageTime.Overcast;

    private StageLightingManager lightingManager;
    private StageTimeOfDayEffects effects;
    private StageLightingManager.StageTime lastAllowedTime;
    private bool hasLastAllowedTime;
    private bool refreshRequested = true;

    public int AllowedPresetCount =>
        (allowDawn ? 1 : 0) + (allowMorning ? 1 : 0) + (allowAfternoon ? 1 : 0) +
        (allowSunset ? 1 : 0) + (allowEvening ? 1 : 0) + (allowOvercast ? 1 : 0) +
        (allowLateAfternoon ? 1 : 0) + (allowBlueHour ? 1 : 0);

    public StageLightingManager.StageTime ResolveTime(StageLightingManager.StageTime requested)
    {
        EnsureAllowedPreset();
        var resolved = IsAllowed(requested) ? requested :
            hasLastAllowedTime && IsAllowed(lastAllowedTime) ? lastAllowedTime : FirstAllowedFallback();
        lastAllowedTime = resolved;
        hasLastAllowedTime = true;
        return resolved;
    }

    public bool IsAllowed(StageLightingManager.StageTime time)
    {
        switch (time)
        {
            case StageLightingManager.StageTime.Dawn: return allowDawn;
            case StageLightingManager.StageTime.Morning: return allowMorning;
            case StageLightingManager.StageTime.Afternoon: return allowAfternoon;
            case StageLightingManager.StageTime.Sunset: return allowSunset;
            case StageLightingManager.StageTime.Evening: return allowEvening;
            case StageLightingManager.StageTime.Overcast: return allowOvercast;
            case StageLightingManager.StageTime.LateAfternoon: return allowLateAfternoon;
            case StageLightingManager.StageTime.BlueHour: return allowBlueHour;
            default: return false;
        }
    }

    /// <summary>Use this API when game code chooses the boss stage's time.</summary>
    public bool TrySetTime(StageLightingManager.StageTime time)
    {
        if (!ResolveReferences()) return false;
        EnsureAllowedPreset();
        if (!IsAllowed(time)) return false;
        ApplyTime(time);
        return true;
    }

    /// <summary>UnityEvent-compatible index selection; disabled presets are ignored.</summary>
    public void SetTimeByIndex(int index)
    {
        if (index >= 0 && index <= (int)StageLightingManager.StageTime.BlueHour)
            TrySetTime((StageLightingManager.StageTime)index);
    }

    [ContextMenu("Preview / Random Allowed Time")]
    public void SelectRandomAllowed()
    {
        if (!ResolveReferences()) return;
        EnsureAllowedPreset();
        int choice = Random.Range(0, AllowedPresetCount);
        for (int i = 0; i <= (int)StageLightingManager.StageTime.BlueHour; i++)
        {
            var time = (StageLightingManager.StageTime)i;
            if (IsAllowed(time) && choice-- == 0)
            {
                ApplyTime(time);
                return;
            }
        }
    }

    [ContextMenu("Apply Allowed Presets")]
    public void RefreshSelection()
    {
        if (!ResolveReferences()) return;
        EnsureAllowedPreset();
        if (!IsAllowed(lightingManager.CurrentTime))
        {
            var next = hasLastAllowedTime && IsAllowed(lastAllowedTime)
                ? lastAllowedTime : FirstAllowedFallback();
            ApplyTime(next);
        }
        lastAllowedTime = lightingManager.CurrentTime;
        hasLastAllowedTime = true;
        refreshRequested = false;
    }

    private void OnEnable()
    {
        if (!ResolveReferences()) return;
        RefreshSelection();
    }

    private void OnDisable()
    {
        hasLastAllowedTime = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= RefreshEditorSelection;
#endif
    }

    private void OnValidate()
    {
        EnsureAllowedPreset();
        refreshRequested = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= RefreshEditorSelection;
        UnityEditor.EditorApplication.delayCall += RefreshEditorSelection;
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

#if UNITY_EDITOR
    private void RefreshEditorSelection()
    {
        if (this && isActiveAndEnabled) RefreshSelection();
    }
#endif

    // Checks changed Inspector flags without allocating or searching the scene each frame.
    private void LateUpdate()
    {
        if (!ResolveReferences()) return;
        if (refreshRequested || !IsAllowed(lightingManager.CurrentTime))
            RefreshSelection();
        else
        {
            lastAllowedTime = lightingManager.CurrentTime;
            hasLastAllowedTime = true;
        }
    }

    private bool ResolveReferences()
    {
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return false;
        if (!lightingManager) lightingManager = GetComponent<StageLightingManager>();
        if (!effects) effects = GetComponent<StageTimeOfDayEffects>();
        return lightingManager != null;
    }

    private void ApplyTime(StageLightingManager.StageTime time)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RecordObject(lightingManager, "Change Boss Lighting Preset");
#endif
        lightingManager.SetTime(time);
        if (effects && effects.isActiveAndEnabled) effects.RefreshNow();
        lastAllowedTime = time;
        hasLastAllowedTime = true;
    }

    private StageLightingManager.StageTime FirstAllowedFallback()
    {
        if (IsAllowed(fallbackTime)) return fallbackTime;
        for (int i = 0; i <= (int)StageLightingManager.StageTime.BlueHour; i++)
        {
            var time = (StageLightingManager.StageTime)i;
            if (IsAllowed(time)) return time;
        }
        return fallbackTime;
    }

    private void EnsureAllowedPreset()
    {
        if ((int)fallbackTime < 0 || (int)fallbackTime > (int)StageLightingManager.StageTime.BlueHour)
            fallbackTime = StageLightingManager.StageTime.Overcast;
        if (AllowedPresetCount > 0) return;
        switch (fallbackTime)
        {
            case StageLightingManager.StageTime.Dawn: allowDawn = true; break;
            case StageLightingManager.StageTime.Morning: allowMorning = true; break;
            case StageLightingManager.StageTime.Afternoon: allowAfternoon = true; break;
            case StageLightingManager.StageTime.Sunset: allowSunset = true; break;
            case StageLightingManager.StageTime.Evening: allowEvening = true; break;
            case StageLightingManager.StageTime.Overcast: allowOvercast = true; break;
            case StageLightingManager.StageTime.LateAfternoon: allowLateAfternoon = true; break;
            case StageLightingManager.StageTime.BlueHour: allowBlueHour = true; break;
        }
    }
}
