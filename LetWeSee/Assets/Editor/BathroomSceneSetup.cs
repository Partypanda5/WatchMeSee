using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class BathroomSceneSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string TextureFolder = "Assets/RenderTextures";
    private const int TextureWidth = 1024;
    private const int TextureHeight = 960;

    [MenuItem("Watch Me See/Build Bathroom Split Screen")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        DeleteByName("RightEyeCamera");
        DeleteByName("BathroomCanvas");

        var leftCamera = Camera.main;
        if (leftCamera == null)
        {
            leftCamera = Object.FindFirstObjectByType<Camera>();
        }
        if (leftCamera == null)
        {
            var cameraObject = new GameObject("LeftEyeCamera");
            leftCamera = cameraObject.AddComponent<Camera>();
        }

        leftCamera.gameObject.name = "LeftEyeCamera";
        leftCamera.tag = "MainCamera";
        leftCamera.orthographic = true;
        leftCamera.orthographicSize = 5f;
        leftCamera.clearFlags = CameraClearFlags.SolidColor;
        leftCamera.backgroundColor = new Color(0.08f, 0.07f, 0.12f, 1f);
        leftCamera.transform.position = new Vector3(-0.04f, 0f, -10f);
        leftCamera.enabled = true;

        EnsureFolder(TextureFolder);
        var leftTexture = GetOrCreateTexture(TextureFolder + "/LeftEyeView.renderTexture");
        var rightTexture = GetOrCreateTexture(TextureFolder + "/RightEyeView.renderTexture");
        leftCamera.targetTexture = leftTexture;

        var leftData = leftCamera.GetComponent<UniversalAdditionalCameraData>();
        var rightCameraObject = new GameObject("RightEyeCamera");
        var rightCamera = rightCameraObject.AddComponent<Camera>();
        EditorUtility.CopySerialized(leftCamera, rightCamera);
        rightCameraObject.tag = "Untagged";
        rightCameraObject.transform.position = new Vector3(0.04f, 0f, -10f);
        rightCamera.targetTexture = rightTexture;
        rightCamera.enabled = true;
        var rightListener = rightCameraObject.GetComponent<AudioListener>();
        if (rightListener != null) Object.DestroyImmediate(rightListener);
        if (leftData != null)
        {
            var rightData = rightCameraObject.AddComponent<UniversalAdditionalCameraData>();
            EditorUtility.CopySerialized(leftData, rightData);
        }

        BuildCanvas(leftTexture, rightTexture);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Bathroom split-screen scene created. Assign your bathroom sprites to the World layer when ready.");
    }

    private static void BuildCanvas(RenderTexture leftTexture, RenderTexture rightTexture)
    {
        var canvasObject = new GameObject("BathroomCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var leftPanel = MakeImage("LeftPanel", canvasObject.transform, sprite, new Color(0.12f, 0.11f, 0.17f, 1f));
        Stretch(leftPanel.rectTransform, new Vector2(0f, 0f), new Vector2(0.4f, 1f));

        var worldPanel = new GameObject("WorldPanel", typeof(RectTransform));
        worldPanel.transform.SetParent(canvasObject.transform, false);
        Stretch(worldPanel.GetComponent<RectTransform>(), new Vector2(0.4f, 0f), Vector2.one);

        var leftView = new GameObject("LeftEyeView", typeof(RectTransform), typeof(RawImage));
        leftView.transform.SetParent(worldPanel.transform, false);
        Stretch(leftView.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        var leftRaw = leftView.GetComponent<RawImage>();
        leftRaw.texture = leftTexture;
        leftRaw.color = Color.white;
        leftRaw.raycastTarget = false;

        var rightView = new GameObject("RightEyeView", typeof(RectTransform), typeof(RawImage));
        rightView.transform.SetParent(worldPanel.transform, false);
        Stretch(rightView.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        var rightRaw = rightView.GetComponent<RawImage>();
        rightRaw.texture = rightTexture;
        rightRaw.color = new Color(0.55f, 0.9f, 1f, 0.48f);
        rightRaw.raycastTarget = false;

        // Simple placeholders make the face panel readable before character art is imported.
        var head = MakeImage("FacePlaceholder", leftPanel.transform, sprite, new Color(0.55f, 0.38f, 0.34f, 1f));
        Place(head.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0f, 20f), new Vector2(320f, 430f));

        var leftEye = MakeImage("LeftEye", head.transform, sprite, Color.white);
        Place(leftEye.rectTransform, new Vector2(0.32f, 0.68f), Vector2.zero, new Vector2(90f, 48f));
        var rightEye = MakeImage("RightEye", head.transform, sprite, Color.white);
        Place(rightEye.rectTransform, new Vector2(0.68f, 0.68f), Vector2.zero, new Vector2(90f, 48f));

        var leftPupil = MakeImage("LeftPupil", leftEye.transform, sprite, new Color(0.12f, 0.18f, 0.24f, 1f));
        Place(leftPupil.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 34f));
        var rightPupil = MakeImage("RightPupil", rightEye.transform, sprite, new Color(0.12f, 0.18f, 0.24f, 1f));
        Place(rightPupil.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 34f));

        var divider = MakeImage("PanelDivider", canvasObject.transform, sprite, new Color(0.02f, 0.02f, 0.03f, 1f));
        Place(divider.rectTransform, new Vector2(0.4f, 0.5f), Vector2.zero, new Vector2(6f, 1080f));
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static RenderTexture GetOrCreateTexture(string path)
    {
        var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (texture == null)
        {
            texture = new RenderTexture(TextureWidth, TextureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path),
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            AssetDatabase.CreateAsset(texture, path);
        }
        texture.width = TextureWidth;
        texture.height = TextureHeight;
        texture.depth = 16;
        texture.antiAliasing = 1;
        EditorUtility.SetDirty(texture);
        return texture;
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder("Assets", "RenderTextures");
    }

    private static void DeleteByName(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) Object.DestroyImmediate(go);
    }
}

[InitializeOnLoad]
public static class ArmPlaceholderSetupOnce
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string HookPath = "Assets/Editor/ArmPlaceholderSetupOnce.cs";
    private const string CompletedKey = "WatchMeSee.ArmPlaceholderSetupCompleted";

    static ArmPlaceholderSetupOnce()
    {
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Watch Me See/Setup Arm Placeholder")]
    public static void SetupFromMenu() => Run();

    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var openScene = SceneManager.GetSceneAt(i);
            if (openScene.isDirty)
            {
                Debug.LogWarning("Arm placeholder setup paused because an open scene has unsaved changes. Save it, then use Watch Me See > Setup Arm Placeholder.");
                return;
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var panelObject = GameObject.Find("LeftPanel");
        var controller = Object.FindFirstObjectByType<EyeGazeController>();
        if (panelObject == null || controller == null)
        {
            Debug.LogError("Could not find LeftPanel or EyeGazeController in SampleScene.");
            return;
        }

        var panel = panelObject.GetComponent<RectTransform>();
        var oldArm = panel.Find("ArmPlaceholder");
        if (oldArm != null) Object.DestroyImmediate(oldArm.gameObject);

        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var arm = MakeImage("ArmPlaceholder", panel, sprite, new Color(0.68f, 0.43f, 0.34f, 1f));
        var armRect = arm.rectTransform;
        armRect.anchorMin = armRect.anchorMax = new Vector2(0.5f, 0.5f);
        armRect.pivot = new Vector2(0.5f, 0f);
        armRect.sizeDelta = new Vector2(52f, 250f);
        armRect.anchoredPosition = new Vector2(panel.rect.xMin + panel.rect.width * 0.16f, panel.rect.yMin + panel.rect.height * 0.08f);
        armRect.SetAsFirstSibling();

        var hand = MakeImage("HandPlaceholder", panel, sprite, new Color(0.82f, 0.59f, 0.47f, 1f));
        var handRect = hand.rectTransform;
        handRect.anchorMin = handRect.anchorMax = new Vector2(0.5f, 0.5f);
        handRect.pivot = new Vector2(0.5f, 0.5f);
        handRect.sizeDelta = new Vector2(78f, 66f);
        handRect.anchoredPosition = armRect.anchoredPosition + new Vector2(0f, 200f);

        AddChildShape("FingerIndex", handRect, sprite, new Vector2(-19f, 29f), new Vector2(19f, 40f), new Color(0.86f, 0.63f, 0.51f, 1f));
        AddChildShape("FingerMiddle", handRect, sprite, new Vector2(0f, 33f), new Vector2(19f, 46f), new Color(0.86f, 0.63f, 0.51f, 1f));
        AddChildShape("FingerRing", handRect, sprite, new Vector2(19f, 27f), new Vector2(18f, 36f), new Color(0.82f, 0.59f, 0.47f, 1f));
        AddChildShape("Thumb", handRect, sprite, new Vector2(-35f, -1f), new Vector2(34f, 19f), new Color(0.82f, 0.59f, 0.47f, 1f), -32f);
        handRect.SetAsLastSibling();

        var serialized = new SerializedObject(controller);
        serialized.FindProperty("leftPanel").objectReferenceValue = panel;
        serialized.FindProperty("armVisual").objectReferenceValue = armRect;
        serialized.FindProperty("handVisual").objectReferenceValue = handRect;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.DeleteAsset(HookPath);
        Debug.Log("Added the mouse-following arm and hand placeholder to the SampleScene left panel.");
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void AddChildShape(string name, RectTransform parent, Sprite sprite, Vector2 position, Vector2 size, Color color, float angle = 0f)
    {
        var image = MakeImage(name, parent, sprite, color);
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}


[InitializeOnLoad]
public static class WorldPanelClipSetupOnce
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string CompletedKey = "WatchMeSee.WorldPanelClipSetupCompleted";

    static WorldPanelClipSetupOnce()
    {
        EditorApplication.delayCall += Run;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Run;
        };
    }

    [MenuItem("Watch Me See/Clip World Panel Contents")]
    public static void SetupFromMenu() => Run();

    private static void Run()
    {
        if (EditorPrefs.GetBool(CompletedKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var openScene = SceneManager.GetSceneAt(i);
            if (openScene.isDirty)
            {
                Debug.LogWarning("World panel clip setup paused because an open scene has unsaved changes. Save it, then use Watch Me See > Clip World Panel Contents.");
                return;
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var worldPanel = GameObject.Find("WorldPanel");
        if (worldPanel == null)
        {
            Debug.LogError("Could not find WorldPanel in SampleScene.");
            return;
        }

        if (worldPanel.GetComponent<RectMask2D>() == null)
            worldPanel.AddComponent<RectMask2D>();
        foreach (var image in worldPanel.GetComponentsInChildren<RawImage>(true))
            image.raycastTarget = false;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorPrefs.SetBool(CompletedKey, true);
        AssetDatabase.SaveAssets();
        Debug.Log("WorldPanel now clips both eye views to its bounds.");
    }
}

[InitializeOnLoad]
public static class CleanupHandPlaceholdersOnce
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string CompletedKey = "WatchMeSee.CleanupHandPlaceholdersCompleted";

    static CleanupHandPlaceholdersOnce()
    {
        EditorApplication.delayCall += Run;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Run;
        };
    }

    [MenuItem("Watch Me See/Clean Up Hand Placeholder")]
    public static void SetupFromMenu() => Run();

    private static void Run()
    {
        if (EditorPrefs.GetBool(CompletedKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var openScene = SceneManager.GetSceneAt(i);
            if (openScene.isDirty)
            {
                Debug.LogWarning("Hand placeholder cleanup paused because an open scene has unsaved changes. Save it, then use Watch Me See > Clean Up Hand Placeholder.");
                return;
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var panelObject = GameObject.Find("LeftPanel");
        var controller = Object.FindAnyObjectByType<EyeGazeController>();
        if (panelObject == null || controller == null)
        {
            Debug.LogError("Could not find LeftPanel or EyeGazeController in SampleScene.");
            return;
        }

        var panel = panelObject.GetComponent<RectTransform>();
        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            var child = panel.GetChild(i);
            if (child.name == "ArmPlaceholder" || child.name == "HandPlaceholder" ||
                child.name == "FingerIndex" || child.name == "FingerMiddle" ||
                child.name == "FingerRing" || child.name == "Thumb")
                Object.DestroyImmediate(child.gameObject);
        }

        if (panelObject.GetComponent<RectMask2D>() == null)
            panelObject.AddComponent<RectMask2D>();

        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var arm = MakeImage("ArmPlaceholder", panel, sprite, new Color(0.68f, 0.43f, 0.34f, 1f));
        var armRect = arm.rectTransform;
        armRect.anchorMin = armRect.anchorMax = new Vector2(0.5f, 0.5f);
        armRect.pivot = new Vector2(0.5f, 0f);
        armRect.sizeDelta = new Vector2(52f, 250f);
        armRect.anchoredPosition = new Vector2(panel.rect.xMin + panel.rect.width * 0.16f, panel.rect.yMin + panel.rect.height * 0.08f);
        armRect.SetAsFirstSibling();

        var hand = MakeImage("HandPlaceholder", panel, sprite, new Color(0.82f, 0.59f, 0.47f, 1f));
        var handRect = hand.rectTransform;
        handRect.anchorMin = handRect.anchorMax = new Vector2(0.5f, 0.5f);
        handRect.pivot = new Vector2(0.5f, 0.5f);
        handRect.sizeDelta = new Vector2(78f, 66f);
        handRect.anchoredPosition = armRect.anchoredPosition + new Vector2(0f, 200f);
        AddShape("FingerIndex", handRect, sprite, new Vector2(-19f, 29f), new Vector2(19f, 40f), new Color(0.86f, 0.63f, 0.51f, 1f));
        AddShape("FingerMiddle", handRect, sprite, new Vector2(0f, 33f), new Vector2(19f, 46f), new Color(0.86f, 0.63f, 0.51f, 1f));
        AddShape("FingerRing", handRect, sprite, new Vector2(19f, 27f), new Vector2(18f, 36f), new Color(0.82f, 0.59f, 0.47f, 1f));
        AddShape("Thumb", handRect, sprite, new Vector2(-35f, -1f), new Vector2(34f, 19f), new Color(0.82f, 0.59f, 0.47f, 1f), -32f);
        handRect.SetAsLastSibling();

        var serialized = new SerializedObject(controller);
        serialized.FindProperty("leftPanel").objectReferenceValue = panel;
        serialized.FindProperty("armVisual").objectReferenceValue = armRect;
        serialized.FindProperty("handVisual").objectReferenceValue = handRect;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        EditorPrefs.SetBool(CompletedKey, true);
        Debug.Log("Removed duplicate hand placeholders, added a left-panel clip, and saved one hand/arm pair.");
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void AddShape(string name, RectTransform parent, Sprite sprite, Vector2 position, Vector2 size, Color color, float angle = 0f)
    {
        var image = MakeImage(name, parent, sprite, color);
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
