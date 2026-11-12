using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Narrative
{
    // Tank entrance written in code (2026-11-10, "Fable take"): shots cut together, each camera worked out from
    // the level at the moment the sequence starts, so it follows the road, the squad leader and the player
    // wherever they are. The tank keeps its normal road speed, so the fight starts where it would have been.
    // The road runs behind a spur for more than half its length: nobody in the trench can see the tank until it
    // comes round (2026-11-11, measured in play). So the first shots stay out on the road with the tank, and the
    // trench shots (the squad leader calling it, the round landing) are timed from the moment it comes into view.
    //   1  long lens down the road: the silhouette comes on under a flare
    //   2  low at the roadside: the tracks grind past
    //   3  in front of the tank: the turret searches (3b, when there is time: riding behind it towards the spur)
    //   4  over the squad leader's shoulder: it comes round the spur, he calls it. When he stands where the spur
    //      hides it much longer than it does from the player, the shot is taken from the player's place instead
    //      (long lens over the trench wall) and his call is heard from off screen
    //   5  in front of the tank: the turret swings to the trench and fires
    //   6  from the trench: the round lands in front of the wall
    //   7  down behind the player into his own eyes, looking at the tank
    public sealed class TankEntranceFableTake : TankEntranceTake
    {
        const float Shot2 = 3.2f, Shot3 = 6.6f, FrontShot = 2.6f, LeaderLead = 2.2f, LeaderHold = 1.4f;
        const float FireAfter = 1.6f, ImpactShot = 1.5f, HandBack = 2.4f;
        const float RoadSpeed = 3.5f;

        TankEntranceContext level;
        Vector3 road, side;          // along the tank's road towards the trench, and to its right
        float routeLength, hullYaw, trenchSide;
        float shot3b, shot4, seen, shot5, fireAt, shot6, shot7, end;
        bool fromTrench;             // shot 4 is taken from the player's place, not over the squad leader's shoulder
        Vector3 impact, longLensCamera, roadsideCamera, frontCamera, fireCamera, leaderCamera;
        readonly List<TankEntranceCue> cues = new List<TankEntranceCue>();

        public override string Name => "Fable";
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

            // the squad leader's shot waits for the tank to come into his view
            float travel = routeLength / RoadSpeed;
            var toRoad = Hull(travel) - (level.squadLeader + Vector3.up * 1.5f);
            toRoad.y = 0f;
            toRoad.Normalize();
            leaderCamera = level.squadLeader + Vector3.up * 1.68f - toRoad * 1.7f + Vector3.Cross(Vector3.up, toRoad) * 0.5f;
            var trenchCamera = level.playerEye + Vector3.up * 0.25f;
            float seenByLeader = SeenFrom(leaderCamera, travel), seenFromTrench = SeenFrom(trenchCamera, travel);
            fromTrench = seenByLeader > seenFromTrench + 1.5f;
            if (fromTrench) leaderCamera = trenchCamera;
            seen = Mathf.Max(fromTrench ? seenFromTrench : seenByLeader, Shot3 + FrontShot + LeaderLead);
            shot4 = seen - LeaderLead;
            shot3b = shot4 - (Shot3 + FrontShot) >= 1.6f ? Shot3 + FrontShot : shot4; // too short to be worth a cut
            shot5 = seen + LeaderHold;
            fireAt = shot5 + FireAfter;
            shot6 = fireAt + 0.15f;
            shot7 = shot6 + ImpactShot;
            end = shot7 + HandBack;

            // the round lands in the open in front of the trench, on the far side of the wall from the player
            var fromTank = level.playerEye - TankAt(fireAt);
            fromTank.y = 0f;
            impact = level.OnGround(level.playerEye - fromTank.normalized * 11f + Vector3.Cross(Vector3.up, fromTank.normalized) * 3f);

            // 1: far down the road, just off its centre line, raised until the whole run of the shot is in view
            longLensCamera = TankAt(Shot2) + road * 34f + side * (1.6f * trenchSide);
            for (float above = 1.3f; above <= 7f; above += 0.6f)
            {
                longLensCamera = level.OnGround(longLensCamera, above);
                if (level.Clear(longLensCamera, Hull(0f)) && level.Clear(longLensCamera, Hull(Shot2))) break;
            }
            // 2: where the tank will be at the middle of the shot, on the trench side of the road
            roadsideCamera = level.OnGround(TankAt((Shot2 + Shot3) * 0.5f) + side * (4.6f * trenchSide), 0.55f);
            // 3 and 5: low in front of the tank, a little to the trench side
            frontCamera = TankAt(Shot3) + road * 14.5f + side * (3.2f * trenchSide);
            fireCamera = TankAt(shot5) + road * 13f + side * (3.2f * trenchSide);

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

        public override TankEntranceFrame Evaluate(float time)
        {
            var frame = new TankEntranceFrame();
            var tank = TankAt(time);
            var hull = Hull(time);
            frame.tankPosition = tank;
            frame.hullYaw = hullYaw;
            // turret: straight ahead on the march, a look to each side, then round to the trench for the shot
            float toTrench = Yaw(impact - tank);
            float looks = shot3b < shot4 ? 2f : 1f;
            float search = hullYaw - 26f * Mathf.Sin(Mathf.Clamp01((time - Shot3) / Mathf.Max(2.2f, shot4 - Shot3)) * Mathf.PI * looks);
            frame.turretYaw = Mathf.LerpAngle(search, toTrench, Ease((time - (shot5 + 0.2f)) / 1.1f));
            frame.gunPitch = Mathf.Lerp(0f, -1.5f, Ease((time - shot5) / 1.3f));

            if (time < Shot2)
            {
                float u = Ease(time / Shot2);
                Aim(ref frame, longLensCamera, hull + Vector3.up * 0.3f, Mathf.Lerp(13f, 10.5f, u));
            }
            else if (time < Shot3)
            {
                Aim(ref frame, roadsideCamera, tank + Vector3.up * 1.2f, 42f);
                // the ground trembles as it comes up to the camera
                frame.shake = Mathf.InverseLerp(26f, 6f, Vector3.Distance(roadsideCamera, tank)) * 0.7f;
            }
            else if (time < shot3b)
            {
                float u = Ease((time - Shot3) / (shot3b - Shot3));
                Aim(ref frame, level.OnGround(frontCamera, Mathf.Lerp(1.0f, 1.25f, u)), tank + Vector3.up * 2.0f, Mathf.Lerp(36f, 32f, u));
                frame.shake = 0.25f;
            }
            else if (time < shot4)
            {
                // riding behind its shoulder: the road ahead, the spur it is about to come round
                float u = Ease((time - shot3b) / (shot4 - shot3b));
                var from = level.OnGround(tank - road * Mathf.Lerp(8.5f, 7.5f, u) - side * (3.4f * trenchSide), 3.1f);
                Aim(ref frame, from, tank + road * 9f + Vector3.up * 1.7f, 44f);
                frame.shake = 0.2f;
            }
            else if (time < shot5)
            {
                // holds on the place where it will come round, then follows it
                float u = Ease((time - shot4) / (shot5 - shot4));
                var toTank = Hull(Mathf.Max(time, seen + 0.5f)) - leaderCamera;
                var back = new Vector3(toTank.x, 0f, toTank.z).normalized;
                Aim(ref frame, leaderCamera + back * (0.35f * u), leaderCamera + toTank + Vector3.up * 0.2f,
                    fromTrench ? Mathf.Lerp(15f, 11f, u) : Mathf.Lerp(30f, 25f, u));
            }
            else if (time < shot6)
            {
                float u = Ease((time - shot5) / (shot6 - shot5));
                Aim(ref frame, level.OnGround(fireCamera, Mathf.Lerp(1.0f, 1.25f, u)), tank + Vector3.up * 2.0f, Mathf.Lerp(36f, 32f, u));
                frame.shake = 0.25f + (time >= fireAt ? Mathf.Exp(-(time - fireAt) * 6f) : 0f);
            }
            else if (time < shot7)
            {
                var from = level.playerEye + Vector3.up * 0.1f;
                Aim(ref frame, from, impact + Vector3.up * 1.2f, 50f);
                frame.shake = Mathf.Exp(-(time - fireAt) * 2.2f);
            }
            else
            {
                float u = Ease((time - shot7) / (end - shot7 - 1.0f));
                var forward = level.playerView * Vector3.forward;
                var from = level.playerEye - forward * Mathf.Lerp(1.6f, 0f, u) + Vector3.up * Mathf.Lerp(0.55f, 0f, u);
                frame.cameraPosition = from;
                frame.cameraRotation = Quaternion.Slerp(Quaternion.LookRotation(hull - from), level.playerView, u);
                frame.fieldOfView = Mathf.Lerp(46f, 60f, u);
            }
            return frame;
        }

        static void Aim(ref TankEntranceFrame frame, Vector3 from, Vector3 at, float fieldOfView)
        {
            frame.cameraPosition = from;
            frame.cameraRotation = Quaternion.LookRotation(at - from);
            frame.fieldOfView = fieldOfView;
        }

        static float Yaw(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        static float Ease(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }
    }
}
