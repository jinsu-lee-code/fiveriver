using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FiveRivers.Core;
using FiveRivers.Narrative;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // Temporary editorial capture only: no lighting, game camera, authored scene or time scale changes.
    public static class DawnTruckCapture
    {
        const string Output = @"C:\Users\mbc\Documents\ChatGPT\다섯개의강\DawnTruck_20261102";
        const int Width = 960, Height = 540;
        static double interval = .125;
        static readonly int[] Stages = { 0, 4, 8, 12, 18, 20 };
        static readonly List<double> times = new List<double>();
        static readonly StringBuilder telemetry = new StringBuilder();
        static Camera camera;
        static RenderTexture target;
        static Texture2D readback;
        static FlowDirector director;
        static double runStart, nextAt;
        static int lastFrame = -1, stage, warmupPhase;
        static bool running;
        public static string Status { get; private set; } = "Not started";

        public static string Begin(Vector3 position, Vector3 lookAt, float fieldOfView = 60f, int excludedLayers = 0, double sampleInterval = .125)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            if (running) throw new InvalidOperationException("Capture already running.");
            if (sampleInterval < 1.0 / 30.0 || sampleInterval > 1.0)
                throw new ArgumentOutOfRangeException(nameof(sampleInterval), "Use an interval between 1/30 and 1 second.");
            interval = sampleInterval;
            var source = Camera.main;
            director = Object.FindFirstObjectByType<FlowDirector>();
            if (source == null || director == null) throw new InvalidOperationException("Game camera/FlowDirector missing.");
            if (director.ClockScale != 1f || director.BaseTimeScale != 1f || director.Flow.Paused)
                throw new InvalidOperationException("Use normal unpaused time for the actual twenty-second sequence.");
            string folder = Path.Combine(Output, "review_frames");
            Directory.CreateDirectory(folder);
            var go = new GameObject("DawnTruckEditorialCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
            camera = go.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            camera.tag = "Untagged";
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
            camera.fieldOfView = fieldOfView;
            camera.aspect = Width / (float)Height;
            camera.cullingMask = source.cullingMask & ~excludedLayers;
            var originalData = source.GetUniversalAdditionalCameraData();
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = originalData.renderPostProcessing;
            data.renderShadows = originalData.renderShadows;
            data.antialiasing = originalData.antialiasing;
            data.antialiasingQuality = originalData.antialiasingQuality;
            data.volumeLayerMask = originalData.volumeLayerMask;
            data.volumeTrigger = originalData.volumeTrigger;
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "DawnTruckReviewRT" };
            target.Create();
            readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            times.Clear(); telemetry.Clear();
            telemetry.AppendLine("frame,time,section,sectionTime,passengers,joined,stopped,truckX,truckY,truckZ");
            stage = 0; lastFrame = -1; nextAt = 0; warmupPhase = 0;
            try
            {
                DawnTruckReview.StartDawn();
                runStart = director.Flow.RunTime;
                running = true;
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                AssemblyReloadEvents.beforeAssemblyReload += Cancel;
                Status = "Warming actual truck shaders before a fresh Dawn recording.";
                return Status;
            }
            catch { Cleanup(); throw; }
        }

        public static string Still(Vector3 position, Vector3 lookAt, float fieldOfView, string path, int excludedLayers = 0)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Live Play mode required for the actual scene still.");
            if (running) throw new InvalidOperationException("Finish the continuous capture before a still render.");
            var source = Camera.main;
            if (source == null) throw new InvalidOperationException("Game camera missing.");
            var go = new GameObject("DawnTruckEditorialStillCamera") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture stillTarget = null;
            Texture2D stillReadback = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                var stillCamera = go.AddComponent<Camera>();
                stillCamera.CopyFrom(source);
                stillCamera.enabled = false;
                stillCamera.tag = "Untagged";
                stillCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
                stillCamera.fieldOfView = fieldOfView;
                stillCamera.aspect = Width / (float)Height;
                stillCamera.cullingMask = source.cullingMask & ~excludedLayers;
                var originalData = source.GetUniversalAdditionalCameraData();
                var data = stillCamera.GetUniversalAdditionalCameraData();
                data.renderType = CameraRenderType.Base;
                data.renderPostProcessing = originalData.renderPostProcessing;
                data.renderShadows = originalData.renderShadows;
                data.antialiasing = originalData.antialiasing;
                data.antialiasingQuality = originalData.antialiasingQuality;
                data.volumeLayerMask = originalData.volumeLayerMask;
                data.volumeTrigger = originalData.volumeTrigger;
                stillTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                stillTarget.Create();
                RenderPipeline.SubmitRenderRequest(stillCamera, new UniversalRenderPipeline.SingleCameraRequest { destination = stillTarget });
                RenderTexture.active = stillTarget;
                stillReadback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                stillReadback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllBytes(path, stillReadback.EncodeToPNG());
                return Path.GetFullPath(path);
            }
            finally
            {
                RenderTexture.active = previous;
                if (stillReadback != null) Object.DestroyImmediate(stillReadback);
                if (stillTarget != null) { stillTarget.Release(); Object.DestroyImmediate(stillTarget); }
                Object.DestroyImmediate(go);
            }
        }
        static void Tick()
        {
            if (!running) return;
            if (!EditorApplication.isPlaying || director == null) { Cancel(); return; }
            if (lastFrame == Time.frameCount) return;
            // First render can compile shaders for several seconds. Warm it before measuring the flow.
            if (warmupPhase < 2)
            {
                lastFrame = Time.frameCount;
                if (warmupPhase == 0)
                {
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    warmupPhase = 1;
                    Status = "Shaders warmed; restarting Dawn on the next Unity frame.";
                }
                else
                {
                    DawnTruckReview.StartDawn();
                    runStart = director.Flow.RunTime;
                    warmupPhase = 2;
                }
                return;
            }
            double elapsed = director.Flow.RunTime - runStart;
            if (elapsed + .0001 < nextAt) return;
            lastFrame = Time.frameCount;
            try
            {
                var sequence = Object.FindFirstObjectByType<DawnTruckSequence>();
                if (sequence == null) throw new InvalidOperationException("Actual truck sequence disappeared during capture.");
                // Exactly one additional scene render per sampled Unity frame. Reuse RT/texture allocations.
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                var previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                }
                finally { RenderTexture.active = previous; }
                byte[] png = readback.EncodeToPNG();
                int index = times.Count;
                File.WriteAllBytes(Path.Combine(Output, "review_frames", "frame_" + index.ToString("D4") + ".png"), png);
                times.Add(elapsed);
                while (stage < Stages.Length && elapsed >= Stages[stage])
                {
                    File.WriteAllBytes(Path.Combine(Output, "stage_" + Stages[stage].ToString("D2") + "s.png"), png);
                    stage++;
                }
                Vector3 p = sequence.transform.position;
                telemetry.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:F4},{2},{3:F4},{4},{5},{6},{7:F3},{8:F3},{9:F3}",
                    index, elapsed, director.Flow.Section, director.Flow.SectionTime, sequence.PassengerCount, sequence.JoinedCount, sequence.Stopped, p.x, p.y, p.z));
                // Never issue catch-up renders from the same frame if capture cost reduced frame rate.
                nextAt = (Math.Floor(elapsed / interval) + 1) * interval;
                Status = "Recording: " + elapsed.ToString("F1") + " s, " + times.Count + " frames, joined " + sequence.JoinedCount;
                if (elapsed >= 20.05) Finish();
            }
            catch (Exception exception)
            {
                Status = "Capture failed: " + exception;
                File.WriteAllText(Path.Combine(Output, "capture_error.txt"), Status);
                Cleanup();
                Debug.LogError(Status);
            }
        }

        static void Finish()
        {
            var concat = new StringBuilder("ffconcat version 1.0\n");
            for (int i = 0; i < times.Count; i++)
            {
                concat.AppendLine("file 'frame_" + i.ToString("D4") + ".png'");
                double duration = i + 1 < times.Count ? times[i + 1] - times[i] : interval;
                concat.AppendLine("duration " + duration.ToString("F6", CultureInfo.InvariantCulture));
            }
            concat.AppendLine("file 'frame_" + (times.Count - 1).ToString("D4") + ".png'");
            File.WriteAllText(Path.Combine(Output, "review_frames", "frames.ffconcat"), concat.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(Output, "capture_telemetry.csv"), telemetry.ToString(), new UTF8Encoding(false));
            Status = "Complete: " + times.Count + " frames / " + times[times.Count - 1].ToString("F2") + " seconds. Run EncodeTruckReview.ps1.";
            File.WriteAllText(Path.Combine(Output, "capture_status.txt"), Status);
            File.WriteAllText(Path.Combine(Output, "capture_fps.txt"), (1.0 / interval).ToString("F3", CultureInfo.InvariantCulture));
            Cleanup();
            Debug.Log("[DawnTruckCapture] " + Status);
        }

        public static void Cancel()
        {
            if (running) Status = "Capture cancelled before completion.";
            Cleanup();
        }
        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) Cancel();
        }
        static void Cleanup()
        {
            running = false;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
            if (camera != null) Object.DestroyImmediate(camera.gameObject);
            if (readback != null) Object.DestroyImmediate(readback);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            camera = null; readback = null; target = null;
        }
    }
}



