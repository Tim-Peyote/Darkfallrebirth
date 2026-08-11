using Darkfall.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.World
{
    public sealed class FogOfWarView : MonoBehaviour
    {
        private DungeonData dungeon;
        private PlayerController player;
        private float nextUpdate;
        private Mesh stateMesh;
        private Color32[] stateColors;
        private readonly List<Vector2Int> stateCells = new List<Vector2Int>();
        private Material stateMaterial;

        public void Initialize(DungeonData data, PlayerController target)
        {
            dungeon = data;
            player = target;
            BuildStateOverlay();
            RefreshVisibilityData();
        }

        private void Update()
        {
            if (player == null || dungeon == null || Time.time < nextUpdate) return;
            nextUpdate = Time.time + .085f;
            RefreshVisibilityData();
        }

        private void RefreshVisibilityData()
        {
            if (player == null || dungeon == null) return;
            var origin = new Vector2Int(Mathf.FloorToInt(player.transform.position.x), Mathf.FloorToInt(player.transform.position.y));
            dungeon.BeginVisibilityUpdate();
            for (var x = origin.x - 12; x <= origin.x + 12; x++)
            for (var y = origin.y - 12; y <= origin.y + 12; y++)
            {
                if (x < 0 || y < 0 || x >= dungeon.Width || y >= dungeon.Height) continue;
                var cell = new Vector2Int(x, y);
                if (IsInsideVision(origin, cell) && HasLineOfSight(origin, cell)) dungeon.Reveal(x, y);
            }
            RefreshStateOverlay();
        }

        private void BuildStateOverlay()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            stateCells.Clear();
            for (var x = 0; x < dungeon.Width; x++)
            for (var y = 0; y < dungeon.Height; y++)
            {
                if (!dungeon.IsFloor(x, y)) continue;
                var index = vertices.Count;
                var height = dungeon.SurfaceHeight(new Vector2(x + .5f, y + .5f)) + .04f;
                var logical = new[]
                {
                    new Vector2(x, y), new Vector2(x + 1, y),
                    new Vector2(x + 1, y + 1), new Vector2(x, y + 1)
                };
                foreach (var point in logical)
                {
                    var projected = IsoWorld.Project(point);
                    projected.y += height;
                    vertices.Add(projected);
                    uvs.Add(Vector2.one * .5f);
                }
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
                stateCells.Add(new Vector2Int(x, y));
            }
            stateColors = new Color32[vertices.Count];
            stateMesh = new Mesh { name = "Fog Of War Cell States", hideFlags = HideFlags.DontSave };
            stateMesh.SetVertices(vertices);
            stateMesh.SetTriangles(triangles, 0);
            stateMesh.SetUVs(0, uvs);
            stateMesh.colors32 = stateColors;
            stateMesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = stateMesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            stateMaterial = new Material(shader)
            {
                name = "Fog Of War Cell State Material",
                hideFlags = HideFlags.DontSave,
                mainTexture = Texture2D.whiteTexture,
                color = Color.white
            };
            renderer.sharedMaterial = stateMaterial;
            renderer.sortingOrder = 30002;
        }

        private void RefreshStateOverlay()
        {
            if (stateMesh == null || stateColors == null) return;
            for (var cellIndex = 0; cellIndex < stateCells.Count; cellIndex++)
            {
                var cell = stateCells[cellIndex];
                // The directional curtain supplies the soft dimming for explored space outside
                // current vision. This layer only makes genuinely unknown cells unmistakable.
                var vertex = cellIndex * 4;
                if (dungeon.IsVisible(cell.x, cell.y) || dungeon.IsExplored(cell.x, cell.y))
                {
                    var clear = new Color32(0, 0, 0, 0);
                    stateColors[vertex] = clear;
                    stateColors[vertex + 1] = clear;
                    stateColors[vertex + 2] = clear;
                    stateColors[vertex + 3] = clear;
                    continue;
                }
                // Each cell owns four duplicate vertices, so calculate alpha from the logical
                // vertex rather than assigning one value to the whole diamond. Adjacent unknown
                // tiles then share the same values and form a continuous fade into black.
                stateColors[vertex] = UnknownVertexColor(cell.x, cell.y);
                stateColors[vertex + 1] = UnknownVertexColor(cell.x + 1, cell.y);
                stateColors[vertex + 2] = UnknownVertexColor(cell.x + 1, cell.y + 1);
                stateColors[vertex + 3] = UnknownVertexColor(cell.x, cell.y + 1);
            }
            stateMesh.colors32 = stateColors;
        }

        private Color32 UnknownVertexColor(int vertexX, int vertexY)
        {
            // Unknown geometry is absence of information, not coloured ambient fog. Pure black
            // also prevents lower floors from acquiring blue rectangular bands.
            if (HasExploredCell(vertexX, vertexY, 0)) return new Color32(0, 0, 0, 28);
            if (HasExploredCell(vertexX, vertexY, 1)) return new Color32(0, 0, 0, 154);
            if (HasExploredCell(vertexX, vertexY, 2)) return new Color32(0, 0, 0, 226);
            return new Color32(0, 0, 0, byte.MaxValue);
        }

        private bool HasExploredCell(int vertexX, int vertexY, int extraRadius)
        {
            for (var x = vertexX - 1 - extraRadius; x <= vertexX + extraRadius; x++)
            for (var y = vertexY - 1 - extraRadius; y <= vertexY + extraRadius; y++)
            {
                if (x < 0 || y < 0 || x >= dungeon.Width || y >= dungeon.Height) continue;
                if (dungeon.IsVisible(x, y) || dungeon.IsExplored(x, y)) return true;
            }
            return false;
        }

        private bool IsInsideVision(Vector2Int origin, Vector2Int cell)
        {
            var offset = (Vector2)(cell - origin);
            var distance = offset.magnitude;
            if (offset.sqrMagnitude < .001f) return true;
            var facing = player.FacingDirection.sqrMagnitude > .001f ? player.FacingDirection.normalized : Vector2.right;
            var visibleRadius = DungeonLighting.PlayerVisionRadius(facing, offset.normalized);
            return distance <= visibleRadius;
        }

        private bool HasLineOfSight(Vector2Int from, Vector2Int to)
        {
            var x = from.x;
            var y = from.y;
            var dx = Mathf.Abs(to.x - from.x);
            var dy = Mathf.Abs(to.y - from.y);
            var sx = from.x < to.x ? 1 : -1;
            var sy = from.y < to.y ? 1 : -1;
            var error = dx - dy;
            while (x != to.x || y != to.y)
            {
                var twice = error * 2;
                if (twice > -dy) { error -= dy; x += sx; }
                if (twice < dx) { error += dx; y += sy; }
                if ((x != to.x || y != to.y) && dungeon.BlocksVision(x, y)) return false;
            }
            return true;
        }

        private void OnDestroy()
        {
            if (stateMesh != null) Destroy(stateMesh);
            if (stateMaterial != null) Destroy(stateMaterial);
        }
    }
}
