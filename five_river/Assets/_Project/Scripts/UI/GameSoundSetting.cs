using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FiveRivers.UI
{
    internal static class GameSoundSetting
    {
        const string MutedKey = "FiveRivers.Audio.Muted";
        const string VolumeKey = "FiveRivers.Audio.VolumeBeforeMute";
        static readonly string OnLabel = UiStyle.Key("M") + " 전체 소리: 켜짐";
        static readonly string OffLabel = UiStyle.Key("M") + " 전체 소리: 꺼짐";
        static bool muted;
        static float volumeBeforeMute;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            muted = PlayerPrefs.GetInt(MutedKey, 0) != 0;
            volumeBeforeMute = PlayerPrefs.GetFloat(VolumeKey, AudioListener.volume);
            if (muted) AudioListener.volume = 0f;
        }

        public static TMP_Text AddRowBelow(TMP_Text previous)
        {
            var row = Object.Instantiate(previous, previous.transform.parent);
            row.name = "GameSoundSetting";
            var rect = row.rectTransform;
            rect.anchoredPosition += Vector2.down * (rect.sizeDelta.y + 10f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 360f);
            row.fontSize = 26f;
            row.color = UiStyle.Text;
            row.text = muted ? OffLabel : OnLabel;
            return row;
        }

        public static bool HandleInput(TMP_Text row)
        {
            row.text = muted ? OffLabel : OnLabel;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool pressed = keyboard != null && keyboard.mKey.wasPressedThisFrame;
            if (!pressed && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                var canvas = row.canvas;
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                pressed = RectTransformUtility.RectangleContainsScreenPoint(row.rectTransform, mouse.position.ReadValue(), camera);
            }
            if (!pressed) return false;

            Toggle();
            row.text = muted ? OffLabel : OnLabel;
            return true;
        }

        internal static void Toggle()
        {
            if (!muted)
            {
                volumeBeforeMute = AudioListener.volume;
                PlayerPrefs.SetFloat(VolumeKey, volumeBeforeMute);
            }
            muted = !muted;
            AudioListener.volume = muted ? 0f : volumeBeforeMute;
            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}