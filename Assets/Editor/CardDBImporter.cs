using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace Battle.EditorTools
{
    /// <summary>
    /// Card_DB.xlsx → Assets/Resources/CardDB/*.json 변환기 (Unity Editor 메뉴).
    /// 기획자 워크플로우: Excel 수정 → Unity 메뉴 클릭 → 게임 반영.
    /// xlsx는 zip + XML 이므로 외부 라이브러리 없이 .NET 표준만으로 파싱.
    /// </summary>
    public static class CardDBImporter
    {
        const string PREF_LAST_PATH = "Battle.CardDB.LastSourcePath";
        const string OUT_DIR = "Assets/Resources/CardDB";

        // ─────────────────────────────────────────────────────────────
        // 메뉴
        // ─────────────────────────────────────────────────────────────

        [MenuItem("Tools/Card DB/Excel → JSON 변환...", priority = 10)]
        public static void ConvertFromExcelMenu()
        {
            string lastPath = EditorPrefs.GetString(PREF_LAST_PATH, "");
            string startDir;
            if (!string.IsNullOrEmpty(lastPath) && File.Exists(lastPath))
                startDir = Path.GetDirectoryName(lastPath);
            else
                startDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads");

            string path = EditorUtility.OpenFilePanel("Card_DB.xlsx 선택", startDir, "xlsx");
            if (string.IsNullOrEmpty(path)) return;

            EditorPrefs.SetString(PREF_LAST_PATH, path);
            ConvertFromExcel(path);
        }

        [MenuItem("Tools/Card DB/마지막 파일 다시 변환", priority = 11)]
        public static void ConvertFromLastPath()
        {
            string lastPath = EditorPrefs.GetString(PREF_LAST_PATH, "");
            if (string.IsNullOrEmpty(lastPath) || !File.Exists(lastPath))
            {
                EditorUtility.DisplayDialog(
                    "Card DB",
                    "최근에 변환한 파일이 없거나 존재하지 않습니다.\n'Excel → JSON 변환...' 메뉴로 먼저 선택하세요.",
                    "확인");
                return;
            }
            ConvertFromExcel(lastPath);
        }

        [MenuItem("Tools/Card DB/마지막 파일 다시 변환", validate = true)]
        static bool ValidateConvertFromLastPath()
        {
            string lastPath = EditorPrefs.GetString(PREF_LAST_PATH, "");
            return !string.IsNullOrEmpty(lastPath) && File.Exists(lastPath);
        }

        [MenuItem("Tools/Card DB/출력 폴더 열기", priority = 50)]
        public static void OpenOutputFolder()
        {
            string full = Path.GetFullPath(OUT_DIR);
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);
            EditorUtility.RevealInFinder(full);
        }

        // ─────────────────────────────────────────────────────────────
        // 변환 실행
        // ─────────────────────────────────────────────────────────────

        public static void ConvertFromExcel(string xlsxPath)
        {
            int cardCount = 0, effectCount = 0;
            try
            {
                EditorUtility.DisplayProgressBar("Card DB", $"파싱: {Path.GetFileName(xlsxPath)}", 0.2f);
                var workbook = ExcelReader.Read(xlsxPath);

                if (!workbook.Sheets.TryGetValue("Cards", out var cardsSheet))
                    throw new Exception("Excel에 'Cards' 시트가 없습니다.");
                if (!workbook.Sheets.TryGetValue("CardEffects", out var effectsSheet))
                    throw new Exception("Excel에 'CardEffects' 시트가 없습니다.");

                EditorUtility.DisplayProgressBar("Card DB", "JSON 생성 중...", 0.5f);
                string cardsJson = BuildCardsJson(cardsSheet, out cardCount);
                string effectsJson = BuildEffectsJson(effectsSheet, out effectCount);

                Directory.CreateDirectory(OUT_DIR);
                var enc = new UTF8Encoding(false);
                File.WriteAllText(Path.Combine(OUT_DIR, "Cards.json"), cardsJson, enc);
                File.WriteAllText(Path.Combine(OUT_DIR, "CardEffects.json"), effectsJson, enc);

                EditorUtility.DisplayProgressBar("Card DB", "Asset 새로고침...", 0.9f);
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();

                string msg =
                    $"변환 완료\n• 카드 {cardCount}장\n• 효과 {effectCount}건\n" +
                    $"출력: {OUT_DIR}/Cards.json + CardEffects.json";
                Debug.Log($"[CardDB] {msg.Replace("\n", " ")}");
                EditorUtility.DisplayDialog("Card DB", msg, "확인");
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[CardDB] 변환 실패: {e}");
                EditorUtility.DisplayDialog("Card DB 변환 실패", e.Message, "확인");
            }
        }

        // ─── Cards 시트 → JSON ──────────────────────────────────

        static string BuildCardsJson(SheetData sheet, out int count)
        {
            count = 0;
            var hdr = sheet.HeaderIndex;
            int idCol         = RequireCol(hdr, "CardID");
            int nameCol       = RequireCol(hdr, "Name");
            int elementCol    = RequireCol(hdr, "Element");
            int typeCol       = RequireCol(hdr, "CardType");
            int gaugeCol      = RequireCol(hdr, "GaugeCost");
            int rarityCol     = TryCol(hdr, "Rarity");
            int tagsCol       = TryCol(hdr, "Tags");
            int comboCol      = TryCol(hdr, "ComboSlot");
            int effectNameCol = TryCol(hdr, "EffectName");
            int skillImgCol   = TryCol(hdr, "SkillImg");
            int descCol       = TryCol(hdr, "DescriptionKR");

            var sb = new StringBuilder();
            sb.Append("{\n  \"cards\": [");
            bool first = true;

            foreach (var row in sheet.DataRows)
            {
                if (!TryGetInt(row, idCol, out int id)) continue;
                if (!first) sb.Append(",");
                first = false;
                sb.Append("\n    {\n");

                string name       = GetCell(row, nameCol).Trim();
                string element    = GetCell(row, elementCol).Trim().ToUpperInvariant();
                string type       = GetCell(row, typeCol).Trim().ToUpperInvariant();
                int    gauge      = ParseInt(GetCell(row, gaugeCol), 0);
                string rarity     = GetCell(row, rarityCol).Trim();
                string[] tags     = ParseTags(GetCell(row, tagsCol));
                bool   comboSlot  = ParseBool(GetCell(row, comboCol), defaultIfEmpty: true);
                string effectName = GetCell(row, effectNameCol).Trim();
                string skillImg   = GetCell(row, skillImgCol).Trim();
                string desc       = GetCell(row, descCol).Trim();

                sb.AppendFormat("      \"id\": {0},\n", id);
                sb.AppendFormat("      \"name\": {0},\n", JsonStr(name));
                sb.AppendFormat("      \"element\": {0},\n", JsonStr(element));
                sb.AppendFormat("      \"type\": {0},\n", JsonStr(type));
                sb.AppendFormat("      \"gauge\": {0},\n", gauge);
                sb.AppendFormat("      \"rarity\": {0},\n", JsonStr(rarity));
                sb.AppendFormat("      \"tags\": [{0}],\n", string.Join(", ", tags.Select(JsonStr)));
                sb.AppendFormat("      \"comboSlot\": {0},\n", comboSlot ? "true" : "false");
                sb.AppendFormat("      \"effectName\": {0},\n", JsonStr(effectName));
                sb.AppendFormat("      \"skillImg\": {0},\n", JsonStr(skillImg));
                sb.AppendFormat("      \"description\": {0}\n", JsonStr(desc));
                sb.Append("    }");
                count++;
            }
            sb.Append("\n  ]\n}\n");
            return sb.ToString();
        }

        // ─── CardEffects 시트 → JSON ──────────────────────────

        struct EffectField
        {
            public string Header;
            public string JsonKey;
            public bool IsInt;
            public EffectField(string h, string j, bool i) { Header = h; JsonKey = j; IsInt = i; }
        }

        static readonly EffectField[] EFFECT_FIELDS = new EffectField[]
        {
            new EffectField("EffectIndex", "index",      true),
            new EffectField("When",        "when",       false),
            new EffectField("If",          "ifCond",     false),
            new EffectField("Do",          "doAction",   false),
            new EffectField("Target",      "target",     false),
            new EffectField("Amount",      "amount",     true),
            new EffectField("Formula",     "formula",    false),
            new EffectField("Hits",        "hits",       true),
            new EffectField("HitFormula",  "hitFormula", false),
            new EffectField("Status",      "status",     false),
            new EffectField("CardFilter",  "cardFilter", false),
            new EffectField("FromZone",    "fromZone",   false),
            new EffectField("ToZone",      "toZone",     false),
            new EffectField("Select",      "select",     false),
            new EffectField("Repeat",      "repeat",     false),
            new EffectField("Extra",       "extra",      false),
            new EffectField("RuntimeKey",  "runtimeKey", false),
        };

        static string BuildEffectsJson(SheetData sheet, out int count)
        {
            count = 0;
            var hdr = sheet.HeaderIndex;
            int idCol = RequireCol(hdr, "CardID");
            int[] cols = new int[EFFECT_FIELDS.Length];
            for (int i = 0; i < EFFECT_FIELDS.Length; i++)
                cols[i] = TryCol(hdr, EFFECT_FIELDS[i].Header);

            var sb = new StringBuilder();
            sb.Append("{\n  \"effects\": [");
            bool first = true;

            foreach (var row in sheet.DataRows)
            {
                if (!TryGetInt(row, idCol, out int cardId)) continue;
                if (!first) sb.Append(",");
                first = false;
                sb.Append("\n    {\n");
                sb.AppendFormat("      \"cardId\": {0}", cardId);

                for (int i = 0; i < EFFECT_FIELDS.Length; i++)
                {
                    var f = EFFECT_FIELDS[i];
                    sb.Append(",\n");
                    if (f.IsInt)
                        sb.AppendFormat("      \"{0}\": {1}", f.JsonKey, ParseInt(GetCell(row, cols[i]), 0));
                    else
                        sb.AppendFormat("      \"{0}\": {1}", f.JsonKey, JsonStr(GetCell(row, cols[i]).Trim()));
                }
                sb.Append("\n    }");
                count++;
            }
            sb.Append("\n  ]\n}\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────
        // 헬퍼
        // ─────────────────────────────────────────────────────────────

        static int RequireCol(Dictionary<string, int> hdr, string key)
        {
            if (!hdr.TryGetValue(key, out int idx))
                throw new Exception($"필수 컬럼 '{key}' 없음");
            return idx;
        }

        static int TryCol(Dictionary<string, int> hdr, string key) =>
            hdr.TryGetValue(key, out int idx) ? idx : -1;

        static string GetCell(List<string> row, int col) =>
            (col < 0 || col >= row.Count) ? "" : (row[col] ?? "");

        static bool TryGetInt(List<string> row, int col, out int v)
        {
            v = 0;
            var s = GetCell(row, col).Trim();
            return !string.IsNullOrEmpty(s) && int.TryParse(s, out v);
        }

        static int ParseInt(string s, int def)
        {
            s = (s ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return def;
            if (int.TryParse(s, out int v)) return v;
            if (double.TryParse(s, out double d)) return (int)d;
            return def;
        }

        static bool ParseBool(string s, bool defaultIfEmpty)
        {
            s = (s ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(s)) return defaultIfEmpty;
            return s == "TRUE" || s == "1" || s == "Y" || s == "YES";
        }

        static string[] ParseTags(string s)
        {
            s = (s ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return Array.Empty<string>();
            return s.Split(';')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToArray();
        }

        static string JsonStr(string raw)
        {
            if (raw == null) return "\"\"";
            var sb = new StringBuilder(raw.Length + 2);
            sb.Append('"');
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b");  break;
                    case '\f': sb.Append("\\f");  break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────
        // 최소 xlsx 파서 (zip + XML, 외부 의존성 없음)
        // ─────────────────────────────────────────────────────────────

        class SheetData
        {
            public List<List<string>> Rows = new List<List<string>>();
            public Dictionary<string, int> HeaderIndex =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            public IEnumerable<List<string>> DataRows
            {
                get { for (int i = 1; i < Rows.Count; i++) yield return Rows[i]; }
            }

            public void BuildHeaderIndex()
            {
                HeaderIndex.Clear();
                if (Rows.Count == 0) return;
                var hdr = Rows[0];
                for (int i = 0; i < hdr.Count; i++)
                {
                    var key = (hdr[i] ?? "").Trim();
                    if (!string.IsNullOrEmpty(key) && !HeaderIndex.ContainsKey(key))
                        HeaderIndex[key] = i;
                }
            }
        }

        class WorkbookData
        {
            public Dictionary<string, SheetData> Sheets =
                new Dictionary<string, SheetData>(StringComparer.Ordinal);
        }

        static class ExcelReader
        {
            static readonly XNamespace NS_MAIN = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            static readonly XNamespace NS_RDOC = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            static readonly XNamespace NS_PKG  = "http://schemas.openxmlformats.org/package/2006/relationships";

            public static WorkbookData Read(string path)
            {
                using (var fs = File.OpenRead(path))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    var sharedStrings = ReadSharedStrings(zip);

                    var wbEntry = zip.GetEntry("xl/workbook.xml");
                    if (wbEntry == null) throw new Exception("xl/workbook.xml 없음 — xlsx 파일이 아닙니다.");
                    XDocument wbXml;
                    using (var s = wbEntry.Open()) wbXml = XDocument.Load(s);

                    var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
                    if (relsEntry == null) throw new Exception("xl/_rels/workbook.xml.rels 없음");
                    XDocument relsXml;
                    using (var s = relsEntry.Open()) relsXml = XDocument.Load(s);

                    var relsMap = new Dictionary<string, string>();
                    foreach (var r in relsXml.Root.Elements(NS_PKG + "Relationship"))
                    {
                        var idAttr = r.Attribute("Id");
                        var tgtAttr = r.Attribute("Target");
                        if (idAttr != null && tgtAttr != null)
                            relsMap[idAttr.Value] = tgtAttr.Value;
                    }

                    var workbook = new WorkbookData();
                    var sheetsRoot = wbXml.Root.Element(NS_MAIN + "sheets");
                    if (sheetsRoot == null) return workbook;

                    foreach (var sheetEl in sheetsRoot.Elements(NS_MAIN + "sheet"))
                    {
                        string name = sheetEl.Attribute("name")?.Value ?? "";
                        string rId = sheetEl.Attribute(NS_RDOC + "id")?.Value ?? "";
                        if (string.IsNullOrEmpty(name) || !relsMap.TryGetValue(rId, out string target))
                            continue;

                        if (target.StartsWith("/")) target = target.TrimStart('/');
                        else target = "xl/" + target;

                        var sheetEntry = zip.GetEntry(target);
                        if (sheetEntry == null) continue;

                        var sheetData = ReadSheet(sheetEntry, sharedStrings);
                        sheetData.BuildHeaderIndex();
                        workbook.Sheets[name] = sheetData;
                    }
                    return workbook;
                }
            }

            static List<string> ReadSharedStrings(ZipArchive zip)
            {
                var list = new List<string>();
                var entry = zip.GetEntry("xl/sharedStrings.xml");
                if (entry == null) return list;

                XDocument xml;
                using (var s = entry.Open()) xml = XDocument.Load(s);

                foreach (var si in xml.Root.Elements(NS_MAIN + "si"))
                {
                    var sb = new StringBuilder();
                    foreach (var t in si.Descendants(NS_MAIN + "t"))
                        sb.Append(t.Value);
                    list.Add(sb.ToString());
                }
                return list;
            }

            static SheetData ReadSheet(ZipArchiveEntry entry, List<string> sharedStrings)
            {
                var data = new SheetData();
                XDocument xml;
                using (var s = entry.Open()) xml = XDocument.Load(s);

                var sheetData = xml.Root.Element(NS_MAIN + "sheetData");
                if (sheetData == null) return data;

                foreach (var rowEl in sheetData.Elements(NS_MAIN + "row"))
                {
                    var rowCells = new List<string>();
                    foreach (var cellEl in rowEl.Elements(NS_MAIN + "c"))
                    {
                        string cellRef = cellEl.Attribute("r")?.Value ?? "";
                        int colIdx = ColRefToIndex(cellRef);
                        if (colIdx < 0) continue;
                        while (rowCells.Count <= colIdx) rowCells.Add("");

                        string type = cellEl.Attribute("t")?.Value ?? "";
                        string value = "";

                        if (type == "s")
                        {
                            var v = cellEl.Element(NS_MAIN + "v");
                            if (v != null && int.TryParse(v.Value, out int idx)
                                && idx >= 0 && idx < sharedStrings.Count)
                            {
                                value = sharedStrings[idx];
                            }
                        }
                        else if (type == "inlineStr")
                        {
                            var inl = cellEl.Element(NS_MAIN + "is");
                            if (inl != null)
                            {
                                var sb = new StringBuilder();
                                foreach (var t in inl.Descendants(NS_MAIN + "t"))
                                    sb.Append(t.Value);
                                value = sb.ToString();
                            }
                        }
                        else if (type == "b")
                        {
                            var v = cellEl.Element(NS_MAIN + "v");
                            value = (v != null && v.Value == "1") ? "TRUE" : "FALSE";
                        }
                        else
                        {
                            // "str" (formula 결과 문자열), "n" (숫자), 빈 type 모두 v 텍스트 그대로
                            value = cellEl.Element(NS_MAIN + "v")?.Value ?? "";
                        }

                        rowCells[colIdx] = value;
                    }
                    data.Rows.Add(rowCells);
                }
                return data;
            }

            static int ColRefToIndex(string cellRef)
            {
                int idx = 0;
                bool any = false;
                for (int i = 0; i < cellRef.Length; i++)
                {
                    char c = cellRef[i];
                    if (c >= 'A' && c <= 'Z') { idx = idx * 26 + (c - 'A' + 1); any = true; }
                    else if (c >= 'a' && c <= 'z') { idx = idx * 26 + (c - 'a' + 1); any = true; }
                    else break;
                }
                return any ? idx - 1 : -1;
            }
        }
    }
}
