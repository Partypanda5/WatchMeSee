using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

[InitializeOnLoad]
public static class MenuSceneBuilder
{
    private const string MenuScenePath = "Assets/Scenes/Menu.unity";
    private const float ReferenceWidth = 1594f;
    private const float ReferenceHeight = 1920f;

    static MenuSceneBuilder()
    {
        EditorApplication.delayCall += BuildOrRepairOpenMenu;
    }

    [MenuItem("Tools/Build Responsive Menu Scene")]
    private static void BuildOrRepairOpenMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != MenuScenePath) return;

        Sprite reflectionSprite = LoadSprite("Assets/Sprites/Menu/Reflection.png");
        Sprite fogSprite = LoadSprite("Assets/Sprites/Menu/Foggy Glass.png");
        Sprite frameSprite = LoadSprite("Assets/Sprites/Menu/Frame.png");
        Sprite titleSprite = LoadSprite("Assets/Sprites/Menu/You See Now Text.png");
        Texture2D buttonsTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Menu/Play Quit.png");
        AudioClip hoverSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Glass Squeak.wav");
        if (reflectionSprite == null || fogSprite == null || frameSprite == null || titleSprite == null || buttonsTexture == null || hoverSound == null)
        {
            Debug.LogError("A menu artwork asset could not be loaded. Check that the Menu assets are imported.");
            return;
        }

        GameObject existingCanvas = GameObject.Find("Menu Canvas");
        if (existingCanvas != null && HasCompleteMenu(existingCanvas))
        {
            bool changed = EnsureReflectionBlur(existingCanvas);
            changed |= EnsureButtonHoverAudio(existingCanvas, hoverSound);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            return;
        }
        if (existingCanvas != null) Object.DestroyImmediate(existingCanvas);

        BuildMenu(scene, reflectionSprite, fogSprite, frameSprite, titleSprite, buttonsTexture, hoverSound);
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    private static bool HasCompleteMenu(GameObject canvas)
    {
        string[] requiredImages = { "Face Reflection", "Foggy Glass", "Frame", "Menu Title" };
        foreach (string imageName in requiredImages)
        {
            Transform imageTransform = canvas.transform.Find("Menu Artwork/" + imageName);
            Image image = imageTransform != null ? imageTransform.GetComponent<Image>() : null;
            if (image == null || image.sprite == null) return false;
        }

        string[] buttonNames = { "Play Button", "Quit Button" };
        foreach (string buttonName in buttonNames)
        {
            Transform buttonTransform = canvas.transform.Find("Menu Artwork/" + buttonName);
            if (buttonTransform == null || buttonTransform.GetComponent<Button>() == null ||
                buttonTransform.GetComponent<MenuButtonHoverScale>() == null)
                return false;
            RawImage label = buttonTransform.Find("Button Label") != null
                ? buttonTransform.Find("Button Label").GetComponent<RawImage>()
                : null;
            if (label == null || label.texture == null) return false;
        }
        bool hasInputModule = Resources.FindObjectsOfTypeAll<EventSystem>()
            .Any(candidate => candidate.gameObject.scene == canvas.scene &&
                candidate.GetComponent<InputSystemUIInputModule>() != null);
        return canvas.GetComponent<MenuController>() != null && hasInputModule;
    }

    private static bool EnsureButtonHoverAudio(GameObject canvas, AudioClip hoverSound)
    {
        bool changed = false;
        string[] buttonNames = { "Play Button", "Quit Button" };
        foreach (string buttonName in buttonNames)
        {
            Transform buttonTransform = canvas.transform.Find("Menu Artwork/" + buttonName);
            MenuButtonHoverScale hover = buttonTransform != null
                ? buttonTransform.GetComponent<MenuButtonHoverScale>()
                : null;
            if (hover == null) continue;

            SerializedObject serialized = new SerializedObject(hover);
            SerializedProperty soundProperty = serialized.FindProperty("hoverSound");
            if (soundProperty != null && soundProperty.objectReferenceValue == null)
            {
                soundProperty.objectReferenceValue = hoverSound;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
        }
        return changed;
    }
    private static bool EnsureReflectionBlur(GameObject canvas)
    {
        Transform reflection = canvas.transform.Find("Menu Artwork/Face Reflection");
        if (reflection == null || reflection.GetComponent<Graphic>() == null) return false;
        if (reflection.GetComponent<MenuReflectionBlur>() != null) return false;
        reflection.gameObject.AddComponent<MenuReflectionBlur>();
        return true;
    }
    private static void BuildMenu(Scene scene, Sprite reflectionSprite, Sprite fogSprite,
        Sprite frameSprite, Sprite titleSprite, Texture2D buttonsTexture, AudioClip hoverSound)
    {
        GameObject canvasObject = new GameObject("Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Image canvasBackground = canvasObject.AddComponent<Image>();
        canvasBackground.color = new Color(1f, 0.92f, 0.66f, 0f);
        canvasBackground.raycastTarget = false;
        RectTransform canvasRect = canvasObject.transform as RectTransform;
        Stretch(canvasRect);

        GameObject artObject = new GameObject("Menu Artwork", typeof(RectTransform), typeof(AspectRatioFitter), typeof(RectMask2D));
        artObject.transform.SetParent(canvasRect, false);
        RectTransform art = artObject.transform as RectTransform;
        art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
        art.pivot = new Vector2(0.5f, 0.5f);
        art.sizeDelta = Vector2.zero;
        AspectRatioFitter artFitter = artObject.GetComponent<AspectRatioFitter>();
        artFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        artFitter.aspectRatio = ReferenceWidth / ReferenceHeight;

        Image reflection = CreateImage(art, "Face Reflection", reflectionSprite, Color.white, true);
        RectTransform reflectionRect = reflection.rectTransform;
        reflectionRect.sizeDelta = new Vector2(36f, 36f);
        reflection.gameObject.AddComponent<MenuReflectionBlur>();
        MenuReflectionParallax parallax = reflection.gameObject.AddComponent<MenuReflectionParallax>();
        SetReference(parallax, "artworkArea", art);
        SetReference(parallax, "reflection", reflectionRect);

        CreateImage(art, "Foggy Glass", fogSprite, new Color(1f, 1f, 1f, 0.56f), true);

        Image title = CreateImage(art, "Menu Title", titleSprite, Color.white, true);
        SetAnchors(title.rectTransform, new Vector2(0.06f, 0.63f), new Vector2(0.94f, 0.89f));

        CreateCredits(art);
        Button play = CreateButton(art, "Play Button", new Vector2(0.25f, 0.445f), new Vector2(0.75f, 0.585f),
            buttonsTexture, new Rect(0f, 0.5f, 1f, 0.5f), hoverSound);
        Button quit = CreateButton(art, "Quit Button", new Vector2(0.25f, 0.305f), new Vector2(0.75f, 0.445f),
            buttonsTexture, new Rect(0f, 0f, 1f, 0.5f), hoverSound);

        // Draw the decorative frame above the reflection, glass, and menu content.
        CreateImage(art, "Frame", frameSprite, Color.white, true);

        MenuController controller = canvasObject.AddComponent<MenuController>();
        SetReference(controller, "playButton", play);
        SetReference(controller, "quitButton", quit);
        SetString(controller, "playSceneName", "SampleScene");

        EnsureEventSystem(scene);
        EnsureBuildScenes();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Built the responsive Menu Canvas with independently animated Play and Quit buttons.");
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color, bool preserveAspect)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.transform as RectTransform;
        Stretch(rect);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        return image;
    }

    private static void CreateCredits(Transform parent)
    {
        GameObject textObject = new GameObject("Credits", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.transform as RectTransform;
        SetAnchors(rect, new Vector2(0.07f, 0.045f), new Vector2(0.93f, 0.205f));

        Text text = textObject.GetComponent<Text>();
        Font creditsFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Sprites/Menu/GochiHand-Regular.ttf");
        text.font = creditsFont != null ? creditsFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "Made by\nAndrea Hayes, Benjamin Crooks\n& Morgan Crooks";
        text.fontSize = 54;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 30;
        text.resizeTextMaxSize = 54;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.lineSpacing = 1.05f;
        text.color = new Color(1f, 0.85f, 0.28f, 1f);
        text.raycastTarget = false;
    }

    private static Button CreateButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Texture2D labelTexture, Rect labelUvRect, AudioClip hoverSound)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.transform as RectTransform;
        SetAnchors(rect, anchorMin, anchorMax);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        MenuButtonHoverScale hoverScale = buttonObject.AddComponent<MenuButtonHoverScale>();
        SetReference(hoverScale, "hoverSound", hoverSound);

        GameObject labelObject = new GameObject("Button Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.transform as RectTransform;
        Stretch(labelRect);
        RawImage label = labelObject.GetComponent<RawImage>();
        label.texture = labelTexture; label.color = new Color32(239, 188, 54, 255); // #EFBC36
        label.uvRect = labelUvRect;
        label.raycastTarget = false;
        AspectRatioFitter labelFitter = labelObject.GetComponent<AspectRatioFitter>();
        labelFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        labelFitter.aspectRatio = labelTexture.width / (labelTexture.height * labelUvRect.height);
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        SetAnchors(rect, Vector2.zero, Vector2.one);
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void SetReference(Object target, string fieldName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(fieldName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(Object target, string fieldName, string value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(fieldName).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureEventSystem(Scene scene)
    {
        EventSystem eventSystem = Resources.FindObjectsOfTypeAll<EventSystem>()
            .FirstOrDefault(candidate => candidate.gameObject.scene == scene);
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
            return;
        }

        StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacyModule != null) Object.DestroyImmediate(legacyModule);
        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }
    private static void EnsureBuildScenes()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Cafe.unity", true)
        };
    }
}