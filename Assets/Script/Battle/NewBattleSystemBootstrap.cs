using System.Collections.Generic;
using UnityEngine;
using Battle.Relic;
using Battle.UI;

namespace Battle
{
    // SampleScene에서 신규 카드 전투 시스템을 항상 활성화하는 부트스트랩
    [DefaultExecutionOrder(-1000)]
    public class NewBattleSystemBootstrap : MonoBehaviour
    {
        [Header("시작 유물 (테스트/밸런스)")]
        [Tooltip("런 시작 시 자동 지급할 유물 에셋(RelicSO). 비우면 지급 안 함.")]
        [SerializeField] private List<RelicSO> startingRelics = new List<RelicSO>(); // 런 시작 시 지급할 유물 목록

        // 레거시 전투 오브젝트 차단 후 신규 컨트롤러/유물/런덱 준비 (실행 순서 -1000)
        void Awake()
        {
            PreDisableLegacyCombatObjects();

            EnsureNewBattleController();
            EnsureRelicSystems();
            RunDeckState.EnsureExists().EnsureSeeded();
        }

        // 런 시작 시 Inspector에 지정된 시작 유물을 지급한다
        public void GrantStartingRelics()
        {
            if (startingRelics == null || startingRelics.Count == 0 || RelicManager.Instance == null)
                return;

            foreach (RelicSO relic in startingRelics)
                RelicManager.Instance.AddRelic(relic);
        }

        // 씬에 남은 레거시 전투 오브젝트(4슬롯/콤보/구 손패)를 비활성화
        void PreDisableLegacyCombatObjects()
        {
            DisableAll<ElementSlotSystem>();
            DisableAll<ComboSystem>();
            DisableAll<ElementSlotHUD>();
            DisableAll<CardSystem>();
        }

        // 지정 타입의 모든 오브젝트를 찾아 비활성화
        static void DisableAll<T>() where T : MonoBehaviour
        {
            var found = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in found)
                if (c != null) c.gameObject.SetActive(false);
        }

        // NewBattleController가 없으면 찾거나 생성해 보장
        void EnsureNewBattleController()
        {
            if (NewBattleController.Instance != null) return;

            var existing = FindFirstObjectByType<NewBattleController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (!existing.gameObject.activeSelf) existing.gameObject.SetActive(true);
                return;
            }

            var go = new GameObject("NewBattleController");
            go.AddComponent<NewBattleController>();
            Debug.Log("[Bootstrap] NewBattleController 자동 생성");
        }

        // 유물 매니저/HUD를 찾거나 생성해 보장
        void EnsureRelicSystems()
        {
            if (RelicManager.Instance == null)
            {
                var existing = FindFirstObjectByType<RelicManager>(FindObjectsInactive.Include);
                if (existing == null)
                    new GameObject("RelicManager").AddComponent<RelicManager>();
            }

            if (RelicHUD.Instance == null && FindFirstObjectByType<RelicHUD>(FindObjectsInactive.Include) == null)
            {
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null)
                {
                    var hudGo = new GameObject("RelicHUD", typeof(RectTransform));
                    hudGo.transform.SetParent(canvas.transform, false);
                    hudGo.AddComponent<RelicHUD>();
                }
            }
        }
    }
}
