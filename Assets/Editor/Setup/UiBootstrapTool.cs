using Blast.Levels;
using Blast.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Blast.EditorTools
{
    /// <summary>
    /// Builds the menu and level interfaces.
    ///
    /// Generated in code alongside the rest of the scene setup so the whole project can be rebuilt
    /// from source. The panel positions inside the top bar are expressed as fractions of the artwork
    /// rather than pixel offsets, because they were measured from the supplied image and stay correct
    /// however the bar is scaled.
    /// </summary>
    public static class UiBootstrapTool
    {
        private const string MenuButtonPath = "Assets/Art/UI/Menu/button.png";
        private const string MenuButtonFramePath = "Assets/Art/UI/Menu/button_frame.png";
        private const string TopBarPath = "Assets/Art/UI/Gameplay/Top/top_ui.png";
        private const string GoalCheckPath = "Assets/Art/UI/Gameplay/Top/goal_check.png";
        private const string PopupBasePath = "Assets/Art/UI/Gameplay/Popup/popup_base.png";
        private const string PopupRibbonPath = "Assets/Art/UI/Gameplay/Popup/popup_ribbon.png";
        private const string CloseButtonPath = "Assets/Art/UI/Gameplay/Popup/close_button.png";
        private const string StarPath = "Assets/Art/UI/Gameplay/Celebration/star.png";
        private const string SparklePath = "Assets/Art/UI/Gameplay/Celebration/Particles/additive_particle_star.png";

        private const string VaseIconPath = "Assets/Art/Obstacles/Vase/vase_01.png";
        private const string StoneIconPath = "Assets/Art/Obstacles/Stone/stone.png";
        private const string ChaliceIconPath = "Assets/Art/Obstacles/ChaliceBox/Chalice.png";

        /// <summary>Top bar artwork is 1125x650; the bar keeps that aspect at the canvas width.</summary>
        private const float TopBarAspect = 650f / 1125f;

        /// <summary>
        /// The two cream panels inside the bar, measured from the artwork and expressed as anchor
        /// fractions so they track the bar at any size.
        /// </summary>
        private static readonly Rect GoalPanelAnchors = Rect.MinMaxRect(0.051f, 0.263f, 0.265f, 0.634f);

        private static readonly Rect MovePanelAnchors = Rect.MinMaxRect(0.737f, 0.263f, 0.951f, 0.634f);
        private static readonly Rect GoalTabAnchors = Rect.MinMaxRect(0.055f, 0.78f, 0.26f, 0.95f);
        private static readonly Rect MoveTabAnchors = Rect.MinMaxRect(0.74f, 0.78f, 0.945f, 0.95f);

        /// <summary>Dark brown, matching the numerals printed on the artwork's cream panels.</summary>
        private static readonly Color PanelTextColor = new(0.29f, 0.16f, 0.05f);

        /// <summary>
        /// Behind a popup or the celebration. The project renders in linear colour space, where a
        /// given alpha darkens far less than the same value would in gamma, hence the high value.
        /// </summary>
        private static readonly Color DimColor = new(0.02f, 0f, 0.06f, 0.8f);

        private static Font _font;

        /// <summary>Unity's built-in font. TextMeshPro's essential resources are not imported.</summary>
        private static Font Font =>
            _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ------------------------------------------------------------------ main menu

        /// <summary>Adds the level button and returns the controller that drives it.</summary>
        public static MainMenuController BuildMainMenu(Canvas canvas)
        {
            // Proportions taken from the case study's reference screenshot: a wide button sitting low
            // over the area artwork.
            var button = CreateLabelledButton("LevelButton", (RectTransform)canvas.transform, "Level 1", new Vector2(470f, 180f));
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 280f);

            var label = button.GetComponentInChildren<Text>();

            var controller = new GameObject("MainMenu").AddComponent<MainMenuController>();
            ProjectBootstrapTool.Wire(controller,
                ("_levelDatabase", AssetDatabase.LoadAssetAtPath<LevelDatabase>(GameAssetsBootstrapTool.LevelDatabasePath)),
                ("_levelButton", button),
                ("_levelLabel", label));

            return controller;
        }

        // ------------------------------------------------------------------ level UI

        public sealed class LevelUi
        {
            public TopBarView TopBar;
            public FailPopup FailPopup;
            public WinCelebration WinCelebration;
            public ChaliceFlightView ChaliceFlight;
        }

        public static LevelUi BuildLevelUi(Canvas canvas)
        {
            return new LevelUi
            {
                TopBar = BuildTopBar(canvas),
                FailPopup = BuildFailPopup(canvas),
                WinCelebration = BuildWinCelebration(canvas),

                // Added last so flying chalices draw over the bar they are heading for.
                ChaliceFlight = BuildChaliceFlight(canvas)
            };
        }

        private static TopBarView BuildTopBar(Canvas canvas)
        {
            var bar = CreateImage("TopBar", canvas.transform, TopBarPath);
            var rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;

            // Width follows the canvas; height keeps the artwork's aspect at the reference width.
            rect.sizeDelta = new Vector2(0f, 1080f * TopBarAspect);

            CreateLabelInPanel(rect, "GoalTab", GoalTabAnchors, "Goal", 44);
            CreateLabelInPanel(rect, "MoveTab", MoveTabAnchors, "Move", 44);

            var goalPanel = CreatePanel(rect, "GoalPanel", GoalPanelAnchors);
            var grid = goalPanel.gameObject.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(4f, 4f);
            grid.childAlignment = TextAnchor.MiddleCenter;

            // Wraps to a second row at three goals. The cell size is set by TopBarView from how many
            // goals the level actually has, so a single goal fills the panel.
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.cellSize = new Vector2(106f, 106f);

            var vase = CreateGoalItem(goalPanel, "VaseGoal", VaseIconPath);
            var stone = CreateGoalItem(goalPanel, "StoneGoal", StoneIconPath);
            var chalice = CreateGoalItem(goalPanel, "ChaliceGoal", ChaliceIconPath);

            var movePanel = CreatePanel(rect, "MovePanel", MovePanelAnchors);
            var moveLabel = CreateText("MoveLabel", movePanel, "0", 92, TextAnchor.MiddleCenter);
            moveLabel.color = PanelTextColor;
            moveLabel.GetComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.35f);
            Stretch(moveLabel.rectTransform, 0f);

            var view = bar.gameObject.AddComponent<TopBarView>();
            ProjectBootstrapTool.Wire(view,
                ("_moveLabel", moveLabel),
                ("_goalLayout", grid),
                ("_vaseGoal", vase),
                ("_stoneGoal", stone),
                ("_chaliceGoal", chalice));

            return view;
        }

        private static GoalItemView CreateGoalItem(RectTransform parent, string name, string iconPath)
        {
            var root = CreateRect(name, parent);

            var icon = CreateImage("Icon", root, iconPath);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform, 6f);

            // Sits in the corner rather than under the icon, so the icon can fill its slot.
            var count = CreateText("Count", root, "0", 38, TextAnchor.LowerRight);
            count.color = PanelTextColor;
            count.GetComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.9f);
            Stretch(count.rectTransform, 0f);

            // Overlaps the icon's bottom-right corner, as in the reference screenshot, rather than
            // replacing the icon: the player still sees which goal was completed.
            var check = CreateImage("Check", root, GoalCheckPath);
            check.preserveAspect = true;
            check.rectTransform.anchorMin = new Vector2(1f, 0f);
            check.rectTransform.anchorMax = new Vector2(1f, 0f);
            check.rectTransform.pivot = new Vector2(1f, 0f);
            check.rectTransform.sizeDelta = new Vector2(47f, 43f);
            check.rectTransform.anchoredPosition = new Vector2(4f, -2f);
            check.enabled = false;

            var view = root.gameObject.AddComponent<GoalItemView>();
            ProjectBootstrapTool.Wire(view, ("_icon", icon), ("_countLabel", count), ("_checkmark", check));
            return view;
        }

        private static FailPopup BuildFailPopup(Canvas canvas)
        {
            var root = CreateRect("FailPopup", canvas.transform);
            Stretch(root, 0f);

            // Also swallows taps, so the board cannot be played behind the popup.
            var dim = CreateSolid("Dim", root, DimColor);
            Stretch(dim.rectTransform, 0f);

            // popup_base.png is 1193x1594; the panel keeps that aspect so the art is never distorted.
            var panel = CreateRect("Panel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(620f, 828f);

            var basePanel = CreateImage("Base", panel, PopupBasePath);
            Stretch(basePanel.rectTransform, 0f);

            // Straddles the top edge of the base, which is how the two pieces are drawn to sit.
            var ribbon = CreateImage("Ribbon", panel, PopupRibbonPath);
            ribbon.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            ribbon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            ribbon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ribbon.rectTransform.sizeDelta = new Vector2(590f, 200f);
            ribbon.rectTransform.anchoredPosition = new Vector2(0f, -24f);

            var title = CreateText("Title", ribbon.rectTransform, "Out of Moves", 54, TextAnchor.MiddleCenter);
            Stretch(title.rectTransform, 0f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -4f);

            var message = CreateText("Message", panel, "You ran out of moves!", 48, TextAnchor.MiddleCenter);
            message.rectTransform.anchorMin = message.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            message.rectTransform.sizeDelta = new Vector2(540f, 200f);
            message.rectTransform.anchoredPosition = new Vector2(0f, 40f);

            var close = CreateIconButton("CloseButton", panel, CloseButtonPath, new Vector2(104f, 104f));
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-8f, -8f);

            var retry = CreateLabelledButton("RetryButton", panel, "Try Again", new Vector2(430f, 156f));
            var retryRect = retry.GetComponent<RectTransform>();
            retryRect.anchorMin = new Vector2(0.5f, 0f);
            retryRect.anchorMax = new Vector2(0.5f, 0f);
            retryRect.anchoredPosition = new Vector2(0f, 140f);

            var popup = root.gameObject.AddComponent<FailPopup>();
            ProjectBootstrapTool.Wire(popup,
                ("_root", root.gameObject),
                ("_panel", panel),
                ("_closeButton", close),
                ("_retryButton", retry));

            return popup;
        }

        private static WinCelebration BuildWinCelebration(Canvas canvas)
        {
            var root = CreateRect("WinCelebration", canvas.transform);
            Stretch(root, 0f);

            // The whole screen is the continue button, matching the reference's "Tap To Continue".
            // Its image doubles as the dim, so there is only one full-screen graphic.
            var dim = CreateSolid("Dim", root, DimColor);
            Stretch(dim.rectTransform, 0f);

            var continueButton = dim.gameObject.AddComponent<Button>();
            continueButton.targetGraphic = dim;
            continueButton.transition = Selectable.Transition.None;

            // Sparkles and the star share this container so the burst originates from the star.
            var centre = CreateRect("Centre", root);
            centre.anchorMin = centre.anchorMax = new Vector2(0.5f, 0.5f);
            centre.sizeDelta = Vector2.zero;

            var star = CreateImage("Star", centre, StarPath);
            star.preserveAspect = true;
            star.rectTransform.anchorMin = star.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            star.rectTransform.sizeDelta = new Vector2(560f, 527f);

            var sparkle = CreateSparklePrefab(centre);

            // Between the bottom of the top bar and the top of the board, as in the reference.
            var title = CreateText("Title", root, "Perfect!", 120, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            title.rectTransform.sizeDelta = new Vector2(1000f, 170f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 330f);

            var prompt = CreateText("ContinuePrompt", root, "Tap To Continue", 62, TextAnchor.MiddleCenter);
            prompt.rectTransform.anchorMin = prompt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            prompt.rectTransform.sizeDelta = new Vector2(1000f, 120f);
            prompt.rectTransform.anchoredPosition = new Vector2(0f, -700f);

            var celebration = root.gameObject.AddComponent<WinCelebration>();
            ProjectBootstrapTool.Wire(celebration,
                ("_root", root.gameObject),
                ("_star", star.rectTransform),
                ("_title", title),
                ("_continueButton", continueButton),
                ("_continuePrompt", prompt),
                ("_sparklePrefab", sparkle),
                ("_sparkleContainer", centre));

            return celebration;
        }

        private static ChaliceFlightView BuildChaliceFlight(Canvas canvas)
        {
            var root = CreateRect("ChaliceFlight", canvas.transform);
            Stretch(root, 0f);

            // Purely a container for in-flight images, so it must not swallow taps.
            var view = root.gameObject.AddComponent<ChaliceFlightView>();

            var prefab = CreateImage("ChaliceTemplate", root, ChaliceIconPath);
            prefab.preserveAspect = true;
            prefab.raycastTarget = false;
            prefab.rectTransform.sizeDelta = new Vector2(72f, 84f);
            prefab.gameObject.SetActive(false);

            ProjectBootstrapTool.Wire(view, ("_chalicePrefab", prefab), ("_container", root));
            return view;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Template for a single sparkle. Parented and left inactive rather than saved as a prefab
        /// asset, since it exists only for this one effect and nothing else instantiates it.
        /// </summary>
        private static Image CreateSparklePrefab(RectTransform parent)
        {
            var image = CreateImage("SparkleTemplate", parent, SparklePath);
            image.rectTransform.sizeDelta = new Vector2(70f, 70f);
            image.gameObject.SetActive(false);
            return image;
        }

        private static RectTransform CreatePanel(RectTransform parent, string name, Rect anchors)
        {
            var rect = CreateRect(name, parent);
            rect.anchorMin = new Vector2(anchors.xMin, anchors.yMin);
            rect.anchorMax = new Vector2(anchors.xMax, anchors.yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void CreateLabelInPanel(RectTransform parent, string name, Rect anchors, string text, int size)
        {
            var label = CreateText(name, parent, text, size, TextAnchor.MiddleCenter);
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(anchors.xMin, anchors.yMin);
            rect.anchorMax = new Vector2(anchors.xMax, anchors.yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Button CreateIconButton(string name, RectTransform parent, string spritePath, Vector2 size)
        {
            var image = CreateImage(name, parent, spritePath);
            image.preserveAspect = true;
            image.raycastTarget = true;
            image.rectTransform.pivot = new Vector2(1f, 1f);
            image.rectTransform.sizeDelta = size;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        /// <summary>
        /// The green capsule with its gold outline, used for both the level button and Try Again.
        /// The two sprites are 9-sliced to the same rect, the frame slightly larger, which is what
        /// produces the outline — they are supplied as tall capsules and are never used at that size.
        /// </summary>
        private static Button CreateLabelledButton(string name, RectTransform parent, string text, Vector2 size)
        {
            var root = CreateRect(name, parent);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = size;

            var frame = CreateImage("Frame", root, MenuButtonFramePath, sliced: true);
            Stretch(frame.rectTransform, -10f);

            var face = CreateImage("Face", root, MenuButtonPath, sliced: true);
            face.raycastTarget = true;
            Stretch(face.rectTransform, 0f);

            var label = CreateText("Label", root, text, 64, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 0f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 4f);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            return button;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image CreateImage(string name, Transform parent, string spritePath, bool sliced = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = LoadSprite(spritePath);
            image.raycastTarget = false;

            if (sliced)
            {
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        private static Image CreateSolid(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;

            // Keeps large labels legible over busy artwork without needing a separate shadow sprite.
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            outline.effectDistance = new Vector2(3f, -3f);

            return text;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError($"[UiBootstrap] Missing sprite: {path}");
            }

            return sprite;
        }
    }
}
