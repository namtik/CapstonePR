using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Battle.Card;

namespace Battle.UI
{
    /// <summary>
    /// 책 보상 선택지 1개를 바인딩하는 뷰 컴포넌트.
    /// </summary>
    public class BookRewardItemUI : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private Button selectButton;
        [SerializeField] private GameObject selectionFrame;

        [Header("Texts")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;

        [Header("Images")]
        [SerializeField] private Image skillIconImage;
        [SerializeField] private Image[] elementImages = new Image[3];

        private Sprite _fireSprite;
        private Sprite _waterSprite;
        private Sprite _windSprite;
        private Sprite _earthSprite;

        ComboSkillDef _boundSkill;

        public ComboSkillDef BoundSkill => _boundSkill;

        public void SetElementSprites(Sprite fire, Sprite water, Sprite wind, Sprite earth)
        {
            _fireSprite = fire;
            _waterSprite = water;
            _windSprite = wind;
            _earthSprite = earth;
        }

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

        public void SetSelected(bool selected)
        {
            if (selectionFrame != null)
                selectionFrame.SetActive(selected);
        }

        void SetElementImage(int index, CardElement element)
        {
            if (elementImages == null || index < 0 || index >= elementImages.Length) return;

            Image img = elementImages[index];
            if (img == null) return;

            img.sprite = ResolveElementSprite(element);
            img.enabled = img.sprite != null;
        }

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
