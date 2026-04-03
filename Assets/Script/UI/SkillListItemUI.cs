using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using static SkillDataParser;

public class SkillListItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI 연결")]
    public TextMeshProUGUI nameText;
    public Transform commandIconContainer; // 원소 아이콘들이 들어갈 부모 객체

    [Header("커맨드 전용 아이콘 설정")]
    public Sprite iconQ; // Q
    public Sprite iconW; // W
    public Sprite iconE; // E
    public Sprite iconR; // R 

    private GameObject tooltipObj; // 툴팁 저장용

    public void Setup(SkillData skill)
    {
        // 스킬 이름 세팅
        if (nameText != null)
            nameText.text = skill.name;

        //  커맨드(예: "qqw")를 읽어서 원소 아이콘 생성
        CreateCommandIcons(skill.combo);

        // 툴팁 생성
        CreateTooltip(skill);
    }

    // 커맨드 문자열을 분석해서 원소 아이콘을 만드는 함수
    private void CreateCommandIcons(string combo)
    {
        if (commandIconContainer == null || string.IsNullOrEmpty(combo)) return;

        // 기존에 생성된 게 있다면 싹 지우기
        foreach (Transform child in commandIconContainer)
        {
            Destroy(child.gameObject);
        }

        // 대문자 입력도 처리하기 위해 소문자로 통일
        combo = combo.ToLower();

        // 문자 하나하나(q, w, e, r)를 확인하며 이미지 생성
        foreach (char c in combo)
        {
            GameObject iconObj = new GameObject($"Element_{c}");
            iconObj.transform.SetParent(commandIconContainer, false);

            // 아이콘 크기 설정
            RectTransform rect = iconObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(50f, 50f);

            Image img = iconObj.AddComponent<Image>();


            // ComboSystem에서 알파벳에 맞는 스프라이트를 가져옴
            Sprite elementSprite = GetElementSprite(c);
            if (elementSprite != null)
            {
                img.sprite = elementSprite;
            }
            else
            {
                img.color = Color.gray; // 매칭되는 게 없으면 회색 네모
            }
        }
    }

    // Q, W, E, R 알파벳에 맞춰서 ComboSystem의 스프라이트를 반환
    private Sprite GetElementSprite(char c)
    {
        switch (c)
        {
            case 'q': return iconQ;
            case 'w': return iconW;
            case 'e': return iconE;
            case 'r': return iconR;
            default: return null;
        }
    }

    // 마우스 이벤트 (툴팁 On/Off)
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipObj != null) tooltipObj.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipObj != null) tooltipObj.SetActive(false);
    }

    // 툴팁 생성 (이전과 동일, 우측 표시)
    private void CreateTooltip(SkillData skillData)
    {
        tooltipObj = new GameObject("Tooltip");
        tooltipObj.transform.SetParent(transform, false);

        RectTransform tooltipRect = tooltipObj.AddComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0f, 0.5f);
        tooltipRect.pivot = new Vector2(1f, 0.5f); // 기준점을 툴팁의 우측으로
        tooltipRect.anchoredPosition = new Vector2(-15f, 0f); // 패널 좌측으로 15만큼 띄움
        tooltipRect.sizeDelta = new Vector2(200f, 150f);

        Image bgImage = tooltipObj.AddComponent<Image>();
        bgImage.color = new Color(0.9f, 0.7f, 0.3f, 1f);

        Outline bgOutline = tooltipObj.AddComponent<Outline>();
        bgOutline.effectColor = Color.black;
        bgOutline.effectDistance = new Vector2(2, -2);

        // 제목
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.sizeDelta = new Vector2(-10f, 40f);
        titleRect.anchoredPosition = new Vector2(0f, -5f);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 20;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.black;
        titleText.fontStyle = FontStyle.Bold;
        titleText.text = skillData.name;

        // 설명
        GameObject descObj = new GameObject("Description");
        descObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = Vector2.zero;
        descRect.anchorMax = Vector2.one;
        descRect.sizeDelta = new Vector2(-10f, -50f);
        descRect.anchoredPosition = new Vector2(0f, -10f);

        Text descText = descObj.AddComponent<Text>();
        descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        descText.fontSize = 16;
        descText.alignment = TextAnchor.MiddleCenter;
        descText.color = Color.black;
        descText.text = $"콤보: {skillData.combo}\n\n{skillData.description}";

        tooltipObj.SetActive(false); // 시작 시 꺼둠
    }
}