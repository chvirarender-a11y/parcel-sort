using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort.Game.World
{
    /// <summary>Procedural meshes for the toy-like props: rounded boxes and cylinders.</summary>
    public static class MeshFactory
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// <summary>
        /// A box with rounded edges and corners. Built from a subdivided cube whose vertices
        /// are pulled onto a smaller inner box and pushed back out by the radius.
        /// </summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int segments = 6)
        {
            string key = "box" + size + radius + segments;
            Mesh mesh;
            if (cache.TryGetValue(key, out mesh)) return mesh;

            radius = Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f);
            Vector3 half = size * 0.5f;
            Vector3 inner = half - Vector3.one * radius;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // Six faces, each a (segments+1)^2 grid on the unit cube.
            Vector3[] faceNormals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            foreach (var n in faceNormals)
            {
                Vector3 u = n == Vector3.up || n == Vector3.down ? Vector3.right : Vector3.Cross(Vector3.up, n);
                Vector3 v = Vector3.Cross(n, u);
                int start = vertices.Count;
                for (int j = 0; j <= segments; j++)
                {
                    for (int i = 0; i <= segments; i++)
                    {
                        Vector3 p = n + u * (i * 2f / segments - 1f) + v * (j * 2f / segments - 1f);
                        p = Vector3.Scale(p, half);
                        Vector3 core = new Vector3(
                            Mathf.Clamp(p.x, -inner.x, inner.x),
                            Mathf.Clamp(p.y, -inner.y, inner.y),
                            Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 normal = (p - core).sqrMagnitude > 1e-8f ? (p - core).normalized : n;
                        vertices.Add(core + normal * radius);
                        normals.Add(normal);
                    }
                }
                for (int j = 0; j < segments; j++)
                {
                    for (int i = 0; i < segments; i++)
                    {
                        int a = start + j * (segments + 1) + i;
                        int b = a + 1, c = a + segments + 1, d = c + 1;
                        triangles.Add(a); triangles.Add(c); triangles.Add(b);
                        triangles.Add(b); triangles.Add(c); triangles.Add(d);
                    }
                }
            }

            mesh = new Mesh { name = "RoundedBox" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            cache[key] = mesh;
            return mesh;
        }

        /// <summary>A capped cylinder along the local Z axis (handy for wheels).</summary>
        public static Mesh Cylinder(float radius, float length, int sides = 20)
        {
            string key = "cyl" + radius + length + sides;
            Mesh mesh;
            if (cache.TryGetValue(key, out mesh)) return mesh;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            float h = length * 0.5f;

            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                vertices.Add(dir * radius + Vector3.back * h); normals.Add(dir);
                vertices.Add(dir * radius + Vector3.forward * h); normals.Add(dir);
            }
            for (int i = 0; i < sides; i++)
            {
                int a = i * 2;
                triangles.Add(a); triangles.Add(a + 2); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(a + 2); triangles.Add(a + 3);
            }
            foreach (float z in new[] { -h, h })
            {
                var n = z < 0 ? Vector3.back : Vector3.forward;
                int center = vertices.Count;
                vertices.Add(new Vector3(0, 0, z)); normals.Add(n);
                for (int i = 0; i <= sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    vertices.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, z)); normals.Add(n);
                }
                for (int i = 0; i < sides; i++)
                {
                    if (z < 0) { triangles.Add(center); triangles.Add(center + i + 1); triangles.Add(center + i + 2); }
                    else { triangles.Add(center); triangles.Add(center + i + 2); triangles.Add(center + i + 1); }
                }
            }

            mesh = new Mesh { name = "Cylinder" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            cache[key] = mesh;
            return mesh;
        }
    }
}
