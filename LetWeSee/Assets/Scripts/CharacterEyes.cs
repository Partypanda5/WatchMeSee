using System.Collections;
using System.Collections.Generic;
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

    [Tooltip("How far below the bottom of the face view the end of the arm is kept, so it never shows.")]
    [SerializeField, Range(0f, 5f)] private float armBelowViewMargin = 1f;

    [Header("Blinking")]
    [Tooltip("Shortest and longest wait between blinks, in seconds.")]
    [SerializeField] private Vector2 blinkInterval = new Vector2(3f, 7f);
    [Tooltip("Time for the lids to close and open again, in seconds.")]
    [SerializeField, Range(0.1f, 3f)] private float blinkDuration = 1f;
    [Tooltip("Where the lids meet when closed: 0 = at the bottom lid, 1 = at the top lid.")]
    [SerializeField, Range(0f, 1f)] private float blinkMeetPoint = 0.3f;
    [Tooltip("How far the lids overlap when shut, so no gap shows.")]
    [SerializeField, Range(0f, 0.3f)] private float blinkOverlap = 0.03f;

    [Header("Face poking")]
    [Tooltip("How much of the mouse drag a grabbed face part follows.")]
    [SerializeField, Range(0f, 1f)] private float facePokeFollow = 0.12f;
    [Tooltip("Furthest a grabbed face part can be pulled from where it sits, in world units.")]
    [SerializeField, Range(0f, 1f)] private float facePokeMaxDistance = 0.15f;
    [SerializeField, Range(1f, 40f)] private float facePokeReturnSpeed = 14f;
    [Tooltip("Face parts that can't be grabbed (and their children).")]
    [SerializeField] private Transform[] facePokeIgnore = new Transform[0];

    [Header("Hand")]
    [Tooltip("Follows the pointer by its pivot (the fingertip). While dragging an eye it only moves as far as the pupil does.")]
    [SerializeField] private SpriteRenderer hand;
    [SerializeField] private Sprite pointingHand;
    [SerializeField] private Sprite pressingHand;
    [SerializeField] private Sprite deodorantSprayHand;
    [SerializeField] private Vector2 deodorantCanNozzleNormalized = new Vector2(-0.78f, 0.77f);
    [SerializeField] private Sprite deodorantIdleHand;
    [SerializeField] private Sprite mouthwashIdleHand;
    [SerializeField] private Sprite mouthwashHoldHand;
    [Tooltip("How much of the mouse movement the toothbrush follows while brushing (1 = normal).")]
    [SerializeField, Range(0.05f, 1f)] private float toothbrushBrushSensitivity = 0.35f;
    [Header("Coffee")]
    [SerializeField] private Sprite coffeeIdleHand;
    [SerializeField] private Sprite coffeeSipHand;
    [Tooltip("Normalized cup-rim position within the coffee hand sprite (lines up with the mouse).")]
    [SerializeField] private Vector2 coffeeCupTipNormalized = new Vector2(-0.46f, 0.42f);
    [SerializeField, Range(1, 10)] private int coffeeSipCount = 5;
    [Tooltip("How long the mouse has to be held over the mouth for one sip, in seconds.")]
    [SerializeField, Range(0.1f, 5f)] private float coffeeSipDuration = 1.2f;
    [Tooltip("Hand shake before the first sip, in world units.")]
    [SerializeField, Range(0f, 0.5f)] private float coffeeShakeStart = 0.01f;
    [Tooltip("Hand shake on the last sip, in world units. It ramps up faster towards the end.")]
    [SerializeField, Range(0f, 1f)] private float coffeeShakeEnd = 0.3f;
    [SerializeField, Range(0.5f, 30f)] private float coffeeShakeSpeed = 9f;
    [SerializeField] private Sprite toothbrushIdleHand;
    [SerializeField] private Sprite toothbrushHoldHand;
    [SerializeField] private Sprite holdingGlassesHand;
    [Header("Glasses and the normal man")]
    [Tooltip("Normalized centre of the glasses within the holding-glasses hand sprite (lines up with the mouse).")]
    [SerializeField] private Vector2 glassesTipNormalized = new Vector2(0f, 0.25f);
    [Tooltip("Glasses closer to the eyes than this (world units) can be put on with a click.")]
    [SerializeField, Range(0.05f, 5f)] private float glassesOverEyesDistance = 0.8f;
    [Tooltip("Shown instead of the face once the glasses are on (hidden until then).")]
    [SerializeField] private GameObject normalManRoot;
    [SerializeField] private SpriteRenderer normalManFace;
    [SerializeField] private Sprite normalManBlinkSprite;
    [SerializeField] private Transform normalManPupils;
    [Tooltip("How far right the normal man's pupils sit when looking away, in local units.")]
    [SerializeField, Range(0f, 2f)] private float normalManLookAwayOffset = 0.35f;
    [Tooltip("How quickly the pupils drift towards their next position.")]
    [SerializeField, Range(0.05f, 5f)] private float normalManGazeSpeed = 0.5f;
    [SerializeField] private SpriteRenderer mouthVisual;
    [Tooltip("Swapped with the normal mouth sprite while the player is talking.")]
    [SerializeField] private Sprite talkingMouthSprite;
    [SerializeField, Range(0.05f, 3f)] private float mouthFlapInterval = 0.7f;
    [Tooltip("Normalized bottle-tip position within the mouthwash hand sprite.")]
    [SerializeField] private Vector2 mouthwashBottleTipNormalized = new Vector2(-0.46f, 0.42f);
    [SerializeField] private Vector2 toothbrushTipNormalized = new Vector2(-0.48f, 0.4f);
    [SerializeField] private AudioClip deodorantSpraySound;
    [SerializeField] private AudioClip mouthwashSwishingSound;
    [SerializeField] private AudioClip toothBrushSound;
    [SerializeField] private AudioClip[] manMoanClips = new AudioClip[0];
    [SerializeField] private AudioClip dialogueGroansSound;
    [SerializeField] private AudioClip womanTalkSound;
    [SerializeField] private AudioClip sippingCoffeeSound;
    [SerializeField] private AudioClip faceTapSound;
    [SerializeField] private AudioClip eyeDragSquishSound;

    private InputAction pointerPosition;
    private InputAction eyeDrag;
    private Eye dragged;
    private AudioSource faceTapAudioSource;
    private AudioSource eyeDragAudioSource;
    private AudioSource deodorantSprayAudioSource;
    private AudioSource mouthwashAudioSource;
    private AudioSource coffeeSipAudioSource;
    private AudioSource toothbrushAudioSource;
    private AudioSource manMoanAudioSource;
    private AudioSource dialogueGroansAudioSource;
    private float manMoanDelay;
    private bool deodorantWasHeld;
    private int lastManMoanClipIndex = -1;
    private Vector2 lastDragScreenPosition;
    private float accumulatedDragScreenMovement;
    private float lastEyeMovementTime;
    private float dragSquishBasePitch = 1f;
    private float dragSquishPitchPhase;
    private bool faceTapPending;
    private bool mirrorCopyMode;
    private bool deodorantUseActive;
    private bool mouthwashUseActive;
    private bool toothbrushUseActive;
    private bool coffeeUseActive;
    private bool glassesUseActive;
    private bool normalManActive;
    private float normalManGaze;
    private float normalManGazeTarget;
    private Vector3 normalManPupilsRest;
    private Sprite normalManFaceSprite;
    private bool introDialogueLocked;
    private Coroutine introHandPopRoutine;
    private Sprite dialogueReturnHandSprite;
    private Vector3 dialogueReturnHandScale;
    private Quaternion dialogueReturnHandRotation;
    private Vector2 lastInteractionPointerPosition;
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
    private Transform pokedPart;
    private bool talking;
    private bool mouthFlapped;
    private float nextMouthFlap;
    private Sprite restingMouthSprite;
    private float blinkAmount;
    private float blinkStartTime = -1f;
    private float nextBlinkTime = -1f;
    private Vector3 pokeStartWorld;
    private Vector3 lastPokeWorld;
    private readonly Dictionary<Transform, Vector3> pokeOffsets = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3> pokeRest = new Dictionary<Transform, Vector3>();
    private readonly List<Transform> pokeFinished = new List<Transform>();
    private float leftBrowOffset;
    private float rightBrowOffset;
    private float turnOffset;

    private bool initialized;

    // Fired when the player grabs an eye, before the drag starts moving it.
    public event System.Action DragStarted;
    // Fired once the player puts the glasses on and the face has become the normal man.
    public event System.Action GlassesPutOn;

    // -1..1 aim of each pupil within its reachable range: where the pupil is now...
    public Vector2 LeftAim => Aim(leftEye, leftEye.pupilCurrent);
    public Vector2 RightAim => Aim(rightEye, rightEye.pupilCurrent);
    // ...and where it is heading.
    public Vector2 LeftAimTarget => Aim(leftEye, leftEye.pupilTarget);
    public Vector2 RightAimTarget => Aim(rightEye, rightEye.pupilTarget);
    public Sprite PointingHandSprite => pointingHand;

    // The glasses follow the mouse in the hand. Clicking with them over the eyes puts them on.
    public void HoldGlasses()
    {
        if (mirrorCopyMode || hand == null || holdingGlassesHand == null || glassesUseActive || normalManActive) return;
        glassesUseActive = true;
        dragged = null;
        faceTapPending = false;
        if (eyeDragAudioSource != null) eyeDragAudioSource.Stop();
        StartCoroutine(GlassesUseSequence());
    }

    // 0 = the normal man looks off to the right, 1 = straight at the player. The pupils drift there slowly.
    public void SetNormalManGaze(float lookAtPlayer)
    {
        normalManGazeTarget = Mathf.Clamp01(lookAtPlayer);
    }

    private IEnumerator GlassesUseSequence()
    {
        hand.sprite = holdingGlassesHand;
        hand.enabled = true;

        while (true)
        {
            hand.sprite = holdingGlassesHand;
            hand.enabled = true;
            MoveInteractionHandTipWithPointer(glassesTipNormalized);
            Vector3 glassesCentre = hand.transform.position + GetUseHandTipOffset(glassesTipNormalized);
            Vector3 eyesCentre = (leftEye.eyeBall.bounds.center + rightEye.eyeBall.bounds.center) * 0.5f;
            float distance = Vector2.Distance(glassesCentre, eyesCentre);

            if (eyeDrag.WasPressedThisFrame() && distance <= glassesOverEyesDistance)
                break;
            yield return null;
        }

        PutOnGlasses();
    }

    private void PutOnGlasses()
    {
        glassesUseActive = false;
        normalManActive = true;

        // Everything else on the face goes, including the hand, so there's nothing left to poke or drag.
        foreach (Transform child in transform)
            if (normalManRoot == null || child != normalManRoot.transform)
                child.gameObject.SetActive(false);
        if (normalManRoot != null) normalManRoot.SetActive(true);
        if (normalManFace != null) normalManFaceSprite = normalManFace.sprite;
        if (normalManPupils != null) normalManPupilsRest = normalManPupils.localPosition;
        normalManGaze = normalManGazeTarget = 0f;
        UpdateNormalMan();
        GlassesPutOn?.Invoke();
    }

    // Pupils drift from looking right towards the player, and the normal man blinks now and then.
    private void UpdateNormalMan()
    {
        normalManGaze = Mathf.MoveTowards(normalManGaze, normalManGazeTarget, Time.deltaTime * normalManGazeSpeed);
        if (normalManPupils != null)
            normalManPupils.localPosition = normalManPupilsRest + Vector3.right * (normalManLookAwayOffset * (1f - normalManGaze));

        if (nextBlinkTime < 0f) ScheduleNextBlink();
        if (blinkStartTime < 0f && Time.time >= nextBlinkTime) blinkStartTime = Time.time;
        bool blinkingNow = blinkStartTime >= 0f && Time.time - blinkStartTime < 0.15f;
        if (blinkStartTime >= 0f && !blinkingNow)
        {
            blinkStartTime = -1f;
            ScheduleNextBlink();
        }
        if (normalManFace != null && normalManBlinkSprite != null && normalManFaceSprite != null)
            normalManFace.sprite = blinkingNow ? normalManBlinkSprite : normalManFaceSprite;
        if (normalManPupils != null && normalManPupils.gameObject.activeSelf == blinkingNow)
            normalManPupils.gameObject.SetActive(!blinkingNow);
    }
    public bool IsUsingDeodorant => deodorantUseActive;
    public bool IsUsingMouthwash => mouthwashUseActive;
    public bool IsUsingToothbrush => toothbrushUseActive;
    public bool IsUsingCoffee => coffeeUseActive;
    // Once the normal man is showing, the old mouth is hidden, so use where his mouth is drawn on his face sprite.
    public Vector3 MouthWorldPosition => normalManActive && normalManFace != null
        ? normalManFace.bounds.center + new Vector3(normalManFace.bounds.size.x * 0.118f, -normalManFace.bounds.size.y * 0.083f, 0f)
        : mouthVisual != null ? mouthVisual.bounds.center : transform.position;

    // Called after the deodorant pickup animation completes. The player can spray by holding LMB.
    public void BeginDeodorantUse()
    {
        if (mirrorCopyMode || hand == null || deodorantIdleHand == null || eyeDrag == null || deodorantUseActive || mouthwashUseActive || toothbrushUseActive || coffeeUseActive)
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

    // Starts the mouthwash action after its pickup animation. Mouse motion moves the idle hand;
    // a held click on the mouth locks the hand and hides the mouth until release.
    public void BeginMouthwashUse()
    {
        if (mirrorCopyMode || hand == null || mouthwashIdleHand == null || eyeDrag == null ||
            deodorantUseActive || mouthwashUseActive || toothbrushUseActive || coffeeUseActive)
            return;

        mouthwashUseActive = true;
        dragged = null;
        faceTapPending = false;
        if (eyeDragAudioSource != null) eyeDragAudioSource.Stop();
        originalHandSprite = hand.sprite;
        originalHandPosition = hand.transform.position;
        originalHandScale = hand.transform.localScale;
        originalHandRotation = hand.transform.localRotation;
        originalHandEnabled = hand.enabled;
        StartCoroutine(MouthwashUseSequence(false));
    }

    // Coffee: a few short sips over the mouth, the hand getting shakier after each one.
    public void BeginCoffeeUse()
    {
        if (mirrorCopyMode || hand == null || (coffeeIdleHand == null && mouthwashIdleHand == null) || eyeDrag == null ||
            deodorantUseActive || mouthwashUseActive || toothbrushUseActive || coffeeUseActive)
            return;

        coffeeUseActive = true;
        dragged = null;
        faceTapPending = false;
        if (eyeDragAudioSource != null) eyeDragAudioSource.Stop();
        originalHandSprite = hand.sprite;
        originalHandPosition = hand.transform.position;
        originalHandScale = hand.transform.localScale;
        originalHandRotation = hand.transform.localRotation;
        originalHandEnabled = hand.enabled;
        StartCoroutine(CoffeeUseSequence());
    }

    // Called after the toothbrush pickup animation completes.
    public void BeginToothbrushUse()
    {
        if (mirrorCopyMode || hand == null || toothbrushIdleHand == null || eyeDrag == null ||
            deodorantUseActive || mouthwashUseActive || toothbrushUseActive || coffeeUseActive)
            return;

        toothbrushUseActive = true;
        dragged = null;
        faceTapPending = false;
        if (eyeDragAudioSource != null) eyeDragAudioSource.Stop();
        originalHandSprite = hand.sprite;
        originalHandPosition = hand.transform.position;
        originalHandScale = hand.transform.localScale;
        originalHandRotation = hand.transform.localRotation;
        originalHandEnabled = hand.enabled;
        StartCoroutine(ToothbrushUseSequence());
    }
    public void SetIntroDialogueLocked(bool locked)
    {
        introDialogueLocked = locked;
        if (!locked) return;

        dragged = null;
        faceTapPending = false;
        if (hand != null) hand.enabled = false;
    }

    public void PopHandUpAfterIntroDialogue()
    {
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (hand == null || cam == null || pointerPosition == null)
        {
            introDialogueLocked = false;
            return;
        }

        if (introHandPopRoutine != null) StopCoroutine(introHandPopRoutine);
        introDialogueLocked = true;
        hand.sprite = dialogueReturnHandSprite;
        hand.transform.localScale = dialogueReturnHandScale;
        hand.transform.localRotation = dialogueReturnHandRotation;
        hand.enabled = true;

        Rect panel = cam.pixelRect;
        Vector2 pointer = pointerPosition.ReadValue<Vector2>();
        pointer.x = Mathf.Clamp(pointer.x, panel.xMin + 2f, panel.xMax - 2f);
        pointer.y = Mathf.Clamp(pointer.y, panel.yMin + 2f, panel.yMax - 2f);
        float depth = -cam.transform.position.z;
        Vector3 end = cam.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, depth));
        end.z = hand.transform.position.z;
        Vector3 start = cam.ScreenToWorldPoint(new Vector3(
            pointer.x, panel.yMin - Mathf.Max(60f, Screen.height * 0.08f), depth));
        start.z = hand.transform.position.z;
        hand.transform.position = start;
        introHandPopRoutine = StartCoroutine(AnimateIntroHandPop(start, end));
    }

    private IEnumerator AnimateIntroHandPop(Vector3 start, Vector3 end)
    {
        const float duration = 0.55f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f);
            hand.transform.position = Vector3.LerpUnclamped(start, end, eased);
            yield return null;
        }

        hand.transform.position = end;
        introHandPopRoutine = null;
        introDialogueLocked = false;
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
        // The copy keeps its own sprite masks (remapped by the mirror setup), so its eyelids show and blink like the real
        // face, and the hand stays clipped to the mirror glass.
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

    private void SetAim(Eye eye, Vector2 aim)
    {
        if (eye.root == null || eye.pupil == null) return;
        Vector2 room = Vector2.Max(eye.radius - eye.pupilExtents, Vector2.one * 0.001f);
        eye.pupilTarget = ClampPupil(eye, eye.center + new Vector2(aim.x * room.x, aim.y * room.y));
    }

    private void Awake()
    {
        EnsureInitialized();
        if (hand != null)
        {
            dialogueReturnHandSprite = pointingHand != null ? pointingHand : hand.sprite;
            dialogueReturnHandScale = hand.transform.localScale;
            dialogueReturnHandRotation = hand.transform.localRotation;
        }
        if (deodorantSpraySound != null)
        {
            deodorantSprayAudioSource = gameObject.AddComponent<AudioSource>();
            deodorantSprayAudioSource.playOnAwake = false;
            deodorantSprayAudioSource.spatialBlend = 0f;
        }
        if (manMoanClips != null && manMoanClips.Length > 0)
        {
            manMoanAudioSource = gameObject.AddComponent<AudioSource>();
            manMoanAudioSource.playOnAwake = false;
            manMoanAudioSource.spatialBlend = 0f;
        }

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
        SetDeodorantSprayAudio(false);
        StopManMoanAudio();
        SetMouthwashAudio(false);
        SetCoffeeSipAudio(false);
        SetToothbrushAudio(false);
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
        if (normalManActive)
        {
            UpdateNormalMan();
            return;
        }

        if (!introDialogueLocked && !glassesUseActive && !deodorantUseActive && !mouthwashUseActive && !toothbrushUseActive && !coffeeUseActive && pointerPosition != null && eyeDrag != null)
            HandleInput();

        UpdateDragSquishAudio();
        UpdateBlink();
        UpdateMouthFlap();
        UpdateEye(leftEye);
        UpdateEye(rightEye);
        UpdateFaceFeatures();
        UpdateFacePokes();
    }

    // While talking, the mouth swaps between its normal sprite and the talking sprite every mouthFlapInterval.
    public void SetTalking(bool value)
    {
        if (mouthVisual == null || talkingMouthSprite == null || value == talking) return;
        talking = value;
        if (talking)
        {
            restingMouthSprite = mouthVisual.sprite;
            mouthFlapped = true;
            mouthVisual.sprite = talkingMouthSprite;
            nextMouthFlap = Time.time + mouthFlapInterval;
        }
        else if (restingMouthSprite != null)
        {
            mouthVisual.sprite = restingMouthSprite;
            mouthFlapped = false;
        }
    }

    private void UpdateMouthFlap()
    {
        if (!talking || mouthVisual == null || Time.time < nextMouthFlap) return;
        mouthFlapped = !mouthFlapped;
        mouthVisual.sprite = mouthFlapped ? talkingMouthSprite : restingMouthSprite;
        nextMouthFlap = Time.time + mouthFlapInterval;
    }

    // Every few seconds both eyes blink: the lids close and open over blinkDuration. Never while an eye is being dragged.
    private void UpdateBlink()
    {
        if (nextBlinkTime < 0f) ScheduleNextBlink();
        bool draggingEye = dragged != null && eyeDrag != null && eyeDrag.IsPressed();
        if (draggingEye)
        {
            blinkStartTime = -1f;
            blinkAmount = Mathf.MoveTowards(blinkAmount, 0f, Time.deltaTime * 6f);
            ScheduleNextBlink();
            return;
        }

        if (blinkStartTime < 0f && Time.time >= nextBlinkTime) blinkStartTime = Time.time;
        if (blinkStartTime >= 0f)
        {
            float t = (Time.time - blinkStartTime) / Mathf.Max(0.01f, blinkDuration);
            if (t >= 1f)
            {
                blinkStartTime = -1f;
                ScheduleNextBlink();
                t = 1f;
            }
            blinkAmount = Mathf.Sin(t * Mathf.PI);
        }
        else
        {
            blinkAmount = Mathf.MoveTowards(blinkAmount, 0f, Time.deltaTime * 6f);
        }
    }

    private void ScheduleNextBlink()
    {
        nextBlinkTime = Time.time + Random.Range(Mathf.Min(blinkInterval.x, blinkInterval.y), Mathf.Max(blinkInterval.x, blinkInterval.y));
    }

    // Grabbing a face part (anything but the eyes and hand) and dragging nudges it slightly; it springs back on release.
    private void StartFacePoke(Vector3 world)
    {
        pokedPart = null;
        int highestOrder = int.MinValue;
        foreach (SpriteRenderer part in GetComponentsInChildren<SpriteRenderer>())
        {
            if (!part.enabled || part == hand || IsPartOfEye(part.transform) || IsPokeIgnored(part.transform)) continue;
            Bounds b = part.bounds;
            if (world.x < b.min.x || world.x > b.max.x || world.y < b.min.y || world.y > b.max.y) continue;
            if (part.sortingOrder <= highestOrder) continue;
            highestOrder = part.sortingOrder;
            pokedPart = part.transform;
        }
        if (pokedPart == null) return;
        pokeStartWorld = world;
        if (!pokeOffsets.ContainsKey(pokedPart)) pokeOffsets[pokedPart] = Vector3.zero;
        if (!IsGazeDrivenPart(pokedPart) && !pokeRest.ContainsKey(pokedPart)) pokeRest[pokedPart] = pokedPart.localPosition;
    }

    private void UpdateFacePokes()
    {
        if (pokeOffsets.Count == 0) return;
        bool held = eyeDrag != null && eyeDrag.IsPressed() && pokedPart != null;
        if (!held) pokedPart = null;
        float blend = 1f - Mathf.Exp(-facePokeReturnSpeed * Time.deltaTime);

        pokeFinished.Clear();
        foreach (Transform part in new List<Transform>(pokeOffsets.Keys))
        {
            if (part == null) { pokeFinished.Add(part); continue; }
            Vector3 wanted = Vector3.zero;
            if (held && part == pokedPart && part.parent != null)
            {
                Vector3 worldPull = (lastPokeWorld - pokeStartWorld) * facePokeFollow;
                worldPull = Vector3.ClampMagnitude(new Vector3(worldPull.x, worldPull.y, 0f), facePokeMaxDistance);
                wanted = part.parent.InverseTransformVector(worldPull);
            }
            Vector3 offset = Vector3.Lerp(pokeOffsets[part], wanted, blend);
            pokeOffsets[part] = offset;

            // Brows and turning parts are repositioned by the gaze every frame, so the nudge is added on top.
            if (IsGazeDrivenPart(part)) part.localPosition += offset;
            else if (pokeRest.TryGetValue(part, out Vector3 rest)) part.localPosition = rest + offset;

            if (part != pokedPart && offset.sqrMagnitude < 0.0000001f) pokeFinished.Add(part);
        }
        foreach (Transform part in pokeFinished)
        {
            if (part != null && pokeRest.TryGetValue(part, out Vector3 rest)) part.localPosition = rest;
            pokeOffsets.Remove(part);
            pokeRest.Remove(part);
        }
    }

    private bool IsPokeIgnored(Transform part)
    {
        foreach (Transform ignored in facePokeIgnore)
            if (ignored != null && (part == ignored || part.IsChildOf(ignored))) return true;
        return false;
    }

    private bool IsPartOfEye(Transform part)
    {
        return (leftEye.root != null && (part == leftEye.root || part.IsChildOf(leftEye.root))) ||
            (rightEye.root != null && (part == rightEye.root || part.IsChildOf(rightEye.root)));
    }

    private bool IsGazeDrivenPart(Transform part)
    {
        if (part == leftBrow || part == rightBrow) return true;
        foreach (Transform turning in turningParts)
            if (turning == part) return true;
        return false;
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
                StartFacePoke(world);
            }
        }
        lastPokeWorld = world;

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
        Sprite wanted = eyeDrag.IsPressed() ? pressingHand : pointingHand;
        if (wanted != null) hand.sprite = wanted;
        hand.transform.position = new Vector3(tip.x, tip.y, hand.transform.position.z);
        hand.transform.localScale = Vector3.one * 0.88f;
        hand.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
        KeepArmReachingViewBottom(cam);
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
        float referenceHeight = pointingHand != null ? pointingHand.bounds.size.y : 1f;
        float idleHandHeight = Mathf.Max(0.01f, deodorantIdleHand.bounds.size.y);
        hand.transform.localScale = Vector3.one * (0.88f * referenceHeight / idleHandHeight);
        hand.sprite = deodorantIdleHand;
        hand.enabled = true;
        lastInteractionPointerPosition = pointerPosition.ReadValue<Vector2>();

        const float requiredHoldDuration = 4f;
        float heldDuration = 0f;
        float emissionTimer = 0f;
        while (heldDuration < requiredHoldDuration)
        {
            MoveInteractionHandWithPointer();
            if (eyeDrag.IsPressed())
            {
                SetDeodorantSprayAudio(true);
                if (!deodorantWasHeld)
                {
                    deodorantWasHeld = true;
                    manMoanDelay = Random.Range(0.15f, 0.65f);
                }
                UpdateManMoanAudio();
                hand.sprite = deodorantSprayHand != null ? deodorantSprayHand : deodorantIdleHand;
                heldDuration += Time.deltaTime;
                emissionTimer += Time.deltaTime;
                while (emissionTimer >= 0.025f)
                {
                    emissionTimer -= 0.025f;
                    EmitDeodorantMist(faceBounds);
                }
            }
            else
            {
                SetDeodorantSprayAudio(false);
                StopManMoanAudio();
                deodorantWasHeld = false;
                // The four-second action requires one continuous hold. Releasing early cancels it.
                hand.sprite = deodorantIdleHand;
                heldDuration = 0f;
                emissionTimer = 0f;
            }
            yield return null;
        }

        SetDeodorantSprayAudio(false);
        StopManMoanAudio();
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

    private void SetDeodorantSprayAudio(bool shouldPlay)
    {
        if (deodorantSpraySound == null) return;
        if (deodorantSprayAudioSource == null)
        {
            deodorantSprayAudioSource = gameObject.AddComponent<AudioSource>();
            deodorantSprayAudioSource.playOnAwake = false;
            deodorantSprayAudioSource.spatialBlend = 0f;
        }

        if (shouldPlay)
        {
            if (deodorantSprayAudioSource.clip != deodorantSpraySound || !deodorantSprayAudioSource.loop)
            {
                deodorantSprayAudioSource.Stop();
                deodorantSprayAudioSource.clip = deodorantSpraySound;
                deodorantSprayAudioSource.loop = true;
            }
            if (!deodorantSprayAudioSource.isPlaying) deodorantSprayAudioSource.Play();
        }
        else if (deodorantSprayAudioSource.isPlaying)
        {
            deodorantSprayAudioSource.Stop();
        }
    }

    public void PlayDialogueVoice(bool dateIsSpeaking)
    {
        AudioClip voiceClip = dateIsSpeaking ? womanTalkSound : dialogueGroansSound;
        if (voiceClip == null) return;
        if (dialogueGroansAudioSource == null)
        {
            dialogueGroansAudioSource = gameObject.AddComponent<AudioSource>();
            dialogueGroansAudioSource.playOnAwake = false;
            dialogueGroansAudioSource.spatialBlend = 0f;
        }

        dialogueGroansAudioSource.PlayOneShot(voiceClip);
    }
    private void UpdateManMoanAudio()
    {
        if (manMoanClips == null || manMoanClips.Length == 0 || manMoanAudioSource == null ||
            manMoanAudioSource.isPlaying)
            return;

        manMoanDelay -= Time.deltaTime;
        if (manMoanDelay > 0f) return;

        int clipIndex = Random.Range(0, manMoanClips.Length);
        if (manMoanClips.Length > 1 && clipIndex == lastManMoanClipIndex)
            clipIndex = (clipIndex + Random.Range(1, manMoanClips.Length)) % manMoanClips.Length;
        lastManMoanClipIndex = clipIndex;

        AudioClip clip = manMoanClips[clipIndex];
        if (clip == null) return;
        manMoanAudioSource.clip = clip;
        manMoanAudioSource.loop = false;
        manMoanAudioSource.Play();
        manMoanDelay = Random.Range(0.35f, 1f);
    }

    private void StopManMoanAudio()
    {
        deodorantWasHeld = false;
        if (manMoanAudioSource != null && manMoanAudioSource.isPlaying)
            manMoanAudioSource.Stop();
    }
    private void MoveInteractionHandWithPointer()
    {
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam == null || pointerPosition == null) return;

        Vector2 pointer = pointerPosition.ReadValue<Vector2>();
        float depth = -cam.transform.position.z;
        Vector3 previousPointerWorld = cam.ScreenToWorldPoint(new Vector3(
            lastInteractionPointerPosition.x, lastInteractionPointerPosition.y, depth));
        Vector3 currentPointerWorld = cam.ScreenToWorldPoint(new Vector3(
            pointer.x, pointer.y, depth));
        Vector3 handPosition = hand.transform.position + (currentPointerWorld - previousPointerWorld);
        lastInteractionPointerPosition = pointer;

        Vector3 viewportPosition = cam.WorldToViewportPoint(handPosition);
        viewportPosition.x = Mathf.Clamp(viewportPosition.x, 0.02f, 0.98f);
        viewportPosition.y = Mathf.Clamp(viewportPosition.y, 0.02f, 0.98f);
        handPosition = cam.ViewportToWorldPoint(viewportPosition);
        handPosition.z = hand.transform.position.z;
        hand.transform.position = handPosition;
    }
    private IEnumerator MouthwashUseSequence(bool coffeeSip)
    {
        hand.sprite = mouthwashIdleHand;
        MoveInteractionHandTipWithPointer(mouthwashBottleTipNormalized);
        bool restoreMouth = mouthVisual != null && mouthVisual.enabled;
        const float requiredHoldDuration = 4f;
        float heldDuration = 0f;

        while (heldDuration < requiredHoldDuration)
        {
            Vector2 pointer = pointerPosition.ReadValue<Vector2>();
            if (eyeDrag.WasPressedThisFrame() && IsPointerOverMouth(pointer))
            {
                if (mouthVisual != null) mouthVisual.enabled = false;
                hand.sprite = mouthwashHoldHand != null ? mouthwashHoldHand : mouthwashIdleHand;
                MoveInteractionHandTipWithPointer(mouthwashBottleTipNormalized);
                if (coffeeSip) SetCoffeeSipAudio(true);
                else SetMouthwashAudio(true);

                while (eyeDrag.IsPressed() && heldDuration < requiredHoldDuration)
                {
                    heldDuration += Time.deltaTime;
                    yield return null;
                }

                if (coffeeSip)
        {
            SetCoffeeSipAudio(false);
        }
        else
        {
            SetMouthwashAudio(false);
        }
                if (mouthVisual != null) mouthVisual.enabled = restoreMouth;

                if (heldDuration >= requiredHoldDuration)
                    break;

                // An early release pauses the action. The player can reposition the hand
                // and resume by pressing over the mouth; held progress is kept.
                hand.sprite = mouthwashIdleHand;
                MoveInteractionHandTipWithPointer(mouthwashBottleTipNormalized);
            }
            else if (!eyeDrag.IsPressed())
            {
                MoveInteractionHandTipWithPointer(mouthwashBottleTipNormalized);
            }

            yield return null;
        }

        if (coffeeSip)
        {
            SetCoffeeSipAudio(false);
        }
        else
        {
            SetMouthwashAudio(false);
        }
        if (mouthVisual != null) mouthVisual.enabled = restoreMouth;
        hand.sprite = pointingHand != null ? pointingHand : originalHandSprite;
        hand.transform.localScale = originalHandScale;
        hand.transform.localRotation = originalHandRotation;
        hand.enabled = true;
        if (coffeeSip)
        {
            coffeeUseActive = false;
        }
        else
        {
            mouthwashUseActive = false;
        }
    }
    private IEnumerator CoffeeUseSequence()
    {
        Sprite cupHand = coffeeIdleHand != null ? coffeeIdleHand : mouthwashIdleHand;
        Sprite sipHand = coffeeSipHand != null ? coffeeSipHand : mouthwashHoldHand != null ? mouthwashHoldHand : cupHand;
        bool restoreMouth = mouthVisual != null && mouthVisual.enabled;
        int sipsTaken = 0;
        hand.sprite = cupHand;
        MoveInteractionHandTipWithPointer(coffeeCupTipNormalized);

        while (sipsTaken < coffeeSipCount)
        {
            float progress = coffeeSipCount > 1 ? sipsTaken / (float)(coffeeSipCount - 1) : 1f;
            float shake = Mathf.Lerp(coffeeShakeStart, coffeeShakeEnd, progress * progress);
            Vector2 pointer = pointerPosition.ReadValue<Vector2>();
            if (eyeDrag.WasPressedThisFrame() && IsPointerOverMouth(pointer))
            {
                if (mouthVisual != null) mouthVisual.enabled = false;
                hand.sprite = sipHand;
                MoveInteractionHandTipWithPointer(coffeeCupTipNormalized);
                Vector3 sipPosition = hand.transform.position;
                SetCoffeeSipAudio(true);

                float held = 0f;
                while (eyeDrag.IsPressed() && held < coffeeSipDuration)
                {
                    held += Time.deltaTime;
                    hand.transform.position = sipPosition + CoffeeShake(shake);
                    yield return null;
                }

                // A full hold is one sip; letting go early doesn't count. Either way it's back to the cup.
                SetCoffeeSipAudio(false);
                if (mouthVisual != null) mouthVisual.enabled = restoreMouth;
                hand.sprite = cupHand;
                if (held >= coffeeSipDuration) sipsTaken++;
            }
            else
            {
                MoveInteractionHandTipWithPointer(coffeeCupTipNormalized);
                hand.transform.position += CoffeeShake(shake);
            }
            yield return null;
        }

        SetCoffeeSipAudio(false);
        if (mouthVisual != null) mouthVisual.enabled = restoreMouth;
        hand.sprite = pointingHand != null ? pointingHand : originalHandSprite;
        hand.transform.localScale = originalHandScale;
        hand.transform.localRotation = originalHandRotation;
        hand.enabled = true;
        coffeeUseActive = false;
    }

    // Smooth random wobble for the coffee hand.
    private Vector3 CoffeeShake(float amount)
    {
        if (amount <= 0f) return Vector3.zero;
        float time = Time.time * coffeeShakeSpeed;
        return new Vector3(
            (Mathf.PerlinNoise(time, 0.37f) - 0.5f) * 2f * amount,
            (Mathf.PerlinNoise(0.71f, time) - 0.5f) * 2f * amount,
            0f);
    }

    private IEnumerator ToothbrushUseSequence()
    {
        hand.sprite = toothbrushIdleHand;
        MoveInteractionHandTipWithPointer(toothbrushTipNormalized);
        bool restoreMouth = mouthVisual != null && mouthVisual.enabled;
        const float requiredBrushDuration = 4f;
        const float movementThreshold = 0.5f;
        const float movementGracePeriod = 0.2f;
        float brushedDuration = 0f;

        while (brushedDuration < requiredBrushDuration)
        {
            Vector2 pointer = pointerPosition.ReadValue<Vector2>();
            if (eyeDrag.WasPressedThisFrame() && IsPointerOverMouth(pointer))
            {
                Camera faceCamera = viewCamera != null ? viewCamera : Camera.main;
                Rect brushingArea = GetMouthScreenRect(faceCamera);
                // Keep brush strokes away from the far upper-left edge of the mouth.
                brushingArea.xMin += brushingArea.width * 0.24f;
                brushingArea.xMax -= brushingArea.width * 0.12f;
                brushingArea.yMax -= brushingArea.height * 0.24f;
                if (mouthVisual != null) mouthVisual.enabled = false;
                hand.sprite = toothbrushHoldHand != null ? toothbrushHoldHand : toothbrushIdleHand;
                Vector2 brushPointer = ClampPointerToMouth(pointer, brushingArea);
                MoveInteractionHandTipWithPointer(toothbrushTipNormalized, brushPointer);
                Vector2 lastBrushPointer = brushPointer;
                Vector2 lastMousePosition = pointer;
                float lastBrushMovementTime = float.NegativeInfinity;

                while (eyeDrag.IsPressed() && brushedDuration < requiredBrushDuration)
                {
                    // While brushing, the brush only follows part of the mouse movement.
                    Vector2 mousePosition = pointerPosition.ReadValue<Vector2>();
                    brushPointer = ClampPointerToMouth(
                        brushPointer + (mousePosition - lastMousePosition) * toothbrushBrushSensitivity, brushingArea);
                    lastMousePosition = mousePosition;
                    MoveInteractionHandTipWithPointer(toothbrushTipNormalized, brushPointer);

                    if ((brushPointer - lastBrushPointer).sqrMagnitude >= movementThreshold * movementThreshold)
                        lastBrushMovementTime = Time.time;
                    lastBrushPointer = brushPointer;

                    // Small pauses between strokes are fine, but simply holding still
                    // does not count toward the four seconds of brushing.
                    bool brushingMotionActive = Time.time - lastBrushMovementTime <= movementGracePeriod;
                    SetToothbrushAudio(brushingMotionActive);
                    if (brushingMotionActive)
                        brushedDuration += Time.deltaTime;

                    yield return null;
                }

                SetToothbrushAudio(false);
                if (mouthVisual != null) mouthVisual.enabled = restoreMouth;
                if (brushedDuration >= requiredBrushDuration)
                    break;

                // Releasing early pauses brushing. Keep earned time and let the player
                // resume by pressing over the mouth again.
                hand.sprite = toothbrushIdleHand;
                MoveInteractionHandTipWithPointer(toothbrushTipNormalized);
            }
            else if (!eyeDrag.IsPressed())
            {
                MoveInteractionHandTipWithPointer(toothbrushTipNormalized);
            }

            yield return null;
        }

        SetToothbrushAudio(false);
        if (mouthVisual != null) mouthVisual.enabled = restoreMouth;
        hand.sprite = pointingHand != null ? pointingHand : originalHandSprite;
        hand.transform.localScale = originalHandScale;
        hand.transform.localRotation = originalHandRotation;
        hand.enabled = true;
        toothbrushUseActive = false;
    }
    private void SetMouthwashAudio(bool shouldPlay)
    {
        if (mouthwashAudioSource == null && mouthwashSwishingSound != null)
        {
            mouthwashAudioSource = gameObject.AddComponent<AudioSource>();
            mouthwashAudioSource.playOnAwake = false;
            mouthwashAudioSource.spatialBlend = 0f;
        }
        if (mouthwashAudioSource == null) return;

        if (shouldPlay && mouthwashSwishingSound != null)
        {
            if (mouthwashAudioSource.clip != mouthwashSwishingSound || !mouthwashAudioSource.loop)
            {
                mouthwashAudioSource.Stop();
                mouthwashAudioSource.clip = mouthwashSwishingSound;
                mouthwashAudioSource.loop = true;
            }
            if (!mouthwashAudioSource.isPlaying) mouthwashAudioSource.Play();
        }
        else if (mouthwashAudioSource.isPlaying)
        {
            mouthwashAudioSource.Stop();
        }
    }
    private void SetCoffeeSipAudio(bool shouldPlay)
    {
        if (coffeeSipAudioSource == null && shouldPlay && sippingCoffeeSound != null)
        {
            coffeeSipAudioSource = gameObject.AddComponent<AudioSource>();
            coffeeSipAudioSource.playOnAwake = false;
            coffeeSipAudioSource.spatialBlend = 0f;
        }
        if (coffeeSipAudioSource == null) return;

        if (shouldPlay && sippingCoffeeSound != null)
        {
            if (coffeeSipAudioSource.clip != sippingCoffeeSound || !coffeeSipAudioSource.loop)
            {
                coffeeSipAudioSource.Stop();
                coffeeSipAudioSource.clip = sippingCoffeeSound;
                coffeeSipAudioSource.loop = true;
            }
            if (!coffeeSipAudioSource.isPlaying) coffeeSipAudioSource.Play();
        }
        else if (coffeeSipAudioSource.isPlaying)
        {
            coffeeSipAudioSource.Stop();
        }
    }
    private void SetToothbrushAudio(bool shouldPlay)
    {
        if (toothbrushAudioSource == null && shouldPlay && toothBrushSound != null)
        {
            toothbrushAudioSource = gameObject.AddComponent<AudioSource>();
            toothbrushAudioSource.playOnAwake = false;
            toothbrushAudioSource.spatialBlend = 0f;
        }
        if (toothbrushAudioSource == null) return;

        if (shouldPlay && toothBrushSound != null)
        {
            if (toothbrushAudioSource.clip != toothBrushSound || !toothbrushAudioSource.loop)
            {
                toothbrushAudioSource.Stop();
                toothbrushAudioSource.clip = toothBrushSound;
                toothbrushAudioSource.loop = true;
            }
            if (!toothbrushAudioSource.isPlaying) toothbrushAudioSource.Play();
        }
        else if (toothbrushAudioSource.isPlaying)
        {
            toothbrushAudioSource.Stop();
        }
    }
    private void MoveInteractionHandTipWithPointer(Vector2 tipNormalized)
    {
        if (pointerPosition == null) return;
        MoveInteractionHandTipWithPointer(tipNormalized, pointerPosition.ReadValue<Vector2>());
    }

    private void MoveInteractionHandTipWithPointer(Vector2 tipNormalized, Vector2 pointerScreen)
    {
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam == null || hand == null) return;

        // Keep the fingertip inside the face panel so it never vanishes when the mouse
        // drifts over the divider or the world panel.
        Rect panel = cam.pixelRect;
        Vector2 screen = pointerScreen;
        const float edgeInset = 2f;
        screen.x = Mathf.Clamp(screen.x, panel.xMin + edgeInset, panel.xMax - edgeInset);
        screen.y = Mathf.Clamp(screen.y, panel.yMin + edgeInset, panel.yMax - edgeInset);

        Vector3 pointerWorld = cam.ScreenToWorldPoint(new Vector3(
            screen.x, screen.y, -cam.transform.position.z));
        pointerWorld.z = hand.transform.position.z;
        hand.enabled = true;
        hand.transform.localScale = Vector3.one * 0.88f;
        hand.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
        hand.transform.position = pointerWorld - GetUseHandTipOffset(tipNormalized);
        KeepArmReachingViewBottom(cam);
    }

    // The arm art is shorter than the face view, so stop the hand rising past the point where
    // the bottom of the arm sprite would come up off the bottom edge of the view.
    private void KeepArmReachingViewBottom(Camera cam)
    {
        if (hand == null || cam == null || hand.sprite == null) return;
        float depth = hand.transform.position.z - cam.transform.position.z;
        float viewBottom = cam.ViewportToWorldPoint(new Vector3(0.5f, 0f, depth)).y;
        float lowestAllowed = viewBottom - armBelowViewMargin;
        float armBottom = hand.bounds.min.y;
        if (armBottom > lowestAllowed)
            hand.transform.position += Vector3.down * (armBottom - lowestAllowed);
    }
    private Vector3 GetUseHandTipOffset(Vector2 tipNormalized)
    {
        if (hand == null || hand.sprite == null) return Vector3.zero;

        // The sprite pivot is centered, while the bottle neck is near its upper-left
        // edge. Offset the hand so the actual tip, rather than its center, tracks the mouse.
        Bounds spriteBounds = hand.sprite.bounds;
        Vector3 localTipOffset = new Vector3(
            tipNormalized.x * spriteBounds.size.x,
            tipNormalized.y * spriteBounds.size.y,
            0f);
        return hand.transform.TransformVector(localTipOffset);
    }
    private bool IsPointerOverMouth(Vector2 screenPointer)
    {
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam == null || mouthVisual == null || !mouthVisual.enabled || !cam.pixelRect.Contains(screenPointer))
            return false;

        return GetMouthScreenRect(cam).Contains(screenPointer);
    }

    private static Vector2 ClampPointerToMouth(Vector2 screenPointer, Rect mouthRect)
    {
        return new Vector2(
            Mathf.Clamp(screenPointer.x, mouthRect.xMin, mouthRect.xMax),
            Mathf.Clamp(screenPointer.y, mouthRect.yMin, mouthRect.yMax));
    }
    private Rect GetMouthScreenRect(Camera cam)
    {
        Bounds bounds = mouthVisual.bounds;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Rect mouthRect = Rect.MinMaxRect(float.PositiveInfinity, float.PositiveInfinity,
            float.NegativeInfinity, float.NegativeInfinity);
        for (int x = 0; x <= 1; x++)
        for (int y = 0; y <= 1; y++)
        for (int z = 0; z <= 1; z++)
        {
            Vector3 corner = new Vector3(x == 0 ? min.x : max.x,
                y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
            Vector3 projected = cam.WorldToScreenPoint(corner);
            mouthRect.xMin = Mathf.Min(mouthRect.xMin, projected.x);
            mouthRect.yMin = Mathf.Min(mouthRect.yMin, projected.y);
            mouthRect.xMax = Mathf.Max(mouthRect.xMax, projected.x);
            mouthRect.yMax = Mathf.Max(mouthRect.yMax, projected.y);
        }

        // Keep a small cushion around the lips, and never extend into the world panel.
        float paddingX = Mathf.Max(12f, mouthRect.width * 0.08f);
        float paddingY = Mathf.Max(12f, mouthRect.height * 0.08f);
        mouthRect.xMin = Mathf.Max(mouthRect.xMin - paddingX, cam.pixelRect.xMin);
        mouthRect.xMax = Mathf.Min(mouthRect.xMax + paddingX, cam.pixelRect.xMax);
        mouthRect.yMin = Mathf.Max(mouthRect.yMin - paddingY, cam.pixelRect.yMin);
        mouthRect.yMax = Mathf.Min(mouthRect.yMax + paddingY, cam.pixelRect.yMax);
        return mouthRect;
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

    private Vector3 GetDeodorantNozzleWorldPosition()
    {
        Sprite spraySprite = hand.sprite != null ? hand.sprite : deodorantSprayHand;
        if (spraySprite == null) return hand.transform.position;

        Bounds localBounds = spraySprite.bounds;
        Vector3 nozzleLocal = new Vector3(
            localBounds.center.x + localBounds.extents.x * deodorantCanNozzleNormalized.x,
            localBounds.center.y + localBounds.extents.y * deodorantCanNozzleNormalized.y,
            0f);
        return hand.transform.TransformPoint(nozzleLocal);
    }

    private Vector3 ClampMistToFacePanel(Vector3 worldPosition)
    {
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam == null) return worldPosition;

        Rect panel = cam.pixelRect;
        Vector3 screen = cam.WorldToScreenPoint(worldPosition);
        const float edgePadding = 18f;
        screen.x = Mathf.Clamp(screen.x, panel.xMin + edgePadding, panel.xMax - edgePadding);
        screen.y = Mathf.Clamp(screen.y, panel.yMin + edgePadding, panel.yMax - edgePadding);
        return cam.ScreenToWorldPoint(screen);
    }

    private void EmitDeodorantMist(Bounds faceBounds)
    {
        if (softMistSprite == null) softMistSprite = CreateSoftMistSprite();

        Vector3 source = ClampMistToFacePanel(GetDeodorantNozzleWorldPosition());
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
        sprite.color = new Color(0.9f, 0.97f, 1f, Random.Range(0.85f, 1f));
        float size = Random.Range(0.8f, 1.35f);
        puff.transform.localScale = Vector3.one * size;
        StartCoroutine(AnimateDeodorantMist(sprite, direction * Random.Range(1.8f, 3f), Random.Range(0.85f, 1.3f)));
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
        }

        if (eye.bottomLid != null)
        {
            float clearEdge = pupilBottom - Mathf.Lerp(lidGap, lidGapAtEdge, Mathf.Max(0f, -aimY));
            float edge = Mathf.Clamp(Mathf.Lerp(eye.bottomEdgeRest, clearEdge, lidFollow),
                eye.bottomEdgeRest - bottomLidMaxLower, eye.bottomEdgeRest + bottomLidMaxRaise);
            edge = Mathf.Min(edge, clearEdge);
            float wanted = edge - eye.bottomEdgeRest;
            eye.bottomOffset = wanted < eye.bottomOffset ? wanted : Mathf.Lerp(eye.bottomOffset, wanted, lidStep);
        }

        // Blinking pulls both lids towards a meeting point between them, on top of where they'd otherwise sit.
        float topShown = eye.topOffset;
        float bottomShown = eye.bottomOffset;
        if (blinkAmount > 0f && eye.topLid != null && eye.bottomLid != null)
        {
            float topEdge = eye.topEdgeRest + eye.topOffset;
            float bottomEdge = eye.bottomEdgeRest + eye.bottomOffset;
            float meet = Mathf.Lerp(bottomEdge, topEdge, blinkMeetPoint);
            topShown += (meet - blinkOverlap - topEdge) * blinkAmount;
            bottomShown += (meet + blinkOverlap - bottomEdge) * blinkAmount;
        }
        if (eye.topLid != null)
            eye.topLid.position = ToWorld(eye, eye.topLidRest + Vector2.up * topShown, eye.topLid.position.z);
        if (eye.bottomLid != null)
            eye.bottomLid.position = ToWorld(eye, eye.bottomLidRest + Vector2.up * bottomShown, eye.bottomLid.position.z);
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
