using System;
using System.Collections.Generic;
using UnityEngine;
using Battle.Card;

namespace Battle
{
    // 콤보 스킬/효과 정의를 로드·조회하는 정적 데이터베이스
    public static class ComboSkillDatabase
    {
        const string SKILLS_RESOURCE_PATH  = "ComboDB/ComboSkills";  // 콤보 스킬 JSON 경로
        const string EFFECTS_RESOURCE_PATH = "ComboDB/ComboEffects"; // 콤보 효과 JSON 경로
        const string ICONS_RESOURCE_PATH   = "ComboSkillIcons";      // 콤보 아이콘 리소스 경로

        private static List<ComboSkillData> _all;                              // 전체 콤보 목록
        private static Dictionary<int, List<ComboEffectData>> _effectsByRef;   // 참조 ID → 효과 목록 맵

        // 최초 1회 로드 보장
        public static void EnsureInit()
        {
            if (_all != null) return;
            LoadAll();
        }

        // 캐시 초기화(JSON 재변환 후 재로드용)
        public static void ResetCache()
        {
            _all = null;
            _effectsByRef = null;
        }

        // 전체 콤보 목록
        public static IReadOnlyList<ComboSkillData> All
        {
            get { EnsureInit(); return _all; }
        }

        // 참조 콤보 ID로 효과 목록 조회
        public static IReadOnlyList<ComboEffectData> GetEffects(int refComboId)
        {
            EnsureInit();
            return _effectsByRef.TryGetValue(refComboId, out var list)
                ? list
                : (IReadOnlyList<ComboEffectData>)Array.Empty<ComboEffectData>();
        }

        // 보유 refComboId 집합으로 런타임 콤보 정의 목록 구성
        public static List<ComboSkillDef> BuildOwnedCombos(IEnumerable<int> ownedRefIds = null)
        {
            EnsureInit();

            var canonicalByRef = new Dictionary<int, ComboSkillData>();

            foreach (var c in _all)
            {
                // refComboId의 대표 행 선택 — 정규 행(id==refComboId) 우선, 없으면 첫 행
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
                    displayName   = !string.IsNullOrEmpty(data.comboName)
                                    ? data.comboName
                                    : $"콤보 {refId}",
                    effect        = HasDamage(effects) ? ComboEffectType.Damage : ComboEffectType.Draw,
                };
                result.Add(def);
            }

            result.Sort((a, b) => a.refComboId.CompareTo(b.refComboId));
            return result;
        }

        // 효과 목록에 DAMAGE 동사 포함 여부 판정
        static bool HasDamage(List<ComboEffectData> effects)
        {
            if (effects == null) return false;
            foreach (var e in effects)
                if (string.Equals(e.doAction, "DAMAGE", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        // 스킬 이미지명으로 아이콘 스프라이트 로드
        static Sprite ResolveSkillIcon(string skillImg)
        {
            if (string.IsNullOrWhiteSpace(skillImg)) return null;
            return Resources.Load<Sprite>($"{ICONS_RESOURCE_PATH}/{skillImg.Trim()}");
        }

        // 문자열을 CardElement로 파싱
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

        // 콤보 스킬/효과 JSON 전체 로드
        static void LoadAll()
        {
            _all = new List<ComboSkillData>();
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
