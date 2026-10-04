using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Sprite-based eye controls for the Character prefab. Click an eye and drag to move its pupil.
// Intended to replace the UI-based EyeGazeController once the world view is hooked up.
public sealed class CharacterEyes : MonoBehaviour
{
    [System.Serializable]
    public sealed class Eye
    {
        public Transform root;
        public SpriteRenderer eyeBall;
        public Transform pupil;
        public Transform topLid;
        public Transform bottomLid;

        [System.NonSerialized] public Vector2 center;
        [System.NonSerialized] public Vector2 radius;
        [System.NonSerialized] public Vector2 pupilExtents;
        [System.NonSerialized] public Vector2 pupilRest;
        [System.NonSerialized] public Vector2 pupilTarget;
        [System.NonSerialized] public Vector2 pupilCurrent;
        [System.NonSerialized] public Vector2 topLidRest;
        [System.NonSerialized] public Vector2 bottomLidRest;
        [System.NonSerialized] public float topEdgeRest;
        [System.NonSerialized] public float bottomEdgeRest;
        [System.NonSerialized] public float topOffset;
        [System.NonSerialized] public float bottomOffset;
    }

    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Eye leftEye = new Eye();
    [SerializeField] private Eye rightEye = new Eye();

    [Header("Pupil")]
    [SerializeField, Range(0.1f, 3f)] private float dragSensitivity = 1f;
    [SerializeField, Range(1f, 30f)] private float pupilFollowSpeed = 14f;

    [Header("Eyelids")]
    [Tooltip("Space kept between each lid edge and the pupil.")]
    [SerializeField, Range(0f, 0.5f)] private float lidGap = 0.04f;
    [Tooltip("The gap used when the pupil is at the very top or bottom of the eye, so it tucks in closer to the lid.")]
    [SerializeField, Range(-0.2f, 0.5f)] private float lidGapAtEdge = 0f;
    [Tooltip("How much the lids drift towards the pupil when they don't have to.")]
    [SerializeField, Range(0f, 1f)] private float lidFollow = 0.6f;
    [SerializeField, Range(1f, 30f)] private float lidFollowSpeed = 10f;
    [Tooltip("How far above its authored pose the top lid drifts on its own. It only opens further when the pupil pushes it.")]
    [SerializeField, Range(0f, 1f)] private float topLidMaxRaise = 0.08f;
    [SerializeField, Range(0f, 2f)] private float topLidMaxLower = 0.25f;
    [Tooltip("How far above its authored pose the bottom lid can rise.")]
    [SerializeField, Range(0f, 1f)] private float bottomLidMaxRaise = 0.06f;
    [SerializeField, Range(0f, 2f)] private float bottomLidMaxLower = 0.15f;

    [Header("Face features")]
    [SerializeField] private Transform leftBrow;
    [SerializeField] private Transform rightBrow;
    [Tooltip("Parts that slide sideways together with the average gaze: ears, nose, forehead, mouth.")]
    [SerializeField] private Transform[] turningParts = new Transform[0];
    [Tooltip("How far each brow moves up or down when its eye looks fully up or down.")]
    [SerializeField, Range(0f, 1f)] private float browTravel = 0.12f;
    [Tooltip("How far the turning parts slide sideways when the eyes, on average, look fully left or right. Negative flips the direction.")]
    [SerializeField, Range(-1f, 1f)] private float turnTravel = 0.07f;
    [SerializeField, Range(1f, 30f)] private float featureFollowSpeed = 8f;

    [Header("Hand")]
    [Tooltip("Follows the pointer by its pivot (the fingertip). While dragging an eye it only moves as far as the pupil does.")]
    [SerializeField] private SpriteRenderer hand;
    [SerializeField] private Sprite pointingHand;
    [SerializeField] private Sprite pressingHand;
    [SerializeField] private Sprite deodorantSprayHand;
    [SerializeField] private Sprite deodorantIdleHand;
    [SerializeField] private AudioClip faceTapSound;
    [SerializeField] private AudioClip eyeDragSquishSound;

    private InputAction pointerPosition;
    private InputAction eyeDrag;
    private Eye dragged;
    private AudioSource faceTapAudioSource;
    private AudioSource eyeDragAudioSource;
    private Vector2 lastDragScreenPosition;
    private float accumulatedDragScreenMovement;
    private float lastEyeMovementTime;
    private float dragSquishBasePitch = 1f;
    private float dragSquishPitchPhase;
    private bool faceTapPending;
    private bool mirrorCopyMode;
    private bool deodorantUseActive;
    private Sprite originalHandSprite;
    private Vector3 originalHandPosition;
    private Vector3 originalHandScale;
    private Quaternion originalHandRotation;
    private bool originalHandEnabled;
    private static Sprite softMistSprite;
    private Vector2 dragStartPointer;
    private Vector2 dragStartPupil;
    private Vector3 leftBrowRest;
    private Vector3 rightBrowRest;
    private Vector3[] turningRest;
    private float leftBrowOffset;
    private float rightBrowOffset;
    private float turnOffset;

    private bool initialized;

    // Fired when the player grabs an eye, before the drag starts moving it.
    public event System.Action DragStarted;

    // -1..1 aim of each pupil within its reachable range: where the pupil is now...
    public Vector2 LeftAim => Aim(leftEye, leftEye.pupilCurrent);
    public Vector2 RightAim => Aim(rightEye, rightEye.pupilCurrent);
    // ...and where it is heading.
    public Vector2 LeftAimTarget => Aim(leftEye, leftEye.pupilTarget);
    public Vector2 RightAimTarget => Aim(rightEye, rightEye.pupilTarget);
    public Sprite PointingHandSprite => pointingHand;
    public bool IsUsingDeodorant => deodorantUseActive;

    // Called after the deodorant pickup animation completes. The player can spray by holding LMB.
    public void BeginDeodorantUse()
    {
        if (mirrorCopyMode || hand == null || deodorantIdleHand == null || eyeDrag == null || deodorantUseActive)
            return;

        deodorantUseActive = true;
        dragged = null;
        faceTapPending = false;
        if (eyeDragAudioSource != null) eyeDragAudioSource.Stop();
        originalHandSprite = hand.sprite;
        originalHandPosition = hand.transform.position;
        originalHandScale = hand.transform.localScale;
        originalHandRotation = hand.transform.localRotation;
        originalHandEnabled = hand.enabled;
        StartCoroutine(DeodorantUseSequence());
    }

    // Sends both pupils towards a -1..1 aim. The pupils ease there like a drag would.
    public void SetAims(Vector2 left, Vector2 right)
    {
        EnsureInitialized();
        SetAim(leftEye, left);
        SetAim(rightEye, right);
    }

    // The mirror uses a visual copy of this face and follows its live animated pose.
    public void SetMirrorCopyMode()
    {
        mirrorCopyMode = true;
        pointerPosition = null;
        eyeDrag = null;

        foreach (AudioSource source in GetComponents<AudioSource>())
        {
            source.Stop();
            source.enabled = false;
        }
    }
    // Copies all animated child transforms and sprite states into the mirror while keeping its fitted root transform.
    public void CopyVisualPoseFrom(CharacterEyes source)
    {
        if (source == null) return;
        CopyChildVisualPose(source.transform, transform);

        // Copy the driven eye parts directly as well. These references are serialized on
        // CharacterEyes, so pupil and eyelid motion does not depend on child order matching.
        CopyEyePose(source.leftEye, leftEye);
        CopyEyePose(source.rightEye, rightEye);

        if (mirrorCopyMode)
            KeepMirrorEyesVisible();
    }

    private static void CopyChildVisualPose(Transform source, Transform target)
    {
        for (int i = 0; i < source.childCount; i++)
        {
            Transform sourceChild = source.GetChild(i);
            Transform targetChild = target.Find(sourceChild.name);
            if (targetChild == null) continue;

            if (targetChild.gameObject.activeSelf != sourceChild.gameObject.activeSelf)
                targetChild.gameObject.SetActive(sourceChild.gameObject.activeSelf);
            CopyLocalTransform(sourceChild, targetChild);

            SpriteRenderer sourceSprite = sourceChild.GetComponent<SpriteRenderer>();
            SpriteRenderer targetSprite = targetChild.GetComponent<SpriteRenderer>();
            if (sourceSprite != null && targetSprite != null)
            {
                targetSprite.enabled = sourceSprite.enabled;
                targetSprite.sprite = sourceSprite.sprite;
                targetSprite.color = sourceSprite.color;
                targetSprite.flipX = sourceSprite.flipX;
                targetSprite.flipY = sourceSprite.flipY;
            }

            CopyChildVisualPose(sourceChild, targetChild);
        }
    }

    private static void CopyEyePose(Eye source, Eye target)
    {
        if (source == null || target == null) return;
        CopyLocalTransform(source.pupil, target.pupil);
        CopyLocalTransform(source.topLid, target.topLid);
        CopyLocalTransform(source.bottomLid, target.bottomLid);
        CopyRendererPose(source.eyeBall, target.eyeBall);
        CopyRendererPose(source.pupil != null ? source.pupil.GetComponent<SpriteRenderer>() : null,
            target.pupil != null ? target.pupil.GetComponent<SpriteRenderer>() : null);
    }

    private static void CopyLocalTransform(Transform source, Transform target)
    {
        if (source == null || target == null) return;
        target.localPosition = source.localPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }

    private static void CopyRendererPose(SpriteRenderer source, SpriteRenderer target)
    {
        if (source == null || target == null) return;
        target.enabled = source.enabled;
        target.sprite = source.sprite;
        target.color = source.color;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
    }

    private void KeepMirrorEyesVisible()
    {
        // The face art uses SpriteMasks for eyelids. Their sorting ranges overlap the
        // mirror's remapped sprite orders, which can leave the copied eyes fully covered.
        // The mirror has no blink animation, so show the copied eye and pupil sprites directly.
        foreach (SpriteMask mask in GetComponentsInChildren<SpriteMask>(true))
            mask.enabled = false;

        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sprite.maskInteraction = SpriteMaskInteraction.None;
            if (sprite.gameObject.name.IndexOf("lid", System.StringComparison.OrdinalIgnoreCase) >= 0)
                sprite.enabled = false;
        }
    }
    private void SetAim(Eye eye, Vector2 aim)
    {
        if (eye.root == null || eye.pupil == null) return;
        Vector2 room = Vector2.Max(eye.radius - eye.pupilExtents, Vector2.one * 0.001f);
        eye.pupilTarget = ClampPupil(eye, eye.center + new Vector2(aim.x * room.x, aim.y * room.y));
    }

    private void Awake()
    {
        EnsureInitialized();

        if (inputActions == null) return;
        InputActionMap map = inputActions.FindActionMap("EyeControls", true);
        pointerPosition = map.FindAction("PointerPosition", true);
        eyeDrag = map.FindAction("EyeDrag", true);
    }

    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;
        Init(leftEye);
        Init(rightEye);
        if (leftBrow != null) leftBrowRest = leftBrow.localPosition;
        if (rightBrow != null) rightBrowRest = rightBrow.localPosition;
        turningRest = new Vector3[turningParts.Length];
        for (int i = 0; i < turningParts.Length; i++)
            if (turningParts[i] != null) turningRest[i] = turningParts[i].localPosition;
    }

    private void OnEnable()
    {
        if (pointerPosition != null) pointerPosition.actionMap.Enable();
    }

    private void OnDisable()
    {
        dragged = null;
        if (eyeDragAudioSource != null)
        {
            eyeDragAudioSource.Stop();
            eyeDragAudioSource.loop = false;
            eyeDragAudioSource.pitch = 1f;
        }
    }

    private void Init(Eye eye)
    {
        if (eye.root == null || eye.eyeBall == null || eye.pupil == null) return;

        Bounds ball = eye.eyeBall.bounds;
        eye.center = ToEye(eye, ball.center);
        eye.radius = ToEyeSize(eye, ball.extents);

        SpriteRenderer pupilRenderer = eye.pupil.GetComponent<SpriteRenderer>();
        eye.pupilExtents = pupilRenderer != null ? ToEyeSize(eye, pupilRenderer.bounds.extents) : Vector2.zero;
        eye.pupilRest = ToEye(eye, eye.pupil.position);
        eye.pupilTarget = eye.pupilRest;
        eye.pupilCurrent = eye.pupilRest;

        if (eye.topLid != null)
        {
            eye.topLidRest = ToEye(eye, eye.topLid.position);
            eye.topEdgeRest = LidEdge(eye, eye.topLid, true);
        }
        if (eye.bottomLid != null)
        {
            eye.bottomLidRest = ToEye(eye, eye.bottomLid.position);
            eye.bottomEdgeRest = LidEdge(eye, eye.bottomLid, false);
        }
    }

    // Lid edge height in eye space: the bottom of the top lid's masks, or the top of the bottom lid's.
    private float LidEdge(Eye eye, Transform lid, bool top)
    {
        float edge = top ? float.MaxValue : float.MinValue;
        bool found = false;
        foreach (SpriteMask mask in lid.GetComponentsInChildren<SpriteMask>(true))
        {
            Bounds b = mask.bounds;
            float y = ToEye(eye, top ? b.min : b.max).y;
            edge = top ? Mathf.Min(edge, y) : Mathf.Max(edge, y);
            found = true;
        }
        if (!found)
        {
            foreach (SpriteRenderer sprite in lid.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!sprite.enabled || sprite.maskInteraction != SpriteMaskInteraction.None) continue;
                Bounds b = sprite.bounds;
                float y = ToEye(eye, top ? b.min : b.max).y;
                edge = top ? Mathf.Min(edge, y) : Mathf.Max(edge, y);
                found = true;
            }
        }
        if (found) return edge;
        return top ? eye.center.y + eye.radius.y : eye.center.y - eye.radius.y;
    }

    private void Update()
    {
        if (mirrorCopyMode) return;

        if (!deodorantUseActive && pointerPosition != null && eyeDrag != null)
            HandleInput();

        UpdateDragSquishAudio();
        UpdateEye(leftEye);
        UpdateEye(rightEye);
        UpdateFaceFeatures();
    }

    // Each brow follows its own eye up and down; the turning parts slide sideways together with the average gaze.
    private void UpdateFaceFeatures()
    {
        float step = Time.deltaTime * featureFollowSpeed;
        Vector2 left = LeftAim;
        Vector2 right = RightAim;

        leftBrowOffset = Mathf.Lerp(leftBrowOffset, left.y * browTravel, step);
        rightBrowOffset = Mathf.Lerp(rightBrowOffset, right.y * browTravel, step);
        turnOffset = Mathf.Lerp(turnOffset, (left.x + right.x) * 0.5f * turnTravel, step);

        if (leftBrow != null) leftBrow.localPosition = leftBrowRest + Vector3.up * leftBrowOffset;
        if (rightBrow != null) rightBrow.localPosition = rightBrowRest + Vector3.up * rightBrowOffset;
        for (int i = 0; i < turningParts.Length; i++)
            if (turningParts[i] != null) turningParts[i].localPosition = turningRest[i] + Vector3.right * turnOffset;
    }

    private void HandleInput()
    {
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam == null) return;

        Vector2 screen = pointerPosition.ReadValue<Vector2>();
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));

        if (eyeDrag.WasPressedThisFrame())
        {
            dragged = null;
            faceTapPending = false;
            if (IsOverEye(leftEye, world)) dragged = leftEye;
            else if (IsOverEye(rightEye, world)) dragged = rightEye;

            if (dragged != null)
            {
                DragStarted?.Invoke();
                dragStartPointer = ToEye(dragged, world);
                dragStartPupil = dragged.pupilTarget;
                lastDragScreenPosition = screen;
                accumulatedDragScreenMovement = 0f;
            }
            else if (cam.pixelRect.Contains(screen))
            {
                faceTapPending = true;
            }
        }

        if (!eyeDrag.IsPressed())
        {
            dragged = null;
            faceTapPending = false;
        }

        if (dragged != null)
        {
            Vector2 screenMovement = screen - lastDragScreenPosition;
            lastDragScreenPosition = screen;
            Vector2 delta = ToEye(dragged, world) - dragStartPointer;
            Vector2 previousPupilTarget = dragged.pupilTarget;
            dragged.pupilTarget = ClampPupil(dragged, dragStartPupil + delta * dragSensitivity);

            bool pupilMoved = (dragged.pupilTarget - previousPupilTarget).sqrMagnitude > 0.000001f;
            if (pupilMoved)
            {
                accumulatedDragScreenMovement += screenMovement.magnitude;
                lastEyeMovementTime = Time.unscaledTime;
                if (accumulatedDragScreenMovement >= 6f)
                {
                    StartDragSquishLoop();
                    accumulatedDragScreenMovement = 0f;
                }
            }
        }

        UpdateHand(cam, screen, world);
    }

    private void PlayFaceTap()
    {
        if (faceTapSound == null) return;
        if (faceTapAudioSource == null)
        {
            faceTapAudioSource = GetComponent<AudioSource>();
            if (faceTapAudioSource == null) faceTapAudioSource = gameObject.AddComponent<AudioSource>();
            faceTapAudioSource.playOnAwake = false;
            faceTapAudioSource.spatialBlend = 0f;
        }

        faceTapAudioSource.PlayOneShot(faceTapSound);
    }

    private void StartDragSquishLoop()
    {
        if (eyeDragSquishSound == null) return;

        if (eyeDragAudioSource == null)
        {
            eyeDragAudioSource = gameObject.AddComponent<AudioSource>();
            eyeDragAudioSource.playOnAwake = false;
            eyeDragAudioSource.spatialBlend = 0f;
        }

        // Use one looping source so movement never stacks overlapping one-shot voices.
        if (eyeDragAudioSource.isPlaying && eyeDragAudioSource.loop && eyeDragAudioSource.clip == eyeDragSquishSound)
            return;

        eyeDragAudioSource.Stop();
        eyeDragAudioSource.clip = eyeDragSquishSound;
        eyeDragAudioSource.loop = true;
        dragSquishBasePitch = Random.Range(0.92f, 1.08f);
        dragSquishPitchPhase = Random.Range(0f, Mathf.PI * 2f);
        eyeDragAudioSource.pitch = dragSquishBasePitch;
        eyeDragAudioSource.Play();
    }

    private void UpdateDragSquishAudio()
    {
        if (eyeDragAudioSource == null) return;

        bool movingEye = eyeDrag != null && eyeDrag.IsPressed() && dragged != null &&
                         Time.unscaledTime - lastEyeMovementTime <= 0.3f;
        if (!movingEye)
        {
            if (eyeDragAudioSource.isPlaying) eyeDragAudioSource.Stop();
            eyeDragAudioSource.loop = false;
            eyeDragAudioSource.pitch = 1f;
            return;
        }

        if (eyeDragAudioSource.isPlaying)
        {
            eyeDragAudioSource.pitch = dragSquishBasePitch +
                Mathf.Sin(Time.unscaledTime * 10f + dragSquishPitchPhase) * 0.035f;
        }
    }
    private void UpdateHand(Camera cam, Vector2 screen, Vector3 world)
    {
        if (hand == null) return;

        // During an eye drag, keep the fingertip on the selected pupil even if the pointer leaves the face panel.
        bool draggingEye = eyeDrag.IsPressed() && dragged != null;
        bool visible = draggingEye || cam.pixelRect.Contains(screen);
        hand.enabled = visible;
        if (!visible) return;

        Vector3 tip = draggingEye
            ? ToWorld(dragged, dragged.pupilCurrent, world.z)
            : world;
        hand.transform.position = new Vector3(tip.x, tip.y, hand.transform.position.z);
        hand.transform.localScale = Vector3.one * 0.88f;
        hand.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);

        Sprite wanted = eyeDrag.IsPressed() ? pressingHand : pointingHand;
        if (wanted != null) hand.sprite = wanted;
        if (faceTapPending && !draggingEye && pressingHand != null && wanted == pressingHand)
        {
            PlayFaceTap();
            faceTapPending = false;
        }
    }

    private IEnumerator DeodorantUseSequence()
    {
        Bounds faceBounds = GetFaceBounds();
        hand.transform.position = new Vector3(
            faceBounds.center.x,
            faceBounds.min.y + faceBounds.size.y * 0.18f,
            originalHandPosition.z);
        hand.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
        SetDeodorantHandSprite(deodorantIdleHand);
        hand.enabled = true;

        const float requiredHoldDuration = 4f;
        float heldDuration = 0f;
        float emissionTimer = 0f;
        while (heldDuration < requiredHoldDuration)
        {
            FollowPointerWithinFacePanel();
            if (eyeDrag.IsPressed())
            {
                SetDeodorantHandSprite(deodorantSprayHand != null ? deodorantSprayHand : deodorantIdleHand);
                heldDuration += Time.deltaTime;
                emissionTimer += Time.deltaTime;
                while (emissionTimer >= 0.075f)
                {
                    emissionTimer -= 0.075f;
                    EmitDeodorantMist(faceBounds);
                }
            }
            else
            {
                // The four-second action requires one continuous hold. Releasing early cancels it.
                SetDeodorantHandSprite(deodorantIdleHand);
                heldDuration = 0f;
                emissionTimer = 0f;
            }
            yield return null;
        }

        Vector3 handEnd = hand.transform.position + Vector3.down * Mathf.Max(0.5f, faceBounds.size.y * 0.55f);
        Vector3 handFrom = hand.transform.position;
        const float lowerDuration = 0.75f;
        for (float elapsedLower = 0f; elapsedLower < lowerDuration; elapsedLower += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsedLower / lowerDuration);
            float eased = t * t * (3f - 2f * t);
            hand.transform.position = Vector3.Lerp(handFrom, handEnd, eased);
            yield return null;
        }

        hand.sprite = pointingHand != null ? pointingHand : originalHandSprite;
        hand.transform.localScale = originalHandScale;
        hand.transform.localRotation = originalHandRotation;
        hand.enabled = true;
        deodorantUseActive = false;
    }

    private void SetDeodorantHandSprite(Sprite sprite)
    {
        if (sprite == null || hand == null) return;
        hand.sprite = sprite;

        float normalSize = pointingHand != null
            ? Mathf.Max(pointingHand.bounds.size.x, pointingHand.bounds.size.y)
            : 1f;
        float poseSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        hand.transform.localScale = Vector3.one * (0.88f * normalSize / Mathf.Max(0.01f, poseSize));
    }

    private void FollowPointerWithinFacePanel()
    {
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam == null || pointerPosition == null || hand == null) return;

        Rect panel = cam.pixelRect;
        Bounds handBounds = hand.bounds;
        Vector3 pivotScreen = cam.WorldToScreenPoint(hand.transform.position);
        Vector3 leftEdgeScreen = cam.WorldToScreenPoint(
            new Vector3(handBounds.min.x, handBounds.center.y, handBounds.center.z));
        Vector3 rightEdgeScreen = cam.WorldToScreenPoint(
            new Vector3(handBounds.max.x, handBounds.center.y, handBounds.center.z));
        Vector3 bottomEdgeScreen = cam.WorldToScreenPoint(
            new Vector3(handBounds.center.x, handBounds.min.y, handBounds.center.z));

        float leftPadding = Mathf.Max(0f, pivotScreen.x - leftEdgeScreen.x);
        float rightPadding = Mathf.Max(0f, rightEdgeScreen.x - pivotScreen.x);
        float bottomPadding = Mathf.Max(0f, pivotScreen.y - bottomEdgeScreen.y);
        Vector2 pointer = pointerPosition.ReadValue<Vector2>();
        float wantedX = panel.Contains(pointer) ? pointer.x : pivotScreen.x;
        float minimumX = panel.xMin + leftPadding;
        float maximumX = panel.xMax - rightPadding;
        if (minimumX <= maximumX) wantedX = Mathf.Clamp(wantedX, minimumX, maximumX);

        // Keep the bottom of the arm touching the bottom edge of the face view.
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(wantedX, panel.yMin + bottomPadding, pivotScreen.z));
        world.z = hand.transform.position.z;
        hand.transform.position = world;
    }
    private Bounds GetFaceBounds()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        bool found = false;
        Bounds bounds = new Bounds(transform.position, Vector3.one);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == hand || !renderer.enabled || renderer.sprite == null) continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return bounds;
    }

    private void EmitDeodorantMist(Bounds faceBounds)
    {
        if (softMistSprite == null) softMistSprite = CreateSoftMistSprite();

        Vector3 source = hand.transform.position + Vector3.up * (faceBounds.size.y * 0.48f);
        Vector3 target = new Vector3(
            Random.Range(faceBounds.min.x, faceBounds.max.x),
            Random.Range(faceBounds.min.y + faceBounds.size.y * 0.25f, faceBounds.max.y),
            source.z);
        Vector3 direction = (target - source).normalized;
        direction = Quaternion.Euler(0f, 0f, Random.Range(-18f, 18f)) * direction;

        GameObject puff = new GameObject("DeodorantMist");
        puff.transform.position = source;
        SpriteRenderer sprite = puff.AddComponent<SpriteRenderer>();
        sprite.sprite = softMistSprite;
        sprite.sortingLayerID = hand.sortingLayerID;
        sprite.sortingOrder = hand.sortingOrder + 1;
        sprite.color = new Color(0.88f, 0.96f, 1f, Random.Range(0.2f, 0.38f));
        float size = Random.Range(0.18f, 0.38f);
        puff.transform.localScale = Vector3.one * size;
        StartCoroutine(AnimateDeodorantMist(sprite, direction * Random.Range(0.7f, 1.2f), Random.Range(0.45f, 0.8f)));
    }

    private static Sprite CreateSoftMistSprite()
    {
        const int resolution = 32;
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        texture.name = "RuntimeDeodorantMist";
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            float dx = (x + 0.5f) / resolution * 2f - 1f;
            float dy = (y + 0.5f) / resolution * 2f - 1f;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = 1f - Mathf.SmoothStep(0.25f, 1f, radius);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
    }

    private IEnumerator AnimateDeodorantMist(SpriteRenderer sprite, Vector3 velocity, float lifetime)
    {
        if (sprite == null) yield break;
        Color startColor = sprite.color;
        Vector3 startPosition = sprite.transform.position;
        float startScale = sprite.transform.localScale.x;
        for (float elapsed = 0f; elapsed < lifetime; elapsed += Time.deltaTime)
        {
            if (sprite == null) yield break;
            float t = elapsed / lifetime;
            sprite.transform.position = startPosition + velocity * elapsed;
            sprite.transform.localScale = Vector3.one * Mathf.Lerp(startScale, startScale * 2.2f, t);
            Color color = startColor;
            color.a = startColor.a * (1f - t);
            sprite.color = color;
            yield return null;
        }
        if (sprite != null) Destroy(sprite.gameObject);
    }

    private bool IsOverEye(Eye eye, Vector3 world)
    {
        if (eye.root == null || eye.eyeBall == null) return false;
        Vector2 local = ToEye(eye, world) - eye.center;
        Vector2 r = Vector2.Max(eye.radius, Vector2.one * 0.001f);
        return (local.x * local.x) / (r.x * r.x) + (local.y * local.y) / (r.y * r.y) <= 1f;
    }

    private void UpdateEye(Eye eye)
    {
        if (eye.root == null || eye.pupil == null) return;

        eye.pupilTarget = ClampPupil(eye, eye.pupilTarget);
        eye.pupilCurrent = Vector2.Lerp(eye.pupilCurrent, eye.pupilTarget, Time.deltaTime * pupilFollowSpeed);
        eye.pupilCurrent = ClampPupil(eye, eye.pupilCurrent);
        eye.pupil.position = ToWorld(eye, eye.pupilCurrent, eye.pupil.position.z);

        float pupilTop = eye.pupilCurrent.y + eye.pupilExtents.y;
        float pupilBottom = eye.pupilCurrent.y - eye.pupilExtents.y;
        float lidStep = Time.deltaTime * lidFollowSpeed;
        float aimY = Aim(eye, eye.pupilCurrent).y;

        if (eye.topLid != null)
        {
            // Lazily drift towards the pupil within the lid's range, but always open far enough to clear it.
            float clearEdge = pupilTop + Mathf.Lerp(lidGap, lidGapAtEdge, Mathf.Max(0f, aimY));
            float edge = Mathf.Clamp(Mathf.Lerp(eye.topEdgeRest, clearEdge, lidFollow),
                eye.topEdgeRest - topLidMaxLower, eye.topEdgeRest + topLidMaxRaise);
            edge = Mathf.Max(edge, clearEdge);
            float wanted = edge - eye.topEdgeRest;
            // Opening snaps so the pupil is never covered; closing eases.
            eye.topOffset = wanted > eye.topOffset ? wanted : Mathf.Lerp(eye.topOffset, wanted, lidStep);
            eye.topLid.position = ToWorld(eye, eye.topLidRest + Vector2.up * eye.topOffset, eye.topLid.position.z);
        }

        if (eye.bottomLid != null)
        {
            float clearEdge = pupilBottom - Mathf.Lerp(lidGap, lidGapAtEdge, Mathf.Max(0f, -aimY));
            float edge = Mathf.Clamp(Mathf.Lerp(eye.bottomEdgeRest, clearEdge, lidFollow),
                eye.bottomEdgeRest - bottomLidMaxLower, eye.bottomEdgeRest + bottomLidMaxRaise);
            edge = Mathf.Min(edge, clearEdge);
            float wanted = edge - eye.bottomEdgeRest;
            eye.bottomOffset = wanted < eye.bottomOffset ? wanted : Mathf.Lerp(eye.bottomOffset, wanted, lidStep);
            eye.bottomLid.position = ToWorld(eye, eye.bottomLidRest + Vector2.up * eye.bottomOffset, eye.bottomLid.position.z);
        }
    }

    // Keeps the pupil inside the eyeball. The lids move out of its way rather than limiting it.
    private Vector2 ClampPupil(Eye eye, Vector2 position)
    {
        Vector2 room = Vector2.Max(eye.radius - eye.pupilExtents, Vector2.one * 0.001f);
        Vector2 offset = position - eye.center;
        Vector2 normalized = new Vector2(offset.x / room.x, offset.y / room.y);
        if (normalized.sqrMagnitude > 1f)
        {
            normalized.Normalize();
            offset = new Vector2(normalized.x * room.x, normalized.y * room.y);
        }
        return eye.center + offset;
    }

    private Vector2 Aim(Eye eye, Vector2 pupilPosition)
    {
        Vector2 room = Vector2.Max(eye.radius - eye.pupilExtents, Vector2.one * 0.001f);
        Vector2 offset = pupilPosition - eye.center;
        return new Vector2(Mathf.Clamp(offset.x / room.x, -1f, 1f), Mathf.Clamp(offset.y / room.y, -1f, 1f));
    }

    private static Vector2 ToEye(Eye eye, Vector3 world) => eye.root.InverseTransformPoint(world);

    private static Vector2 ToEyeSize(Eye eye, Vector3 worldExtents)
    {
        Vector3 scale = eye.root.lossyScale;
        return new Vector2(Mathf.Abs(worldExtents.x / scale.x), Mathf.Abs(worldExtents.y / scale.y));
    }

    private static Vector3 ToWorld(Eye eye, Vector2 local, float z)
    {
        Vector3 world = eye.root.TransformPoint(local);
        world.z = z;
        return world;
    }
}
