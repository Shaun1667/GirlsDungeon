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
    /// 3-4b 파츠 세트 효과 (결정 2026-11-13 사용자: 7개 모두 · 같은 세트 3개를 끼우면 켜짐 · 받은 퍼센트 그대로, BALANCE 18번).
    /// 켜짐 판정 · 효과마다 피해 % · 미리보기 = 실제 · 중첩 · 남은 차례 · 저장을 검사한다.
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
        public void Generate_SetOnlyOnMilitaryAndPrecisionSlots2To4_AllSevenAppear()
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
                seen.Add(part.Set);
            }
            Assert.That(seen.Count, Is.EqualTo(SetEffects.SetCount + 1), "all 7 sets and None");
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
            world.PlayerParts[3] = new WeaponPart(world.PlayerWeapon, 3, PartGrade.Military, new List<PartBonus>(), PartSet.Ambush);
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
        public void TwoSided_SingleWithoutCover_AreaWithCover()
        {
            var (world, enemy, combat) = Room();
            EquipSet(world, PartSet.TwoSided);
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
        public void Fracture_OnlyWhenTheEnemyLostStability()
        {
            var (world, enemy, combat) = Room();
            EquipSet(world, PartSet.Fracture);
            Assert.That(ShotPercent(world, combat, enemy), Is.EqualTo(0));
            enemy.Stability = enemy.MaxStability - 1;
            Assert.That(ShotPercent(world, combat, enemy), Is.EqualTo(15));
        }

        [Test]
        public void Ambush_OnlyOverwatchShotsOnTheEnemyTurn()
        {
            var (world, enemy, combat) = Room();
            EquipSet(world, PartSet.Ambush);
            world.Mode = WorldMode.Combat;
            world.Turn = TurnSide.Player;
            Assert.That(ShotPercent(world, combat, enemy), Is.EqualTo(0));
            world.Turn = TurnSide.Enemies;
            Assert.That(ShotPercent(world, combat, enemy), Is.EqualTo(25));
        }

        [Test]
        public void Chain_StacksPerSkillUpToFour_SkillsOnly_ClearedAfterCombat()
        {
            var (world, enemy, combat) = Room();
            EquipSet(world, PartSet.Chain);
            Assert.That(SetEffects.DamagePercent(world, combat, world.Player, enemy, DamageSource.Sonic), Is.EqualTo(0), "no stacks yet");
            for (int i = 0; i < 6; i++) SetEffects.OnSkillUsed(world, combat);
            Assert.That(world.SetChainStacks, Is.EqualTo(4));
            Assert.That(SetEffects.DamagePercent(world, combat, world.Player, enemy, DamageSource.Grenade), Is.EqualTo(20));
            Assert.That(SetEffects.DamagePercent(world, combat, world.Player, enemy, DamageSource.Shot), Is.EqualTo(0), "shots are not skills");
            SetEffects.OnCombatEnded(world);
            Assert.That(world.SetChainStacks, Is.EqualTo(0));

            var (other, _, _) = Room();
            SetEffects.OnSkillUsed(other, combat);
            Assert.That(other.SetChainStacks, Is.EqualTo(0), "no stacks without the set");
        }

        [Test]
        public void Bulwark_CutsAreaDamageTakenForTwoPlayerTurns()
        {
            var (world, enemy, combat) = Room();
            EquipSet(world, PartSet.Bulwark);
            var player = world.Player;
            Assert.That(SetEffects.DamagePercent(world, combat, player, player, DamageSource.Grenade), Is.EqualTo(0));
            SetEffects.OnOverwatchSet(world, combat);
            Assert.That(world.SetBulwarkTurns, Is.EqualTo(2));
            Assert.That(SetEffects.DamagePercent(world, combat, player, player, DamageSource.Grenade), Is.EqualTo(-20));
            Assert.That(SetEffects.DamagePercent(world, combat, enemy, player, DamageSource.Shot), Is.EqualTo(0), "shots are not area damage");
            SetEffects.OnPlayerTurnStarted(world);
            Assert.That(SetEffects.DamagePercent(world, combat, player, player, DamageSource.Push), Is.EqualTo(-20));
            SetEffects.OnPlayerTurnStarted(world);
            Assert.That(world.SetBulwarkTurns, Is.EqualTo(0));
            Assert.That(SetEffects.DamagePercent(world, combat, player, player, DamageSource.Grenade), Is.EqualTo(0));
        }

        [Test]
        public void Emergency_MedkitThatFillsHpAlsoGivesStability()
        {
            var (world, _, combat) = Room();
            var player = world.Player;
            player.Stability = 1;
            Assert.That(SetEffects.OnMedkitFilledHp(world, combat, player), Is.EqualTo(0), "no set");
            EquipSet(world, PartSet.Emergency);
            Assert.That(SetEffects.OnMedkitFilledHp(world, combat, player), Is.EqualTo(2));
            Assert.That(player.Stability, Is.EqualTo(3));
            player.Stability = player.MaxStability - 1;
            Assert.That(SetEffects.OnMedkitFilledHp(world, combat, player), Is.EqualTo(1), "up to max only");
            Assert.That(player.Stability, Is.EqualTo(player.MaxStability));
        }

        [Test]
        public void Enemies_GetNoAttackBonusFromThePlayersSet()
        {
            var (world, enemy, combat) = Room();
            EquipSet(world, PartSet.Assault);
            Assert.That(SetEffects.DamagePercent(world, combat, enemy, world.Player, DamageSource.Shot), Is.EqualTo(0));
        }

        [Test]
        public void Snapshot_KeepsSetsStacksAndTurns()
        {
            var (world, _, combat) = Room();
            EquipSet(world, PartSet.Chain);
            world.SetChainStacks = 3;
            world.SetBulwarkTurns = 1;
            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
            Assert.That(restored.PlayerParts[1].Set, Is.EqualTo(PartSet.Chain));
            Assert.That(restored.SetChainStacks, Is.EqualTo(3));
            Assert.That(restored.SetBulwarkTurns, Is.EqualTo(1));
            Assert.That(SetEffects.Active(restored, combat), Is.EqualTo(PartSet.Chain));
        }
    }
}
