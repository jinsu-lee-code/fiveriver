# 그레이박스 음성 (SOUND_LIST 3-8 ~ 3-10 "그레이박스 임시본: TTS 기본 음성")
# Windows 기본 한국어 음성으로 SOUND_LIST 파일명 그대로 WAV(48kHz · 16bit · 모노)를 만든다.
# 이미 있는 파일은 건드리지 않는다 (sound 브랜치에서 받은 최종본 보호).
# 실행: powershell -ExecutionPolicy Bypass -File tools\make_graybox_vo.ps1
param([string]$Out = "$PSScriptRoot\..\five_river\Assets\_Project\Audio\VO")

Add-Type -AssemblyName System.Speech
New-Item -ItemType Directory -Force $Out | Out-Null

# 파일명, 문장, 말 빠르기(-10~10), 음높이(SSML)
$lines = @(
  @("VO_SQD_Scout_01",   "앞에 움직임 있다! 쏴!",            2, "x-low"),
  @("VO_SQD_Flare_01",   "조명탄이다! 엄폐하고 쏴!",          2, "x-low"),
  @("VO_SQD_Flare_02",   "어두워진다. 지금 움직여!",          1, "x-low"),
  @("VO_SQD_Retreat_01", "물러간다. 탄약 챙겨!",              0, "x-low"),
  @("VO_PARK_Call_01",   "만수야.",                           -2, "low"),
  @("VO_PARK_Call_02",   "최만수! 만수 못 봤어?",             -1, "low"),
  @("VO_SQD_Tank_01",    "전차다! 정면은 안 돼, 옆으로 돌아!", 2, "x-low"),
  @("VO_SQD_Tank_02",    "옆구리! 옆구리를 노려!",            2, "x-low"),
  @("VO_SQD_Ally_01",    "엎드려! 지원 온다!",                2, "x-low"),
  @("VO_KPA_Shout_01",   "앞으로!",                           3, "low"),
  @("VO_KPA_Shout_02",   "돌격!",                             3, "medium"),
  @("VO_KPA_Shout_03",   "쏴라!",                             3, "x-low")
)

$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voice = $synth.GetInstalledVoices() | Where-Object { $_.VoiceInfo.Culture.Name -eq "ko-KR" } | Select-Object -First 1
if ($null -eq $voice) { throw "한국어 음성이 없다" }
$synth.SelectVoice($voice.VoiceInfo.Name)
$format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(48000, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)

foreach ($l in $lines) {
  $path = Join-Path $Out ($l[0] + ".wav")
  if (Test-Path $path) { "skip " + $l[0]; continue }
  $synth.Rate = $l[2]
  $ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='ko-KR'><prosody pitch='" + $l[3] + "'>" + $l[1] + "</prosody></speak>"
  $synth.SetOutputToWaveFile($path, $format)
  $synth.SpeakSsml($ssml)
  $synth.SetOutputToNull()
  "made " + $l[0]
}
$synth.Dispose()
