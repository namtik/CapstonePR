using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Sashindo.VFX
{
    internal static class GuardFractureLayout
    {
        internal readonly struct Cell
        {
            public readonly Vector2[] Points;
            public readonly float[] Arrival;

            public Cell(Vector2[] points, float[] arrival)
            {
                Points = points;
                Arrival = arrival;
            }
        }

        private sealed class Edge
        {
            public readonly int A;
            public readonly int B;
            public readonly bool Outer;
            public int Bend = -1;

            public Edge(int a, int b, bool outer)
            {
                A = a;
                B = b;
                Outer = outer;
            }
        }

        internal static Cell[] Build(Vector2 size, int count, float variation, int seed)
        {
            var random = new Random(seed);
            var sites = CreateSites(size, count, variation, random);
            var vertices = new List<Vector2>();
            var outlines = new List<int[]>(count);
            var edges = new Dictionary<(int, int), Edge>();

            for (int i = 0; i < sites.Length; i++)
            {
                List<Vector2> cell = ClipCell(sites, i, size);
                var outline = new List<int>(cell.Count);
                foreach (Vector2 point in cell)
                {
                    int index = FindVertex(vertices, point, size);
                    if (!outline.Contains(index))
                        outline.Add(index);
                }
                outlines.Add(outline.ToArray());
                for (int p = 0; p < outline.Count; p++)
                {
                    var key = Key(outline[p], outline[(p + 1) % outline.Count]);
                    if (!edges.ContainsKey(key))
                        edges.Add(key, new Edge(key.Item1, key.Item2, OuterEdge(vertices[key.Item1], vertices[key.Item2], size)));
                }
            }

            var links = new List<List<(int index, float length)>>();
            foreach (Vector2 _ in vertices)
                links.Add(new List<(int, float)>());

            int jointCount = vertices.Count;
            foreach (Edge edge in edges.Values)
            {
                if (edge.Outer)
                    continue;
                edge.Bend = AddBend(vertices, edges.Values, edge, size, random);
                if (edge.Bend < 0)
                {
                    Join(links, vertices, edge.A, edge.B);
                    continue;
                }

                links.Add(new List<(int, float)>());
                Join(links, vertices, edge.A, edge.Bend);
                Join(links, vertices, edge.Bend, edge.B);
            }

            float[] timing = TraceCracks(vertices, links, jointCount, size, count, random);
            var output = new Cell[outlines.Count];
            for (int i = 0; i < outlines.Count; i++)
            {
                int[] outline = outlines[i];
                var points = new List<Vector2>(outline.Length * 2);
                var arrival = new List<float>(outline.Length * 2);
                for (int p = 0; p < outline.Length; p++)
                {
                    int start = outline[p];
                    int bend = edges[Key(start, outline[(p + 1) % outline.Length])].Bend;
                    points.Add(vertices[start]);
                    arrival.Add(timing[start]);
                    if (bend >= 0)
                    {
                        points.Add(vertices[bend]);
                        arrival.Add(timing[bend]);
                    }
                }
                output[i] = new Cell(points.ToArray(), arrival.ToArray());
            }

            return output;
        }

        private static Vector2[] CreateSites(Vector2 size, int count, float variation, Random random)
        {
            // Pick spaced locations without an underlying grid, then introduce regions of denser fractures.
            var sites = new Vector2[count];
            int samples = Mathf.RoundToInt(Mathf.Lerp(24, 5, variation));
            for (int i = 0; i < count; i++)
            {
                float bestDistance = -1f;
                for (int candidate = 0; candidate < samples; candidate++)
                {
                    Vector2 position = Vector2.Scale(size, new Vector2(Range(random, 0.025f, 0.975f),
                        Range(random, 0.025f, 0.975f)));
                    float distance = float.PositiveInfinity;
                    for (int j = 0; j < i; j++)
                        distance = Mathf.Min(distance, (position - sites[j]).sqrMagnitude);
                    if (distance > bestDistance)
                    {
                        bestDistance = distance;
                        sites[i] = position;
                    }
                }
            }
            return sites;
        }

        private static List<Vector2> ClipCell(Vector2[] sites, int owner, Vector2 size)
        {
            var polygon = new List<Vector2> { Vector2.zero, new Vector2(0f, size.y), size, new Vector2(size.x, 0f) };
            var clipped = new List<Vector2>();
            for (int i = 0; i < sites.Length && polygon.Count > 0; i++)
            {
                if (i == owner)
                    continue;

                Vector2 normal = sites[i] - sites[owner];
                Vector2 midpoint = (sites[i] + sites[owner]) * 0.5f;
                clipped.Clear();
                Vector2 last = polygon[polygon.Count - 1];
                float lastSide = Vector2.Dot(last - midpoint, normal);
                foreach (Vector2 point in polygon)
                {
                    float side = Vector2.Dot(point - midpoint, normal);
                    if ((lastSide < 0f) != (side < 0f))
                        clipped.Add(Vector2.LerpUnclamped(last, point, lastSide / (lastSide - side)));
                    if (side <= 0f)
                        clipped.Add(point);
                    last = point;
                    lastSide = side;
                }
                (clipped, polygon) = (polygon, clipped);
            }
            return polygon;
        }

        private static int FindVertex(List<Vector2> vertices, Vector2 point, Vector2 size)
        {
            float tolerance = Mathf.Min(size.x, size.y) * 0.00004f;
            for (int i = 0; i < vertices.Count; i++)
            {
                if ((vertices[i] - point).sqrMagnitude <= tolerance * tolerance)
                    return i;
            }
            vertices.Add(point);
            return vertices.Count - 1;
        }

        private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

        internal static bool OuterEdge(Vector2 a, Vector2 b, Vector2 size)
        {
            float tolerance = Mathf.Min(size.x, size.y) * 0.00006f;
            return (a.x < tolerance && b.x < tolerance) || (size.x - a.x < tolerance && size.x - b.x < tolerance)
                || (a.y < tolerance && b.y < tolerance) || (size.y - a.y < tolerance && size.y - b.y < tolerance);
        }

        private static int AddBend(List<Vector2> vertices, ICollection<Edge> edges, Edge edge, Vector2 size, Random random)
        {
            Vector2 a = vertices[edge.A];
            Vector2 b = vertices[edge.B];
            Vector2 axis = b - a;
            float length = axis.magnitude;
            if (length < Mathf.Min(size.x, size.y) * 0.04f)
                return -1;

            Vector2 middle = Vector2.Lerp(a, b, Range(random, 0.36f, 0.64f));
            float clearance = Mathf.Min(size.x, size.y) * 0.065f;
            foreach (Edge other in edges)
            {
                if (other == edge)
                    continue;
                Vector2 p = vertices[other.A];
                Vector2 line = vertices[other.B] - p;
                float along = Mathf.Clamp01(Vector2.Dot(middle - p, line) / line.sqrMagnitude);
                clearance = Mathf.Min(clearance, Vector2.Distance(middle, p + along * line));
            }

            Vector2 normal = new Vector2(-axis.y, axis.x) / length;
            float bend = Mathf.Min(length * 0.085f, clearance * 0.29f) * Range(random, 0.57f, 1f);
            if (bend <= 0f)
                return -1;
            vertices.Add(middle + normal * (random.Next(2) == 0 ? bend : -bend));
            return vertices.Count - 1;
        }

        private static void Join(List<List<(int index, float length)>> links, List<Vector2> vertices, int a, int b)
        {
            float length = Vector2.Distance(vertices[a], vertices[b]);
            links[a].Add((b, length));
            links[b].Add((a, length));
        }

        private static float[] TraceCracks(List<Vector2> vertices, List<List<(int index, float length)>> links,
            int jointCount, Vector2 size, int count, Random random)
        {
            float[] distance = new float[vertices.Count];
            for (int i = 0; i < distance.Length; i++)
                distance[i] = float.PositiveInfinity;
            var sources = new List<int>();
            int sourceCount = count < 14 ? 2 : count < 30 ? 3 : 4;

            // Several separated joints start on their own clocks. Cracks advance along the shared edges.
            for (int i = 0; i < sourceCount; i++)
            {
                int origin = -1;
                float best = -1f;
                for (int j = 0; j < jointCount; j++)
                {
                    if (links[j].Count == 0 || sources.Contains(j))
                        continue;
                    Vector2 p = vertices[j];
                    float margin = Mathf.Min(p.x, size.x - p.x, p.y, size.y - p.y);
                    float spacing = size.magnitude;
                    foreach (int source in sources)
                        spacing = Mathf.Min(spacing, Vector2.Distance(p, vertices[source]));
                    float score = Mathf.Lerp(0.30f, 1f, Mathf.Clamp01(margin / (Mathf.Min(size.x, size.y) * 0.17f)))
                        * Mathf.Lerp(0.34f, 1f, Mathf.Clamp01(spacing / (size.magnitude * 0.38f))) * Range(random, 0.02f, 1f);
                    if (score > best)
                    {
                        origin = j;
                        best = score;
                    }
                }

                if (origin < 0)
                    break;
                sources.Add(origin);
                distance[origin] = i * size.magnitude * Range(random, 0.035f, 0.07f);
            }

            var visited = new bool[vertices.Count];
            float latest = 1f;
            for (int step = 0; step < distance.Length; step++)
            {
                int next = -1;
                float travel = float.PositiveInfinity;
                for (int i = 0; i < distance.Length; i++)
                {
                    if (!visited[i] && distance[i] < travel)
                    {
                        travel = distance[i];
                        next = i;
                    }
                }
                if (next < 0)
                    break;

                visited[next] = true;
                latest = Mathf.Max(latest, travel);
                foreach (var link in links[next])
                    distance[link.index] = Mathf.Min(distance[link.index], travel + link.length);
            }

            for (int i = 0; i < distance.Length; i++)
                distance[i] = float.IsPositiveInfinity(distance[i]) ? 0.9f : distance[i] / latest * 0.9f;
            return distance;
        }

        private static float Range(Random random, float min, float max) =>
            Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}
