using System;
using System.Collections.Generic;
using System.Linq;
using FiveRivers.Core;
using FiveRivers.Narrative;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // Optional editor-only test helper. Does not save scenes or change authored configuration.
    public static class DawnTruckReview
    {
        static readonly Dictionary<Transform, Vector3> pausedPositions = new Dictionary<Transform, Vector3>();
        static readonly Dictionary<Transform, Quaternion> pausedRotations = new Dictionary<Transform, Quaternion>();
        static double pausedAt;
        static int pauseFrame;
        static bool pauseBaselineReady;
        static float onsetBoneDrift, onsetRootDrift;
        static FlowDirector pauseDirector;
        static FlowDirector Director()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play mode is required.");
            var director = Object.FindFirstObjectByType<FlowDirector>();
            if (director == null) throw new InvalidOperationException("FlowDirector missing.");
            return director;
        }
        static DawnTruckSequence Sequence()
        {
            var sequence = Object.FindFirstObjectByType<DawnTruckSequence>();
            if (sequence == null) throw new InvalidOperationException("Actual truck sequence is missing; fallback walkers do not validate this feature.");
            return sequence;
        }
        public static string StartDawn()
        {
            var director = Director();
            if (director.ClockScale != 1f || director.BaseTimeScale != 1f)
                throw new InvalidOperationException("Restore normal clock/time scale before real-time review.");
            var dawn = Object.FindFirstObjectByType<DawnDirector>();
            if (dawn == null || new SerializedObject(dawn).FindProperty("truckPrefab").objectReferenceValue == null)
                throw new InvalidOperationException("DawnDirector actual truck prefab is not assigned.");
            director.SetPaused(false);
            director.Flow.DebugJumpTo(FlowSection.Dawn);
            var sequence = Sequence();
            if (sequence.PassengerCount != 4) throw new InvalidOperationException("Expected four student passengers.");
            return Snapshot();
        }
        public static string Snapshot()
        {
            var director = Director();
            var sequence = Sequence();
            var students = sequence.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("DawnStudent_", StringComparison.Ordinal)).ToArray();
            string locations = string.Join("; ", students.Select(t => t.name + "=" + t.position.ToString("F2")));
            return "section=" + director.Flow.Section + " sectionTime=" + director.Flow.SectionTime.ToString("F2")
                + " paused=" + director.Flow.Paused + " truck=" + sequence.transform.position.ToString("F2")
                + " stopped=" + sequence.Stopped + " passengers=" + sequence.PassengerCount + " joined=" + sequence.JoinedCount
                + " wheels=" + (sequence.wheelPivots == null ? 0 : sequence.wheelPivots.Length) + " " + locations;
        }
        public static string Pause()
        {
            pauseDirector = Director();
            var sequence = Sequence();
            SavePausePose(sequence);
            pauseFrame = Time.frameCount;
            pauseBaselineReady = false;
            onsetBoneDrift = onsetRootDrift = 0f;
            pauseDirector.SetPaused(true);
            EditorApplication.update -= CapturePauseBaseline;
            EditorApplication.update += CapturePauseBaseline;
            return "Paused; sampling a stable pose after the next Unity frame. Wait at least 1.2 real seconds before CheckPauseAndResume().";
        }
        static void SavePausePose(DawnTruckSequence sequence)
        {
            pausedPositions.Clear(); pausedRotations.Clear();
            foreach (var t in sequence.GetComponentsInChildren<Transform>(true))
            {
                pausedPositions[t] = t.position; pausedRotations[t] = t.rotation;
            }
        }
        static bool IsActorRoot(Transform transform)
        {
            return transform.GetComponent<DawnTruckSequence>() != null
                || transform.name.StartsWith("DawnStudent_", StringComparison.Ordinal);
        }
        static void CapturePauseBaseline()
        {
            if (!EditorApplication.isPlaying || pauseDirector == null || !pauseDirector.Flow.Paused)
            {
                EditorApplication.update -= CapturePauseBaseline;
                return;
            }
            if (Time.frameCount <= pauseFrame) return;
            EditorApplication.update -= CapturePauseBaseline;
            foreach (var pair in pausedPositions)
            {
                if (pair.Key == null) continue;
                float drift = Vector3.Distance(pair.Key.position, pair.Value);
                onsetBoneDrift = Mathf.Max(onsetBoneDrift, drift);
                if (IsActorRoot(pair.Key)) onsetRootDrift = Mathf.Max(onsetRootDrift, drift);
            }
            SavePausePose(Sequence());
            pausedAt = EditorApplication.timeSinceStartup;
            pauseBaselineReady = true;
        }
        public static string CheckPauseAndResume()
        {
            var director = Director();
            if (!director.Flow.Paused || !pauseBaselineReady) throw new InvalidOperationException("Wait for the paused frame baseline.");
            if (EditorApplication.timeSinceStartup - pausedAt < 1.0) throw new InvalidOperationException("Allow at least one real second after the pause baseline.");
            float positionDrift = 0f, angleDrift = 0f, rootDrift = 0f;
            string worstBone = "";
            foreach (var pair in pausedPositions)
            {
                if (pair.Key == null) throw new InvalidOperationException("Truck/passenger destroyed while paused.");
                float drift = Vector3.Distance(pair.Key.position, pair.Value);
                if (drift > positionDrift) { positionDrift = drift; worstBone = pair.Key.name; }
                if (IsActorRoot(pair.Key)) rootDrift = Mathf.Max(rootDrift, drift);
                angleDrift = Mathf.Max(angleDrift, Quaternion.Angle(pair.Key.rotation, pausedRotations[pair.Key]));
            }
            director.SetPaused(false);
            string evidence = "steady root=" + rootDrift + " m, bone=" + positionDrift + " m (" + worstBone + "), angle=" + angleDrift
                + "; onset root=" + onsetRootDrift + " m, bone=" + onsetBoneDrift + " m";
            if (positionDrift > .002f || angleDrift > .2f || onsetRootDrift > .002f)
                throw new InvalidOperationException("Pause drift: " + evidence);
            // Onset bone displacement is retained as evidence, not silently discarded: compare rendered frames
            // to distinguish a command sampled before LateUpdate from an actual visible pose pop.
            return "PASS steady pause; " + evidence + ". Resume complete. Inspect rendered pause-onset frames separately.";
        }
        public static string AssertJoinedBeforeResult()
        {
            var director = Director();
            var sequence = Sequence();
            if (director.Flow.Section != FlowSection.Dawn || sequence.JoinedCount != 4)
                throw new InvalidOperationException("All four must finish joining during Dawn, before Result: " + Snapshot());
            return "PASS four joined before Result. " + Snapshot();
        }
        public static string ResetToTitle()
        {
            Director().Flow.AbortToTitle();
            return "Title requested. On the next frame call AssertCleanTitle().";
        }
        public static string AssertCleanTitle()
        {
            var director = Director();
            var sequences = Object.FindObjectsByType<DawnTruckSequence>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int students = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(t => t.name.StartsWith("DawnStudent_", StringComparison.Ordinal));
            if (director.Flow.Section != FlowSection.Title || sequences.Length != 0 || students != 0)
                throw new InvalidOperationException("Cleanup failed: section=" + director.Flow.Section + " sequences=" + sequences.Length + " students=" + students);
            return "PASS Title: zero truck sequences and zero student instances, including inactive objects.";
        }
    }
}

