using System.Collections.Generic;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 시야 안개. 안 본 칸 = 검정, 본 칸 = 어둡게, 보이는 칸 = 없음. VisibilityChanged만 보고 그린다.
    /// MoaDungeon FogPresenter의 부드러운 안개를 그대로 옮겼다: 칸 하나 = 여러 텍셀인 작은 텍스처에
    /// "더 어두운 칸까지의 거리"로 칠하고(smoothstep), 이중 선형으로 늘린다. 3D 높이 차이 때문에
    /// 납작한 판 대신 FogOverlay 셰이더로 지형 메시 위에 덧그린다 (월드 XZ로 텍스처를 읽음).
    /// </summary>
    public sealed class FogView : MonoBehaviour
    {
        private const byte Unexplored = 0;
        private const byte Explored = 1;
        private const byte Visible = 2;

        private static readonly int FogTexId = Shader.PropertyToID("_GD_FogTex");
        private static readonly int FogRectId = Shader.PropertyToID("_GD_FogRect");
        private static readonly int FogStrengthId = Shader.PropertyToID("_GD_FogStrength");

        private Simulation simulation;
        private Texture2D texture;
        private Color32[] pixels;
        private byte[] states;
        private int width;
        private int height;
        private float exploredDarkness;
        private int texelsPerCell;
        private float softCells;
        private bool revealAll;

        /// <summary>디버그: 안개를 숨겨 맵 전체를 밝힌다. 표시만 바꾸고 본 칸 기록은 건드리지 않는다.</summary>
        public bool RevealAll
        {
            get => revealAll;
            set
            {
                revealAll = value;
                Shader.SetGlobalFloat(FogStrengthId, value ? 0f : 1f);
            }
        }

        public void Bind(Simulation newSimulation, GameBalance balance)
        {
            Unbind();
            simulation = newSimulation;
            exploredDarkness = balance.ExploredFogDarkness;
            texelsPerCell = Mathf.Max(1, balance.FogTexelsPerCell);
            softCells = Mathf.Max(0.01f, balance.FogSoftCells);

            var world = simulation.World;
            width = world.Map.Width;
            height = world.Map.Height;
            BuildTexture();

            var player = world.Player;
            var visible = player != null ? Vision.Compute(world.Map, player.Position, player.VisionRange) : new bool[width * height];
            SetStates(visible, world.Explored);
            simulation.EventsPublished += HandleEvents;
            RevealAll = revealAll;
        }

        private void OnDestroy()
        {
            Unbind();
            if (texture != null) Destroy(texture);
            Shader.SetGlobalFloat(FogStrengthId, 0f);
        }

        private void Unbind()
        {
            if (simulation != null) simulation.EventsPublished -= HandleEvents;
            simulation = null;
        }

        private void HandleEvents(IReadOnlyList<GameEvent> events)
        {
            VisibilityChanged latest = null;
            foreach (var gameEvent in events)
                if (gameEvent is VisibilityChanged changed) latest = changed;
            if (latest != null) SetStates(latest.Visible, latest.Explored);
        }

        /// <summary>둘레 한 칸을 더한 맵 크기의 텍스처. 텍스처 왼쪽 아래 = 칸 (−1, −1)의 왼쪽 아래 모서리.</summary>
        private void BuildTexture()
        {
            if (texture != null) Destroy(texture);
            int w = (width + 2) * texelsPerCell;
            int h = (height + 2) * texelsPerCell;
            texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "SoftFog",
            };
            pixels = new Color32[w * h];
            states = new byte[width * height];

            Shader.SetGlobalTexture(FogTexId, texture);
            // 칸 가운데가 정수 좌표이므로 칸 x는 [x − 0.5, x + 0.5). 둘레 한 칸 때문에 −1.5에서 시작한다.
            Shader.SetGlobalVector(FogRectId, new Vector4(-1.5f, -1.5f, width + 2, height + 2));
        }

        private void SetStates(bool[] visible, bool[] explored)
        {
            for (int i = 0; i < states.Length; i++)
                states[i] = visible[i] ? Visible : explored != null && explored[i] ? Explored : Unexplored;
            Paint();
        }

        /// <summary>칸의 어둡기 (보임 0 · 본 칸 exploredDarkness · 안 본 칸 · 맵 밖 1).</summary>
        private float Darkness(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return 1f;
            var state = states[x + y * width];
            return state == Visible ? 0f : state == Explored ? exploredDarkness : 1f;
        }

        private void Paint()
        {
            int r = texelsPerCell;
            int w = (width + 2) * r;
            int h = (height + 2) * r;
            int reach = Mathf.CeilToInt(softCells);

            for (int ty = 0; ty < h; ty++)
            {
                float py = (ty + 0.5f) / r - 1f;
                int cy = Mathf.FloorToInt(py);
                for (int tx = 0; tx < w; tx++)
                {
                    float px = (tx + 0.5f) / r - 1f;
                    int cx = Mathf.FloorToInt(px);
                    float d = Darkness(cx, cy);
                    for (int oy = -reach; oy <= reach; oy++)
                    {
                        for (int ox = -reach; ox <= reach; ox++)
                        {
                            if (ox == 0 && oy == 0) continue;
                            float v = Darkness(cx + ox, cy + oy);
                            if (v <= d) continue;
                            float dx = Mathf.Max(0f, Mathf.Max(cx + ox - px, px - (cx + ox + 1)));
                            float dy = Mathf.Max(0f, Mathf.Max(cy + oy - py, py - (cy + oy + 1)));
                            float k = 1f - Mathf.Sqrt(dx * dx + dy * dy) / softCells;
                            if (k > 0f) d = Mathf.Max(d, v * k * k * (3f - 2f * k));
                        }
                    }
                    pixels[tx + ty * w] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(d) * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }
    }
}
