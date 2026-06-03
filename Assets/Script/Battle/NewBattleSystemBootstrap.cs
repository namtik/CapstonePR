using System.Collections.Generic;
using UnityEngine;
using Battle.Relic;
using Battle.UI;

namespace Battle
{
    /// <summary>
    /// SampleScene(본 게임)에서 신규 카드 전투 시스템을 "항상" 활성화하는 부트스트랩.
    ///
    /// Awake에서 NewBattleController를 먼저 생성해 Roundmanager의
    /// IsNewBattleSystemActive()(= Instance != null) 가드가 라운드 시작 전에 true가 되도록 보장한다.
    /// 동시에 레거시(ElementSlotSystem/ComboSystem 등)를 비활성화하고 유물/HUD/런덱을 준비한다.
    ///
    /// 전투 시작(StartBattle)/종료(EndBattle)는 Roundmanager가 노드 진입/종료 시 호출한다(여기선 안 함).
    /// BattleTestController의 격리 셋업과 동일 패턴이되, 단일 전투가 아니라 런 전체를 전제로 한다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class NewBattleSystemBootstrap : MonoBehaviour
    {
        [Header("시작 유물 (테스트/밸런스)")]
        [Tooltip("런 시작 시 자동 지급할 유물. 이무기의 여의주=AwakenGaugeRecoverPerCombo, 비급서=ComboBonusSecondsBoost")]
        [SerializeField] private List<RelicEffectType> startingRelics = new List<RelicEffectType>();

        void Awake()
        {
            // 다른 Awake 실행 전에 레거시 전투 오브젝트를 차단 (실행 순서 -1000)
            PreDisableLegacyCombatObjects();

            EnsureNewBattleController();
            EnsureRelicSystems();
            RunDeckState.EnsureExists().EnsureSeeded();
        }

        /// <summary>씬에 남아있는 레거시 전투 오브젝트(4슬롯/콤보/구 손패)를 비활성화.</summary>
        void PreDisableLegacyCombatObjects()
        {
            DisableAll<ElementSlotSystem>();
            DisableAll<ComboSystem>();
            DisableAll<ElementSlotHUD>();
            DisableAll<CardSystem>();
        }

        static void DisableAll<T>() where T : MonoBehaviour
        {
            var found = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in found)
                if (c != null) c.gameObject.SetActive(false);
        }

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
            go.AddComponent<NewBattleController>(); // Awake에서 Instance 설정
            Debug.Log("[Bootstrap] NewBattleController 자동 생성");
        }

        void EnsureRelicSystems()
        {
            // RelicManager
            if (RelicManager.Instance == null)
            {
                var existing = FindFirstObjectByType<RelicManager>(FindObjectsInactive.Include);
                if (existing == null)
                    new GameObject("RelicManager").AddComponent<RelicManager>(); // Awake에서 Instance 설정
            }

            // 시작 유물 지급
            if (startingRelics != null && RelicManager.Instance != null)
            {
                foreach (var fx in startingRelics)
                    RelicManager.Instance.GiveRelicByEffect(fx);
            }

            // RelicHUD (좌상단 아이콘) — Canvas 하위에 자동 생성
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
