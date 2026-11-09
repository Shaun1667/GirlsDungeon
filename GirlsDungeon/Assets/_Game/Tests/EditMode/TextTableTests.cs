using System;
using System.Linq;
using GirlsDungeon.Core.Localization;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    public class TextTableTests
    {
        [Test]
        public void Parse_ReadsLanguagesAndRows()
        {
            var table = TextTable.Parse("key,ko,en\nui.a,가,A\n");

            CollectionAssert.AreEqual(new[] { "ko", "en" }, table.Languages.ToArray());
            Assert.That(table.DefaultLanguage, Is.EqualTo("ko"));
            Assert.That(table.TryGet("ui.a", "en", out var text), Is.True);
            Assert.That(text, Is.EqualTo("A"));
        }

        [Test]
        public void Parse_HandlesBomCrLfQuotedCommaAndEscapedQuote()
        {
            var table = TextTable.Parse("﻿key,ko\r\nui.a,\"하나, 둘\"\r\nui.b,\"말 \"\"인용\"\"\"\r\n");

            table.TryGet("ui.a", "ko", out var comma);
            table.TryGet("ui.b", "ko", out var quote);
            Assert.That(comma, Is.EqualTo("하나, 둘"));
            Assert.That(quote, Is.EqualTo("말 \"인용\""));
        }

        [Test]
        public void TryGet_FallsBackToDefaultLanguage_WhenTranslationIsEmpty()
        {
            var table = TextTable.Parse("key,ko,en\nui.a,가,\n");

            Assert.That(table.TryGet("ui.a", "en", out var text), Is.True);
            Assert.That(text, Is.EqualTo("가"));
            Assert.That(table.HasText("ui.a", "en"), Is.False);
        }

        [Test]
        public void TryGet_ReturnsFalse_WhenKeyIsMissing()
        {
            var table = TextTable.Parse("key,ko\nui.a,가\n");
            Assert.That(table.TryGet("ui.none", "ko", out _), Is.False);
        }

        [Test]
        public void Parse_Throws_OnBadTables()
        {
            Assert.Throws<FormatException>(() => TextTable.Parse("name,ko\n"));
            Assert.Throws<FormatException>(() => TextTable.Parse("key,ko\nui.a,가\nui.a,나\n"));
            Assert.Throws<FormatException>(() => TextTable.Parse("key,ko\nui.a,가,나\n"));
            Assert.Throws<FormatException>(() => TextTable.Parse("key,ko\nui.a,\"닫히지 않음\n"));
        }
    }
}
