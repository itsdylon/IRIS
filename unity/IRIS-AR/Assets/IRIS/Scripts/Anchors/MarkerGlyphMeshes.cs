using System.Collections.Generic;
using UnityEngine;

namespace IRIS.Anchors
{
    /// <summary>Procedural read-only meshes for tactical marker glyphs (shared across instances).</summary>
    public static class MarkerGlyphMeshes
    {
        private static Mesh _tetraUp;
        private static Mesh _tetraDown;
        private static Mesh _octahedron;
        private static Mesh _disc;
        private static Mesh _cross;
        private static Mesh _octagonPrism;

        public static Mesh ForMarkerType(string type)
        {
            return type switch
            {
                "threat" => TetraUp(),
                "friendly" => Disc(),
                "waypoint" => Octahedron(),
                "objective" => Octahedron(),
                "extraction" => Cross(),
                "info" => OctagonPrism(),
                "generic" => TetraDown(),
                _ => TetraDown()
            };
        }

        private static Mesh TetraUp() => _tetraUp ??= BuildTetrahedron(pointUp: true);
        private static Mesh TetraDown() => _tetraDown ??= BuildTetrahedron(pointUp: false);
        private static Mesh Octahedron() => _octahedron ??= BuildOctahedron();
        private static Mesh Disc() => _disc ??= BuildDisc(segments: 24);
        private static Mesh Cross() => _cross ??= BuildCross();
        private static Mesh OctagonPrism() => _octagonPrism ??= BuildOctagonPrism();

        private static Mesh BuildTetrahedron(bool pointUp)
        {
            float r = 0.75f;
            var v0 = new Vector3(0f, 0.85f, 0f);
            float a = 2f * Mathf.PI / 3f;
            var v1 = new Vector3(r * Mathf.Cos(0f), 0f, r * Mathf.Sin(0f));
            var v2 = new Vector3(r * Mathf.Cos(a), 0f, r * Mathf.Sin(a));
            var v3 = new Vector3(r * Mathf.Cos(2f * a), 0f, r * Mathf.Sin(2f * a));

            var verts = new[] { v0, v1, v2, v3 };
            if (!pointUp)
            {
                for (int i = 0; i < verts.Length; i++)
                    verts[i].y *= -1f;
            }

            var tris = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 3, 1, 2, 3 };
            return BuildMesh(pointUp ? "TetraUp" : "TetraDown", verts, tris);
        }

        private static Mesh BuildOctahedron()
        {
            var verts = new[]
            {
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, -1f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(-1f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 0f, -1f),
            };
            var tris = new[]
            {
                0, 4, 2, 0, 2, 5, 0, 5, 3, 0, 3, 4,
                1, 2, 4, 1, 5, 2, 1, 3, 5, 1, 4, 3,
            };
            return BuildMesh("Octa", verts, tris);
        }

        private static Mesh BuildDisc(int segments)
        {
            var verts = new List<Vector3> { Vector3.zero };
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(t) * 0.65f, 0f, Mathf.Sin(t) * 0.65f));
            }

            var tris = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                tris.Add(0);
                tris.Add(i + 1);
                tris.Add(i + 2);
            }

            return BuildMesh("Disc", verts.ToArray(), tris.ToArray());
        }

        private static Mesh BuildCross()
        {
            float t = 0.12f;
            float L = 0.55f;
            var verts = new List<Vector3>();
            var tris = new List<int>();

            void AddBox(Vector3 c, Vector3 half)
            {
                int b = verts.Count;
                var e = half;
                verts.Add(c + new Vector3(-e.x, -e.y, -e.z));
                verts.Add(c + new Vector3(e.x, -e.y, -e.z));
                verts.Add(c + new Vector3(e.x, e.y, -e.z));
                verts.Add(c + new Vector3(-e.x, e.y, -e.z));
                verts.Add(c + new Vector3(-e.x, -e.y, e.z));
                verts.Add(c + new Vector3(e.x, -e.y, e.z));
                verts.Add(c + new Vector3(e.x, e.y, e.z));
                verts.Add(c + new Vector3(-e.x, e.y, e.z));

                int[] f =
                {
                    0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
                    0, 1, 5, 0, 5, 4, 2, 3, 7, 2, 7, 6,
                    0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5,
                };
                foreach (var idx in f)
                    tris.Add(b + idx);
            }

            AddBox(Vector3.zero, new Vector3(t, L, t));
            AddBox(Vector3.zero, new Vector3(L, t, t));

            return BuildMesh("Cross", verts.ToArray(), tris.ToArray());
        }

        private static Mesh BuildOctagonPrism()
        {
            const int n = 8;
            float r = 0.55f;
            float h = 0.16f;
            var verts = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                float ang = (i / (float)n) * Mathf.PI * 2f + Mathf.PI / n;
                verts.Add(new Vector3(Mathf.Cos(ang) * r, h, Mathf.Sin(ang) * r));
            }

            for (int i = 0; i < n; i++)
            {
                float ang = (i / (float)n) * Mathf.PI * 2f + Mathf.PI / n;
                verts.Add(new Vector3(Mathf.Cos(ang) * r, -h, Mathf.Sin(ang) * r));
            }

            verts.Add(new Vector3(0f, h, 0f));
            verts.Add(new Vector3(0f, -h, 0f));
            const int cTop = 16;
            const int cBot = 17;

            var tris = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                tris.Add(i);
                tris.Add(j);
                tris.Add(i + n);
                tris.Add(j);
                tris.Add(j + n);
                tris.Add(i + n);
            }

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                tris.Add(cTop);
                tris.Add(j);
                tris.Add(i);
            }

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                tris.Add(cBot);
                tris.Add(i + n);
                tris.Add(j + n);
            }

            return BuildMesh("Octagon", verts.ToArray(), tris.ToArray());
        }

        private static Mesh BuildMesh(string name, Vector3[] vertices, int[] triangles)
        {
            var m = new Mesh { name = name };
            m.vertices = vertices;
            m.triangles = triangles;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
