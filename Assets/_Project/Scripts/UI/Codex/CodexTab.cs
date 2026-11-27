using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Battle.UI
{
    // 도감 탭 공통 베이스 — CodexPanelController가 탭 전환 시 Refresh를 호출한다
    public abstract class CodexTab : MonoBehaviour
    {
        public abstract void Refresh();

        // 수집률 표시 문구(퍼센트, 반올림)
        protected static string CollectionPercent(int discovered, int total)
        {
            int pct = total > 0 ? Mathf.RoundToInt(100f * discovered / total) : 0;
            return pct + "%";
        }

        // 수집률 고리가 있으면 고리로, 없으면 텍스트로 표시
        protected static void ShowCollection(TMP_Text countText, int discovered, int total)
        {
            if (countText == null) return;
            CodexCollectionRing ring = countText.GetComponentInParent<CodexCollectionRing>();
            if (ring != null) ring.Set(discovered, total);
            else countText.text = CollectionPercent(discovered, total);
        }

        // 미발견 항목 실루엣 처리 — 모든 이미지를 color로 칠하고(원래 알파 유지) 텍스트는 숨긴다
        public static void ApplySilhouette(GameObject root, Color color)
        {
            if (root == null) return;
            foreach (var g in root.GetComponentsInChildren<Graphic>(true))
            {
                if (g is TMP_Text) { g.enabled = false; continue; }
                Color c = color;
                c.a *= g.color.a;
                g.color = c;
            }
        }

        // from 위쪽에서 스크롤 마스크(RectMask2D/Mask) RectTransform을 찾는다(없으면 null)
        protected static RectTransform FindClipRect(Transform from)
        {
            Transform t = from;
            while (t != null)
            {
                if (t.GetComponent<RectMask2D>() != null || t.GetComponent<Mask>() != null)
                    return t as RectTransform;
                t = t.parent;
            }
            return null;
        }
    }
}
