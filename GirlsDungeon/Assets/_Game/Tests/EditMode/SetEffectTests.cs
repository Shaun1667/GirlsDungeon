using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// 파츠 세트 효과 (3-4b → 3-16e 새 목록 13종, 결정 2026-11-16 사용자: 같은 세트 3개를 끼우면 켜짐 · 받은 퍼센트 그대로, BALANCE 18번).
    /// 켜짐 판정 · 효과마다 피해 % · 미리보기 = 실제 · 중첩 · 아직 조건이 없는 세트는 후보에서 빠짐 · 저장을 검사한다.
    /// </summary>
    public class SetEffectTests
    {
        /// <summary>열린 방: 플레이어 (2,2), 사격병 (6,2). 엄폐물은 없다 (필요한 테스트가 놓는다).</summary>
        private static (World world, Unit enemy, CombatSettings combat) Room(WeaponKind weapon = WeaponKind.AssaultRifle)
        {
            var world = new World(1, new RandomStreams(1))
            {
                Map = TestData.Parse(
                    "##############",
                    "#............#",
                    "#............#",
                    "#............#",
                    "##############"),
            };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(2, 2)).Id;
            world.PlayerWeapon = weapon;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(6, 2));
            Loadout.Apply(world, combat);
            return (world, enemy, combat);
        }

        private static void EquipSet(World world, PartSet set, int count = 3)
        {
            for (int slot = SetEffects.FirstSetSlot; slot < WeaponPart.SlotCount; slot++)
                world.PlayerParts[slot] = slot - SetEffects.FirstSetSlot < count
                    ? new WeaponPart(world.PlayerWeapon, slot, PartGrade.Military, new List<PartBonus>(), set)
                    : null;
        }

        private static int ShotPercent(World world, CombatSettings combat, Unit enemy)
            => CombatRules.Preview(world, combat, world.Player, world.Player.Position, enemy).SetPercent;

        [Test]
        public void Generate_SetOnlyOnMilitaryAndPrecisionSlots2To4_AllPoolSetsAppear()
        {
            var combat = TestData.Combat();
            var random = new RandomStreams(5).Get(RandomPurpose.Item);
            var seen = new HashSet<PartSet>();
            for (int i = 0; i < 2000; i++)
            {
                var part = Parts.Generate(combat, WeaponKind.Pistol, random);
                bool canHave = part.Slot >= 1 && part.Grade != PartGrade.Industrial;
                if (canHave) Assert.That(part.Set, Is.Not.EqualTo(PartSet.None), $"slot {part.Slot} {part.Grade}");
                else Assert.That(part.Set, Is.EqualTo(PartSet.None), $"slot {part.Slot} {part.Grade}");
                if (part.Set != PartSet.None) Assert.That(SetEffects.HasSource(part.Set), Is.True, part.Set.ToString());
                seen.Add(part.Set);
            }
            Assert.That(seen.Count, Is.EqualTo(SetEffects.Pool().Count + 1), "every pool set and None");
        }

        [Test]
        public void Pool_LeavesOutSetsWhoseConditionsDoNotExistYet()
        {
            // 결정 2026-11-16 사용자: 지금 피해는 모두 물리 — 위상 · 약점 · 소환물 · 다른 속성 세트는 그 시스템이 생기면 켜진다.
            Assert.That(SetEffects.Pool(), Is.EqualTo(new[] { PartSet.Decisive, PartSet.DualCounter, PartSet.Kinetic, PartSet.Tactical, PartSet.Assault }));
            foreach (var set in new[] { PartSet.PhaseStrike, PartSet.PhaseAmp, PartSet.Blaze, PartSet.Frost, PartSet.Acid, PartSet.Magnetic, PartSet.Torrent, PartSet.Apex })
                Assert.That(SetEffects.HasSource(set), Is.False, set.ToString());
        }

        [Test]
        public void Active_NeedsThreeOfTheSameSet()
        {
            var (world, _, combat) = Room();
            Assert.That(SetEffects.Active(world, combat), Is.EqualTo(PartSet.None));
            EquipSet(world, PartSet.Assault, 2);
            Assert.That(SetEffects.Active(world, combat), Is.EqualTo(PartSet.None), "two pieces");
            EquipSet(world, PartSet.Assault, 3);
            Assert.That(SetEffects.Active(world, combat), Is.EqualTo(PartSet.Assault));
            world.PlayerParts[3] = new WeaponPart(world.PlayerWeapon, 3, PartGrade.Military, new List<PartBonus>(), PartSet.Tactical);
            Assert.That(SetEffects.Active(world, combat), Is.EqualTo(PartSet.None), "mixed sets");
        }

        [Test]
        public void Assault_Adds12_AndMeleeAdds24More_PreviewEqualsResult()
        {
            var (world, enemy, combat) = Room();
            int plain = CombatRules.Preview(world, combat, world.Player, world.Player.Position, enemy).Damage;
            EquipSet(world, PartSet.Assault);
            var preview = CombatRules.Preview(world, combat, world.Player, world.Player.Position, enemy);
            Assert.That(preview.SetPercent, Is.EqualTo(12));
            Assert.That(preview.Damage, Is.EqualTo(CombatRules.Damage(world.Player.Attack, enemy.Defense, 100, 0, combat.MinDamage, 100, 12)));
            Assert.That(preview.Damage, Is.GreaterThanOrEqualTo(plain));

            int hp = enemy.Hp;
            Assert.That(CombatRules.Resolve(world, combat, world.Player, enemy, new List<GameEvent>()), Is.True);
            Assert.That(hp - enemy.Hp, Is.EqualTo(preview.Damage).Or.EqualTo(preview.CritDamage), "result uses the same numbers");

            var (bladeWorld, bladeEnemy, _) = Room(WeaponKind.Blade);
            EquipSet(bladeWorld, PartSet.Assault);
            Assert.That(SetEffects.DamagePercent(bladeWorld, combat, bladeWorld.Player, bladeEnemy, DamageSource.Shot), Is.EqualTo(36));
            Assert.That(SetEffects.DamagePercent(bladeWorld, combat, bladeWorld.Player, bladeEnemy, DamageSource.Grenade), Is.EqualTo(12), "only weapon attacks are melee");
        }

        [Test]
        public void DualCounter_SingleWithoutCover_AreaWithCover()
        {
            var (world, enemy, combat) = Room();
            EquipSet(world, PartSet.DualCounter);
            Assert.That(ShotPercent(world, combat, enemy), Is.EqualTo(10), "open target: single +10");
            Assert.That(SetEffects.DamagePercent(world, combat, world.Player, enemy, DamageSource.Grenade), Is.EqualTo(0));

            world.Map.SetTile(new GridPos(6, 3), TileType.Crate);
            Assert.That(SetEffects.NearCover(world.Map, enemy.Position), Is.True);
            Assert.That(ShotPercent(world, combat, enemy), Is.EqualTo(0));
            Assert.That(SetEffects.DamagePercent(world, combat, world.Player, enemy, DamageSource.Grenade), Is.EqualTo(10), "cover near: area +10");
            var hit = AreaAttack.Preview(world, combat, world.Player, enemy.Position).Hits.First(h => h.Target == enemy);
            Assert.That(hit.SetPercent, Is.EqualTo(10));
        }

        [Test]
        public void Kinetic_AllCurrentDamageIsPhysical()
        {
            var (world, enemy, combat) = Room();
            EquipSet(world, PartSet.Kinetic);
            Assert.That(ShotPercent(world, combat, enemy), Is.EqualTo(20));
            foreach (var source in new[] { DamageSource.Overwatch, DamageSource.Grenade, DamageSource.Push, DamageSource.Sonic })
                Assert.That(SetEffects.DamagePercent(world, combat, world.Player, enemy, source), Is.EqualTo(20), source.ToString());
        }

        [Test]
        public void OtherElementSets_PhaseSummon_DoNothingYet()
        {
            var (world, enemy, combat) = Room();
            foreach (var set in new[] { PartSet.Blaze, PartSet.Frost, PartSet.Acid, PartSet.Magnetic, PartSet.Torrent, PartSet.PhaseStrike, PartSet.PhaseAmp, PartSet.Apex })
            {
                EquipSet(world, set);
                Assert.That(SetEffects.Active(world, combat), Is.EqualTo(set));
                Assert.That(ShotPercent(world, combat, enemy), Is.EqualTo(0), set.ToString());
                Assert.That(SetEffects.DamagePercent(world, combat, world.Player, enemy, DamageSource.Sonic), Is.EqualTo(0), set.ToString());
            }
        }

        [Test]
        public void Tactical_OnlyOverwatchShotsOnTheEnemyTurn()
        {
            var (world, enemy, combat) = Room();
            EquipSet(world, PartSet.Tactical);
            world.Mode = WorldMode.Combat;
            world.Turn = TurnSide.Player;
            Assert.That(ShotPercent(world, combat, enemy), Is.EqualTo(0));
            world.Turn = TurnSide.Enemies;
            Assert.That(ShotPercent(world, combat, enemy), Is.EqualTo(25));
        }

        [Test]
        public void Decisive_StacksOnlyFromTheThirdSkill_UpToFour_ClearedAfterCombat()
        {
            var (world, enemy, combat) = Room();
            world.PlayerCharacter = CharacterKind.HeroA;
            EquipSet(world, PartSet.Decisive);
            var slots = combat.SkillSlots(world.PlayerCharacter);
            var decisive = slots[SetEffects.DecisiveSkillIndex];
            // 결정 2026-11-16 사용자: 결전기 = 세 번째 스킬 — 대원 A는 수류탄.
            Assert.That(decisive, Is.EqualTo(SkillKind.Grenade));
            var other = slots[0];
            Assert.That(other, Is.Not.EqualTo(decisive));
            DamageSource SourceOf(SkillKind skill) => skill == SkillKind.Grenade ? DamageSource.Grenade : skill == SkillKind.Push ? DamageSource.Push : DamageSource.Sonic;

            SetEffects.OnSkillUsed(world, combat, other);
            Assert.That(world.SetDecisiveStacks, Is.EqualTo(0), "other skills give no stack");
            Assert.That(SetEffects.DamagePercent(world, combat, world.Player, enemy, SourceOf(decisive)), Is.EqualTo(0), "no stacks yet");
            for (int i = 0; i < 6; i++) SetEffects.OnSkillUsed(world, combat, decisive);
            Assert.That(world.SetDecisiveStacks, Is.EqualTo(4));
            Assert.That(SetEffects.DamagePercent(world, combat, world.Player, enemy, SourceOf(decisive)), Is.EqualTo(20));
            Assert.That(SetEffects.DamagePercent(world, combat, world.Player, enemy, SourceOf(other)), Is.EqualTo(0), "only the ultimate");
            Assert.That(SetEffects.DamagePercent(world, combat, world.Player, enemy, DamageSource.Shot), Is.EqualTo(0), "shots are not skills");
            SetEffects.OnCombatEnded(world);
            Assert.That(world.SetDecisiveStacks, Is.EqualTo(0));

            var (plain, _, _) = Room();
            plain.PlayerCharacter = CharacterKind.HeroA;
            SetEffects.OnSkillUsed(plain, combat, decisive);
            Assert.That(plain.SetDecisiveStacks, Is.EqualTo(0), "no stacks without the set");

            // 세 번째 스킬 칸이 비어 있는 캐릭터(B)는 결전기가 없다.
            world.PlayerCharacter = CharacterKind.HeroB;
            Assert.That(combat.SkillSlots(CharacterKind.HeroB)[SetEffects.DecisiveSkillIndex], Is.EqualTo(SkillKind.None));
            Assert.That(SetEffects.IsDecisive(world, combat, SkillKind.Grenade), Is.False);
        }

        [Test]
        public void Enemies_GetNoAttackBonusFromThePlayersSet()
        {
            var (world, enemy, combat) = Room();
            EquipSet(world, PartSet.Assault);
            Assert.That(SetEffects.DamagePercent(world, combat, enemy, world.Player, DamageSource.Shot), Is.EqualTo(0));
        }

        [Test]
        public void Snapshot_KeepsSetsAndStacks()
        {
            var (world, _, combat) = Room();
            EquipSet(world, PartSet.Decisive);
            world.SetDecisiveStacks = 3;
            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
            Assert.That(restored.PlayerParts[1].Set, Is.EqualTo(PartSet.Decisive));
            Assert.That(restored.SetDecisiveStacks, Is.EqualTo(3));
            Assert.That(SetEffects.Active(restored, combat), Is.EqualTo(PartSet.Decisive));
        }
    }
}
