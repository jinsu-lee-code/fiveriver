using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Narrative
{
    // Tank entrance, combined take (2026-11-12): the shots that worked best in the two takes of 11-10/11, with a
    // camera that is held by hand. Worked out from the level when the sequence starts, like the Fable take, but
    // most cameras move, like the Blender take. The tank keeps its road speed on its own road, and the trench
    // shots are timed from the moment it comes out from behind the spur as seen from the player's place.
    //   1  crane (Blender A)        far down the road, up out of the grass while the tank comes on under a flare
    //   2  roadside (Fable 2)       low and close: the wheels and the track belt go past
    //   3  dolly (Blender B)        the tank catches up, passes and pulls ahead; the turret looks to each side
    //   4  trench (Blender C + Fable 4)  up from behind the sandbags on a long lens: it comes round the spur and
    //                               the squad leader calls it
    //   5  over the turret (Blender D)   the gun swings to the trench and fires
    //   6  trench (both)            the round lands in front of the wall, then round to the tank and into the
    //                               player's eyes
    // Camera shake is part of the take (the frame's own shake stays 0): a slow hand-held sway on every shot, a
    // ground rumble that grows as the tank comes close to the camera, and a short kick on the shot and the blast.
    // All of it is gone before the last second, so the view settles into the player's eyes cleanly.
    public sealed class TankEntranceMixedTake : TankEntranceTake
    {
        const float RoadSpeed = 3.5f;
        const float CraneEnd = 3.4f, RoadsideEnd = 6.2f, DollyLeast = 3.0f;
        const float TrenchLead = 2.3f, TrenchHold = 1.2f, FireAfter = 1.7f, ImpactAfter = 0.15f, ImpactHold = 1.4f, Home = 2.0f;
        const float ShakeOff = 1.3f, ShakeFade = 0.6f;   // no shake over the last ShakeOff seconds; it fades out before that

        TankEntranceContext level;
        Vector3 road, side;          // along the tank's road towards the trench, and to its right
        float routeLength, hullYaw, trenchSide;
        float seen, shotTrench, shotTurret, fireAt, shotImpact, end;
        Vector3 impact, craneFrom, craneTo, roadsideCamera, toSpur;
        readonly float[] dollyTime = new float[5], trenchTime = new float[4];
        readonly Vector3[] dollyCamera = new Vector3[5], dollyAim = new Vector3[5], trenchCamera = new Vector3[4], trenchAim = new Vector3[4];
        readonly float[] dollyLens = { 38f, 38f, 40f, 42f, 43f }, trenchLens = { 32f, 24f, 14f, 10.5f };
        readonly List<TankEntranceCue> cues = new List<TankEntranceCue>();

        public override string Name => "합본";
        public override float Duration => end;
        public override IReadOnlyList<TankEntranceCue> Cues => cues;

        public override bool Begin(TankEntranceContext context)
        {
            level = context;
            var route = level.tankHold - level.tankStart;
            route.y = 0f;
            routeLength = route.magnitude;
            if (routeLength < 1f) return false;
            road = route / routeLength;
            side = Vector3.Cross(Vector3.up, road);
            hullYaw = Mathf.Atan2(road.x, road.z) * Mathf.Rad2Deg;
            trenchSide = Vector3.Dot(level.playerEye - level.tankStart, side) >= 0f ? 1f : -1f;

            // the trench shots wait for the tank to come into view from the player's place
            var eye = level.playerEye;
            seen = Mathf.Max(SeenFrom(eye + Vector3.up * 0.25f, routeLength / RoadSpeed), RoadsideEnd + DollyLeast + TrenchLead);
            shotTrench = seen - TrenchLead;
            shotTurret = seen + TrenchHold;
            fireAt = shotTurret + FireAfter;
            shotImpact = fireAt + ImpactAfter;
            end = shotImpact + ImpactHold + Home;

            // the round lands in the open in front of the trench, on the far side of the wall from the player
            var fromTank = eye - TankAt(fireAt);
            fromTank.y = 0f;
            impact = level.OnGround(eye - fromTank.normalized * 11f + Vector3.Cross(Vector3.up, fromTank.normalized) * 3f);

            // 1: at the roadside far down the road, from the grass up to head height
            craneFrom = Along(40f, 2.4f, 0.32f);
            craneTo = Along(38.2f, 2.1f, 2.1f);
            // 2: where the tank will be at the middle of the shot, on the trench side of the road
            roadsideCamera = level.OnGround(TankAt((CraneEnd + RoadsideEnd) * 0.5f) + side * (5f * trenchSide), 0.55f);
            // 3: starts ahead of the tank and is overtaken (abreast it stands well back so the whole tank is in frame)
            float[] part = { 0f, 0.32f, 0.57f, 0.78f, 1f }, lead = { 9.75f, 4.3f, 0f, -3.5f, -7f };
            float[] off = { 5f, 6.6f, 7.6f, 6f, 4.2f }, above = { 0.85f, 1f, 1.2f, 2f, 2.8f }, ahead = { 0f, 0.5f, 1f, 2.5f, 4f };
            for (int i = 0; i < 5; i++)
            {
                float t = Mathf.Lerp(RoadsideEnd, shotTrench, part[i]);
                dollyTime[i] = t;
                dollyCamera[i] = Along(RoadSpeed * t + lead[i], off[i], above[i]);
                dollyAim[i] = Hull(t) + road * ahead[i];
            }
            // 4: behind the sandbags, then up and onto the place where it comes round, then with it
            var emerge = Hull(seen + 0.5f);
            toSpur = emerge - eye;
            toSpur.y = 0f;
            toSpur.Normalize();
            trenchTime[0] = shotTrench;
            trenchTime[1] = seen - 0.6f;
            trenchTime[2] = seen + 0.6f;
            trenchTime[3] = shotTurret;
            trenchCamera[0] = eye - toSpur * 1.1f + Vector3.down * 0.42f;
            trenchCamera[1] = eye - toSpur * 0.5f + Vector3.up * 0.05f;
            trenchCamera[2] = eye - toSpur * 0.2f + Vector3.up * 0.22f;
            trenchCamera[3] = eye + Vector3.up * 0.3f;
            trenchAim[0] = eye + toSpur * 6f + Vector3.down * 0.25f;
            trenchAim[1] = emerge;
            trenchAim[2] = Hull(trenchTime[2]);
            trenchAim[3] = Hull(trenchTime[3]);

            cues.Clear();
            cues.Add(new TankEntranceCue { time = 0.4f, kind = TankEntranceCueKind.Flare });
            cues.Add(new TankEntranceCue { time = seen + 0.3f, kind = TankEntranceCueKind.Line });
            cues.Add(new TankEntranceCue { time = fireAt, kind = TankEntranceCueKind.Fire, point = impact });
            return true;
        }

        // When the tank has come out from behind the ground for good, as seen from this place.
        float SeenFrom(Vector3 camera, float travel)
        {
            float lastHidden = -1f;
            for (float t = 0f; t <= travel; t += 0.1f)
                if (!level.Clear(camera, Hull(t))) lastHidden = t;
            return lastHidden + 0.4f;
        }

        Vector3 TankAt(float time) => level.tankStart + road * Mathf.Min(RoadSpeed * time, routeLength);
        Vector3 Hull(float time) => level.OnGround(TankAt(time), 1.5f); // about the top of the hull
        // a place beside the road: metres from its start, metres out on the trench side, metres above the ground
        Vector3 Along(float distance, float out_, float above) => level.OnGround(level.tankStart + road * distance + side * (out_ * trenchSide), above);

        public override TankEntranceFrame Evaluate(float time)
        {
            var frame = new TankEntranceFrame();
            var tank = TankAt(time);
            var hull = Hull(time);
            frame.tankPosition = tank;
            frame.hullYaw = hullYaw;
            // turret: straight ahead on the march, a look to each side while the dolly passes, then onto the trench
            float u = Mathf.InverseLerp(RoadsideEnd, shotTrench, time);
            float search = hullYaw - 24f * Bump((u - 0.12f) / 0.38f) + 18f * Bump((u - 0.56f) / 0.38f);
            frame.turretYaw = Mathf.LerpAngle(search, Yaw(impact - tank), Ease((time - (shotTurret + 0.15f)) / 1.2f));
            frame.gunPitch = Mathf.Lerp(0f, -1.5f, Ease((time - shotTurret) / 1.3f));

            float sway = 1f, rumble, most = 1f, kick = 0f;
            if (time < CraneEnd)
            {
                u = time / CraneEnd;
                Aim(ref frame, Vector3.Lerp(craneFrom, craneTo, Ease(u)), hull + Vector3.up * Mathf.Lerp(0.4f, 0.1f, u), Mathf.Lerp(15f, 12.5f, u));
                sway = 0.7f;
            }
            else if (time < RoadsideEnd)
            {
                Aim(ref frame, roadsideCamera, level.OnGround(tank, 1.0f), 40f);
            }
            else if (time < shotTrench)
            {
                Aim(ref frame, Curve(dollyTime, dollyCamera, time), Curve(dollyTime, dollyAim, time), Curve(dollyTime, dollyLens, time));
                sway = 1.3f;
            }
            else if (time < shotTurret)
            {
                Aim(ref frame, Curve(trenchTime, trenchCamera, time), Curve(trenchTime, trenchAim, time), Curve(trenchTime, trenchLens, time));
                sway = 0.8f;
            }
            else if (time < shotImpact)
            {
                // behind and above the turret, on the far side from the trench
                float since = time - shotTurret;
                var from = level.OnGround(tank - road * (5.6f - 0.4f * since) - side * (1.9f * trenchSide), 3.3f + 0.15f * since);
                Aim(ref frame, from, Vector3.Lerp(impact, level.OnGround(tank, 2.2f), 0.25f), 40f);
                most = 0.5f; // riding with it: a steady tremble, not the full pass-by rumble
                if (time >= fireAt) kick = Mathf.Exp(-(time - fireAt) * 7f);
            }
            else
            {
                // the blast, then round to the tank and home
                float since = time - shotImpact;
                u = Ease((since - ImpactHold) / (Home - 0.7f));
                var from = level.playerEye + Vector3.up * Mathf.Lerp(0.1f, 0f, u);
                var look = Quaternion.LookRotation(impact + Vector3.up * Mathf.Lerp(1.2f, 1.4f, since / ImpactHold) - from);
                frame.cameraPosition = from;
                frame.cameraRotation = Quaternion.Slerp(look, level.playerView, u);
                frame.fieldOfView = Mathf.Lerp(50f, 60f, u);
                kick = 0.8f * Mathf.Exp(-(time - fireAt) * 5f);
            }
            // the ground trembles as the tank comes up to the camera
            rumble = Mathf.Min(most, Mathf.Pow(Mathf.InverseLerp(26f, 5f, Vector3.Distance(frame.cameraPosition, hull)), 1.6f));
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(end - ShakeOff - ShakeFade, end - ShakeOff, time));
            Shake(ref frame, time, sway * fade, rumble * fade, kick * fade);
            return frame;
        }

        // sway: slow drift of a camera held by hand. rumble: fast tremble of the ground. kick: one sharp blow that
        // rings out. Turns are scaled with the lens, so a long lens does not swim more on screen than a wide one.
        static void Shake(ref TankEntranceFrame frame, float time, float sway, float rumble, float kick)
        {
            float lens = frame.fieldOfView / 40f;
            float slow = time * 0.55f, fast = time * 15f, ring = time * 24f;
            var move = new Vector3(Noise(slow, 1.3f), Noise(slow, 4.7f), 0f) * (0.024f * sway)
                + new Vector3(Noise(fast, 2.9f) * 0.5f, Noise(fast, 7.1f), 0f) * (0.024f * rumble)
                + new Vector3(Noise(ring, 3.3f), Noise(ring, 8.9f), 0f) * (0.09f * kick);
            float pitch = Noise(slow * 1.3f, 11.1f) * 0.55f * sway + Noise(fast, 13.7f) * 0.38f * rumble + (Noise(ring, 17.3f) * 2.4f - 0.9f) * kick;
            float yaw = Noise(slow, 19.9f) * 0.7f * sway + Noise(fast, 23.3f) * 0.28f * rumble + Noise(ring, 29.1f) * 1.8f * kick;
            float roll = Noise(slow * 0.8f, 31.7f) * 0.5f * sway + Noise(fast, 37.9f) * 0.2f * rumble + Noise(ring, 41.3f) * 1.2f * kick;
            frame.cameraPosition += frame.cameraRotation * move;
            frame.cameraRotation *= Quaternion.Euler(pitch * lens, yaw * lens, roll);
        }

        static float Noise(float x, float seed) => Mathf.PerlinNoise(x, seed) * 2f - 1f;

        // Smooth path through keys (cubic Hermite, slopes from the neighbouring keys).
        static Vector3 Curve(float[] times, Vector3[] keys, float time)
        {
            int i = Segment(times, time);
            float span = times[i + 1] - times[i], u = Mathf.Clamp01((time - times[i]) / span);
            var m0 = Slope(times, keys, i) * span;
            var m1 = Slope(times, keys, i + 1) * span;
            float u2 = u * u, u3 = u2 * u;
            return keys[i] * (2f * u3 - 3f * u2 + 1f) + m0 * (u3 - 2f * u2 + u) + keys[i + 1] * (-2f * u3 + 3f * u2) + m1 * (u3 - u2);
        }

        static Vector3 Slope(float[] times, Vector3[] keys, int i)
        {
            int a = Mathf.Max(i - 1, 0), b = Mathf.Min(i + 1, keys.Length - 1);
            return (keys[b] - keys[a]) / (times[b] - times[a]);
        }

        static float Curve(float[] times, float[] keys, float time)
        {
            int i = Segment(times, time);
            return Mathf.Lerp(keys[i], keys[i + 1], Ease((time - times[i]) / (times[i + 1] - times[i])));
        }

        static int Segment(float[] times, float time)
        {
            int i = 0;
            while (i < times.Length - 2 && time >= times[i + 1]) i++;
            return i;
        }

        static void Aim(ref TankEntranceFrame frame, Vector3 from, Vector3 at, float fieldOfView)
        {
            frame.cameraPosition = from;
            frame.cameraRotation = Quaternion.LookRotation(at - from);
            frame.fieldOfView = fieldOfView;
        }

        static float Yaw(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        static float Ease(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }
        static float Bump(float u) { float s = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI); return s * s; }
    }
}
