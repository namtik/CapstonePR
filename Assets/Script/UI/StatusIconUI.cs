using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatusIconUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;   // 상태이상 아이콘 이미지
    [SerializeField] private TextMeshProUGUI countText;   // 스택 수 텍스트

    // 현재 표시 중인 상태이상 키
    public string CurrentKey { get; private set; }

    // 아이콘과 스택 수, 키를 설정하고 표시한다
    public void SetStatus(Sprite icon, int count, string key)
    {
        CurrentKey = key;
        iconImage.sprite = icon;
        UpdateCount(count);
        gameObject.SetActive(true);
    }

    // 스택 숫자 스타일을 적용한다. 아웃라인 색은 재질에 다시 써야 반영된다.
    public void ApplyCountStyle(TMP_FontAsset font, float size, Color color, FontStyles style, Vector2 offset, float outlineWidth, Color outlineColor)
    {
        if (countText == null) return;

        if (font != null) countText.font = font;
        countText.fontSize = size;
        countText.color = color;
        countText.fontStyle = style;
        countText.alignment = TextAlignmentOptions.Center;
        countText.enableWordWrapping = false;
        countText.overflowMode = TextOverflowModes.Overflow;
        countText.outlineWidth = Mathf.Clamp01(outlineWidth);
        _ = countText.outlineColor;
        countText.outlineColor = outlineColor;

        RectTransform rt = countText.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(80f, 40f);
    }

    // 스택 수를 갱신하고 0 이하이면 아이콘을 숨긴다
    public void UpdateCount(int count)
    {
        countText.text = count.ToString();

        if (count <= 0)
        {
            gameObject.SetActive(false);
        }
    }
}