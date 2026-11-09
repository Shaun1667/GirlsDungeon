using System;
using System.Collections.Generic;
using System.Text;

namespace GirlsDungeon.Core.Localization
{
    /// <summary>
    /// 키 → 언어별 문장 표. 첫 줄은 "key,언어1,언어2…"이고 첫 언어가 기본 언어다.
    /// 번역이 비어 있으면 기본 언어 문장으로 대신한다.
    /// </summary>
    public sealed class TextTable
    {
        private readonly List<string> languages = new List<string>();
        private readonly Dictionary<string, string[]> rows = new Dictionary<string, string[]>(StringComparer.Ordinal);

        public IReadOnlyList<string> Languages => languages;
        public IEnumerable<string> Keys => rows.Keys;
        public string DefaultLanguage => languages[0];

        private TextTable() { }

        public static TextTable Parse(string csv)
        {
            if (csv == null) throw new ArgumentNullException(nameof(csv));

            var records = ReadRecords(csv);
            if (records.Count == 0) throw new FormatException("Text table has no header.");

            var header = records[0];
            if (header.Count < 2 || header[0] != "key")
                throw new FormatException("Text table header must be: key,<language>,...");

            var table = new TextTable();
            for (int i = 1; i < header.Count; i++) table.languages.Add(header[i]);

            for (int r = 1; r < records.Count; r++)
            {
                var record = records[r];
                var key = record[0];
                if (key.Length == 0) continue;
                if (record.Count > header.Count)
                    throw new FormatException($"Too many columns for key '{key}'. Use quotes around commas.");
                if (table.rows.ContainsKey(key))
                    throw new FormatException($"Duplicate text key '{key}'.");

                var values = new string[table.languages.Count];
                for (int c = 0; c < values.Length; c++)
                    values[c] = c + 1 < record.Count ? record[c + 1] : string.Empty;
                table.rows.Add(key, values);
            }

            return table;
        }

        public bool Contains(string key) => key != null && rows.ContainsKey(key);

        /// <summary>해당 언어 문장만 조회한다. 비어 있거나 없으면 false.</summary>
        public bool HasText(string key, string language)
        {
            if (!Contains(key)) return false;
            int column = languages.IndexOf(language);
            return column >= 0 && rows[key][column].Length > 0;
        }

        /// <summary>문장을 찾는다. 번역이 없으면 기본 언어로 대신한다. 키 자체가 없으면 false.</summary>
        public bool TryGet(string key, string language, out string text)
        {
            text = null;
            if (!Contains(key)) return false;

            var values = rows[key];
            int column = languages.IndexOf(language);
            text = column >= 0 && values[column].Length > 0 ? values[column] : values[0];
            return true;
        }

        private static List<List<string>> ReadRecords(string csv)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            int start = csv.Length > 0 && csv[0] == '﻿' ? 1 : 0;

            for (int i = start; i < csv.Length; i++)
            {
                char ch = csv[i];
                if (inQuotes)
                {
                    if (ch != '"') field.Append(ch);
                    else if (i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else if (ch == '"') inQuotes = true;
                else if (ch == ',') { record.Add(field.ToString()); field.Clear(); }
                else if (ch == '\r') { }
                else if (ch == '\n') { EndRecord(records, ref record, field); }
                else field.Append(ch);
            }

            if (inQuotes) throw new FormatException("Text table has an unclosed quote.");
            EndRecord(records, ref record, field);
            return records;
        }

        private static void EndRecord(List<List<string>> records, ref List<string> record, StringBuilder field)
        {
            record.Add(field.ToString());
            field.Clear();
            bool isEmptyLine = record.Count == 1 && record[0].Length == 0;
            if (!isEmptyLine) records.Add(record);
            record = new List<string>();
        }
    }
}
