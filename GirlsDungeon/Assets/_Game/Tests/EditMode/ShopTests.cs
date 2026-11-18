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
    /// 3-15 상점 · 사르디스 골드 (BALANCE 31 · 32번): 구역의 첫 층에만 상점 · 진열 6개(구급상자 2 · 열쇠 · 군용 · 정밀 파츠 · 무기) ·
    /// 밟으면 사기 창, 값을 내야 줍는다 · 상인에게 팔기(사는 값 35%) · 적 5 · 보급 상자 15 · 보물 상자 40 골드.
    /// </summary>
    public class ShopTests
    {
        [Test]
        public void TheShop_IsOnlyOnTheFirstFloorOfAZone_AndHasNoEnemies()
        {
            Assert.That(SpecialRoomLayout.IsShopFloor(TestData.Map(), 0), Is.True);
            for (int floor = 1; floor <= 4; floor++) Assert.That(SpecialRoomLayout.IsShopFloor(TestData.Map(), floor), Is.False, $"floor {floor}");
            Assert.That(SpecialRoomLayout.IsShopFloor(TestData.Map(), 5), Is.True, "first floor of zone 2");

            for (int seed = 1; seed <= 20; seed++)
            {
                var world = TestData.NewRun(seed, 6);
                var map = world.Map;
                int shops = Enumerable.Range(0, map.Rooms.Count).Count(i => map.RoomKind(i) == SpecialRoomKind.Shop);
                Assert.That(shops, Is.EqualTo(1), $"seed {seed}");
                Assert.That(world.Units.Any(u => !u.IsPlayer && u.Kind != UnitKind.Shopkeeper && map.SpecialRoomAt(u.Position) == SpecialRoomKind.Shop), Is.False, $"seed {seed} enemy in shop");

                RunFactory.NextFloor(world, TestData.Map(), TestData.Combat(6), null);
                Assert.That(Enumerable.Range(0, world.Map.Rooms.Count).Any(i => world.Map.RoomKind(i) == SpecialRoomKind.Shop), Is.False, $"seed {seed} floor 2");
                Assert.That(world.Items.Any(i => i.Price > 0), Is.False, $"seed {seed} floor 2 items");
            }
        }

        [Test]
        public void Stock_IsSixPricedItems_InsideTheShop_AwayFromTheCounter()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var world = TestData.NewRun(seed);
                var stock = world.Items.Where(i => i.Price > 0).ToList();
                Assert.That(stock.Count, Is.EqualTo(6), $"seed {seed}");
                Assert.That(stock.Count(i => i.Kind == ItemKind.Medkit && i.Price == 25), Is.EqualTo(2));
                Assert.That(stock.Count(i => i.Kind == ItemKind.Key && i.Price == 35), Is.EqualTo(1));
                Assert.That(stock.Count(i => i.Kind == ItemKind.Part && i.Part.Grade == PartGrade.Military && i.Price == 45), Is.EqualTo(1));
                Assert.That(stock.Count(i => i.Kind == ItemKind.Part && i.Part.Grade == PartGrade.Precision && i.Price == 80), Is.EqualTo(1));
                Assert.That(stock.Count(i => i.Kind == ItemKind.Weapon && i.Weapon != world.PlayerWeapon && i.Price == 60), Is.EqualTo(1));
                foreach (var item in stock)
                {
                    Assert.That(world.Map.SpecialRoomAt(item.Position), Is.EqualTo(SpecialRoomKind.Shop), $"seed {seed}");
                    Assert.That(world.Map.IsWalkable(item.Position), Is.True);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            var near = new GridPos(item.Position.X + dx, item.Position.Y + dy);
                            Assert.That(world.Map.InBounds(near) && world.Map.Get(near) == TileType.ShopCounter, Is.False, $"seed {seed} next to counter");
                        }
                }
            }
        }

        /// <summary>바닥 3줄, 계산대 (4,1) · 상점 주인 (5,1), 플레이어 (1,1), 발밑 (2,1)에 진열 열쇠 35 · (2,0)에 정밀 파츠 80.</summary>
        private static (World world, FloorItem key, FloorItem part) ShopWorld()
        {
            var combat = TestData.Combat();
            var world = new World(1, new RandomStreams(1)) { Map = TestData.Parse("......", "......", "......") };
            world.Map.SetTile(new GridPos(4, 1), TileType.ShopCounter);
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 1)).Id;
            world.AddUnit(combat.StatsFor(UnitKind.Shopkeeper), new GridPos(5, 1));
            Loadout.Apply(world, combat);
            var key = world.AddItem(ItemKind.Key, default, new GridPos(2, 1));
            key.Price = 35;
            var part = world.AddPartItem(Parts.GenerateWithGrade(combat, world.PlayerWeapon, PartGrade.Precision, world.Random.Get(RandomPurpose.Shop)), new GridPos(2, 0));
            part.Price = 80;
            return (world, key, part);
        }

        [Test]
        public void SteppingOnAPricedItem_OnlyOffersIt_AndBuyingPaysThenPicksUp()
        {
            var (world, key, _) = ShopWorld();
            var combat = TestData.Combat();
            var simulation = new Simulation(world, combat);
            var events = new List<GameEvent>();
            simulation.EventsPublished += published => events.AddRange(published);

            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 1), 1)), Is.True);
            Assert.That(events.OfType<ShopItemOffered>().Single().ItemId, Is.EqualTo(key.Id));
            Assert.That(world.Keys, Is.EqualTo(0), "not picked up before paying");

            world.Gold = 30;
            Assert.That(simulation.Execute(new BuyItemCommand(key.Id, combat)), Is.False, "30 < 35");
            Assert.That(world.Gold, Is.EqualTo(30));

            world.Gold = 50;
            Assert.That(simulation.Execute(new BuyItemCommand(key.Id, combat)), Is.True);
            Assert.That(world.Gold, Is.EqualTo(15));
            Assert.That(world.Keys, Is.EqualTo(1));
            Assert.That(world.FindItem(key.Id), Is.Null);
        }

        [Test]
        public void BuyingAPart_OffersTheNormalCompareWindow_AndLeavesItFree()
        {
            var (world, _, part) = ShopWorld();
            var combat = TestData.Combat();
            var simulation = new Simulation(world, combat);
            var events = new List<GameEvent>();
            simulation.EventsPublished += published => events.AddRange(published);
            world.Player.Position = part.Position;
            world.Gold = 100;

            Assert.That(simulation.Execute(new BuyItemCommand(part.Id, combat)), Is.True);
            Assert.That(world.Gold, Is.EqualTo(20));
            Assert.That(part.Price, Is.EqualTo(0));
            Assert.That(events.OfType<PartFound>().Single().ItemId, Is.EqualTo(part.Id));
            Assert.That(simulation.Execute(new BuyItemCommand(part.Id, combat)), Is.False, "already bought");
        }

        [Test]
        public void Selling_AtTheCounter_PaysPartOfThePrice()
        {
            var (world, _, _) = ShopWorld();
            var combat = TestData.Combat();
            world.Keys = 2;
            world.PartMaterials = 1;
            world.SpareParts.Add(Parts.GenerateWithGrade(combat, world.PlayerWeapon, PartGrade.Military, world.Random.Get(RandomPurpose.Shop)));

            Assert.That(Shop.Sell(world, combat, ShopSale.Key, 0, new List<GameEvent>()), Is.False, "not next to the counter");
            world.Player.Position = new GridPos(3, 1);

            Assert.That(Shop.Sell(world, combat, ShopSale.Key, 0, new List<GameEvent>()), Is.True);
            Assert.That(world.Gold, Is.EqualTo(12), "35 x 35% = 12.25");
            Assert.That(world.Keys, Is.EqualTo(1));
            Assert.That(Shop.Sell(world, combat, ShopSale.SparePart, 0, new List<GameEvent>()), Is.True);
            Assert.That(world.Gold, Is.EqualTo(12 + 16), "45 x 35% = 15.75");
            Assert.That(world.SpareParts, Is.Empty);
            Assert.That(Shop.Sell(world, combat, ShopSale.Material, 0, new List<GameEvent>()), Is.True);
            Assert.That(world.Gold, Is.EqualTo(12 + 16 + 3));
            Assert.That(Shop.Sell(world, combat, ShopSale.Material, 0, new List<GameEvent>()), Is.False, "no materials left");
        }

        [Test]
        public void Gold_ComesFromKills_Crates_AndTreasure()
        {
            var combat = TestData.Combat();
            var world = new World(1, new RandomStreams(1)) { Map = TestData.Parse(".....", ".....", ".....") };
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(0, 1)).Id;
            Loadout.Apply(world, combat);
            var events = new List<GameEvent> { new UnitDied(9, UnitKind.Rifleman, new GridPos(3, 1)), new UnitDied(10, UnitKind.Assault, new GridPos(4, 1)) };
            Gold.CheckKills(world, combat, events);
            Assert.That(world.Gold, Is.EqualTo(10));

            world.Map.SetTile(new GridPos(1, 1), TileType.SupplyCrate);
            Assert.That(Loot.OpenCrate(world, combat, new GridPos(1, 1), new List<GameEvent>()), Is.True);
            Assert.That(world.Gold, Is.EqualTo(25));
            world.Map.SetTile(new GridPos(1, 0), TileType.TreasureChest);
            Assert.That(Loot.OpenCrate(world, combat, new GridPos(1, 0), new List<GameEvent>()), Is.True);
            Assert.That(world.Gold, Is.EqualTo(65));
        }

        // ---------- 상점 주인 (3-15b) ----------

        [Test]
        public void TheShopkeeper_StandsNextToTheCounter_Neutral()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var world = TestData.NewRun(seed, 6);
                var keeper = Shop.Keeper(world);
                Assert.That(keeper, Is.Not.Null, $"seed {seed}");
                Assert.That(keeper.MaxHp, Is.EqualTo(120));
                Assert.That(world.Map.SpecialRoomAt(keeper.Position), Is.EqualTo(SpecialRoomKind.Shop));
                bool nextToCounter = false;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var near = new GridPos(keeper.Position.X + dx, keeper.Position.Y + dy);
                        if (world.Map.InBounds(near) && world.Map.Get(near) == TileType.ShopCounter) nextToCounter = true;
                    }
                Assert.That(nextToCounter, Is.True, $"seed {seed}");
                Assert.That(Shop.IsNeutral(world, keeper), Is.True);
                Assert.That(world.Combatants, Has.No.Member(keeper.Id));
            }
        }

        /// <summary>열린 방 (1,1) ~ (10,5), 계산대 (6,3), 상점 주인 (6,2), 진열 열쇠 (2,5) 35골드, 플레이어 (2,3). 상점 방으로 표시.</summary>
        private static (Simulation simulation, World world, Unit keeper, FloorItem key, CombatSettings combat) KeeperWorld()
        {
            var combat = TestData.Combat();
            var room = new RoomRect(1, 1, 10, 5);
            var world = new World(1, new RandomStreams(1))
            {
                Map = TestData.ParseWithRooms(new[] { room },
                    "############",
                    "#..........#",
                    "#..........#",
                    "#..........#",
                    "#..........#",
                    "#..........#",
                    "############"),
            };
            world.Map.SetRoomKind(0, SpecialRoomKind.Shop);
            world.Map.SetTile(new GridPos(6, 3), TileType.ShopCounter);
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(2, 3)).Id;
            var keeper = world.AddUnit(combat.StatsFor(UnitKind.Shopkeeper), new GridPos(6, 2));
            Loadout.Apply(world, combat);
            var key = world.AddItem(ItemKind.Key, default, new GridPos(2, 5));
            key.Price = 35;
            var simulation = new Simulation(world, combat);
            simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(3, 3), 1));
            return (simulation, world, keeper, key, combat);
        }

        [Test]
        public void SeeingTheNeutralShopkeeper_DoesNotStartCombat()
        {
            var (_, world, keeper, _, _) = KeeperWorld();
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
            Assert.That(world.Combatants, Is.Empty);
            Assert.That(Shop.IsNeutral(world, keeper), Is.True);
        }

        [Test]
        public void ASkillWhileExploring_AngersTheShopkeeper_StartsCombat_AndClosesTheShop()
        {
            var (simulation, world, keeper, key, combat) = KeeperWorld();
            world.Gold = 100;
            Assert.That(simulation.Execute(new AreaAttackCommand(keeper.Position, combat)), Is.True, "grenade while exploring");

            Assert.That(world.ShopHostile, Is.True);
            Assert.That(keeper.Hp, Is.LessThan(keeper.MaxHp));
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            Assert.That(world.Combatants, Has.Member(keeper.Id));
            Assert.That(world.Round, Is.EqualTo(2), "the grenade was the first action — the shopkeeper already took his turn");
            Assert.That(TurnFlow.IsPlayerTurn(world), Is.True);

            world.Player.Position = key.Position;
            Assert.That(Shop.Buy(world, combat, key.Id, new List<GameEvent>()), Is.False, "no buying while hostile");
        }

        [Test]
        public void DefeatingTheShopkeeper_MakesEveryItemFree()
        {
            var (simulation, world, keeper, key, combat) = KeeperWorld();
            Assert.That(simulation.Execute(new AreaAttackCommand(keeper.Position, combat)), Is.True);
            Assert.That(simulation.Execute(new DebugDefeatCommand(keeper.Id)), Is.True);

            Assert.That(Shop.Keeper(world), Is.Null);
            Assert.That(key.Price, Is.EqualTo(0));
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
            int keys = world.Keys;
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 4), 1)), Is.True);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, key.Position, 1)), Is.True);
            Assert.That(world.Keys, Is.EqualTo(keys + 1), "picked up for free");
        }

        [Test]
        public void ASkillWithNoEnemyInSight_StaysInExploration_WithoutCooldown()
        {
            var combat = TestData.Combat();
            var world = new World(1, new RandomStreams(1)) { Map = TestData.Parse("..........", "..........", "..........") };
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 1)).Id;
            Loadout.Apply(world, combat);
            var simulation = new Simulation(world, combat);
            Assert.That(simulation.Execute(new AreaAttackCommand(new GridPos(5, 1), combat)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
            Assert.That(world.GrenadeCooldown, Is.EqualTo(0));
        }

        [Test]
        public void Snapshot_KeepsGoldAndPrices()
        {
            var world = TestData.NewRun(3);
            world.Gold = 77;
            var restored = WorldSnapshot.Decode(WorldSnapshot.Encode(world));
            Assert.That(restored.Gold, Is.EqualTo(77));
            CollectionAssert.AreEqual(world.Items.Select(i => i.Price), restored.Items.Select(i => i.Price));
            Assert.That(restored.Items.Count(i => i.Price > 0), Is.EqualTo(6));
        }
    }
}
