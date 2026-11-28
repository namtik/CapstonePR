using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEngine;

namespace Battle.EditorTools
{
    /// <summary>
    /// JSON -> Excel 역변환기. 현재 게임이 읽는 Resources JSON을 xlsx 원본으로 되돌린다.
    /// 출력 xlsx는 CardDBImporter / ComboSkillDBImporter가 그대로 다시 읽을 수 있는 형식
    /// (1행 헤더, 시트명 Cards/CardEffects, ComboSkills/ComboEffects).
    /// 기존 xlsx에 데이터 시트 외의 시트(키워드 설명 등)가 있으면 값만 복사해 보존한다.
    /// </summary>
    public static class DBExcelExporter
    {
        const string CARD_JSON_DIR  = "Assets/_Project/Resources/CardDB";
        const string COMBO_JSON_DIR = "Assets/_Project/Resources/ComboDB";
        const string DEFAULT_CARD_XLSX  = "DataSource/Card_DB.xlsx";
        const string DEFAULT_COMBO_XLSX = "DataSource/ComboSkill_DB.xlsx";

        // 임포터와 같은 키: 내보낸 파일을 "마지막 파일 다시 변환" 대상으로 지정
        const string PREF_CARD_LAST  = "Battle.CardDB.LastSourcePath";
        const string PREF_COMBO_LAST = "Battle.ComboDB.LastSourcePath";

        // 메뉴

        [MenuItem("Tools/Card DB/JSON -> Excel 내보내기 (DataSource)", priority = 30)]
        static void ExportCardMenu()
        {
            string path = Path.GetFullPath(DEFAULT_CARD_XLSX);
            if (!ConfirmOverwrite("Card DB", path)) return;
            RunMenu("Card DB", () => ExportCardDB(
                File.ReadAllText(Path.Combine(CARD_JSON_DIR, "Cards.json"), Encoding.UTF8),
                File.ReadAllText(Path.Combine(CARD_JSON_DIR, "CardEffects.json"), Encoding.UTF8),
                path), path, PREF_CARD_LAST);
        }

        [MenuItem("Tools/Combo DB/JSON -> Excel 내보내기 (DataSource)", priority = 30)]
        static void ExportComboMenu()
        {
            string path = Path.GetFullPath(DEFAULT_COMBO_XLSX);
            if (!ConfirmOverwrite("Combo DB", path)) return;
            RunMenu("Combo DB", () => ExportComboDB(
                File.ReadAllText(Path.Combine(COMBO_JSON_DIR, "ComboSkills.json"), Encoding.UTF8),
                File.ReadAllText(Path.Combine(COMBO_JSON_DIR, "ComboEffects.json"), Encoding.UTF8),
                path), path, PREF_COMBO_LAST);
        }

        static bool ConfirmOverwrite(string title, string path)
        {
            if (!File.Exists(path)) return true;
            return EditorUtility.DisplayDialog(title,
                $"{path}\n\n이미 있는 파일을 현재 JSON 내용으로 덮어씁니다.\n" +
                "(데이터 시트 외의 시트는 값만 보존됩니다. Excel에서 파일을 닫아 주세요.)",
                "덮어쓰기", "취소");
        }

        static void RunMenu(string title, Func<string> export, string path, string prefKey)
        {
            try
            {
                string summary = export();
                EditorPrefs.SetString(prefKey, path);
                Debug.Log($"[{title}] JSON -> Excel 완료: {path} ({summary})");
                EditorUtility.DisplayDialog(title, $"내보내기 완료\n{summary}\n\n{path}\n\n" +
                    "이 파일이 '마지막 파일 다시 변환' 대상으로 지정되었습니다.", "확인");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{title}] JSON -> Excel 실패: {e}");
                EditorUtility.DisplayDialog($"{title} 내보내기 실패", e.Message, "확인");
            }
        }

        // JSON DTO (임포터가 생성하는 JSON과 1:1)

        [Serializable] public class CardRow
        {
            public int id; public string name; public string element; public string type;
            public int gauge; public string rarity; public string[] tags; public bool comboSlot;
            public string effectName; public string skillImg; public string description;
        }
        [Serializable] public class CardsFile { public CardRow[] cards; }

        [Serializable] public class EffectRow
        {
            public int cardId; public int refComboId;
            public int index; public string when; public string ifCond; public string doAction;
            public string target; public int amount; public string formula; public int hits;
            public string hitFormula; public string status; public string cardFilter;
            public string fromZone; public string toZone; public string select;
            public string repeat; public string extra; public string runtimeKey;
        }
        [Serializable] public class EffectsFile { public EffectRow[] effects; }

        [Serializable] public class ComboRow
        {
            public int id; public string slot1; public string slot2; public string slot3;
            public int cooldown; public string comboName; public string skillImg;
            public string description; public int refComboId;
        }
        [Serializable] public class CombosFile { public ComboRow[] combos; }

        static readonly string[] EFFECT_HEADERS =
        {
            "EffectIndex", "When", "If", "Do", "Target", "Amount", "Formula", "Hits", "HitFormula",
            "Status", "CardFilter", "FromZone", "ToZone", "Select", "Repeat", "Extra", "RuntimeKey",
        };

        static object[] EffectCells(EffectRow e) => new object[]
        {
            e.index, e.when, e.ifCond, e.doAction, e.target, e.amount, e.formula, e.hits, e.hitFormula,
            e.status, e.cardFilter, e.fromZone, e.toZone, e.select, e.repeat, e.extra, e.runtimeKey,
        };

        // 변환

        /// <returns>요약 문자열</returns>
        public static string ExportCardDB(string cardsJson, string effectsJson, string xlsxPath)
        {
            var cards = JsonUtility.FromJson<CardsFile>(cardsJson)?.cards
                        ?? throw new Exception("Cards.json 파싱 실패");
            var effects = JsonUtility.FromJson<EffectsFile>(effectsJson)?.effects
                          ?? throw new Exception("CardEffects.json 파싱 실패");

            var cardSheet = new Sheet("Cards",
                "CardID", "Name", "Element", "CardType", "GaugeCost", "Rarity", "Tags",
                "ComboSlot", "EffectName", "SkillImg", "DescriptionKR");
            foreach (var c in cards)
                cardSheet.Rows.Add(new object[]
                {
                    c.id, c.name, c.element, c.type, c.gauge, c.rarity,
                    string.Join(";", c.tags ?? Array.Empty<string>()),
                    c.comboSlot, c.effectName, c.skillImg, c.description,
                });

            var effSheet = new Sheet("CardEffects", Prepend("CardID", EFFECT_HEADERS));
            foreach (var e in effects)
                effSheet.Rows.Add(Prepend(e.cardId, EffectCells(e)));

            XlsxWriter.Write(xlsxPath, WithPreservedSheets(xlsxPath, cardSheet, effSheet));
            return $"카드 {cards.Length}장 / 효과 {effects.Length}건";
        }

        public static string ExportComboDB(string skillsJson, string effectsJson, string xlsxPath)
        {
            var combos = JsonUtility.FromJson<CombosFile>(skillsJson)?.combos
                         ?? throw new Exception("ComboSkills.json 파싱 실패");
            var effects = JsonUtility.FromJson<EffectsFile>(effectsJson)?.effects
                          ?? throw new Exception("ComboEffects.json 파싱 실패");

            var skillSheet = new Sheet("ComboSkills",
                "ComboID", "Slot1", "Slot2", "Slot3", "Cooldown", "ComboName", "Combo_Img",
                "DescriptionKR", "RefComboID");
            foreach (var c in combos)
                skillSheet.Rows.Add(new object[]
                {
                    c.id, c.slot1, c.slot2, c.slot3, c.cooldown, c.comboName, c.skillImg,
                    c.description, c.refComboId,
                });

            var effSheet = new Sheet("ComboEffects", Prepend("RefComboID", EFFECT_HEADERS));
            foreach (var e in effects)
                effSheet.Rows.Add(Prepend(e.refComboId, EffectCells(e)));

            XlsxWriter.Write(xlsxPath, WithPreservedSheets(xlsxPath, skillSheet, effSheet));
            return $"콤보 {combos.Length}개 / 효과 {effects.Length}건";
        }

        static T[] Prepend<T>(T first, T[] rest)
        {
            var arr = new T[rest.Length + 1];
            arr[0] = first;
            Array.Copy(rest, 0, arr, 1, rest.Length);
            return arr;
        }

        /// <summary>기존 xlsx의 데이터 시트 외 시트(설명·키워드표 등)를 값만 복사해 뒤에 붙인다.</summary>
        static List<Sheet> WithPreservedSheets(string existingPath, params Sheet[] dataSheets)
        {
            var result = new List<Sheet>(dataSheets);
            if (!File.Exists(existingPath)) return result;

            var names = new HashSet<string>();
            foreach (var s in dataSheets) names.Add(s.Name);

            var wb = CardDBImporter.ExcelReader.Read(existingPath);
            foreach (var kv in wb.Sheets)
            {
                if (names.Contains(kv.Key) || kv.Value.Rows.Count == 0) continue;
                var copy = new Sheet(kv.Key, kv.Value.Rows[0].ToArray()) { BoldHeader = false };
                for (int i = 1; i < kv.Value.Rows.Count; i++)
                    copy.Rows.Add(kv.Value.Rows[i].ToArray());
                result.Add(copy);
            }
            return result;
        }

        // 최소 xlsx 작성기 (zip + XML, 외부 의존성 없음)

        public class Sheet
        {
            public string Name;
            public object[] Header;
            public List<object[]> Rows = new List<object[]>();
            public bool BoldHeader = true;
            public Sheet(string name, params object[] header) { Name = name; Header = header; }
        }

        public static class XlsxWriter
        {
            const string NS = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            const string NS_R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

            public static void Write(string path, IList<Sheet> sheets)
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                // 임시 파일에 쓴 뒤 교체 -> 실패 시 기존 파일 보존
                string tmp = path + ".tmp";
                using (var fs = new FileStream(tmp, FileMode.Create))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    Entry(zip, "[Content_Types].xml", w =>
                    {
                        w.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                        Default(w, "rels", "application/vnd.openxmlformats-package.relationships+xml");
                        Default(w, "xml", "application/xml");
                        Override(w, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
                        Override(w, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
                        for (int i = 0; i < sheets.Count; i++)
                            Override(w, $"/xl/worksheets/sheet{i + 1}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                        w.WriteEndElement();
                    });
                    Entry(zip, "_rels/.rels", w =>
                    {
                        w.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                        Rel(w, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
                        w.WriteEndElement();
                    });
                    Entry(zip, "xl/workbook.xml", w =>
                    {
                        w.WriteStartElement("workbook", NS);
                        w.WriteAttributeString("xmlns", "r", null, NS_R);
                        w.WriteStartElement("sheets", NS);
                        for (int i = 0; i < sheets.Count; i++)
                        {
                            w.WriteStartElement("sheet", NS);
                            w.WriteAttributeString("name", sheets[i].Name);
                            w.WriteAttributeString("sheetId", (i + 1).ToString());
                            w.WriteAttributeString("id", NS_R, $"rId{i + 1}");
                            w.WriteEndElement();
                        }
                        w.WriteEndElement();
                        w.WriteEndElement();
                    });
                    Entry(zip, "xl/_rels/workbook.xml.rels", w =>
                    {
                        w.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                        for (int i = 0; i < sheets.Count; i++)
                            Rel(w, $"rId{i + 1}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", $"worksheets/sheet{i + 1}.xml");
                        Rel(w, $"rId{sheets.Count + 1}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml");
                        w.WriteEndElement();
                    });
                    Entry(zip, "xl/styles.xml", WriteStyles);
                    for (int i = 0; i < sheets.Count; i++)
                    {
                        var sheet = sheets[i];
                        Entry(zip, $"xl/worksheets/sheet{i + 1}.xml", w => WriteSheet(w, sheet));
                    }
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }

            static void Entry(ZipArchive zip, string name, Action<XmlWriter> body)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                var settings = new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    NewLineHandling = NewLineHandling.Entitize, // 셀 안 줄바꿈(\r\n) 보존
                };
                using (var s = entry.Open())
                using (var w = XmlWriter.Create(s, settings))
                {
                    w.WriteStartDocument(true);
                    body(w);
                    w.WriteEndDocument();
                }
            }

            static void Default(XmlWriter w, string ext, string type)
            {
                w.WriteStartElement("Default");
                w.WriteAttributeString("Extension", ext);
                w.WriteAttributeString("ContentType", type);
                w.WriteEndElement();
            }

            static void Override(XmlWriter w, string part, string type)
            {
                w.WriteStartElement("Override");
                w.WriteAttributeString("PartName", part);
                w.WriteAttributeString("ContentType", type);
                w.WriteEndElement();
            }

            static void Rel(XmlWriter w, string id, string type, string target)
            {
                w.WriteStartElement("Relationship");
                w.WriteAttributeString("Id", id);
                w.WriteAttributeString("Type", type);
                w.WriteAttributeString("Target", target);
                w.WriteEndElement();
            }

            // 스타일 0 = 기본, 1 = 굵게+회색 배경(헤더)
            static void WriteStyles(XmlWriter w)
            {
                w.WriteStartElement("styleSheet", NS);
                w.WriteStartElement("fonts", NS); w.WriteAttributeString("count", "2");
                w.WriteStartElement("font", NS); El(w, "sz", "11"); El(w, "name", "맑은 고딕"); w.WriteEndElement();
                w.WriteStartElement("font", NS); w.WriteElementString("b", NS, ""); El(w, "sz", "11"); El(w, "name", "맑은 고딕"); w.WriteEndElement();
                w.WriteEndElement();
                w.WriteStartElement("fills", NS); w.WriteAttributeString("count", "3");
                w.WriteStartElement("fill", NS); w.WriteStartElement("patternFill", NS); w.WriteAttributeString("patternType", "none"); w.WriteEndElement(); w.WriteEndElement();
                w.WriteStartElement("fill", NS); w.WriteStartElement("patternFill", NS); w.WriteAttributeString("patternType", "gray125"); w.WriteEndElement(); w.WriteEndElement();
                w.WriteStartElement("fill", NS); w.WriteStartElement("patternFill", NS); w.WriteAttributeString("patternType", "solid");
                w.WriteStartElement("fgColor", NS); w.WriteAttributeString("rgb", "FFD9D9D9"); w.WriteEndElement();
                w.WriteEndElement(); w.WriteEndElement();
                w.WriteEndElement();
                w.WriteStartElement("borders", NS); w.WriteAttributeString("count", "1"); w.WriteElementString("border", NS, ""); w.WriteEndElement();
                w.WriteStartElement("cellStyleXfs", NS); w.WriteAttributeString("count", "1"); Xf(w, 0, 0, false); w.WriteEndElement();
                w.WriteStartElement("cellXfs", NS); w.WriteAttributeString("count", "2"); Xf(w, 0, 0, false); Xf(w, 1, 2, true); w.WriteEndElement();
                w.WriteEndElement();
            }

            static void El(XmlWriter w, string name, string val)
            {
                w.WriteStartElement(name, NS); w.WriteAttributeString("val", val); w.WriteEndElement();
            }

            static void Xf(XmlWriter w, int font, int fill, bool apply)
            {
                w.WriteStartElement("xf", NS);
                w.WriteAttributeString("numFmtId", "0");
                w.WriteAttributeString("fontId", font.ToString());
                w.WriteAttributeString("fillId", fill.ToString());
                w.WriteAttributeString("borderId", "0");
                if (apply) { w.WriteAttributeString("applyFont", "1"); w.WriteAttributeString("applyFill", "1"); }
                w.WriteEndElement();
            }

            static void WriteSheet(XmlWriter w, Sheet sheet)
            {
                w.WriteStartElement("worksheet", NS);

                // 헤더 고정
                w.WriteStartElement("sheetViews", NS);
                w.WriteStartElement("sheetView", NS); w.WriteAttributeString("workbookViewId", "0");
                w.WriteStartElement("pane", NS);
                w.WriteAttributeString("ySplit", "1"); w.WriteAttributeString("topLeftCell", "A2");
                w.WriteAttributeString("activePane", "bottomLeft"); w.WriteAttributeString("state", "frozen");
                w.WriteEndElement();
                w.WriteEndElement();
                w.WriteEndElement();

                // 열 너비: 내용 길이 기준 (한글은 2칸으로 계산)
                int cols = sheet.Header.Length;
                foreach (var r in sheet.Rows) cols = Math.Max(cols, r.Length);
                w.WriteStartElement("cols", NS);
                for (int c = 0; c < cols; c++)
                {
                    int width = c < sheet.Header.Length ? DisplayLen(sheet.Header[c]) : 4;
                    foreach (var r in sheet.Rows)
                        if (c < r.Length) width = Math.Max(width, DisplayLen(r[c]));
                    w.WriteStartElement("col", NS);
                    w.WriteAttributeString("min", (c + 1).ToString());
                    w.WriteAttributeString("max", (c + 1).ToString());
                    w.WriteAttributeString("width", Math.Min(Math.Max(width + 2, 6), 80).ToString());
                    w.WriteAttributeString("customWidth", "1");
                    w.WriteEndElement();
                }
                w.WriteEndElement();

                w.WriteStartElement("sheetData", NS);
                WriteRow(w, 1, sheet.Header, sheet.BoldHeader ? 1 : 0);
                for (int i = 0; i < sheet.Rows.Count; i++)
                    WriteRow(w, i + 2, sheet.Rows[i], 0);
                w.WriteEndElement();

                w.WriteEndElement();
            }

            static int DisplayLen(object v)
            {
                string s = v as string ?? v?.ToString() ?? "";
                int n = 0;
                foreach (char ch in s) n += ch > 0x2E80 ? 2 : 1;
                return n;
            }

            static void WriteRow(XmlWriter w, int rowNum, object[] cells, int style)
            {
                w.WriteStartElement("row", NS);
                w.WriteAttributeString("r", rowNum.ToString());
                for (int c = 0; c < cells.Length; c++)
                {
                    object v = cells[c];
                    string s = v as string;
                    if (v == null || (s != null && s.Length == 0)) continue; // 빈 셀은 생략

                    w.WriteStartElement("c", NS);
                    w.WriteAttributeString("r", ColName(c) + rowNum);
                    if (style != 0) w.WriteAttributeString("s", style.ToString());

                    if (v is int || v is long)
                    {
                        w.WriteElementString("v", NS, Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else if (v is bool b)
                    {
                        w.WriteAttributeString("t", "b");
                        w.WriteElementString("v", NS, b ? "1" : "0");
                    }
                    else
                    {
                        w.WriteAttributeString("t", "inlineStr");
                        w.WriteStartElement("is", NS);
                        w.WriteStartElement("t", NS);
                        if (s != null && s.Trim().Length != s.Length)
                            w.WriteAttributeString("xml", "space", null, "preserve");
                        w.WriteString(s ?? v.ToString());
                        w.WriteEndElement();
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                }
                w.WriteEndElement();
            }

            static string ColName(int idx)
            {
                var sb = new StringBuilder();
                idx++;
                while (idx > 0)
                {
                    int m = (idx - 1) % 26;
                    sb.Insert(0, (char)('A' + m));
                    idx = (idx - 1) / 26;
                }
                return sb.ToString();
            }
        }
    }
}
