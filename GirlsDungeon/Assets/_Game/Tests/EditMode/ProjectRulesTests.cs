using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GirlsDungeon.Core.Localization;
using NUnit.Framework;
using UnityEngine;

namespace GirlsDungeon.Tests
{
    /// <summary>핵심 규칙을 코드로 지킨다: 규칙 코드의 엔진 분리, 텍스트 표 한곳 관리.</summary>
    public class ProjectRulesTests
    {
        private const string GameRoot = "Assets/_Game";
        private const string TablePath = GameRoot + "/Resources/Text/text_table.csv";

        [Test]
        public void CoreAssembly_DoesNotReferenceTheEngine()
        {
            var references = typeof(TextTable).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            Assert.That(references.Where(n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor")), Is.Empty);
        }

        [Test]
        public void EveryKeyUsedByCodeOrScenes_ExistsInTable()
        {
            var table = LoadTable();
            var usedKeys = new HashSet<string>();

            foreach (var path in GameCodeFiles())
                foreach (Match match in Regex.Matches(File.ReadAllText(path), "Text\\.Get\\(\\s*\"([^\"]+)\""))
                    usedKeys.Add(match.Groups[1].Value);

            foreach (var path in Directory.GetFiles(GameRoot, "*.unity", SearchOption.AllDirectories))
                foreach (Match match in Regex.Matches(File.ReadAllText(path), @"textKey: (\S+)"))
                    usedKeys.Add(match.Groups[1].Value);

            Assert.That(usedKeys.Where(k => !table.Contains(k)), Is.Empty);
        }

        [Test]
        public void EveryKey_HasDefaultLanguageText()
        {
            var table = LoadTable();

            Assert.That(table.Keys.Where(k => !table.HasText(k, table.DefaultLanguage)), Is.Empty);
        }

        [Test]
        public void GameCode_HasNoHangulStringLiterals()
        {
            var hangulLiteral = new Regex("\"[^\"\\n]*[\\uAC00-\\uD7A3][^\"\\n]*\"");
            var offenders = new List<string>();

            foreach (var path in GameCodeFiles())
            {
                var code = Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/|//[^\n]*", string.Empty, RegexOptions.Singleline);
                if (hangulLiteral.IsMatch(code)) offenders.Add(path);
            }

            Assert.That(offenders, Is.Empty);
        }

        private static TextTable LoadTable() => TextTable.Parse(File.ReadAllText(TablePath));

        /// <summary>규칙 · 게임 코드만 검사한다. 에디터 도구와 디버그 글은 표에 넣지 않아도 된다.</summary>
        private static IEnumerable<string> GameCodeFiles()
        {
            return new[] { GameRoot + "/Scripts/Core", GameRoot + "/Scripts/Game" }
                .SelectMany(folder => Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories));
        }
    }
}
