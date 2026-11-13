using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 세트 진열실의 한 곳 (3-14g — 잠긴 특수 방). 진열장(TileType.SetGalleryCase) 바로 옆에서, 탐험 중에만.
    /// - 열기 `Open`: 처음 열 때 든 무기용 파츠 3개(같은 세트, 2 · 3 · 4번 칸 하나씩, 등급 SetGalleryPartGrade)를 만들어 World.GalleryOffers에 둔다.
    ///   세트는 지금 끼운 파츠 중 가장 많은 세트(같으면 앞 번호), 없으면 무작위 — 세트 모으기를 돕게. 굴림은 아이템 난수.
    /// - 고르기 `Pick`: 하나를 진열장 옆 바닥에 놓고(밟아서 끼우기 · 보관), 나머지는 사라지며 진열장은 빈다.
    /// </summary>
    public static class SetGallery
    {
        public const int OfferCount = 3;

        /// <summary>플레이어가 닫힌 진열장 바로 옆에 있고 탐험 중이면 그 칸.</summary>
        public static GridPos? CaseNextTo(World world)
        {
            var player = world.Player;
            if (player == null || world.Mode != WorldMode.Exploration) return null;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var cell = new GridPos(player.Position.X + dx, player.Position.Y + dy);
                    if ((dx != 0 || dy != 0) && world.Map.InBounds(cell) && world.Map.Get(cell) == TileType.SetGalleryCase) return cell;
                }
            return null;
        }

        public static bool AtCase(World world) => CaseNextTo(world).HasValue;

        /// <summary>진열할 세트 — 지금 끼운 파츠 중 가장 많은 세트, 없으면 무작위.</summary>
        public static PartSet ChooseSet(World world, IRandom random)
        {
            var counts = new int[SetEffects.SetCount + 1];
            foreach (var part in world.PlayerParts)
                if (part != null && part.Set != PartSet.None) counts[(int)part.Set]++;
            int best = 0;
            for (int i = 1; i <= SetEffects.SetCount; i++)
                if (counts[i] > counts[best]) best = i;
            return best > 0 ? (PartSet)best : (PartSet)random.Range(1, SetEffects.SetCount + 1);
        }

        /// <summary>진열장을 연다 (처음이면 파츠 3개를 만든다). 진열장 옆이 아니면 false.</summary>
        public static bool Open(World world, CombatSettings settings, List<GameEvent> events)
        {
            if (!AtCase(world)) return false;
            if (world.GalleryOffers.Count == 0)
            {
                var random = world.Random.Get(RandomPurpose.Item);
                var set = ChooseSet(world, random);
                for (int slot = SetEffects.FirstSetSlot; slot < WeaponPart.SlotCount; slot++)
                    world.GalleryOffers.Add(Parts.GenerateForSlot(settings, world.PlayerWeapon, slot, settings.SetGalleryPartGrade, set, random));
            }
            events.Add(new GalleryOpened(world.GalleryOffers.Count));
            return true;
        }

        /// <summary>진열된 파츠 하나를 고른다: 진열장 옆 빈 칸에 놓고, 나머지는 사라지며 진열장은 빈다.</summary>
        public static bool Pick(World world, int index, List<GameEvent> events)
        {
            var at = CaseNextTo(world);
            if (!at.HasValue || index < 0 || index >= world.GalleryOffers.Count) return false;
            var part = world.GalleryOffers[index];
            world.GalleryOffers.Clear();
            world.Map.SetTile(at.Value, TileType.SetGalleryCaseUsed);
            var cell = Loot.FreeCellNear(world, at.Value) ?? world.Player.Position;
            var item = world.AddPartItem(part, cell);
            events.Add(new ItemDropped(item.Id, item.Kind, item.Weapon, item.Position, DropSource.SupplyCrate));
            events.Add(new GalleryPicked(part.Set, part.Slot));
            return true;
        }
    }

    /// <summary>진열장을 연다 (진열 창을 띄우기 전에 파츠를 만든다). 행동 · 틱을 쓰지 않는다.</summary>
    public sealed class OpenGalleryCommand : ICommand
    {
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public OpenGalleryCommand(CombatSettings settings)
        {
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events) => SetGallery.Open(world, settings, events);
    }

    /// <summary>진열된 파츠 하나를 고른다.</summary>
    public sealed class PickGalleryPartCommand : ICommand
    {
        private readonly int index;

        public int CostTicks => 0;

        public PickGalleryPartCommand(int index)
        {
            this.index = index;
        }

        public bool TryApply(World world, List<GameEvent> events) => SetGallery.Pick(world, index, events);
    }

    /// <summary>진열장을 열었다 (파츠 Count개가 진열됨).</summary>
    public sealed class GalleryOpened : GameEvent
    {
        public int Count { get; }

        public GalleryOpened(int count)
        {
            Count = count;
        }
    }

    /// <summary>진열된 파츠를 골랐다. 화면은 지형을 다시 그린다.</summary>
    public sealed class GalleryPicked : GameEvent
    {
        public PartSet Set { get; }
        public int Slot { get; }

        public GalleryPicked(PartSet set, int slot)
        {
            Set = set;
            Slot = slot;
        }
    }
}
