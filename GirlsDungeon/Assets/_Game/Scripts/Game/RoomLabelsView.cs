using System.Collections.Generic;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 특수 방 이름 표시 (3-14a, 결정 2026-11-13 사용자: 문에 방 이름이 보임). 특수 방 입구 칸 위에 이름 글을 띄운다.
    /// 그 칸을 한 번이라도 봤을 때(World.Explored)만 보이고, 카메라를 향해 돈다. 이름은 텍스트 표 room.kind.{이름}.name.
    /// </summary>
    public sealed class RoomLabelsView : MonoBehaviour
    {
        [SerializeField] private float height = 1.7f;
        [SerializeField] private float characterSize = 0.12f;
        [SerializeField] private int fontSize = 48;
        [SerializeField] private Color color = new Color(1f, 0.92f, 0.6f);

        private readonly List<(GameObject view, GirlsDungeon.Core.Grid.GridPos cell)> labels = new List<(GameObject, GirlsDungeon.Core.Grid.GridPos)>();
        private IEventSource source;
        private Font font;

        public int LabelCount => labels.Count;

        public void Bind(IEventSource newSource)
        {
            if (source != null) source.EventsPublished -= OnEvents;
            source = newSource;
            source.EventsPublished += OnEvents;
            Rebuild();
        }

        private void OnDestroy()
        {
            if (source != null) source.EventsPublished -= OnEvents;
        }

        private void OnEvents(IReadOnlyList<GameEvent> events)
        {
            foreach (var gameEvent in events)
                if (gameEvent is FloorChanged) Rebuild();
        }

        /// <summary>지금 층의 특수 방 입구마다 이름 글을 다시 만든다.</summary>
        public void Rebuild()
        {
            foreach (var (view, _) in labels) Destroy(view);
            labels.Clear();
            if (source?.World?.Map == null) return;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var map = source.World.Map;
            for (int i = 0; i < map.Rooms.Count; i++)
            {
                var kind = map.RoomKind(i);
                if (kind == SpecialRoomKind.None) continue;
                string name = Text.Get(NameKey(kind));
                foreach (var cell in FloorDecor.Entrances(map, map.Rooms[i]))
                {
                    var view = new GameObject("RoomLabel " + kind);
                    view.transform.SetParent(transform, false);
                    view.transform.position = GridSpace.ToWorld(cell) + Vector3.up * height;
                    var text = view.AddComponent<TextMesh>();
                    text.font = font;
                    text.text = name;
                    text.fontSize = fontSize;
                    text.characterSize = characterSize;
                    text.anchor = TextAnchor.MiddleCenter;
                    text.alignment = TextAlignment.Center;
                    text.color = color;
                    view.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                    view.SetActive(false);
                    labels.Add((view, cell));
                }
            }
        }

        private void LateUpdate()
        {
            var world = source?.World;
            if (world == null) return;
            var cameraTransform = Camera.main != null ? Camera.main.transform : null;
            foreach (var (view, cell) in labels)
            {
                bool seen = world.Explored != null && Vision.IsSet(world.Explored, world.Map, cell);
                if (view.activeSelf != seen) view.SetActive(seen);
                if (seen && cameraTransform != null) view.transform.rotation = cameraTransform.rotation;
            }
        }

        /// <summary>방 종류 → 이름 키 (키를 통째로 — 이어 붙이면 키 검사가 못 찾는다).</summary>
        public static string NameKey(SpecialRoomKind kind)
        {
            switch (kind)
            {
                case SpecialRoomKind.Workshop: return "room.kind.workshop.name";
                case SpecialRoomKind.Treasure: return "room.kind.treasure.name";
                case SpecialRoomKind.Storehouse: return "room.kind.storehouse.name";
                case SpecialRoomKind.SetGallery: return "room.kind.set_gallery.name";
                case SpecialRoomKind.Exchange: return "room.kind.exchange.name";
                case SpecialRoomKind.Altar: return "room.kind.altar.name";
                case SpecialRoomKind.Repair: return "room.kind.repair.name";
                case SpecialRoomKind.Arena: return "room.kind.arena.name";
                case SpecialRoomKind.Trap: return "room.kind.trap.name";
                case SpecialRoomKind.Gamble: return "room.kind.gamble.name";
                case SpecialRoomKind.Observatory: return "room.kind.observatory.name";
                case SpecialRoomKind.Archive: return "room.kind.archive.name";
                default: return "room.kind.workshop.name";
            }
        }
    }
}
