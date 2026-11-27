using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 등에 멘 소총 · 멜빵의 몸 겉면 계산 (부족한 점 7번, 2026-11-27). 순수 계산 (테스트 SlingGeometryTests), 엔진 쪽은 RifleSling.
    // 좌표는 "몸 틀": 가슴 뼈에 붙은 x 오른쪽 · y 위 · z 앞 (미터). 몸 메시(옷 · 장비 포함)를 구운 꼭짓점으로
    // 앞면 · 뒷면 높이 지도(칸마다 가장 바깥 깊이)를 만든다. 여러 번 재서 합치면 칸마다 더 바깥 값이 남는다(숨 · 자세가 바뀌는 첫 프레임).
    public sealed class BodySurface
    {
        public readonly float XMin, XMax, YMin, YMax, Cell;
        readonly int nx, ny;
        readonly float[] front, back; // outward depth per cell (front: z, back: -z); NaN = no surface seen

        public BodySurface(float xMin, float xMax, float yMin, float yMax, float cell)
        {
            XMin = xMin; XMax = xMax; YMin = yMin; YMax = yMax; Cell = Mathf.Max(0.005f, cell);
            nx = Mathf.Max(1, Mathf.CeilToInt((xMax - xMin) / Cell));
            ny = Mathf.Max(1, Mathf.CeilToInt((yMax - yMin) / Cell));
            front = new float[nx * ny]; back = new float[nx * ny];
            for (int i = 0; i < front.Length; i++) front[i] = back[i] = float.NaN;
        }

        public int Columns => nx;
        public int Rows => ny;

        bool CellOf(float x, float y, out int i, out int j)
        {
            i = Mathf.FloorToInt((x - XMin) / Cell); j = Mathf.FloorToInt((y - YMin) / Cell);
            return i >= 0 && j >= 0 && i < nx && j < ny;
        }

        static float Max(float a, float b) => float.IsNaN(a) ? b : float.IsNaN(b) ? a : Mathf.Max(a, b);

        // a point of the body's outer surface
        public void Add(Vector3 p)
        {
            if (!CellOf(p.x, p.y, out int i, out int j)) return;
            int k = j * nx + i;
            if (p.z >= 0f) front[k] = Max(front[k], p.z);
            else back[k] = Max(back[k], -p.z);
        }

        public void AddAll(IEnumerable<Vector3> points) { foreach (var p in points) Add(p); }

        // the outermost of two measurements, cell by cell
        public void Merge(BodySurface other)
        {
            if (other == null || other.nx != nx || other.ny != ny) return;
            for (int k = 0; k < front.Length; k++) { front[k] = Max(front[k], other.front[k]); back[k] = Max(back[k], other.back[k]); }
        }

        // empty cells take the largest of their filled neighbours (a few passes), so a query between sparse vertices has a value
        public void Fill(int passes = 3)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                FillOnce(front);
                FillOnce(back);
            }
        }

        void FillOnce(float[] grid)
        {
            var copy = (float[])grid.Clone();
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i;
                    if (!float.IsNaN(copy[k])) continue;
                    float best = float.NaN;
                    for (int dj = -1; dj <= 1; dj++)
                        for (int di = -1; di <= 1; di++)
                        {
                            int a = i + di, b = j + dj;
                            if (a < 0 || b < 0 || a >= nx || b >= ny) continue;
                            best = Max(best, copy[b * nx + a]);
                        }
                    grid[k] = best;
                }
        }

        // the outward depth at (x, y): the largest of the cell and its neighbours that touch the point (never under-reports a bump)
        bool Sample(float[] grid, float x, float y, out float depth)
        {
            depth = float.NaN;
            if (!CellOf(x, y, out int i, out int j)) return false;
            float fx = (x - XMin) / Cell - i, fy = (y - YMin) / Cell - j;
            int i2 = fx < 0.5f ? i - 1 : i + 1, j2 = fy < 0.5f ? j - 1 : j + 1;
            depth = grid[j * nx + i];
            if (i2 >= 0 && i2 < nx) depth = Max(depth, grid[j * nx + i2]);
            if (j2 >= 0 && j2 < ny) depth = Max(depth, grid[j2 * nx + i]);
            if (i2 >= 0 && i2 < nx && j2 >= 0 && j2 < ny) depth = Max(depth, grid[j2 * nx + i2]);
            return !float.IsNaN(depth);
        }

        public bool Front(float x, float y, out float depth) => Sample(front, x, y, out depth);
        public bool Back(float x, float y, out float depth) => Sample(back, x, y, out depth);

        // how far behind the back surface this point is (negative: inside the body); NaN where the back is not known
        public float BackGap(Vector3 p) => Back(p.x, p.y, out float b) ? -p.z - b : float.NaN;
        public float FrontGap(Vector3 p) => Front(p.x, p.y, out float f) ? p.z - f : float.NaN;

        // The back gap only for a point on the back side of the body (a rifle still in front of him, at port arms, has none:
        // it must not be taken as "deep inside the back" and pushed through him).
        public float BackGapBehind(Vector3 p)
        {
            if (!Back(p.x, p.y, out float b)) return float.NaN;
            bool behind = Front(p.x, p.y, out float f) ? !OnFront(p, f, b) : p.z < 0f;
            return behind ? -p.z - b : float.NaN;
        }

        // Which side of the body a point is on: in front of the middle between the two surfaces, or behind it.
        bool OnFront(Vector3 p, float f, float b) => p.z >= (f - b) * 0.5f;

        // A strap point brought out to clearance over the surface on its side of the body (unchanged where nothing is known).
        public Vector3 PushOut(Vector3 p, float clearance)
        {
            bool hasF = Front(p.x, p.y, out float f), hasB = Back(p.x, p.y, out float b);
            if (!hasF && !hasB) return p;
            if (hasF && hasB ? OnFront(p, f, b) : hasF)
            {
                if (p.z < f + clearance) p.z = f + clearance;
            }
            else if (-p.z < b + clearance) p.z = -(b + clearance);
            return p;
        }

        // Is the point inside the body (by more than tolerance)?
        public bool Inside(Vector3 p, float tolerance)
        {
            bool hasF = Front(p.x, p.y, out float f), hasB = Back(p.x, p.y, out float b);
            if (!hasF || !hasB) return false;
            return p.z < f - tolerance && -p.z < b - tolerance;
        }

        public int CountInside(IEnumerable<Vector3> points, float tolerance)
        {
            int n = 0;
            foreach (var p in points) if (Inside(p, tolerance)) n++;
            return n;
        }
    }

    public static class SlingFit
    {
        // The rifle lies against the back: its points at distances s along it have gaps g behind the back surface.
        // Returns the correction gap'(s) = g + shift + slope * s that lays it along the back (least-squares slope, clamped)
        // with its nearest point exactly at `target` (never inside). count: how many points have a known gap.
        public static bool Solve(IReadOnlyList<float> s, IReadOnlyList<float> gaps, float target, float maxSlope, out float shift, out float slope)
        {
            shift = 0f; slope = 0f;
            int n = 0; float ms = 0f, mg = 0f;
            for (int i = 0; i < gaps.Count; i++) { if (float.IsNaN(gaps[i])) continue; n++; ms += s[i]; mg += gaps[i]; }
            if (n == 0) return false;
            ms /= n; mg /= n;
            if (n >= 2)
            {
                float sxx = 0f, sxy = 0f;
                for (int i = 0; i < gaps.Count; i++)
                {
                    if (float.IsNaN(gaps[i])) continue;
                    sxx += (s[i] - ms) * (s[i] - ms); sxy += (s[i] - ms) * (gaps[i] - mg);
                }
                if (sxx > 1e-6f) slope = Mathf.Clamp(-sxy / sxx, -maxSlope, maxSlope);
            }
            float least = float.MaxValue;
            for (int i = 0; i < gaps.Count; i++) if (!float.IsNaN(gaps[i])) least = Mathf.Min(least, gaps[i] + slope * s[i]);
            shift = target - least;
            return true;
        }

        // the greatest gap after the correction (how far the furthest known point of the rifle stands off the back)
        public static float WorstGap(IReadOnlyList<float> s, IReadOnlyList<float> gaps, float shift, float slope)
        {
            float most = float.NaN;
            for (int i = 0; i < gaps.Count; i++)
                if (!float.IsNaN(gaps[i])) most = float.IsNaN(most) ? gaps[i] + shift + slope * s[i] : Mathf.Max(most, gaps[i] + shift + slope * s[i]);
            return most;
        }
    }
}
