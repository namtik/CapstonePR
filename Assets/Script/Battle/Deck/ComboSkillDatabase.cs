using System;
using System.Collections.Generic;
using UnityEngine;
using Battle.Card;

namespace Battle
{
    /// <summary>
    /// 콤보 스킬 정의 (Resources/ComboDB/ComboSkills.json + ComboEffects.json 기반).
    /// 기획자가 ComboSkill_DB.xlsx → Tools/Combo DB 메뉴로 JSON 갱신 → 게임 반영.
    /// canonical(1000~1019)에 효과가 정의되고, alias(1020~1063)는 refComboId로 효과 공유.
    /// </summary>
    public static class ComboSkillDatabase
    {
        const string SKILLS_RESOURCE_PATH  = "ComboDB/ComboSkills";
        const string EFFECTS_RESOURCE_PATH = "ComboDB/ComboEffects";
        const string ICONS_RESOURCE_PATH   = "ComboSkillIcons";

        private static List<ComboSkillData> _all;
        private static Dictionary<int, ComboSkillData> _byId;
        private static Dictionary<int, List<ComboEffectData>> _effectsByRef;

        public static void EnsureInit()
        {
            if (_all != null) return;
            LoadAll();
        }

        /// <summary>캐시 초기화 — 에디터에서 JSON 재변환 후 다시 로드할 때 사용.</summary>
        public static void ResetCache()
        {
            _all = null;
            _byId = null;
            _effectsByRef = null;
        }

        public static IReadOnlyList<ComboSkillData> All
        {
            get { EnsureInit(); return _all; }
        }

        public static IReadOnlyList<ComboEffectData> GetEffects(int refComboId)
        {
            EnsureInit();
            return _effectsByRef.TryGetValue(refComboId, out var list)
                ? list
                : (IReadOnlyList<ComboEffectData>)Array.Empty<ComboEffectData>();
        }

        // ─────────────────────────────────────────────────────────────
        // 보유 콤보 구성
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 보유할 refComboId 집합으로 런타임 ComboSkillDef 리스트를 만든다.
        /// 각 refComboId당 1개(canonical 슬롯 순서로 표시), 매칭은 모든 순서 변형 허용.
        /// ownedRefIds가 null/비어있으면 전체(1000~1019) 보유.
        /// </summary>
        public static List<ComboSkillDef> BuildOwnedCombos(IEnumerable<int> ownedRefIds = null)
        {
            EnsureInit();

            // refComboId → 모든 슬롯 순서 키 모음 (순서무관 매칭용)
            var ordersByRef = new Dictionary<int, HashSet<string>>();
            // refComboId → canonical(=id가 refComboId와 같은) 데이터. 없으면 첫 등장 데이터.
            var canonicalByRef = new Dictionary<int, ComboSkillData>();

            foreach (var c in _all)
            {
                CardElement e1 = ParseElement(c.slot1);
                CardElement e2 = ParseElement(c.slot2);
                CardElement e3 = ParseElement(c.slot3);

                if (!ordersByRef.TryGetValue(c.refComboId, out var set))
                {
                    set = new HashSet<string>();
                    ordersByRef[c.refComboId] = set;
                }
                set.Add(ComboSkillDef.OrderKey(e1, e2, e3));

                bool isCanonical = c.id == c.refComboId;
                if (isCanonical || !canonicalByRef.ContainsKey(c.refComboId))
                    canonicalByRef[c.refComboId] = c;
            }

            HashSet<int> owned = null;
            if (ownedRefIds != null)
            {
                owned = new HashSet<int>(ownedRefIds);
                if (owned.Count == 0) owned = null;
            }

            var result = new List<ComboSkillDef>();
            foreach (var kv in canonicalByRef)
            {
                int refId = kv.Key;
                if (owned != null && !owned.Contains(refId)) continue;

                var data = kv.Value;
                var effects = _effectsByRef.TryGetValue(refId, out var list) ? list : new List<ComboEffectData>();

                var def = new ComboSkillDef
                {
                    fromDatabase  = true,
                    refComboId    = refId,
                    slot1         = ParseElement(data.slot1),
                    slot2         = ParseElement(data.slot2),
                    slot3         = ParseElement(data.slot3),
                    skillImg      = data.skillImg,
                    skillIcon     = ResolveSkillIcon(data.skillImg),
                    descriptionKR = data.description,
                    dbEffects     = effects,
                    acceptedOrders = ordersByRef[refId],
                    // 표시 이름: comboName 비어있으면 설명 앞부분/슬롯으로 폴백
                    displayName   = !string.IsNullOrEmpty(data.comboName)
                                    ? data.comboName
                                    : $"콤보 {refId}",
                    // 시각 효과(EndAwaken 셰이크/분산)는 effect==Damage 여부만 보므로 데미지 포함 시 Damage로.
                    effect        = HasDamage(effects) ? ComboEffectType.Damage : ComboEffectType.Draw,
                };
                result.Add(def);
            }

            // refComboId 오름차순으로 정렬(표시 안정성)
            result.Sort((a, b) => a.refComboId.CompareTo(b.refComboId));
            return result;
        }

        static bool HasDamage(List<ComboEffectData> effects)
        {
            if (effects == null) return false;
            foreach (var e in effects)
                if (string.Equals(e.doAction, "DAMAGE", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        static Sprite ResolveSkillIcon(string skillImg)
        {
            if (string.IsNullOrWhiteSpace(skillImg)) return null;
            return Resources.Load<Sprite>($"{ICONS_RESOURCE_PATH}/{skillImg.Trim()}");
        }

        public static CardElement ParseElement(string s)
        {
            switch ((s ?? "").Trim().ToUpperInvariant())
            {
                case "FIRE":  return CardElement.Fire;
                case "WATER": return CardElement.Water;
                case "WIND":  return CardElement.Wind;
                case "EARTH": return CardElement.Earth;
                default:      return CardElement.Neutral;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 로드
        // ─────────────────────────────────────────────────────────────

        static void LoadAll()
        {
            _all = new List<ComboSkillData>();
            _byId = new Dictionary<int, ComboSkillData>();
            _effectsByRef = new Dictionary<int, List<ComboEffectData>>();

            var skillsAsset = Resources.Load<TextAsset>(SKILLS_RESOURCE_PATH);
            if (skillsAsset == null)
            {
                Debug.LogError($"[ComboSkillDatabase] Resources/{SKILLS_RESOURCE_PATH}.json 없음. " +
                               "Tools/Combo DB/Excel → JSON 변환을 실행했는지 확인.");
                return;
            }

            try
            {
                var payload = JsonUtility.FromJson<ComboSkillDataList>(skillsAsset.text);
                if (payload?.combos != null)
                {
                    foreach (var c in payload.combos)
                    {
                        _all.Add(c);
                        _byId[c.id] = c;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ComboSkillDatabase] ComboSkills.json 파싱 실패: {e.Message}");
            }

            var effectsAsset = Resources.Load<TextAsset>(EFFECTS_RESOURCE_PATH);
            if (effectsAsset == null)
            {
                Debug.LogWarning($"[ComboSkillDatabase] Resources/{EFFECTS_RESOURCE_PATH}.json 없음 — 효과 없는 콤보로 로드.");
                return;
            }

            try
            {
                var payload = JsonUtility.FromJson<ComboEffectDataList>(effectsAsset.text);
                if (payload?.effects != null)
                {
                    foreach (var e in payload.effects)
                    {
                        if (!_effectsByRef.TryGetValue(e.refComboId, out var list))
                        {
                            list = new List<ComboEffectData>();
                            _effectsByRef[e.refComboId] = list;
                        }
                        list.Add(e);
                    }
                    // EffectIndex 순서 보장
                    foreach (var list in _effectsByRef.Values)
                        list.Sort((a, b) => a.index.CompareTo(b.index));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ComboSkillDatabase] ComboEffects.json 파싱 실패: {e.Message}");
            }

            Debug.Log($"[ComboSkillDatabase] 로드 완료 — 콤보 {_all.Count}개, 효과 정의 {_effectsByRef.Count}종");
        }
    }
}
