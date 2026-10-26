using System.Collections.Generic;
using UnityEngine;

namespace RiftForge
{
    /// <summary>Late dodges leave up to three spatial charges. A burst detonates each once.</summary>
    public sealed class PhaseImprints
    {
        public const int Capacity = 3;
        public const float Radius = 2.4f;
        private readonly List<Vector3> points = new List<Vector3>();
        public IReadOnlyList<Vector3> Points => points;
        public int Count => points.Count;
        public void Add(Vector3 at)
        {
            at.y = 0f;
            if (points.Count == Capacity) points.RemoveAt(0);
            points.Add(at);
        }
        public Vector3[] Consume() { var result = points.ToArray(); points.Clear(); return result; }
        public void Clear() => points.Clear();
    }
}
