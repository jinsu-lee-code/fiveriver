using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FiveRivers.Combat;
using FiveRivers.Core;
using UnityEngine;

namespace FiveRivers.DebugTools
{
    // Development-only reproducible visual review. Uses the production fire/reload methods.
    public sealed class M1VisualReview : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        M1Rifle rifle;
        bool aiming;
        string output;
        readonly List<string> samples = new List<string>();
        bool explicitEditorReview;
        public static void BeginEditorReview()
        {
            if (!Application.isEditor || !Application.isPlaying) throw new System.InvalidOperationException("Explicit review requires Editor Play mode.");
            var review = new GameObject("M1VisualReview_Explicit").AddComponent<M1VisualReview>();
            review.explicitEditorReview = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (System.Environment.GetCommandLineArgs().Contains("-fiverivers-m1-review")) new GameObject("M1VisualReview").AddComponent<M1VisualReview>();
        }
        IEnumerator Start()
        {
            if (!explicitEditorReview && !System.Environment.GetCommandLineArgs().Contains("-fiverivers-m1-review")) yield break;
            Application.runInBackground=true;
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            output=Path.Combine(Path.GetDirectoryName(Application.dataPath),"M1_VisualReview");
            Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(2f);
            var director=FindFirstObjectByType<FlowDirector>();
            director.Flow.StartRun();director.ClockScale=0f;
            director.Flow.ReportGoalMet(FlowSection.Tutorial);
            rifle=FindFirstObjectByType<M1Rifle>();
            rifle.GetComponent<WeaponHolder>().enabled=false;
            rifle.GetComponent<PlayerController>().enabled=false;
            foreach(var enemy in FindObjectsByType<EnemyAgent>(FindObjectsSortMode.None)) enemy.enabled=false;
            rifle.Model.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture("01_idle_night");
            aiming=true;
            yield return new WaitForSecondsRealtime(.35f);
            yield return Capture("02_ADS_night");
            aiming=false;
            yield return new WaitForSecondsRealtime(.35f);
            rifle.SendMessage("Shoot",false);
            yield return new WaitForSeconds(.025f);
            yield return Capture("03_recoil_peak");
            yield return new WaitForSeconds(.28f);
            yield return Capture("04_recoil_recovered");
            rifle.TryReload();
            yield return CaptureReload("05_partial");
            for(int i=0;i<8;i++)
            {
                rifle.SendMessage("Shoot",false);
                if(i<7)yield return new WaitForSeconds(.24f);
            }
            yield return new WaitForSeconds(.025f);
            yield return Capture("06_last_round_clip_eject");
            yield return CaptureReload("07_empty");
            aiming=true;
            yield return new WaitForSeconds(.3f);
            // Diagnostic neutral illumination; the night captures above remain the shipped lighting.
            var fill=rifle.Model.GetComponentInChildren<Light>();
            if(fill!=null){fill.color=Color.white;fill.intensity=1.1f;}
            yield return Capture("08_ADS_neutral_diagnostic");
            File.WriteAllLines(Path.Combine(output,"samples.txt"),samples);
            Debug.Log("M1_VISUAL_REVIEW_COMPLETE "+output);
            yield return new WaitForSecondsRealtime(.5f);
            Application.Quit();
        }
        void Update()
        {
            if(rifle==null)return;
            rifle.Tick(false,aiming);
            Camera.main.fieldOfView=Mathf.Lerp(70f,50f,rifle.AimBlend);
        }
        IEnumerator CaptureReload(string prefix)
        {
            foreach(float phase in new[]{.02f,.25f,.45f,.50f,.55f,.60f,.65f,.68f,.75f,.90f})
            {
                while(rifle.Reloading && rifle.ReloadProgress<phase)yield return null;
                yield return Capture(prefix+"_"+Mathf.RoundToInt(phase*100).ToString("D2"));
            }
            while(rifle.Reloading)yield return null;
            yield return new WaitForSeconds(.15f);
            yield return Capture(prefix+"_100");
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));
            samples.Add(name+" frame="+Time.frameCount+" time="+Time.time.ToString("F4")+" size="+Screen.width+"x"+Screen.height+" ammo="+rifle.Magazine.InClip+" reload="+rifle.Reloading+" phase="+rifle.ReloadProgress.ToString("F4"));
            yield return null;
        }
#endif
    }
}
