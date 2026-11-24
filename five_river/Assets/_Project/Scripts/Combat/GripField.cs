using UnityEngine;

namespace FiveRivers.Combat
{
    // How far a point is from the surface of a held weapon's grip, inside negative (2026-11-23, for the first-person
    // fingers to close on the grips whatever their shape: the round fluted front grip of the bazooka, its flat pistol grip
    // and the trigger guard in front of it). A grid of signed distances in the weapon's own frame, baked from its mesh by
    // the setup menu (FiveRivers/Setup/Bazooka Tripo Arms), read with trilinear interpolation.
    public sealed class GripField : ScriptableObject
    {
        public Vector3 origin;          // the first cell's centre, in the weapon's frame
        public float cell;              // cell size, weapon units
        public int nx, ny, nz;
        public float limit;             // distances are clamped to +-limit
        [HideInInspector] public byte[] data;   // short per cell, little endian, distance / limit * 32767

        float At(int x, int y, int z)
        {
            int i = ((z * ny + y) * nx + x) * 2;
            return (short)(data[i] | (data[i + 1] << 8)) * (limit / 32767f);
        }

        // signed distance at a point of the weapon's frame (weapon units); `limit` outside the grid
        public float Sample(Vector3 p)
        {
            if (data == null || nx < 2 || ny < 2 || nz < 2 || cell <= 0f || data.Length < nx * ny * nz * 2) return limit;
            float fx = (p.x - origin.x) / cell, fy = (p.y - origin.y) / cell, fz = (p.z - origin.z) / cell;
            // (written so a NaN falls outside too)
            if (!(fx >= 0f && fy >= 0f && fz >= 0f && fx <= nx - 1 && fy <= ny - 1 && fz <= nz - 1)) return limit;
            int x = Mathf.Min((int)fx, nx - 2), y = Mathf.Min((int)fy, ny - 2), z = Mathf.Min((int)fz, nz - 2);
            float tx = fx - x, ty = fy - y, tz = fz - z;
            float c00 = Mathf.Lerp(At(x, y, z), At(x + 1, y, z), tx), c10 = Mathf.Lerp(At(x, y + 1, z), At(x + 1, y + 1, z), tx);
            float c01 = Mathf.Lerp(At(x, y, z + 1), At(x + 1, y, z + 1), tx), c11 = Mathf.Lerp(At(x, y + 1, z + 1), At(x + 1, y + 1, z + 1), tx);
            return Mathf.Lerp(Mathf.Lerp(c00, c10, ty), Mathf.Lerp(c01, c11, ty), tz);
        }
    }
}
