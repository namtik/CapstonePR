using System;
using UnityEngine;
using UnityEngine.EventSystems;

// 유물 보상 이미지의 포인터 이벤트를 외부 콜백으로 중계
public class RelicRewardInteractable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Action onPointerEnter; // 포인터 진입 콜백
    public Action onPointerExit; // 포인터 이탈 콜백
    public Action onPointerClick; // 클릭 콜백

    // 포인터 진입 시 콜백 호출
    public void OnPointerEnter(PointerEventData eventData)
    {
        onPointerEnter?.Invoke();
    }

    // 포인터 이탈 시 콜백 호출
    public void OnPointerExit(PointerEventData eventData)
    {
        onPointerExit?.Invoke();
    }

    // 좌클릭일 때만 클릭 콜백 호출
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        onPointerClick?.Invoke();
    }
}
