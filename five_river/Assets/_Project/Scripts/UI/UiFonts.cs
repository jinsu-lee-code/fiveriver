using TMPro;
using UnityEngine;

namespace FiveRivers.UI
{
    // UI 글꼴. FONT_UI_Sans.ttf · FONT_Letter_Serif.ttf(art 브랜치, OFL)가 연결되면 그것을 쓰고,
    // 그 전에는 Windows 글꼴을 실행 중에만 불러온다 (그레이박스: 시스템 글꼴 파일을 프로젝트에 넣거나 배포하지 않음).
    [DefaultExecutionOrder(-50)]
    public sealed class UiFonts : MonoBehaviour
    {
        [SerializeField] Font uiFontFile;     // FONT_UI_Sans.ttf
        [SerializeField] Font letterFontFile; // FONT_Letter_Serif.ttf
        [SerializeField] string fallbackUiFamily = "Malgun Gothic";
        [SerializeField] string fallbackLetterFamily = "Batang";

        public TMP_FontAsset Ui { get; private set; }
        public TMP_FontAsset Letter { get; private set; }

        void Awake()
        {
            Ui = Create(uiFontFile, fallbackUiFamily);
            Letter = Create(letterFontFile, fallbackLetterFamily) ?? Ui;
            foreach (var t in GetComponentsInChildren<TMP_Text>(true))
                if (Ui != null) t.font = t.GetComponent<LetterFont>() != null ? Letter : Ui;
        }

        static TMP_FontAsset Create(Font file, string family)
        {
            if (file != null) return TMP_FontAsset.CreateFontAsset(file);
            var asset = TMP_FontAsset.CreateFontAsset(family, "Regular");
            if (asset == null) Debug.LogWarning("[UiFonts] FiveRivers: 글꼴을 찾지 못함 " + family);
            return asset;
        }
    }
}
