var d = UnityEngine.Object.FindAnyObjectByType<FiveRivers.Narrative.TankEntranceDirector>();
return "playing=" + d.Playing + " t=" + d.Elapsed.ToString("0.0") + " cf=" + UnityEngine.Time.captureFramerate;
