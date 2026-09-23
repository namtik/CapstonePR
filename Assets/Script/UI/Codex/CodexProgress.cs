using System.Collections.Generic;
using UnityEngine;

namespace Battle.UI
{
    // 도감 발견 기록 — 런이 끝나도 유지되도록 PlayerPrefs에 저장한다
    public static class CodexProgress
    {
        const string RelicKey = "Codex.DiscoveredRelics"; // 발견 유물 id 목록 키(';' 구분)
        const char Separator = ';';                       // id 구분자

        static HashSet<string> _relics; // 발견 유물 id 캐시(최초 접근 시 로드)

        // 발견한 유물 수
        public static int DiscoveredRelicCount => Relics.Count;

        // 해당 id의 유물을 발견했는지
        public static bool IsRelicDiscovered(string relicId)
        {
            return !string.IsNullOrWhiteSpace(relicId) && Relics.Contains(relicId);
        }

        // 유물을 발견 처리하고 저장(이미 발견했거나 이미지 전용 임시 유물이면 무시)
        public static void MarkRelic(string relicId)
        {
            if (string.IsNullOrWhiteSpace(relicId) || relicId.StartsWith("sprite_")) return;
            if (!Relics.Add(relicId)) return;
            PlayerPrefs.SetString(RelicKey, string.Join(Separator.ToString(), Relics));
            PlayerPrefs.Save();
        }

        // 유물 발견 기록 초기화(디버그용)
        public static void ResetRelics()
        {
            Relics.Clear();
            PlayerPrefs.DeleteKey(RelicKey);
            PlayerPrefs.Save();
        }

        static HashSet<string> Relics
        {
            get
            {
                if (_relics != null) return _relics;
                _relics = new HashSet<string>();
                string raw = PlayerPrefs.GetString(RelicKey, string.Empty);
                foreach (var id in raw.Split(Separator))
                    if (!string.IsNullOrWhiteSpace(id)) _relics.Add(id);
                return _relics;
            }
        }
    }
}
