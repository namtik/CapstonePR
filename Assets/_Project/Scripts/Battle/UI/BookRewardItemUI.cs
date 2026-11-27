using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Battle.Card;

namespace Battle.UI
{
    // 책 보상 선택지 1개를 바인딩하는 뷰 컴포넌트
    public class BookRewardItemUI : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private Button selectButton; // 선택 버튼
        [SerializeField] private GameObject selectionFrame; // 선택 표시 프레임

        [Header("Texts")]
        [SerializeField] private TMP_Text nameText; // 스킬 이름 텍스트
        [SerializeField] private TMP_Text descriptionText; // 스킬 설명 텍스트

        [Header("Images")]
        [SerializeField] private Image skillIconImage; // 스킬 아이콘 이미지
        [SerializeField] private Image[] elementImages = new Image[3]; // 슬롯 속성 아이콘 이미지

        private Sprite _fireSprite; // 불 속성 스프라이트
        private Sprite _waterSprite; // 물 속성 스프라이트
        private Sprite _windSprite; // 바람 속성 스프라이트
        private Sprite _earthSprite; // 땅 속성 스프라이트

        ComboSkillDef _boundSkill; // 현재 바인딩된 스킬

        // 현재 바인딩된 스킬 반환
        public ComboSkillDef BoundSkill => _boundSkill;

        // 속성별 스프라이트를 주입
        public void SetElementSprites(Sprite fire, Sprite water, Sprite wind, Sprite earth)
        {
            _fireSprite = fire;
            _waterSprite = water;
            _windSprite = wind;
            _earthSprite = earth;
        }

        // 스킬과 선택 콜백을 받아 뷰를 채우고 버튼을 연결
        public void Bind(ComboSkillDef skill, System.Action<ComboSkillDef> onSelected)
        {
            _boundSkill = skill;

            if (nameText != null)
                nameText.text = skill != null && !string.IsNullOrWhiteSpace(skill.displayName)
                    ? skill.displayName
                    : "콤보 스킬";

            if (descriptionText != null)
                descriptionText.text = skill != null ? (skill.descriptionKR ?? string.Empty) : string.Empty;

            if (skillIconImage != null)
            {
                skillIconImage.sprite = skill != null ? skill.skillIcon : null;
                skillIconImage.enabled = skillIconImage.sprite != null;
            }

            if (elementImages != null && elementImages.Length >= 3)
            {
                SetElementImage(0, skill != null ? skill.slot1 : CardElement.Neutral);
                SetElementImage(1, skill != null ? skill.slot2 : CardElement.Neutral);
                SetElementImage(2, skill != null ? skill.slot3 : CardElement.Neutral);
            }

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() =>
                {
                    if (_boundSkill != null)
                        onSelected?.Invoke(_boundSkill);
                });
            }

            SetSelected(false);
        }

        // 선택 표시 프레임을 켜고 끔
        public void SetSelected(bool selected)
        {
            if (selectionFrame != null)
                selectionFrame.SetActive(selected);
        }

        // 지정 슬롯 인덱스에 속성 아이콘을 설정
        void SetElementImage(int index, CardElement element)
        {
            if (elementImages == null || index < 0 || index >= elementImages.Length) return;

            Image img = elementImages[index];
            if (img == null) return;

            img.sprite = ResolveElementSprite(element);
            img.enabled = img.sprite != null;
        }

        // 속성 값을 대응하는 스프라이트로 변환
        Sprite ResolveElementSprite(CardElement element)
        {
            return element switch
            {
                CardElement.Fire => _fireSprite,
                CardElement.Water => _waterSprite,
                CardElement.Wind => _windSprite,
                CardElement.Earth => _earthSprite,
                _ => null
            };
        }
    }
}
