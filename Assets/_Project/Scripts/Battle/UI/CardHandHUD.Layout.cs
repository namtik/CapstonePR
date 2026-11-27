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
        // handRoot가 없으면 캔버스 안에 자동 생성
        void EnsureHandRoot()
        {
            if (handRoot != null) return;

            // 캔버스 밖이어도 슬롯이 보이도록 캔버스 안에 생성
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;
            if (canvas == null)
                Debug.LogWarning("[CardHandHUD] 씬에 Canvas가 없어 handRoot가 캔버스 밖에 만들어집니다 — UI가 렌더되지 않을 수 있습니다.");

            var go = new GameObject("HandRoot", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            handRoot = (RectTransform)go.transform;
            handRoot.anchorMin = new Vector2(0.5f, 0f);
            handRoot.anchorMax = new Vector2(0.5f, 0f);
            handRoot.pivot = new Vector2(0.5f, 0f);
            handRoot.anchoredPosition = handAnchoredPos;
            handRoot.sizeDelta = new Vector2(slotSpacing.x * SLOT_COUNT, 400f);
        }

        // dragLayer가 없으면 캔버스 안에 자동 생성
        void EnsureDragLayer()
        {
            if (dragLayer != null) return;

            // 좌표 변환 정확성을 위해 dragLayer는 캔버스 안에 생성
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;
            if (canvas == null)
                Debug.LogWarning("[CardHandHUD] 씬에 Canvas가 없어 dragLayer가 캔버스 밖에 만들어집니다 — 드래그 좌표가 어긋날 수 있습니다.");

            var go = new GameObject("DragLayer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            dragLayer = (RectTransform)go.transform;
            dragLayer.anchorMin = Vector2.zero;
            dragLayer.anchorMax = Vector2.one;
            dragLayer.offsetMin = Vector2.zero;
            dragLayer.offsetMax = Vector2.zero;
            dragLayer.SetAsLastSibling();
        }

        // 레이아웃 설정에 따라 슬롯 앵커를 생성
        void BuildSlotAnchors()
        {
            _slotAnchors.Clear();
            if (fanLayout) BuildFanSlotAnchors();
            else BuildLinearSlotAnchors();
        }

        // 활성 카드 수에 맞춰 슬롯 위치를 재계산(중앙 정렬)
        void PositionSlotAnchorsForCount(int activeCount)
        {
            if (_slotAnchors == null || _slotAnchors.Count == 0) return;
            if (fanLayout) PositionFanForCount(activeCount);
            else PositionLinearForCount(activeCount);
        }

        // 부채꼴 슬롯 위치를 활성 카드 수에 맞춰 재계산
        void PositionFanForCount(int activeCount)
        {
            int total = _slotAnchors.Count;
            // 카드 간격은 기준 카드 수로 유지하고 전체 호만 카드 수에 비례해 축소
            float perStep = fanArcAngle / Mathf.Max(1, LAYOUT_REFERENCE_COUNT - 1);
            float effectiveArc = activeCount > 1 ? perStep * (activeCount - 1) : 0f;
            float startAngle = -effectiveArc * 0.5f;
            float angleStep = activeCount > 1 ? effectiveArc / (activeCount - 1) : 0f;

            for (int i = 0; i < total; i++)
            {
                var rect = _slotAnchors[i];
                if (rect == null) continue;

                if (i < activeCount)
                {
                    float angleDeg = activeCount > 1 ? startAngle + i * angleStep : 0f;
                    float angleRad = angleDeg * Mathf.Deg2Rad;
                    float x = Mathf.Sin(angleRad) * fanRadius;
                    float y = (Mathf.Cos(angleRad) - 1f) * fanRadius - fanVerticalDip;
                    SetSlotBasePos(i, new Vector2(x, y));
                    rect.localRotation = Quaternion.Euler(0f, 0f, -angleDeg);
                }
                else
                {
                    // 비활성 슬롯은 화면 밖으로 보냄
                    SetSlotBasePos(i, new Vector2(0f, -10000f));
                    rect.localRotation = Quaternion.identity;
                }
            }
            ApplyAllSlotSpread();
        }

        // 일렬 슬롯 위치를 활성 카드 수에 맞춰 재계산
        void PositionLinearForCount(int activeCount)
        {
            int total = _slotAnchors.Count;
            float totalWidth = activeCount > 1 ? slotSpacing.x * (activeCount - 1) : 0f;
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < total; i++)
            {
                var rect = _slotAnchors[i];
                if (rect == null) continue;
                if (i < activeCount)
                    SetSlotBasePos(i, new Vector2(startX + i * slotSpacing.x, slotSpacing.y));
                else
                    SetSlotBasePos(i, new Vector2(0f, -10000f));
            }
            ApplyAllSlotSpread();
        }

        // 슬롯 i의 스프레드 미적용 기준 위치를 저장(리스트 자동 확장)
        void SetSlotBasePos(int i, Vector2 pos)
        {
            while (_slotBasePos.Count <= i) _slotBasePos.Add(Vector2.zero);
            _slotBasePos[i] = pos;
        }

        // 손패 카드 hover 시작 — 양옆 카드가 좌우로 벌어질 목표를 설정
        public void ApplyHoverSpread(int hoveredSlot)
        {
            if (!hoverSpreadEnabled) return;
            _hoverSpreadSlot = hoveredSlot;
            EnsureSpreadArrays();
            for (int i = 0; i < _slotSpreadTarget.Length; i++)
            {
                int dist = i - hoveredSlot;
                if (dist == 0) { _slotSpreadTarget[i] = 0f; continue; }
                int absDist = Mathf.Abs(dist);
                // 범위 밖은 밀지 않고, 안쪽은 거리에 반비례해 감소
                float falloff = absDist > hoverSpreadRange
                    ? 0f
                    : 1f - (float)(absDist - 1) / Mathf.Max(1, hoverSpreadRange);
                _slotSpreadTarget[i] = Mathf.Sign(dist) * hoverSpreadDistance * Mathf.Clamp01(falloff);
            }
        }

        // 손패 카드 hover 종료 — 모든 밀림을 0으로 되돌림
        public void ClearHoverSpread()
        {
            _hoverSpreadSlot = -1;
            if (_slotSpreadTarget == null) return;
            for (int i = 0; i < _slotSpreadTarget.Length; i++)
                _slotSpreadTarget[i] = 0f;
        }

        // 스프레드 배열이 슬롯 수에 맞게 준비됐는지 보장
        void EnsureSpreadArrays()
        {
            int n = _slotAnchors.Count;
            if (_slotSpreadCurrent == null || _slotSpreadCurrent.Length != n)
            {
                var oldCur = _slotSpreadCurrent;
                _slotSpreadCurrent = new float[n];
                _slotSpreadTarget = new float[n];
                if (oldCur != null)
                    for (int i = 0; i < n && i < oldCur.Length; i++) _slotSpreadCurrent[i] = oldCur[i];
            }
        }

        // 현재 스프레드 값을 모든 슬롯 위치에 즉시 반영(기준 위치 + x 오프셋)
        void ApplyAllSlotSpread()
        {
            if (!hoverSpreadEnabled) { ApplyRawSlotBase(); return; }
            EnsureSpreadArrays();
            for (int i = 0; i < _slotAnchors.Count; i++)
            {
                var rect = _slotAnchors[i];
                if (rect == null || i >= _slotBasePos.Count) continue;
                float off = _slotSpreadCurrent != null && i < _slotSpreadCurrent.Length ? _slotSpreadCurrent[i] : 0f;
                rect.anchoredPosition = _slotBasePos[i] + new Vector2(off, 0f);
            }
        }

        // 스프레드 비활성 시 기준 위치만 반영
        void ApplyRawSlotBase()
        {
            for (int i = 0; i < _slotAnchors.Count; i++)
            {
                var rect = _slotAnchors[i];
                if (rect == null || i >= _slotBasePos.Count) continue;
                rect.anchoredPosition = _slotBasePos[i];
            }
        }

        // 탭 창이 열려 있으면 손패를 누르거나 드래그하지 못하게 한다.
        void ApplyTabHandBlock()
        {
            if (handRoot == null) return;
            bool open = TabPanelOpen;
            CanvasGroup group = handRoot.GetComponent<CanvasGroup>();
            if (group == null) group = handRoot.gameObject.AddComponent<CanvasGroup>();
            if (group.blocksRaycasts == !open) return;
            group.blocksRaycasts = !open;
            if (!open) return;
            ClearHoverSpread();
            for (int i = 0; i < _cardViews.Count; i++)
            {
                if (_cardViews[i] != null) _cardViews[i].ReleasePointer();
            }
        }

        // 일렬 배치로 슬롯 앵커들을 생성
        void BuildLinearSlotAnchors()
        {
            float totalWidth = slotSpacing.x * (SLOT_COUNT - 1);
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform));
                slotGo.transform.SetParent(handRoot, false);
                var rect = (RectTransform)slotGo.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(startX + i * slotSpacing.x, slotSpacing.y);
                rect.sizeDelta = Vector2.zero;
                _slotAnchors.Add(rect);
            }
        }

        // 호 위에 회전·배치되는 부채꼴 슬롯 앵커들을 생성
        void BuildFanSlotAnchors()
        {
            int n = SLOT_COUNT;
            float startAngle = -fanArcAngle * 0.5f;
            float angleStep = n > 1 ? fanArcAngle / (n - 1) : 0f;

            // 호의 최상단이 (0,0)이 되도록 원 중심을 아래에 둠
            for (int i = 0; i < n; i++)
            {
                float angleDeg = startAngle + i * angleStep;
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // 원 위의 점 좌표 계산
                float x = Mathf.Sin(angleRad) * fanRadius;
                float y = (Mathf.Cos(angleRad) - 1f) * fanRadius - fanVerticalDip;

                var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform));
                slotGo.transform.SetParent(handRoot, false);
                var rect = (RectTransform)slotGo.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(x, y);
                rect.localRotation = Quaternion.Euler(0f, 0f, -angleDeg); // 호 방향 회전
                rect.sizeDelta = Vector2.zero;
                _slotAnchors.Add(rect);
            }
        }
    }
}
