var d = UnityEngine.Object.FindAnyObjectByType<FiveRivers.Narrative.TankEntranceDirector>();
if (d == null) return "no director";
var fd = UnityEngine.Object.FindAnyObjectByType<FiveRivers.Core.FlowDirector>();
fd.BaseTimeScale = 1f;
UnityEngine.Time.captureFramerate = 30;
int frame = 0; bool started = false;
UnityEngine.Events.UnityAction cb = null;
cb = () => {
  if (d.Playing) started = true;
  if (!started) return;
  UnityEngine.ScreenCapture.CaptureScreenshot("C:/sk_assets/capture/compare/mixed/f_" + frame.ToString("0000") + ".png"); // every frame (30 fps): at 15 fps the spoked wheels strobe
  frame++;
  if (!d.Playing && frame > 10) { UnityEngine.Application.onBeforeRender -= cb; UnityEngine.Time.captureFramerate = 0; }
};
UnityEngine.Application.onBeforeRender += cb;
d.Preview(3);
return "recording mixed";
