using System.IO;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.Narrative;
using FiveRivers.Tutorial;
using FiveRivers.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // 화면 UI (1920×1080 기준). 모던 워페어 HUD 방식 참고: 흰 글자, 얇은 선, 구석 배치, 반투명 띠.
    // 씬의 [UI]는 매번 다시 만든다. 위치 · 크기 값은 화면 배치라 여기 둔다 (수치 데이터 아님).
    static class UiBuilder
    {
        const string UiArtDir = "Assets/_Project/Art/UI/";

        public struct Refs
        {
            public ObjectiveDisplay objective;
            public SubtitleDisplay subtitles;
        }

        public static Refs Build(FlowDirector director, PlayerController player, GameText text, ResultConfig result, TutorialConfig tutorialConfig, TutorialTarget target)
        {
            var old = GameObject.Find("/[UI]");
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject("[UI]", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<UiFonts>();

            var health = player.GetComponent<PlayerHealth>();
            var weapons = player.GetComponent<WeaponHolder>();
            var resupplier = player.GetComponent<Resupplier>();
            var run = Object.FindFirstObjectByType<RunCoordinator>();

            // ---- 전투 HUD ----
            var combat = Panel(root.transform, "Combat");
            var combatGroup = combat.gameObject.AddComponent<CanvasGroup>();
            combatGroup.alpha = 0f;

            var vignette = Img(combat, "DamageVignette", Stretch(), Vector2.zero, Vector2.zero, new Color(0.55f, 0.02f, 0.02f, 0f), Vignette());

            // 목표 (왼쪽 위)
            var objRoot = Rect(combat, "Objective", new Vector2(0, 1), new Vector2(64, -64), new Vector2(760, 90), new Vector2(0, 1));
            var objGroup = objRoot.gameObject.AddComponent<CanvasGroup>();
            var objBody = Rect(objRoot, "Body", new Vector2(0, 1), Vector2.zero, new Vector2(760, 90), new Vector2(0, 1));
            Img(objBody, "Bar", new Vector2(0, 1), new Vector2(0, 0), new Vector2(3, 64), UiStyle.Accent, null, new Vector2(0, 1));
            var objLabel = Txt(objBody, "Label", new Vector2(0, 1), new Vector2(16, 0), new Vector2(700, 26), 18, UiStyle.Accent, TextAlignmentOptions.TopLeft, new Vector2(0, 1));
            objLabel.characterSpacing = 12f;
            var objText = Txt(objBody, "Text", new Vector2(0, 1), new Vector2(16, -28), new Vector2(740, 40), 28, UiStyle.Text, TextAlignmentOptions.TopLeft, new Vector2(0, 1));
            var objective = combat.gameObject.AddComponent<ObjectiveDisplay>();
            Ref(objective, "group", objGroup);
            Ref(objective, "body", objBody);
            Ref(objective, "label", objLabel);
            Ref(objective, "objective", objText);

            // 무기 · 탄약 (오른쪽 아래)
            var weapon = Rect(combat, "Weapon", new Vector2(1, 0), new Vector2(-64, 64), new Vector2(360, 150), new Vector2(1, 0));
            var weaponName = Txt(weapon, "Name", new Vector2(1, 0), new Vector2(0, 112), new Vector2(360, 28), 18, UiStyle.Dim, TextAlignmentOptions.BottomRight, new Vector2(1, 0));
            weaponName.characterSpacing = 8f;
            Img(weapon, "Line", new Vector2(1, 0), new Vector2(0, 104), new Vector2(300, 1), UiStyle.Dim, null, new Vector2(1, 0));
            var ammoMain = Txt(weapon, "Main", new Vector2(1, 0), new Vector2(-96, 18), new Vector2(160, 84), 68, UiStyle.Text, TextAlignmentOptions.BottomRight, new Vector2(1, 0));
            Img(weapon, "Divider", new Vector2(1, 0), new Vector2(-84, 28), new Vector2(1, 44), UiStyle.Dim, null, new Vector2(1, 0));
            var ammoReserve = Txt(weapon, "Reserve", new Vector2(1, 0), new Vector2(0, 24), new Vector2(76, 40), 28, UiStyle.Dim, TextAlignmentOptions.BottomRight, new Vector2(1, 0));
            var pips = new Image[8];
            for (int i = 0; i < pips.Length; i++)
                pips[i] = Img(weapon, "Pip_" + i, new Vector2(1, 0), new Vector2(-300 + i * 11, 0), new Vector2(6, 14), UiStyle.Text, null, new Vector2(1, 0));

            // 조준선 (가운데) · 상호작용 안내
            var cross = Rect(combat, "Crosshair", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80));
            var crossGroup = cross.gameObject.AddComponent<CanvasGroup>();
            var lines = new RectTransform[4];
            lines[0] = Img(cross, "Up", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2, 10), UiStyle.Text, null).rectTransform;
            lines[1] = Img(cross, "Down", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2, 10), UiStyle.Text, null).rectTransform;
            lines[2] = Img(cross, "Left", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 2), UiStyle.Text, null).rectTransform;
            lines[3] = Img(cross, "Right", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 2), UiStyle.Text, null).rectTransform;
            var interact = Txt(combat, "Interact", new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(900, 40), 26, UiStyle.Text, TextAlignmentOptions.Center);

            // 튜토리얼 안내 (가운데 아래) · 남은 행동 (오른쪽 위)
            var tutorial = Rect(combat, "Tutorial", new Vector2(0.5f, 0f), new Vector2(0, 250), new Vector2(900, 140), new Vector2(0.5f, 0f));
            var tutGroup = tutorial.gameObject.AddComponent<CanvasGroup>();
            var promptBox = Img(tutorial, "PromptBox", new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(820, 62), UiStyle.Band, null, new Vector2(0.5f, 1f)).rectTransform;
            var promptText = Txt(promptBox, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 56), 30, UiStyle.Text, TextAlignmentOptions.Center);
            var hintText = Txt(tutorial, "Hint", new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(900, 70), 22, UiStyle.Dim, TextAlignmentOptions.Top, new Vector2(0.5f, 1f));
            var remaining = Rect(combat, "TutorialRemaining", new Vector2(1, 1), new Vector2(-64, -64), new Vector2(520, 200), new Vector2(1, 1));
            var remGroup = remaining.gameObject.AddComponent<CanvasGroup>();
            var remText = Txt(remaining, "Text", new Vector2(1, 1), Vector2.zero, new Vector2(520, 200), 20, UiStyle.Dim, TextAlignmentOptions.TopRight, new Vector2(1, 1));

            var tutorialDirector = combat.gameObject.AddComponent<TutorialDirector>();
            Ref(tutorialDirector, "director", director);
            Ref(tutorialDirector, "config", tutorialConfig);
            Ref(tutorialDirector, "text", text);
            Ref(tutorialDirector, "player", player);
            Ref(tutorialDirector, "weapons", weapons);
            Ref(tutorialDirector, "target", target);
            Ref(tutorialDirector, "promptGroup", tutGroup);
            Ref(tutorialDirector, "promptBox", promptBox);
            Ref(tutorialDirector, "promptText", promptText);
            Ref(tutorialDirector, "hintText", hintText);
            Ref(tutorialDirector, "remainingGroup", remGroup);
            Ref(tutorialDirector, "remainingText", remText);

            // ---- 자막 (아래 가운데, 목표와 다른 위치) ----
            var sub = Rect(root.transform, "Subtitles", new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(1500, 100), new Vector2(0.5f, 0f));
            var subGroup = sub.gameObject.AddComponent<CanvasGroup>();
            Img(sub, "Band", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 100), new Color(0, 0, 0, 0.42f), Gradient(true));
            var subText = Txt(sub, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300, 90), 30, UiStyle.Text, TextAlignmentOptions.Center);
            subText.overflowMode = TextOverflowModes.Ellipsis;
            subText.maxVisibleLines = 2;
            var subtitles = sub.gameObject.AddComponent<SubtitleDisplay>();
            Ref(subtitles, "group", subGroup);
            Ref(subtitles, "text", subText);

            // ---- 화면 어둡게 (사망 · 회차 시작) ----
            var fade = Img(root.transform, "Fade", Stretch(), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0f), null);

            var hud = combat.gameObject.AddComponent<Hud>();
            Ref(hud, "director", director);
            Ref(hud, "player", player);
            Ref(hud, "health", health);
            Ref(hud, "weapons", weapons);
            Ref(hud, "resupplier", resupplier);
            Ref(hud, "run", run);
            Ref(hud, "text", text);
            Ref(hud, "combatGroup", combatGroup);
            Ref(hud, "weaponName", weaponName);
            Ref(hud, "ammoMain", ammoMain);
            Ref(hud, "ammoReserve", ammoReserve);
            RefArray(hud, "clipPips", pips);
            RefArray(hud, "crosshairLines", lines);
            Ref(hud, "crosshairGroup", crossGroup);
            Ref(hud, "interactPrompt", interact);
            Ref(hud, "damageVignette", vignette);
            Ref(hud, "fade", fade);

            // ---- 결과 화면 (S7) ----
            var res = Panel(root.transform, "Result");
            var resGroup = res.gameObject.AddComponent<CanvasGroup>();
            resGroup.alpha = 0f;
            Img(res, "Background", Stretch(), Vector2.zero, Vector2.zero, new Color(0.02f, 0.02f, 0.025f, 1f), null);
            var dateRt = Rect(res, "Date", new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(1200, 50));
            var dateText = Txt(dateRt, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 50), 30, UiStyle.Dim, TextAlignmentOptions.Center);
            dateText.characterSpacing = 10f;
            var nameRt = Rect(res, "Name", new Vector2(0.5f, 0.5f), new Vector2(0, 210), new Vector2(1200, 90));
            var nameText = Txt(nameRt, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 90), 60, UiStyle.Text, TextAlignmentOptions.Center);
            var letterRt = Rect(res, "Letter", new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1100, 560));
            Img(letterRt, "Paper", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 560), new Color(0.80f, 0.74f, 0.60f), null);
            var letterText = Txt(letterRt, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940, 440), 32, new Color(0.16f, 0.13f, 0.10f), TextAlignmentOptions.MidlineLeft);
            letterText.lineSpacing = 18f;
            letterText.gameObject.AddComponent<LetterFont>();
            var titleRt = Rect(res, "Title", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 140));
            var resultTitle = Txt(titleRt, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 140), 84, UiStyle.Text, TextAlignmentOptions.Center);
            resultTitle.characterSpacing = 16f;
            var resultScreen = res.gameObject.AddComponent<ResultScreen>();
            Ref(resultScreen, "director", director);
            Ref(resultScreen, "config", result);
            Ref(resultScreen, "text", text);
            Ref(resultScreen, "group", resGroup);
            Ref(resultScreen, "dateGroup", dateRt.gameObject.AddComponent<CanvasGroup>());
            Ref(resultScreen, "nameGroup", nameRt.gameObject.AddComponent<CanvasGroup>());
            Ref(resultScreen, "letterGroup", letterRt.gameObject.AddComponent<CanvasGroup>());
            Ref(resultScreen, "titleGroup", titleRt.gameObject.AddComponent<CanvasGroup>());
            Ref(resultScreen, "date", dateText);
            Ref(resultScreen, "nameText", nameText);
            Ref(resultScreen, "letter", letterText);
            Ref(resultScreen, "title", resultTitle);

            // ---- 시작 화면 ----
            var title = Panel(root.transform, "Title");
            var titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            Img(title, "Shade", new Vector2(0, 0.5f), Vector2.zero, new Vector2(1000, 1080), new Color(0, 0, 0, 0.85f), Gradient(false), new Vector2(0, 0.5f));
            var gameTitle = Txt(title, "GameTitle", new Vector2(0, 0.5f), new Vector2(140, 150), new Vector2(900, 130), 96, UiStyle.Text, TextAlignmentOptions.Left, new Vector2(0, 0.5f));
            gameTitle.characterSpacing = 6f;
            Img(title, "Line", new Vector2(0, 0.5f), new Vector2(144, 78), new Vector2(420, 2), UiStyle.Accent, null, new Vector2(0, 0.5f));
            var gameSub = Txt(title, "Subtitle", new Vector2(0, 0.5f), new Vector2(144, 40), new Vector2(900, 50), 28, UiStyle.Dim, TextAlignmentOptions.Left, new Vector2(0, 0.5f));
            gameSub.characterSpacing = 8f;
            var startPrompt = Txt(title, "Start", new Vector2(0, 0.5f), new Vector2(144, -120), new Vector2(900, 50), 32, UiStyle.Text, TextAlignmentOptions.Left, new Vector2(0, 0.5f));
            var opHint = Txt(title, "OperatorHint", new Vector2(0, 0), new Vector2(48, 36), new Vector2(1200, 30), 16, UiStyle.Dim, TextAlignmentOptions.BottomLeft, new Vector2(0, 0));
            var titleScreen = title.gameObject.AddComponent<TitleScreen>();
            Ref(titleScreen, "director", director);
            Ref(titleScreen, "text", text);
            Ref(titleScreen, "group", titleGroup);
            Ref(titleScreen, "title", gameTitle);
            Ref(titleScreen, "subtitle", gameSub);
            Ref(titleScreen, "startPrompt", startPrompt);
            Ref(titleScreen, "operatorHint", opHint);

            // ---- 일시정지 ----
            var pause = Panel(root.transform, "Pause");
            var pauseGroup = pause.gameObject.AddComponent<CanvasGroup>();
            pauseGroup.alpha = 0f;
            Img(pause, "Dim", Stretch(), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.65f), null);
            var pauseTitle = Txt(pause, "Title", new Vector2(0, 0.5f), new Vector2(140, 90), new Vector2(800, 70), 52, UiStyle.Text, TextAlignmentOptions.Left, new Vector2(0, 0.5f));
            Img(pause, "Line", new Vector2(0, 0.5f), new Vector2(144, 44), new Vector2(360, 2), UiStyle.Accent, null, new Vector2(0, 0.5f));
            var resume = Txt(pause, "Resume", new Vector2(0, 0.5f), new Vector2(144, -10), new Vector2(900, 44), 28, UiStyle.Text, TextAlignmentOptions.Left, new Vector2(0, 0.5f));
            var restart = Txt(pause, "Restart", new Vector2(0, 0.5f), new Vector2(144, -60), new Vector2(900, 44), 28, UiStyle.Dim, TextAlignmentOptions.Left, new Vector2(0, 0.5f));
            var pauseMenu = pause.gameObject.AddComponent<PauseMenu>();
            Ref(pauseMenu, "director", director);
            Ref(pauseMenu, "player", player);
            Ref(pauseMenu, "text", text);
            Ref(pauseMenu, "group", pauseGroup);
            Ref(pauseMenu, "title", pauseTitle);
            Ref(pauseMenu, "resume", resume);
            Ref(pauseMenu, "restart", restart);

            return new Refs { objective = objective, subtitles = subtitles };
        }

        // ---- 만들기 도구 ----
        static (Vector2 min, Vector2 max) Stretch() => (Vector2.zero, Vector2.one);

        static RectTransform Panel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static Image Img(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, Sprite sprite, Vector2? pivot = null)
        {
            var rt = Rect(parent, name, anchor, pos, size, pivot);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        static Image Img(Transform parent, string name, (Vector2 min, Vector2 max) stretch, Vector2 offMin, Vector2 offMax, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = stretch.min;
            rt.anchorMax = stretch.max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Txt(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, Color color, TextAlignmentOptions align, Vector2? pivot = null)
        {
            var rt = Rect(parent, name, anchor, pos, size, pivot);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.richText = true;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.text = "";
            return t;
        }

        static void Ref(Object target, string field, Object value) => SetupUtil.SetRef(target, field, value);

        static void RefArray<T>(Object target, string field, T[] values) where T : Object
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- UI 그림 (코드로 만든 그레이박스 텍스처) ----
        static Sprite Vignette() => GeneratedSprite("T_UI_DamageVignette", 256, (x, y) =>
        {
            float dx = x - 0.5f, dy = y - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.707f;
            return Mathf.Clamp01((d - 0.55f) / 0.45f);
        });

        static Sprite Gradient(bool horizontalBand) => horizontalBand
            ? GeneratedSprite("T_UI_BandFade", 256, (x, y) => Mathf.Clamp01(1f - Mathf.Abs(x - 0.5f) * 2f) * 1.6f)
            : GeneratedSprite("T_UI_TitleShade", 256, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - x), 1.6f));

        static Sprite GeneratedSprite(string name, int size, System.Func<float, float, float> alpha)
        {
            string path = UiArtDir + name + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;
            GrayboxAssets.EnsureFolder(UiArtDir);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size))));
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
