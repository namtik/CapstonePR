using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 클리어/게임오버의 KilledMonster에 처치 초상화를 왼쪽 위부터 채운다.
public static class RunKillGallery
{
    const float PanelWidth = 152f; // 전투 로그 적 기록 패널 폭
    const float PanelHeight = 105f; // 전투 로그 적 기록 패널 높이
    const float BackWidth = 152f;
    const float BackHeight = 102f;
    const float IconWidth = 135f; // 패널 안 초상화 폭
    const float IconHeight = 100f; // 패널 안 초상화 높이
    const float PortraitGap = 10f;
    const float EliteScale = 1.4f; // 일반 대비 정예
    const float BossScale = 1.85f; // 일반 대비 보스. 정예보다 크다.

    static Sprite _enemyFrame;
    static TMP_FontAsset _font;

    // 전투 로그가 적 기록에 올리는 프레임
    static Sprite EnemyFrame
    {
        get
        {
            if (_enemyFrame == null)
                _enemyFrame = Resources.Load<Sprite>("UI/Tab/적 기록 패널");
            return _enemyFrame;
        }
    }

    // 기존 초상화를 지우고, 잡은 순서대로 다시 채운다. 칸이 모자라면 비율을 유지한 채 줄인다.
    public static void Fill(RectTransform root, IReadOnlyList<GameStateController.RunKillRecord> kills)
    {
        if (root == null) return;

        for (int i = root.childCount - 1; i >= 0; i--)
            Object.Destroy(root.GetChild(i).gameObject);

        int count = kills != null ? kills.Count : 0;
        if (count <= 0) return;

        float width = root.rect.width;
        float height = root.rect.height;
        if (width < 1f) width = root.sizeDelta.x;
        if (height < 1f) height = root.sizeDelta.y;
        if (width < 1f) width = 1600f;
        if (height < 1f) height = 600f;

        ResolveFont(root);

        float scale = 1f;
        if (PanelWidth * BossScale > width)
            scale = width / (PanelWidth * BossScale);

        float usedHeight = 0f;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            TryMeasure(kills, count, width, scale, out usedHeight);
            if (usedHeight <= height + 0.5f) break;

            float fit = height / Mathf.Max(1f, usedHeight);
            scale = Mathf.Max(0.2f, scale * Mathf.Clamp(fit, 0.45f, 0.92f));
        }

        float left = -width * 0.5f;
        float top = height * 0.5f;
        float cursorX = 0f;
        float cursorY = 0f;
        float rowH = 0f;
        for (int i = 0; i < count; i++)
        {
            Sprite portrait = kills[i].Portrait;
            if (portrait == null) continue;

            float rank = RankScale(kills[i].Rank);
            float cellW = PanelWidth * scale * rank;
            float cellH = PanelHeight * scale * rank;
            if (cursorX > 0f && cursorX + cellW > width)
            {
                cursorY += rowH + PortraitGap * scale;
                cursorX = 0f;
                rowH = 0f;
            }

            float x = left + cursorX + cellW * 0.5f;
            float y = top - cursorY - cellH * 0.5f;
            CreateSlot(root, portrait, kills[i].EnemyName, x, y, cellW, cellH);
            cursorX += cellW + PortraitGap * scale;
            if (cellH > rowH) rowH = cellH;
        }
    }

    // 이 배율로 한 줄씩 놓았을 때 필요한 높이를 잰다.
    static void TryMeasure(IReadOnlyList<GameStateController.RunKillRecord> kills, int count, float width, float scale, out float usedHeight)
    {
        float cursorX = 0f;
        float cursorY = 0f;
        float rowH = 0f;
        for (int i = 0; i < count; i++)
        {
            if (kills[i].Portrait == null) continue;

            float rank = RankScale(kills[i].Rank);
            float cellW = PanelWidth * scale * rank;
            float cellH = PanelHeight * scale * rank;
            if (cursorX > 0f && cursorX + cellW > width)
            {
                cursorY += rowH + PortraitGap * scale;
                cursorX = 0f;
                rowH = 0f;
            }

            cursorX += cellW + PortraitGap * scale;
            if (cellH > rowH) rowH = cellH;
        }

        usedHeight = cursorY + rowH;
    }

    static float RankScale(EnemyClass rank)
    {
        if (rank == EnemyClass.Boss) return BossScale;
        if (rank == EnemyClass.Elite) return EliteScale;
        return 1f;
    }

    static void ResolveFont(RectTransform root)
    {
        if (_font != null) return;

        Canvas canvas = root.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            TMP_Text[] texts = canvas.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                string name = texts[i].gameObject.name;
                if (name != "WinText" && name != "DefeatText") continue;
                if (texts[i].font == null) continue;
                _font = texts[i].font;
                break;
            }

            if (_font == null && texts.Length > 0)
                _font = texts[0].font;
        }

        if (_font == null)
            _font = TMP_Settings.defaultFontAsset;
    }

    // 전투 로그와 같은 겹침: 회색 바탕, 초상화, 적 기록 프레임, 그 위에 이름
    static void CreateSlot(RectTransform root, Sprite portrait, string enemyName, float x, float y, float cellW, float cellH)
    {
        float s = cellW / PanelWidth;

        GameObject go = new GameObject("KillPortrait", typeof(RectTransform));
        go.transform.SetParent(root, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(cellW, cellH);
        rt.anchoredPosition = new Vector2(x, y);

        CreateImage(rt, "Back", 0f, 0f, BackWidth * s, BackHeight * s, null, new Color(0.45f, 0.45f, 0.45f, 1f));
        CreateImage(rt, "Icon", (PanelWidth - IconWidth) * 0.5f * s, 0f, IconWidth * s, IconHeight * s, portrait, Color.white);
        if (EnemyFrame != null)
            CreateImage(rt, "Frame", 0f, 0f, PanelWidth * s, PanelHeight * s, EnemyFrame, Color.white);
        CreateLabel(rt, enemyName, s);
    }

    static void CreateLabel(RectTransform parent, string value, float s)
    {
        if (string.IsNullOrEmpty(value)) return;

        GameObject textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(parent, false);
        RectTransform rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(136f * s, 22f * s);
        rect.anchoredPosition = new Vector2(8f * s, -80f * s);

        TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
        label.text = value;
        if (_font != null) label.font = _font;
        label.fontSize = Mathf.Max(6f, 16f * s);
        label.color = new Color(0.96f, 0.93f, 0.86f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        if (label.font != null)
        {
            label.outlineWidth = 0.15f;
            label.outlineColor = Color.black;
        }
    }

    static void CreateImage(RectTransform parent, string name, float x, float yFromTop, float w, float h, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -yFromTop);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }
}
