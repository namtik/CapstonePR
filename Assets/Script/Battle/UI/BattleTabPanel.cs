using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Battle.UI
{
    // 전투 중 Tab으로 띄우는 요괴 정보 패널.
    // 프레임과 정보 칸은 CombatStage 캔버스에 미리 배치한다.
    // 플레이 중에는 이 스크립트가 위치·크기를 다시 쓰지 않는다.
    public class BattleTabPanel : MonoBehaviour
    {
        [Tooltip("열고 닫을 프레임. CombatStage 캔버스에 배치된 UI_Tab_Frame.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("왼쪽, 가운데, 오른쪽 요괴 정보 칸.")]
        [SerializeField] private MonsterInfoColumn[] columns;

        readonly List<EnemyController> _alive = new List<EnemyController>();
        readonly StringBuilder _label = new StringBuilder();
        Sprite[] _numberSprites;

        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        // 플레이 시작 시 닫아 둔다. 에디트 모드의 배치 상태는 유지된다.
        void Awake()
        {
            Hide();
        }

        void Update()
        {
            if (IsVisible) RefreshEnemyPortraits();
        }

        public void Toggle()
        {
            if (IsVisible) Hide();
            else Show();
        }

        public void Show()
        {
            if (panelRoot == null) return;
            transform.SetAsLastSibling();
            panelRoot.SetActive(true);
            RefreshEnemyPortraits();
        }

        public void Hide()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(false);
        }

        // 한 마리면 가운데, 두 마리면 왼쪽·가운데, 세 마리면 왼쪽·가운데·오른쪽.
        public void RefreshEnemyPortraits()
        {
            if (columns == null) return;

            EnemyCombatParty.CollectAlive(_alive);
            for (int i = 0; i < columns.Length; i++)
            {
                MonsterInfoColumn column = columns[i];
                if (column == null) continue;

                int enemyIndex = EnemyIndexForSlot(i, _alive.Count);
                EnemyData data = null;
                if (enemyIndex >= 0 && enemyIndex < _alive.Count)
                {
                    EnemyStat stat = _alive[enemyIndex].GetComponent<EnemyStat>();
                    if (stat != null) data = stat.Data;
                }

                ApplyColumn(column, data, data != null ? enemyIndex : -1);
            }
        }

        void ApplyColumn(MonsterInfoColumn column, EnemyData data, int enemyIndex)
        {
            bool show = data != null;
            if (column.group != null)
            {
                column.group.alpha = show ? 1f : 0f;
                column.group.blocksRaycasts = show;
            }

            if (column.enemyImage != null)
            {
                Sprite sprite = show ? data.enemySprite : null;
                column.enemyImage.sprite = sprite;
                column.enemyImage.enabled = sprite != null;
                column.enemyImage.preserveAspect = true;
                column.enemyImage.color = Color.white;
            }

            ApplySlotNumber(column, show ? enemyIndex : -1);
            SetText(column.attackText, show ? FormatAttack(data) : string.Empty);
            SetText(column.absorbText, show ? data.흡수수치.ToString() : string.Empty);
            SetText(column.interferenceText, show ? FormatInterference(data) : string.Empty);
        }

        // 칸 위치가 아니라 적 순번으로 번호를 붙인다. 한 마리면 가운데 칸에도 1.
        void ApplySlotNumber(MonsterInfoColumn column, int enemyIndex)
        {
            Image number = column.slotNumber;
            if (number == null && column.enemyImage != null)
            {
                Transform found = column.enemyImage.transform.parent.Find("SlotNumber");
                if (found != null) number = found.GetComponent<Image>();
                column.slotNumber = number;
            }
            if (number == null) return;

            if (enemyIndex < 0)
            {
                number.enabled = false;
                return;
            }

            EnsureNumberSprites();
            if (_numberSprites == null || enemyIndex >= _numberSprites.Length) return;
            Sprite sprite = _numberSprites[enemyIndex];
            if (sprite == null) return;

            number.enabled = true;
            number.sprite = sprite;
            number.preserveAspect = true;
            number.color = Color.white;
            number.rectTransform.sizeDelta = sprite.rect.size;
        }

        void EnsureNumberSprites()
        {
            if (_numberSprites != null && _numberSprites.Length == 3 && _numberSprites[0] != null) return;
            _numberSprites = new Sprite[3];
            _numberSprites[0] = Resources.Load<Sprite>("UI/Tab/UI_IconNum1");
            _numberSprites[1] = Resources.Load<Sprite>("UI/Tab/UI_IconNum2");
            _numberSprites[2] = Resources.Load<Sprite>("UI/Tab/UI_IconNum3");
        }

        static void SetText(TMP_Text label, string value)
        {
            if (label == null) return;
            if (label.text != value) label.text = value;
        }

        string FormatAttack(EnemyData data)
        {
            int[] sequence = data.attackSequence;
            if (sequence == null || sequence.Length == 0) return "-";

            _label.Length = 0;
            for (int i = 0; i < sequence.Length; i++)
            {
                if (i > 0) _label.Append(' ');
                _label.Append(sequence[i]);
            }
            return _label.ToString();
        }

        string FormatInterference(EnemyData data)
        {
            DisruptionAction[] actions = data.disruptionActions;
            _label.Length = 0;
            if (actions == null || actions.Length == 0)
            {
                AppendAction(_label, DisruptionAction.Curse);
                _label.Append(", ");
                AppendAction(_label, DisruptionAction.Exhaust);
                _label.Append(", ");
                AppendAction(_label, DisruptionAction.Absorb);
                _label.Append(", ");
                AppendAction(_label, DisruptionAction.Enhance);
                _label.Append(", ");
                AppendAction(_label, DisruptionAction.Recover);
                _label.Append(", ");
                AppendAction(_label, DisruptionAction.Discard);
                return _label.ToString();
            }

            for (int i = 0; i < actions.Length; i++)
            {
                if (i > 0) _label.Append(", ");
                AppendAction(_label, actions[i]);
            }
            return _label.ToString();
        }

        static void AppendAction(StringBuilder builder, DisruptionAction action)
        {
            switch (action)
            {
                case DisruptionAction.Curse: builder.Append("저주"); break;
                case DisruptionAction.Exhaust: builder.Append("탈진"); break;
                case DisruptionAction.Absorb: builder.Append("흡수"); break;
                case DisruptionAction.Enhance: builder.Append("강화"); break;
                case DisruptionAction.Recover: builder.Append("회복"); break;
                case DisruptionAction.Discard: builder.Append("버리기"); break;
                default: builder.Append(action.ToString()); break;
            }
        }

        // 빈 칸도 레이아웃에 남겨 두어 가운데·왼쪽 자리가 밀리지 않게 한다.
        static int EnemyIndexForSlot(int slot, int enemyCount)
        {
            if (enemyCount <= 0 || slot < 0) return -1;
            if (enemyCount == 1) return slot == 1 ? 0 : -1;
            if (slot >= enemyCount) return -1;
            return slot;
        }

        [System.Serializable]
        public class MonsterInfoColumn
        {
            public CanvasGroup group;
            public Image enemyImage;
            public Image slotNumber;
            public TMP_Text attackText;
            public TMP_Text absorbText;
            public TMP_Text interferenceText;
        }
    }
}
