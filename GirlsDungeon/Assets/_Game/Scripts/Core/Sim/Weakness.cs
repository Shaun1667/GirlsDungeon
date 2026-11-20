using System.Collections.Generic;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 적의 속성 약점 한 곳 (BALANCE 38번, 3-17h — 결정 2026-11-20 사용자):
    /// - 적이 생길 때(일반 적 · 결투장 · 함정 감시병 · 도박 매복 · 디버그) 연소 · 전도 · 탁류 · 산성 · 빙결 · 물리 중 하나를 무작위로 (Assign, 난수 RandomPurpose.Weakness).
    ///   상점 주인은 약점이 없다 (사용자 결정).
    /// - 서리밭 안의 적은 빙결 약점이 하나 더 붙은 것으로 본다 (3-17c 서리밭 설명).
    /// - 플레이어 공격의 속성(SetEffects.ElementOf)이 약점과 맞으면 약점 1개당 피해 +WeaknessDamagePercent%p (SetEffects.DamagePercent가 더함 — 모든 피해 미리보기 공통)
    ///   · 안정 피해 +WeaknessStabilityDamage (공격마다 StabilityBonus를 더함). 고정 피해(서리 결전 · 부딪힘)는 바뀌지 않는다.
    /// 약점은 Unit.WeaknessMask (DamageElement 비트).
    /// </summary>
    public static class Weakness
    {
        /// <summary>무작위로 고르는 약점 후보 (순서 고정 — 같은 시드 = 같은 약점).</summary>
        public static readonly DamageElement[] Pool =
        {
            DamageElement.Burn, DamageElement.Conduct, DamageElement.Torrent, DamageElement.Acid, DamageElement.Freeze, DamageElement.Physical,
        };

        public static int Bit(DamageElement element) => 1 << (int)element;

        public static bool Has(Unit unit, DamageElement element) => unit != null && (unit.WeaknessMask & Bit(element)) != 0;

        /// <summary>새로 생긴 적에게 약점 하나를 무작위로 붙인다. 플레이어 · 상점 주인은 건너뛴다.</summary>
        public static void Assign(World world, Unit unit)
        {
            if (unit == null || unit.IsPlayer || unit.Kind == UnitKind.Shopkeeper) return;
            var element = Pool[world.Random.Get(RandomPurpose.Weakness).Range(0, Pool.Length)];
            unit.WeaknessMask = Bit(element);
        }

        /// <summary>이 공격이 맞히는 약점 수 (플레이어 공격만). 타고난 약점 + 서리밭 빙결 약점 — 같은 속성이면 하나로 센다.</summary>
        public static int Hits(World world, CombatSettings settings, Unit attacker, Unit target, DamageSource source)
        {
            if (attacker == null || !attacker.IsPlayer || target == null || target.IsPlayer) return 0;
            var element = SetEffects.ElementOf(world, settings, source);
            if (Has(target, element)) return 1;
            if (element == DamageElement.Freeze && InFrostField(world, target)) return 1;
            return 0;
        }

        /// <summary>서리밭 안에 서 있는가 (빙결 약점이 붙음).</summary>
        public static bool InFrostField(World world, Unit unit)
        {
            foreach (var field in world.FrostFields)
                if (field.Contains(unit.Position)) return true;
            return false;
        }

        /// <summary>약점 피해 (%p) — SetEffects.DamagePercent가 더한다.</summary>
        public static int DamagePercent(World world, CombatSettings settings, Unit attacker, Unit target, DamageSource source)
            => settings == null ? 0 : Hits(world, settings, attacker, target, source) * settings.WeaknessDamagePercent;

        /// <summary>약점 안정 피해 (더하는 양) — 공격마다 안정 피해에 더한다.</summary>
        public static int StabilityBonus(World world, CombatSettings settings, Unit attacker, Unit target, DamageSource source)
            => settings == null ? 0 : Hits(world, settings, attacker, target, source) * settings.WeaknessStabilityDamage;

        /// <summary>적의 약점 목록 (화면 표시용, 서리밭 빙결 포함).</summary>
        public static List<DamageElement> Of(World world, Unit unit)
        {
            var list = new List<DamageElement>();
            if (unit == null) return list;
            foreach (var element in Pool)
                if (Has(unit, element)) list.Add(element);
            if (!list.Contains(DamageElement.Freeze) && InFrostField(world, unit)) list.Add(DamageElement.Freeze);
            return list;
        }
    }
}
