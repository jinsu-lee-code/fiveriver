using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Narrative
{
    // One moment of a tank entrance sequence: where the sequence camera is and where the tank is.
    public struct TankEntranceFrame
    {
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public float fieldOfView;
        public Vector3 tankPosition; // on the ground plane; the tank takes its height from the terrain
        public float hullYaw;        // world heading, degrees
        public float turretYaw;      // world heading, degrees
        public float gunPitch;       // degrees above the turret's level
        public float shake;          // 0..1 extra camera tremble (engine close by, cannon blast)
    }

    public enum TankEntranceCueKind
    {
        Flare = 0,     // a flare goes up
        Line = 1,      // the squad leader's "tank!" line
        Fire = 2,      // the tank fires; point = where the round lands
    }

    public struct TankEntranceCue
    {
        public float time;
        public TankEntranceCueKind kind;
        public Vector3 point;
    }

    // What a take may use from the level. Filled in by TankEntranceDirector when the sequence starts.
    public sealed class TankEntranceContext
    {
        public Vector3 tankStart, tankHold;      // the tank's road: it must stay on this line so it can carry on afterwards
        public Vector3 playerEye;                // where the sequence hands the view back
        public Quaternion playerView;
        public Vector3 squadLeader;              // Park Yong-pal (feet)
        public Vector3 trenchFront;              // on the ground just in front of the trench wall, facing the road
        public Vector3 mound;                    // the flank mound the player must reach
        public Terrain terrain;

        public float Ground(Vector3 p) => terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : p.y;
        public Vector3 OnGround(Vector3 p, float above = 0f) => new Vector3(p.x, Ground(p) + above, p.z);

        // True when the ground does not rise between the two points (walls and trees are not counted). The tank's
        // road runs behind a spur for most of its length, so a take has to ask before it frames the tank from the trench.
        public bool Clear(Vector3 from, Vector3 to)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(from, to)));
            for (int i = 1; i < steps; i++)
            {
                var p = Vector3.Lerp(from, to, i / (float)steps);
                if (p.y < Ground(p) + 0.2f) return false;
            }
            return true;
        }
    }

    // A tank entrance sequence: from the tank's first appearance until the player is given the fight.
    // Two were made on 2026-11-10 to compare: one written in code (TankEntranceFableTake) and one authored in
    // Blender and baked (TankEntranceBakedTake). A third (2026-11-12, TankEntranceMixedTake) combines their best shots.
    public abstract class TankEntranceTake
    {
        public abstract string Name { get; }
        public abstract float Duration { get; }
        public abstract IReadOnlyList<TankEntranceCue> Cues { get; }
        // false when the take cannot be played (its data is missing)
        public abstract bool Begin(TankEntranceContext context);
        public abstract TankEntranceFrame Evaluate(float time);
    }
}
