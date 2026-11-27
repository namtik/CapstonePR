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
        // 콤보 슬롯 3칸의 원소 아이콘을 현재 입력 시퀀스로 갱신. 프레임은 유지한다.
        public void UpdateComboSlot(IList<CardElement> input)
        {
            EnsureComboSlotFrames();

            if (comboSlotImages == null || comboSlotImages.Length == 0)
            {
                Debug.LogWarning("[CardHandHUD] UpdateComboSlot: comboSlotImages가 비어있고 자동 탐색도 실패. ComboSlotPanel의 자식 Image 3개를 인스펙터에 연결하세요.");
                return;
            }

            for (int i = 0; i < comboSlotImages.Length; i++)
            {
                Image frame = comboSlotImages[i];
                if (frame == null) continue;
                Image icon = GetComboSlotIcon(frame);
                if (icon == null) continue;

                var vfx = frame.GetComponent<ComboSlotElementVfx>();
                if (input != null && i < input.Count)
                {
                    Sprite s = GetComboSlotElementSprite(input[i]);
                    icon.sprite = s;
                    Color iconColor = s != null ? Color.white : ColorForElement(input[i]);
                    iconColor.a = 1f;
                    icon.color = iconColor;
                    icon.enabled = true;
                    if (comboSlotElementVfxEnabled)
                        GetComboSlotVfx(frame).Show(input[i]);
                    else if (vfx != null)
                        vfx.Hide();
                }
                else
                {
                    icon.sprite = null;
                    icon.enabled = false;
                    if (vfx != null) vfx.Hide();
                }
            }
        }

        // 콤보 슬롯 Image가 미연결이면 패널 자식에서 3개를 자동 탐색
        void EnsureComboSlotImagesBound()
        {
            bool needBind = comboSlotImages == null || comboSlotImages.Length < 3
                || comboSlotImages[0] == null || comboSlotImages[1] == null || comboSlotImages[2] == null;
            if (!needBind) return;
            if (comboSlotPanel == null) return;

            var collected = new List<Image>();
            // 직계 자식 우선 수집
            for (int i = 0; i < comboSlotPanel.transform.childCount && collected.Count < 3; i++)
            {
                var img = comboSlotPanel.transform.GetChild(i).GetComponent<Image>();
                if (img != null) collected.Add(img);
            }
            // 부족하면 모든 후손에서 수집
            if (collected.Count < 3)
            {
                var all = comboSlotPanel.GetComponentsInChildren<Image>(true);
                foreach (var img in all)
                {
                    if (img == null) continue;
                    if (img.gameObject == comboSlotPanel) continue; // 패널 자체 제외
                    if (img.gameObject.name == "ElementIcon") continue; // 프레임 안의 아이콘 제외
                    if (img.gameObject.name == "SlotBg") continue; // 프레임 안 배경 제외
                    if (img.transform.parent != null && img.transform.parent.name == "ElementVfx") continue; // 테두리 이펙트 제외
                    if (collected.Contains(img)) continue;
                    collected.Add(img);
                    if (collected.Count >= 3) break;
                }
            }

            if (collected.Count >= 3)
            {
                comboSlotImages = new Image[3] { collected[0], collected[1], collected[2] };
                Debug.Log("[CardHandHUD] 콤보 슬롯 Image 3개 자동 바인딩 완료.");
            }
        }

        // 3칸 프레임에 스프라이트를 깔고 불투명도를 맞춘다
        void EnsureComboSlotFrames()
        {
            EnsureComboSlotImagesBound();
            if (comboSlotImages == null) return;

            Sprite frameSprite = ResolveComboSlotFrameSprite();
            for (int i = 0; i < comboSlotImages.Length; i++)
            {
                Image frame = comboSlotImages[i];
                if (frame == null) continue;

                if (frameSprite != null)
                {
                    frame.sprite = frameSprite;
                    frame.type = Image.Type.Simple;
                    frame.preserveAspect = true;
                    frame.color = Color.white;
                    RectTransform rect = frame.rectTransform;
                    float side = Mathf.Max(rect.sizeDelta.x, rect.sizeDelta.y);
                    if (side < 1f) side = 120f;
                    rect.sizeDelta = new Vector2(side, side);
                }
                else
                {
                    frame.sprite = null;
                    frame.color = emptyComboSlotColor;
                }

                GetComboSlotBackground(frame);
                Image icon = GetComboSlotIcon(frame);
                if (icon != null) icon.transform.SetAsLastSibling();
            }
        }

        // 인스펙터 스프라이트가 없으면 Resources에서 프레임을 불러온다
        Sprite ResolveComboSlotFrameSprite()
        {
            if (comboSlotFrameSprite != null) return comboSlotFrameSprite;
            if (_comboSlotFrameSpriteResolved != null) return _comboSlotFrameSpriteResolved;
            if (_comboSlotFrameLookupFailed) return null;

            _comboSlotFrameSpriteResolved = Resources.Load<Sprite>(ComboSlotFrameResourcePath);
            if (_comboSlotFrameSpriteResolved == null)
            {
                _comboSlotFrameLookupFailed = true;
                Debug.LogWarning("[CardHandHUD] 콤보 슬롯 프레임 스프라이트를 찾지 못했습니다: " + ComboSlotFrameResourcePath);
            }
            return _comboSlotFrameSpriteResolved;
        }

        // 인스펙터 스프라이트가 없으면 Resources에서 안쪽 배경을 불러온다
        Sprite ResolveComboSlotBgSprite()
        {
            if (comboSlotBgSprite != null) return comboSlotBgSprite;
            if (_comboSlotBgSpriteResolved != null) return _comboSlotBgSpriteResolved;
            if (_comboSlotBgLookupFailed) return null;

            _comboSlotBgSpriteResolved = Resources.Load<Sprite>(ComboSlotBgResourcePath);
            if (_comboSlotBgSpriteResolved == null)
            {
                _comboSlotBgLookupFailed = true;
                Debug.LogWarning("[CardHandHUD] 콤보 슬롯 안쪽 배경을 찾지 못했습니다: " + ComboSlotBgResourcePath);
            }
            return _comboSlotBgSpriteResolved;
        }

        // 금색 프레임 안쪽에 배경을 깔아 둔다. 속성 아이콘은 이 위에 올라온다.
        Image GetComboSlotBackground(Image frame)
        {
            if (frame == null) return null;

            Image bg;
            Transform existing = frame.transform.Find("SlotBg");
            if (existing != null)
                bg = existing.GetComponent<Image>();
            else
            {
                var go = new GameObject("SlotBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(frame.transform, false);
                bg = go.GetComponent<Image>();
                bg.raycastTarget = false;
            }
            if (bg == null) return null;

            FitInsideComboSlotFrame((RectTransform)bg.transform);
            bg.transform.SetAsFirstSibling();
            bg.sprite = ResolveComboSlotBgSprite();
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
            bg.color = Color.white;
            bg.enabled = bg.sprite != null;
            return bg;
        }

        // 프레임 안에 넣을 각성 전용 원소 아이콘. 불/물/바람/땅만 교체한다.
        Sprite GetComboSlotElementSprite(CardElement element)
        {
            switch (element)
            {
                case CardElement.Fire:
                    return ResolveComboSlotElementSprite(ref comboSlotFireSprite, "UI/UI_Awake_FireIcon");
                case CardElement.Water:
                    return ResolveComboSlotElementSprite(ref comboSlotWaterSprite, "UI/UI_Awake_WaterIcon");
                case CardElement.Wind:
                    return ResolveComboSlotElementSprite(ref comboSlotWindSprite, "UI/UI_Awake_WindIcon");
                case CardElement.Earth:
                    return ResolveComboSlotElementSprite(ref comboSlotEarthSprite, "UI/UI_Awake_LandIcon");
                default:
                    return cardPrefab != null ? cardPrefab.GetElementSprite(element) : null;
            }
        }

        // 인스펙터 스프라이트가 없으면 Resources에서 원소 아이콘을 불러온다
        Sprite ResolveComboSlotElementSprite(ref Sprite slot, string resourcePath)
        {
            if (slot != null) return slot;
            slot = Resources.Load<Sprite>(resourcePath);
            if (slot == null && _missingComboSlotSprites.Add(resourcePath))
                Debug.LogWarning("[CardHandHUD] 각성 원소 아이콘을 찾지 못했습니다: " + resourcePath);
            return slot;
        }

        // 프레임 안에 원소 아이콘용 Image를 만들거나 찾아 반환한다
        Image GetComboSlotIcon(Image frame)
        {
            if (frame == null) return null;

            Image icon;
            Transform existing = frame.transform.Find("ElementIcon");
            if (existing != null)
                icon = existing.GetComponent<Image>();
            else
            {
                var go = new GameObject("ElementIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(frame.transform, false);
                icon = go.GetComponent<Image>();
                icon.raycastTarget = false;
                icon.enabled = false;
            }
            if (icon == null) return null;

            FitInsideComboSlotFrame((RectTransform)icon.transform);
            icon.preserveAspect = true;
            return icon;
        }

        // 금색 테두리 안쪽에 딱 맞도록 자식 영역을 맞춘다
        void FitInsideComboSlotFrame(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(ComboSlotIconInset, ComboSlotIconInset);
            rect.anchorMax = new Vector2(1f - ComboSlotIconInset, 1f - ComboSlotIconInset);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        // 프레임에 속성 테두리 이펙트를 붙인다
        ComboSlotElementVfx GetComboSlotVfx(Image frame)
        {
            var vfx = frame.GetComponent<ComboSlotElementVfx>();
            if (vfx == null) vfx = frame.gameObject.AddComponent<ComboSlotElementVfx>();
            return vfx;
        }

        // 콤보가 맞았을 때 채워진 칸의 속성 이펙트를 한꺼번에 요동친다.
        public void PlayComboSlotSurge()
        {
            if (!comboSlotElementVfxEnabled) return;
            EnsureComboSlotImagesBound();
            if (comboSlotImages == null) return;

            for (int i = 0; i < comboSlotImages.Length; i++)
            {
                if (comboSlotImages[i] == null) continue;
                var vfx = comboSlotImages[i].GetComponent<ComboSlotElementVfx>();
                if (vfx != null) vfx.Surge();
            }
        }

        // 콤보 슬롯 slotIndex 칸을 팝(+플래시)으로 강조 — 각성 중 속성이 들어올 때 호출
        public void PlayComboSlotInputEffect(int slotIndex)
        {
            EnsureComboSlotImagesBound();
            if (comboSlotImages == null || slotIndex < 0 || slotIndex >= comboSlotImages.Length) return;
            if (comboSlotImages[slotIndex] == null) return;

            if (comboSlotElementVfxEnabled)
                GetComboSlotVfx(comboSlotImages[slotIndex]).Pulse();

            if (!comboSlotPopEnabled) return;

            // 칸 수에 맞춰 풀/기준 스케일 초기화
            if (_comboSlotPopCos == null || _comboSlotPopCos.Length != comboSlotImages.Length)
            {
                _comboSlotPopCos = new Coroutine[comboSlotImages.Length];
                _comboSlotBaseScales = new Vector3[comboSlotImages.Length];
                for (int i = 0; i < comboSlotImages.Length; i++)
                    _comboSlotBaseScales[i] = comboSlotImages[i] != null
                        ? comboSlotImages[i].transform.localScale : Vector3.one;
            }

            if (_comboSlotPopCos[slotIndex] != null) StopCoroutine(_comboSlotPopCos[slotIndex]);
            _comboSlotPopCos[slotIndex] = StartCoroutine(ComboSlotPopRoutine(slotIndex));
        }

        // 콤보 슬롯 한 칸을 커졌다 줄이며(+흰색 플래시) 강조하는 코루틴
        System.Collections.IEnumerator ComboSlotPopRoutine(int slotIndex)
        {
            var img = comboSlotImages[slotIndex];
            if (img == null) { _comboSlotPopCos[slotIndex] = null; yield break; }

            Transform tr = img.transform;
            Vector3 baseScale = _comboSlotBaseScales[slotIndex];
            Vector3 peak = baseScale * Mathf.Max(1f, comboSlotPopScale);
            Color baseColor = img.color; // UpdateComboSlot가 직전에 칠한 속성 색
            float dur = Mathf.Max(0.01f, comboSlotPopDuration);
            const float upPortion = 0.3f; // 앞 30%는 확대, 나머지는 복귀 구간

            float t = 0f;
            while (t < dur)
            {
                if (img == null) { _comboSlotPopCos[slotIndex] = null; yield break; }
                t += Time.deltaTime;
                float n = Mathf.Clamp01(t / dur);

                // 0→peak(ease-out) 후 peak→base(선형) 엔벨로프
                float env = n < upPortion
                    ? 1f - (1f - n / upPortion) * (1f - n / upPortion)
                    : 1f - (n - upPortion) / (1f - upPortion);
                env = Mathf.Clamp01(env);

                tr.localScale = Vector3.Lerp(baseScale, peak, env);
                if (comboSlotFlashEnabled)
                {
                    Color flash = Color.Lerp(baseColor, Color.white, env * 0.7f);
                    flash.a = baseColor.a;
                    img.color = flash;
                }
                yield return null;
            }

            tr.localScale = baseScale;
            img.color = baseColor;
            _comboSlotPopCos[slotIndex] = null;
        }

        // 콤보 스킬 목록을 프리팹으로 표시(쿨다운은 흐리게 + 남은 횟수)
        public void UpdateComboSkillList(IList<ComboSkillDef> skills, int[] cooldownRemaining)
        {
            if (comboSkillItemPrefab == null)
            {
                Debug.LogWarning("[CardHandHUD] comboSkillItemPrefab이 미연결 — 콤보 스킬 목록 표시 불가.");
                return;
            }

            RectTransform container = comboSkillItemContainer != null
                ? comboSkillItemContainer
                : (comboSkillPanel != null ? comboSkillPanel.transform as RectTransform : null);

            if (container == null)
            {
                Debug.LogWarning("[CardHandHUD] 콤보 스킬 컨테이너가 없음 — comboSkillItemContainer 또는 comboSkillPanel 연결 필요.");
                return;
            }

            // 필요 만큼 항목 인스턴스화
            int needed = skills != null ? skills.Count : 0;
            while (_comboSkillItems.Count < needed)
            {
                var item = Instantiate(comboSkillItemPrefab, container);
                _comboSkillItems.Add(item);
            }

            // 콤보 스킬 목록의 원소 아이콘은 카드에 쓰이던 기존 스프라이트
            Sprite fireSp  = cardPrefab != null ? cardPrefab.GetElementSprite(CardElement.Fire)  : null;
            Sprite waterSp = cardPrefab != null ? cardPrefab.GetElementSprite(CardElement.Water) : null;
            Sprite windSp  = cardPrefab != null ? cardPrefab.GetElementSprite(CardElement.Wind)  : null;
            Sprite earthSp = cardPrefab != null ? cardPrefab.GetElementSprite(CardElement.Earth) : null;

            // 각 항목 표시 갱신
            for (int i = 0; i < _comboSkillItems.Count; i++)
            {
                var item = _comboSkillItems[i];
                if (item == null) continue;

                if (i < needed && skills[i] != null)
                {
                    item.gameObject.SetActive(true);
                    int cooldown = (cooldownRemaining != null && i < cooldownRemaining.Length)
                        ? cooldownRemaining[i] : 0;
                    item.ApplyAwakenTextStyle(
                        comboSkillNameFont, comboSkillNameFontSize, comboSkillNameColor,
                        comboSkillNameFontStyle, comboSkillNameAlignment,
                        comboSkillCooldownFontSize, comboSkillCooldownColor, comboSkillCooldownFontStyle);
                    item.SetupForCombo(skills[i], cooldown, fireSp, waterSp, windSp, earthSp);
                }
                else
                {
                    item.gameObject.SetActive(false);
                }
            }
        }

        // 콤보 스킬 목록의 index번 항목을 한 번 호버하듯 튀게 한다(각성 중 쿨타임이 걸릴 때)
        public void PulseComboSkillItem(int index)
        {
            if (index < 0 || index >= _comboSkillItems.Count) return;
            var item = _comboSkillItems[index];
            if (item != null && item.gameObject.activeInHierarchy) item.PlayHoverPulse();
        }
    }
}
