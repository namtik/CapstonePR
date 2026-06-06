using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 유물 보상 이미지의 포인터 이벤트를 릴레이한다.
/// </summary>
public class RelicRewardInteractable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Action onPointerEnter;
    public Action onPointerExit;
    public Action onPointerClick;

    public void OnPointerEnter(PointerEventData eventData)
    {
        onPointerEnter?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onPointerExit?.Invoke();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        onPointerClick?.Invoke();
    }
}
