using GirlsDungeon.Game;
using NUnit.Framework;
using UnityEditor;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// 3-11 그림 목록의 Animator 값 이름: 속도 "Speed" · 엄폐 "Cover"(0 없음 · 1 낮음 · 2 높음) · 사격 "Fire".
    /// 모델의 Animator Controller가 이 이름을 쓰면 PlayerView가 값을 넣는다 (Docs/ART_ASSETS.md).
    /// </summary>
    public class UnitVisualTests
    {
        [Test]
        public void NewEntry_UsesTheDocumentedParameterNames()
        {
            var entry = new UnitVisualEntry();
            Assert.That(entry.speedParameter, Is.EqualTo("Speed"));
            Assert.That(entry.coverParameter, Is.EqualTo("Cover"));
            Assert.That(entry.fireTrigger, Is.EqualTo("Fire"));
        }

        [Test]
        public void CatalogAsset_CharacterRowsUseTheDocumentedParameterNames()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>("Assets/_Game/Data/UnitVisuals.asset");
            Assert.That(catalog, Is.Not.Null);
            foreach (var id in new[] { "player", "hero_a", "hero_b", "hero_c" })
            {
                var entry = catalog.Find(id);
                Assert.That(entry, Is.Not.Null, id);
                Assert.That(entry.speedParameter, Is.EqualTo("Speed"), id);
                Assert.That(entry.coverParameter, Is.EqualTo("Cover"), id);
                Assert.That(entry.fireTrigger, Is.EqualTo("Fire"), id);
            }
        }
    }
}
