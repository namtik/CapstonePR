using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using Battle.Card;
using Battle.Deck;
using Battle.Relic;
using Battle.UI;
using Sashindo.VFX;

namespace Battle
{
    public partial class NewBattleController
    {
        // 손패 HUD를 찾거나 전투 캔버스 하위에 생성
        CardHandHUD ResolveOrCreateHandHud()
        {
            var existing = FindFirstObjectByType<CardHandHUD>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (!existing.gameObject.activeSelf) existing.gameObject.SetActive(true);
                return existing;
            }

            Canvas combatCanvas = ResolveCombatCanvas();
            var hudGo = new GameObject("CardHandHUD", typeof(RectTransform));
            if (combatCanvas != null)
            {
                hudGo.transform.SetParent(combatCanvas.transform, false);
                var rect = (RectTransform)hudGo.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            return hudGo.AddComponent<CardHandHUD>();
        }

        // 전투 캔버스 탐색(CombatStage 하위 우선, 없으면 첫 캔버스)
        Canvas ResolveCombatCanvas()
        {
            GameObject combatStage = GameObject.Find("CombatStage");
            if (combatStage != null)
            {
                var c = combatStage.GetComponentInChildren<Canvas>(true);
                if (c != null) return c;
            }
            return FindFirstObjectByType<Canvas>();
        }

        // 활성 전투 HP바(PlayerHpBar)의 RectTransform 탐색(맵 HP바 오인식 방지)
        RectTransform ResolveActiveCombatHpBarRect()
        {
            GameObject combatStage = GameObject.Find("CombatStage");
            if (combatStage != null)
            {
                var stageSliders = combatStage.GetComponentsInChildren<Slider>(true);
                foreach (var slider in stageSliders)
                {
                    if (slider == null) continue;
                    if (slider.gameObject.name != "PlayerHpBar") continue;
                    if (!slider.gameObject.activeInHierarchy) continue;

                    var rect = slider.GetComponent<RectTransform>();
                    if (rect != null) return rect;
                }
            }

            Canvas combatCanvas = ResolveCombatCanvas();
            if (combatCanvas != null)
            {
                var canvasSliders = combatCanvas.GetComponentsInChildren<Slider>(true);
                foreach (var slider in canvasSliders)
                {
                    if (slider == null) continue;
                    if (slider.gameObject.name != "PlayerHpBar") continue;
                    if (!slider.gameObject.activeInHierarchy) continue;

                    var rect = slider.GetComponent<RectTransform>();
                    if (rect != null) return rect;
                }
            }

            if (_player != null && _player.hpBar != null && _player.hpBar.gameObject.activeInHierarchy)
                return _player.hpBar.GetComponent<RectTransform>();

            return null;
        }

        // EventSystem이 없으면 생성해 보장
        void EnsureEventSystem()
        {
            var es = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
            if (es != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // 카드 효과 오버레이를 찾거나 전투 캔버스 하위에 생성
        CardEffectOverlay ResolveOrCreateEffectOverlay()
        {
            var existing = FindFirstObjectByType<CardEffectOverlay>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            Canvas combatCanvas = ResolveCombatCanvas();
            var go = new GameObject("CardEffectOverlay", typeof(RectTransform));
            if (combatCanvas != null)
            {
                go.transform.SetParent(combatCanvas.transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.SetAsLastSibling();
            }
            return go.AddComponent<CardEffectOverlay>();
        }

        // 플레이어 피격 오버레이를 찾거나 전투 캔버스 하위에 생성
        PlayerDamageOverlay ResolveOrCreateDamageOverlay()
        {
            var existing = FindFirstObjectByType<PlayerDamageOverlay>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            Canvas combatCanvas = ResolveCombatCanvas();
            var go = new GameObject("PlayerDamageOverlay", typeof(RectTransform));
            if (combatCanvas != null)
            {
                go.transform.SetParent(combatCanvas.transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.SetAsLastSibling();
            }
            return go.AddComponent<PlayerDamageOverlay>();
        }

        // 행동 순서 바를 찾거나 전투 캔버스 하위에 생성
        TurnOrderTimeline ResolveOrCreateTurnOrderTimeline()
        {
            var existing = FindFirstObjectByType<TurnOrderTimeline>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            Canvas combatCanvas = ResolveCombatCanvas();
            if (combatCanvas == null) return null;

            // 프리팹(Resources/UI/TurnOrder/TurnOrderTimeline)이 있으면 그 설정·행 프리팹으로 생성
            var prefab = Resources.Load<TurnOrderTimeline>("UI/TurnOrder/TurnOrderTimeline");
            if (prefab != null)
            {
                var inst = Instantiate(prefab, combatCanvas.transform, false);
                inst.name = "TurnOrderTimeline";
                var irt = (RectTransform)inst.transform;
                irt.anchorMin = Vector2.zero;
                irt.anchorMax = Vector2.one;
                irt.offsetMin = Vector2.zero;
                irt.offsetMax = Vector2.zero;
                return inst;
            }

            var go = new GameObject("TurnOrderTimeline", typeof(RectTransform));
            go.transform.SetParent(combatCanvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go.AddComponent<TurnOrderTimeline>();
        }

        // CombatStage에 배치된 요괴 정보 탭 패널을 찾는다. 없으면 생성하지 않는다.
        BattleTabPanel ResolveTabPanel()
        {
            if (tabPanel != null) return tabPanel;

            var existing = FindFirstObjectByType<BattleTabPanel>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            Debug.LogWarning("[NewBattle] BattleTabPanel이 CombatStage에 없습니다. 캔버스에 배치하세요.");
            return null;
        }
    }
}
