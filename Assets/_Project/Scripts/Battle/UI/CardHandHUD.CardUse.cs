using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using TMPro;
using Battle.Card;
using Battle.Deck;
using Battle;

namespace Battle.UI
{
    public partial class CardHandHUD
    {
        // 드래그 위치가 사용 임계선을 넘으면 카드 사용을 시도
        public bool TryUseFromDrag(NewCardView view, PointerEventData ev)
        {
            if (view.Card == null) return false;
            if (TabPanelOpen) return false;
            // 선택/픽커/더미보기 중에는 사용 금지
            if (IsSelectionMode || IsPickerMode || IsViewerMode) return false;
            if (UseCardCallback == null) return false;

            PendingEnemyTarget = null;
            // 지정 카드는 임계선 위(화살표 표시 중) + 적 위에서 놓아야 사용
            if (ShouldUseEnemyTargeting(view.Card))
            {
                if (ev.position.y < useThresholdY) return false; // 손패 쪽으로 되돌리면 취소
                EnemyController aimed = EnemyCombatParty.HitTest(ev.position);
                if (aimed == null) return false;
                PendingEnemyTarget = aimed;
                return UseCardCallback(view.Card);
            }

            PendingEnemyTarget = null;
            if (ev.position.y < useThresholdY) return false;
            return UseCardCallback(view.Card);
        }

        // 지정 화살표를 켜야 하는 카드인지(각성/선택 모드/적 없음은 제외)
        public bool ShouldUseEnemyTargeting(CardInstance card)
        {
            if (!enemyTargetingEnabled || card == null) return false;
            if (IsSelectionMode || IsPickerMode || IsViewerMode) return false;
            if (NewBattleController.Instance != null && NewBattleController.Instance.IsAwakenActive)
                return false;
            if (!CardDatabase.RequiresEnemyTarget(card.Id)) return false;
            return EnemyCombatParty.AliveCount > 0 || ResolveCurrentEnemy() != null;
        }

        // 지정 모드 중 화살표/록온 브래킷을 갱신한다
        public void UpdateEnemyTargeting(NewCardView view, PointerEventData ev)
        {
            if (view == null || ev == null) { HideEnemyTargeting(); return; }
            EnsureTargetArrow();
            if (_targetArrow == null) return;

            Vector2 start = view.GetTopCenterScreenPosition();
            EnemyController enemy = EnemyCombatParty.HitTest(ev.position);
            bool hover = enemy != null;
            Vector2 end = ev.position;
            float radius = 0f;
            if (hover && enemy.TryGetAimScreen(out Vector2 enemyPos, out radius))
                end = enemyPos;

            if (dragLayer != null) dragLayer.SetAsLastSibling();
            _targetArrow.transform.SetAsLastSibling();
            view.transform.SetAsLastSibling();
            _targetArrow.Show(start, end, hover, radius);
        }

        // 지정 화살표를 숨긴다
        public void HideEnemyTargeting()
        {
            if (_targetArrow != null) _targetArrow.Hide();
        }

        EnemyController ResolveCurrentEnemy()
        {
            var nbc = NewBattleController.Instance;
            if (nbc != null && nbc.CurrentEnemy != null && nbc.CurrentEnemy.IsAlive)
                return nbc.CurrentEnemy;
            var found = FindFirstObjectByType<EnemyController>();
            return found != null && found.IsAlive ? found : null;
        }

        bool IsPointerOverCurrentEnemy(Vector2 screenPos)
        {
            var enemy = ResolveCurrentEnemy();
            return enemy != null && enemy.IsAlive && enemy.ContainsAimPoint(screenPos);
        }

        // 클릭(더블클릭)으로 카드 사용을 시도
        public bool TryUseFromClick(NewCardView view)
        {
            if (view == null || view.Card == null) return false;
            if (TabPanelOpen) return false;
            if (IsSelectionMode || IsPickerMode || IsViewerMode) return false;
            if (UseCardCallback == null) return false;
            if (ShouldUseEnemyTargeting(view.Card)) return false;
            PendingEnemyTarget = null;
            return UseCardCallback(view.Card);
        }

        // 진행 중인 카드 사용 연출을 onDisappear 호출 없이 즉시 정리
        public void CancelCardUsePresentations()
        {
            if (_normalPresentCo != null) { StopCoroutine(_normalPresentCo); _normalPresentCo = null; }
            if (_normalPresentView != null) { Destroy(_normalPresentView.gameObject); _normalPresentView = null; }
            if (_interruptablePresentCo != null) { StopCoroutine(_interruptablePresentCo); _interruptablePresentCo = null; }
            if (_interruptablePresentView != null) { Destroy(_interruptablePresentView.gameObject); _interruptablePresentView = null; }
        }

        // 카드 사용 중앙 표시 연출(등장→유지→효과발동→퇴장) 코루틴
        System.Collections.IEnumerator CardUsePresentationRoutine(CardInstance card, System.Action onDisappear, bool interruptable)
        {
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;

            // 손패 풀과 독립된 임시 카드 뷰 생성
            var view = Instantiate(cardPrefab, parent);
            if (interruptable) _interruptablePresentView = view;
            else _normalPresentView = view;
            view.Bind(this, -1);
            var vrect = (RectTransform)view.transform;
            vrect.anchorMin = vrect.anchorMax = new Vector2(0.5f, 0.5f);
            vrect.pivot = new Vector2(0.5f, 0.5f);
            vrect.anchoredPosition = cardUsePresentPos;
            vrect.localRotation = Quaternion.identity;
            view.SetCard(card);
            if (_talismanHand) view.SetTalismanMode(true); // 각성 중 사용한 카드는 부적 그대로 보여 준다
            view.CaptureHome();
            vrect.SetAsLastSibling();

            var cg = view.GetComponent<CanvasGroup>();
            if (cg == null) cg = view.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; // 연출 카드는 클릭 차단

            Vector3 targetScale = vrect.localScale * Mathf.Max(0.01f, cardUsePresentScale);

            // 시작 상태는 작고 투명
            vrect.localScale = targetScale * 0.6f;
            cg.alpha = 0f;

            // 1) 등장 — 목표 크기·불투명으로 확대
            float t = 0f;
            float enter = Mathf.Max(0.0001f, cardUsePresentEnterTime);
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / enter;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                vrect.localScale = Vector3.LerpUnclamped(targetScale * 0.6f, targetScale, e);
                cg.alpha = Mathf.Clamp01(e);
                yield return null;
            }
            vrect.localScale = targetScale;
            cg.alpha = 1f;

            // 2) 유지 대기
            float hold = Mathf.Max(0f, cardUsePresentHold);
            while (hold > 0f) { hold -= Time.unscaledDeltaTime; yield return null; }

            // 3) 사라지는 순간 효과 발동. 효과 안의 예외가 연출을 끊으면 카드 사용 잠금이 안 풀린다.
            try
            {
                onDisappear?.Invoke();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }

            // 4) 퇴장 — 페이드아웃 + 확대
            t = 0f;
            float exit = Mathf.Max(0.0001f, cardUsePresentExitTime);
            Vector3 exitScale = targetScale * 1.12f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / exit;
                float e = Mathf.Clamp01(t);
                vrect.localScale = Vector3.LerpUnclamped(targetScale, exitScale, e);
                cg.alpha = 1f - e;
                yield return null;
            }

            Destroy(view.gameObject);
            if (interruptable)
            {
                _interruptablePresentView = null;
                _interruptablePresentCo = null;
            }
            else
            {
                _normalPresentView = null;
                _normalPresentCo = null;
            }
        }
    }
}
