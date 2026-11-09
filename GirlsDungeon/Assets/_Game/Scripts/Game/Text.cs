using System.Globalization;
using GirlsDungeon.Core.Localization;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>화면 글 조회의 한 곳. 게임 코드는 Text.Get(키)만 부른다.</summary>
    public static class Text
    {
        public const string ResourcePath = "Text/text_table";

        private static TextTable table;
        private static string language;

        public static TextTable Table
        {
            get
            {
                if (table == null) table = LoadTable();
                return table;
            }
        }

        /// <summary>현재 언어. 정하지 않으면 표의 기본 언어.</summary>
        public static string Language
        {
            get => language ?? Table.DefaultLanguage;
            set => language = value;
        }

        public static string Get(string key)
        {
            if (Table.TryGet(key, Language, out var text)) return text;
            Debug.LogError($"Missing text key: {key}");
            return $"[{key}]";
        }

        /// <summary>자리표시자 {0}, {1}…을 채운다. 순서는 표의 문장이 정한다.</summary>
        public static string Get(string key, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), args);
        }

        private static TextTable LoadTable()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogError($"Text table not found: Resources/{ResourcePath}");
                return TextTable.Parse("key,ko\n");
            }
            return TextTable.Parse(asset.text);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            table = null;
            language = null;
        }
    }
}
