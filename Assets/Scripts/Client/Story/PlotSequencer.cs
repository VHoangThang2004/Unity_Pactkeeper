using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Threading.Tasks;

/// <summary>
/// Reads a PlotConfig and drives the tutorial UI step by step.
/// Lives in the C{x}_S{y} story scene.
/// Calls onComplete when all nodes are finished.
/// </summary>
public class PlotSequencer : MonoBehaviour
{
    [Header("Plot")]
    [SerializeField] private PlotConfig plot;
    public PlotConfig Plot => plot;

    [Header("TextBox UI")]
    [SerializeField] private GameObject textBoxPanel;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button skipButton;

    [Header("GuidedClick UI")]
    [SerializeField] private GameObject blockerPanel;
    [SerializeField] private GameObject tutorialButton;
    [SerializeField] private GameObject highlighter;
    [SerializeField] private AnimationClip highlighterClip;
    [SerializeField] private float highlighterScaleMultiplier = 1.75f;
    [SerializeField] private Vector2 highlighterExtraPadding = new Vector2(36f, 36f);

    private PlayableGraph _highlighterGraph;

    [Header("GuidedCellClick UI")]
    [Tooltip("Camera used to convert world cell position to screen position.")]
    [SerializeField] private Camera gameCamera;
    [SerializeField] private ClientInteractionSystem interactionSystem; // C0_S1 only (guided cell click)
    [SerializeField] private InputReader inputReader; // C0_S1 only (guided right click)
    [SerializeField] private bool allowSkip = true;

    private ClientMatchSession matchSession;

    public GameObject TextBoxPanel => textBoxPanel;

    private int _currentIndex = 0;
    private bool _waitingForInput = false;
    private System.Action _onComplete;

    void Awake()
    {
        EnsureTransparentPanels();
        EnsureHighlightSorting();
        EnsureHighlighterAnimation();
        HideAll();
    }

    private void EnsureTransparentPanels()
    {
        if (blockerPanel != null)
        {
            var img = blockerPanel.GetComponent<Image>();
            if (img != null)
                img.color = new Color(0f, 0f, 0f, 0f);
        }
        if (nextButton != null)
        {
            var img = nextButton.GetComponent<Image>();
            if (img != null)
                img.color = new Color(0f, 0f, 0f, 0f);
        }
        if (tutorialButton != null)
        {
            var img = tutorialButton.GetComponent<Image>();
            if (img != null)
                img.color = new Color(0f, 0f, 0f, 0f);
        }
    }

    private void EnsureHighlightSorting()
    {
        if (highlighter != null)
        {
            var hCanvas = highlighter.GetComponent<Canvas>();
            if (hCanvas != null)
            {
                hCanvas.overrideSorting = true;
                hCanvas.sortingOrder = 1001;
            }
        }
        if (tutorialButton != null)
        {
            var bCanvas = tutorialButton.GetComponent<Canvas>();
            if (bCanvas != null)
            {
                bCanvas.overrideSorting = true;
                bCanvas.sortingOrder = 1002;
            }
        }
        if (textBoxPanel != null)
        {
            var tbCanvas = textBoxPanel.GetComponent<Canvas>();
            if (tbCanvas != null)
            {
                tbCanvas.overrideSorting = true;
                tbCanvas.sortingOrder = 1005;
            }
        }
        if (nextButton != null)
        {
            var nbCanvas = nextButton.GetComponent<Canvas>();
            if (nbCanvas != null)
            {
                nbCanvas.overrideSorting = true;
                nbCanvas.sortingOrder = 1002;
            }
        }
    }

    void OnDestroy()
    {
        PlotDirector.OnTrigger -= HandleTrigger;
        if (_highlighterGraph.IsValid())
            _highlighterGraph.Destroy();
    }

    private void EnsureHighlighterAnimation()
    {
        if (highlighter == null) return;

#if UNITY_EDITOR
        if (highlighterClip == null)
        {
            highlighterClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Prefabs/Sprites/HighLighter/Highlighter.anim");
        }
#endif

        if (highlighterClip == null) return;

        var animator = highlighter.GetComponent<Animator>();
        if (animator == null)
            animator = highlighter.AddComponent<Animator>();

        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (!_highlighterGraph.IsValid())
        {
            _highlighterGraph = PlayableGraph.Create("TutorialHighlighterGraph");
            _highlighterGraph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
            var output = AnimationPlayableOutput.Create(_highlighterGraph, "HighlighterOutput", animator);
            var clipPlayable = AnimationClipPlayable.Create(_highlighterGraph, highlighterClip);
            output.SetSourcePlayable(clipPlayable);
        }

        if (!_highlighterGraph.IsPlaying())
            _highlighterGraph.Play();
    }

    private void ShowHighlighter()
    {
        if (highlighter == null) return;
        highlighter.SetActive(true);
        EnsureHighlighterAnimation();
    }

    public void SetAllowSkip(bool allow)
    {
        allowSkip = allow;
        if (!allow && skipButton != null)
            skipButton.gameObject.SetActive(false);
    }

    public void SetBlockerOnly(bool active)
    {
        HideAll();
        if (blockerPanel != null)
            blockerPanel.SetActive(active);
    }

    public void BindBattleReferences(Camera cam, ClientInteractionSystem interaction, InputReader input)
    {
        if (cam != null) gameCamera = cam;
        if (interaction != null) interactionSystem = interaction;
        if (input != null) inputReader = input;
        if (matchSession == null) matchSession = FindAnyObjectByType<ClientMatchSession>();
    }

    private void EnsureBattleReferences()
    {
        if (gameCamera != null && interactionSystem != null && inputReader != null && matchSession != null)
            return;

        var clientScene = FindAnyObjectByType<ClientScene>();
        if (clientScene != null)
        {
            if (gameCamera == null) gameCamera = clientScene.cam;
            if (interactionSystem == null) interactionSystem = clientScene.clientInteractionSystem;
            if (inputReader == null) inputReader = clientScene.input;
        }
        if (gameCamera == null) gameCamera = Camera.main;
        if (interactionSystem == null) interactionSystem = FindAnyObjectByType<ClientInteractionSystem>();
        if (inputReader == null) inputReader = FindAnyObjectByType<InputReader>();
        if (matchSession == null) matchSession = FindAnyObjectByType<ClientMatchSession>();
    }

    /// <summary>
    /// Waits until any pending Server RPC decision (Confirm / Wait), Snapshot Sync, ResolveReplay,
    /// and LockedInputState have completely finished and Battle UI is refreshed.
    /// </summary>
    private IEnumerator WaitForBattleUIStable()
    {
        EnsureBattleReferences();
        if (matchSession == null || interactionSystem == null)
            yield break;

        // 1. If the previous tutorial step just triggered a server decision (e.g., ConfirmButton or WaitButton),
        // wait until the server responds with a snapshot (which stops decisionSpamRoutine and enters Syncing).
        if (interactionSystem.decisionSpamRoutine != null)
        {
            float rpcWait = 0f;
            while (interactionSystem.decisionSpamRoutine != null &&
                   matchSession.SyncState == SyncStateValue.Idle &&
                   rpcWait < 3.5f)
            {
                rpcWait += Time.deltaTime;
                yield return null;
            }
            yield return null;
        }

        // 2. Wait until snapshot sync, eye animation, effect resolve replay, LockedInputState,
        // and any intervening Enemy turn (e.g. "[Enemy team] wait.") are all finished and it is Player's turn.
        float syncWait = 0f;
        float stableTimer = 0f;
        while (syncWait < 15f)
        {
            bool busy = matchSession.SyncState != SyncStateValue.Idle ||
                        matchSession.IsSceneSyncing ||
                        interactionSystem.decisionSpamRoutine != null ||
                        (interactionSystem.stateMachine?.currentState is LockedInputState) ||
                        !matchSession.Timeline.isPaused ||
                        !matchSession.IsMyTurn();

            if (!busy)
            {
                stableTimer += Time.deltaTime;
                if (stableTimer >= 0.15f)
                    break;
            }
            else
            {
                stableTimer = 0f;
            }

            syncWait += Time.deltaTime;
            yield return null;
        }

        // 3. Wait one frame and force canvas update so all layout groups and buttons settle.
        yield return null;
        Canvas.ForceUpdateCanvases();
    }

    private bool IsTargetVisibleAndReady(TutorialTarget target, string targetId)
    {
        if (target == null || target.RectTransform == null)
            return false;

        // "END" is attached to TextBoxPanel which PlotSequencer activates itself.
        if (targetId == "END" || targetId.EndsWith("_END"))
            return true;

        if (!target.gameObject.activeInHierarchy)
            return false;

        // Ensure no parent CanvasGroup is hiding the element (e.g., Unit1/Unit2 queue slot or ConfirmButton with alpha=0)
        var canvasGroups = target.GetComponentsInParent<CanvasGroup>(true);
        if (canvasGroups != null)
        {
            for (int i = 0; i < canvasGroups.Length; i++)
            {
                if (canvasGroups[i] != null && canvasGroups[i].alpha <= 0.01f)
                    return false;
            }
        }

        // For ready unit queue slots, ensure the timeline is paused on player's turn, sync is idle, and unit icon sprite is assigned
        if (targetId == "Unit1" || targetId.EndsWith("_Unit1"))
        {
            EnsureBattleReferences();
            if (matchSession != null &&
                (!matchSession.Timeline.isPaused ||
                 !matchSession.IsMyTurn() ||
                 matchSession.SyncState != SyncStateValue.Idle ||
                 matchSession.ownedReadyUnitIds == null ||
                 matchSession.ownedReadyUnitIds.Count < 1))
                return false;

            var imgs = target.GetComponentsInChildren<Image>(true);
            bool hasSprite = false;
            if (imgs != null)
            {
                for (int i = 0; i < imgs.Length; i++)
                {
                    if (imgs[i] != null && imgs[i].sprite != null)
                    {
                        hasSprite = true;
                        break;
                    }
                }
            }
            if (!hasSprite)
                return false;
        }
        else if (targetId == "Unit2" || targetId.EndsWith("_Unit2"))
        {
            EnsureBattleReferences();
            if (matchSession != null &&
                (!matchSession.Timeline.isPaused ||
                 !matchSession.IsMyTurn() ||
                 matchSession.SyncState != SyncStateValue.Idle ||
                 matchSession.ownedReadyUnitIds == null ||
                 matchSession.ownedReadyUnitIds.Count < 2))
                return false;

            var imgs = target.GetComponentsInChildren<Image>(true);
            bool hasSprite = false;
            if (imgs != null)
            {
                for (int i = 0; i < imgs.Length; i++)
                {
                    if (imgs[i] != null && imgs[i].sprite != null)
                    {
                        hasSprite = true;
                        break;
                    }
                }
            }
            if (!hasSprite)
                return false;
        }
        else if (targetId == "WaitButton" || targetId.EndsWith("_WaitButton"))
        {
            EnsureBattleReferences();
            if (matchSession != null &&
                (!matchSession.Timeline.isPaused ||
                 !matchSession.IsMyTurn() ||
                 matchSession.SyncState != SyncStateValue.Idle ||
                 matchSession.IsSceneSyncing))
                return false;

            if (interactionSystem != null &&
                (interactionSystem.decisionSpamRoutine != null ||
                 interactionSystem.stateMachine?.currentState is not NoneState))
                return false;

            if (target.Button != null && !target.Button.interactable)
                return false;
        }
        else if (targetId == "ConfirmButton" || targetId.EndsWith("_ConfirmButton") ||
                 targetId == "CancelButton" || targetId.EndsWith("_CancelButton"))
        {
            EnsureBattleReferences();
            if (matchSession != null &&
                (!matchSession.Timeline.isPaused ||
                 !matchSession.IsMyTurn() ||
                 matchSession.SyncState != SyncStateValue.Idle ||
                 matchSession.IsSceneSyncing))
                return false;

            if (interactionSystem != null && interactionSystem.decisionSpamRoutine != null)
                return false;
        }

        return true;
    }

    private void RebuildTargetLayout(TutorialTarget target)
    {
        if (target == null || target.RectTransform == null) return;
        Canvas.ForceUpdateCanvases();
        if (target.RectTransform.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(target.RectTransform);
    }

    private IEnumerator WaitForReadyTarget(string targetId, float timeout, Action<TutorialTarget> onFound)
    {
        if (string.IsNullOrEmpty(targetId))
        {
            onFound?.Invoke(null);
            yield break;
        }

        float maxWait = Mathf.Max(timeout, 5f);
        float waited = 0f;
        TutorialTarget target = null;

        while (waited < maxWait)
        {
            target = TutorialTargetRegistry.Get(targetId);
            if (IsTargetVisibleAndReady(target, targetId))
            {
                RebuildTargetLayout(target);
                onFound?.Invoke(target);
                yield break;
            }

            waited += Time.deltaTime;
            yield return null;
        }

        // Fallback if timeout expires: return registered target if any and rebuild layout
        target = TutorialTargetRegistry.Get(targetId);
        if (target != null)
            RebuildTargetLayout(target);
        onFound?.Invoke(target);
    }

    private static bool ContainsInteractiveBattleNodes(PlotConfig cfg)
    {
        if (cfg == null || cfg.nodes == null) return false;
        foreach (var n in cfg.nodes)
        {
            if (n.type == PlotNodeType.GuidedCellClick ||
                n.type == PlotNodeType.GuidedRightClick ||
                n.type == PlotNodeType.GuidedLeftClick)
                return true;
        }
        return false;
    }

    public void StartPlot(System.Action onComplete)
    {
        _onComplete = onComplete;
        _currentIndex = 0;

        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(SkipPlot);
        }

        StartCoroutine(RunPlot());
    }

    void SkipPlot()
    {
        if (!allowSkip || ContainsInteractiveBattleNodes(plot)) return;
        StopAllCoroutines();
        HideAll();
        _waitingForInput = false;
        _onComplete?.Invoke();
    }

    // -------------------------------------------------------
    // Multi-plot entry point - for scenes like C0_S1 where
    // different plots start on different game triggers.
    // C0_S2/C0_S3 use StartPlot() directly - this is additive only.
    // -------------------------------------------------------

    [System.Serializable]
    public class PlotEntry
    {
        public PlotConfig plot;
        [Tooltip("Leave empty to start immediately on StartPlotWithTriggers call.")]
        public string startTrigger;
    }

    private System.Collections.Generic.Queue<PlotEntry> _pendingPlots;
    private bool _plotRunning = false;

    public void StartPlotWithTriggers(System.Collections.Generic.List<PlotEntry> plots, System.Action onComplete = null)
    {
        _onComplete = onComplete;
        _pendingPlots = new System.Collections.Generic.Queue<PlotEntry>(plots);
        PlotDirector.OnTrigger -= HandleTrigger;
        PlotDirector.OnTrigger += HandleTrigger;
        StartCoroutine(ProcessPendingPlots());
    }

    IEnumerator ProcessPendingPlots()
    {
        // Start any plots with empty trigger immediately
        foreach (var entry in _pendingPlots)
        {
            if (string.IsNullOrEmpty(entry.startTrigger))
            {
                yield return StartCoroutine(RunPlotConfig(entry.plot));
            }
        }
    }

    void HandleTrigger(string triggerId)
    {
        Debug.Log($"[PlotSequencer] Received trigger: {triggerId}");
        if (_plotRunning) return;

        var list = new System.Collections.Generic.List<PlotEntry>(_pendingPlots);
        foreach (var entry in list)
        {
            if (entry.startTrigger == triggerId)
            {
                _pendingPlots = new System.Collections.Generic.Queue<PlotEntry>(
                    list.FindAll(e => e != entry));
                StartCoroutine(RunPlotConfig(entry.plot));
                return;
            }
        }
    }

    IEnumerator RunPlotConfig(PlotConfig config)
    {
        _plotRunning = true;
        // Temporarily swap plot and run it
        var previousPlot = plot;
        plot = config;
        _currentIndex = 0;

        while (_currentIndex < plot.nodes.Count)
        {
            PlotNode node = plot.nodes[_currentIndex];
            yield return StartCoroutine(RunNode(node));
            blockerPanel.SetActive(true);
            _currentIndex++;
            yield return new WaitForSeconds(node.delay);
        }

        HideAll();
        plot = previousPlot;
        _plotRunning = false;
    }

    IEnumerator RunPlot()
    {
        while (_currentIndex < plot.nodes.Count)
        {
            var node = plot.nodes[_currentIndex];
            yield return StartCoroutine(RunNode(node));
            blockerPanel.SetActive(true);
            _currentIndex++;
            yield return new WaitForSeconds(0.3f);
        }

        HideAll();
    }

    IEnumerator RunNode(PlotNode node)
    {
        if (ContainsInteractiveBattleNodes(plot) || matchSession != null || FindAnyObjectByType<ClientMatchSession>() != null)
        {
            yield return StartCoroutine(WaitForBattleUIStable());
        }

        switch (node.type)
        {
            case PlotNodeType.TextBox:
                yield return StartCoroutine(RunTextBox(node));
                break;

            case PlotNodeType.GuidedClick:
                yield return StartCoroutine(RunGuidedClick(node));
                break;

            case PlotNodeType.GuidedRightClick:
                yield return StartCoroutine(RunGuidedRightClickExtendedButton(node));
                break;

            case PlotNodeType.GuidedLeftClick:
                yield return StartCoroutine(RunGuidedLeftClickExtendedButton(node));
                break;

            case PlotNodeType.GuidedCellClick:
                yield return StartCoroutine(RunGuidedCellClick(node));
                break;

            case PlotNodeType.InspectCellClick:
                yield return StartCoroutine(RunInspectCellClick(node));
                break;

            case PlotNodeType.AutoAdvance:
                yield return StartCoroutine(RunAutoAdvance(node));
                break;

            case PlotNodeType.CompleteAndExit:
                yield return StartCoroutine(RunCompleteAndExit(node));
                yield break;
        }
    }

    // -------------------------------------------------------
    // TextBox - show text, wait for Next click
    // -------------------------------------------------------

    IEnumerator RunTextBox(PlotNode node)
    {
        HideAll();
        if (nextButton != null)
        {
            var nbImg = nextButton.GetComponent<Image>();
            if (nbImg != null) nbImg.color = new Color(0f, 0f, 0f, 0f);
            nextButton.gameObject.SetActive(true);
        }
        if (skipButton != null)
            skipButton.gameObject.SetActive(allowSkip && !ContainsInteractiveBattleNodes(plot));

        TutorialTarget target = null;
        if (!string.IsNullOrEmpty(node.tutorialTargetId))
        {
            yield return StartCoroutine(WaitForReadyTarget(node.tutorialTargetId, node.targetWaitTimeout, t => target = t));

            if (target == null)
            {
                Debug.LogError($"[PlotSequencer] TextNode: target '{node.tutorialTargetId}' " +
                               $"not found after {node.targetWaitTimeout}s.");
            }
            else
            {
                PositionTargetHighlight(target);
            }
        }

        textBoxPanel.SetActive(true);
        dialogueText.text = node.text;

        _waitingForInput = true;
        nextButton.onClick.RemoveAllListeners();
        nextButton.onClick.AddListener(() => _waitingForInput = false);

        if (node.delay > 0)
        {
            float timer = 0f;
            while (_waitingForInput && timer < node.delay)
            {
                if (target != null) PositionTargetHighlight(target);
                timer += Time.deltaTime;
                yield return null;
            }
            _waitingForInput = false; // Force advance if timer expires
        }
        else
        {
            while (_waitingForInput)
            {
                if (target != null) PositionTargetHighlight(target);
                yield return null;
            }
        }

        HideAll();
    }

    // -------------------------------------------------------
    // GuidedClick - block everything, highlight target UI button
    // -------------------------------------------------------

    IEnumerator RunGuidedClick(PlotNode node)
    {
        HideAll();

        blockerPanel.SetActive(true);

        TutorialTarget target = null;
        yield return StartCoroutine(WaitForReadyTarget(node.tutorialTargetId, node.targetWaitTimeout, t => target = t));

        if (target == null)
        {
            Debug.LogError($"[PlotSequencer] GuidedClick: target '{node.tutorialTargetId}' " +
                           $"not found after {node.targetWaitTimeout}s.");
            yield break;
        }

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        PositionButtonHighlight(target);

        var btn = tutorialButton.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();

        if (target.Button != null)
        {
            var persistent = target.Button.onClick;
            btn.onClick.AddListener(() => persistent.Invoke());
        }
        else if (target.ExtendedButton != null)
        {
            btn.onClick.AddListener(() => target.ExtendedButton.onLeftClick?.Invoke());
        }

        _waitingForInput = true;
        btn.onClick.AddListener(() => _waitingForInput = false);

        while (_waitingForInput)
        {
            if (target != null) PositionButtonHighlight(target);
            yield return null;
        }

        HideAll();
    }

    // -------------------------------------------------------

    IEnumerator RunGuidedRightClickExtendedButton(PlotNode node)
    {
        EnsureBattleReferences();
        HideAll();
        blockerPanel.SetActive(true);

        TutorialTarget target = null;
        yield return StartCoroutine(WaitForReadyTarget(node.tutorialTargetId, node.targetWaitTimeout, t => target = t));

        if (target == null || inputReader == null)
        {
            Debug.LogError($"[PlotSequencer] GuidedRightClick: target '{node.tutorialTargetId}' " +
                           $"not found after {node.targetWaitTimeout}s.");
            yield break;
        }

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        PositionButtonHighlight(target);
        var btn = tutorialButton.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();

        _waitingForInput = true;

        Action onRightClick = () =>
        {
            if (!_waitingForInput) return;
            var buttonRect = tutorialButton.GetComponent<RectTransform>();
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            if ((buttonRect != null && RectTransformUtility.RectangleContainsScreenPoint(buttonRect, mousePos)) ||
                (target.RectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(target.RectTransform, mousePos)))
            {
                Debug.Log($"[PlotSequencer] GuidedRightClick: right-click detected on target '{node.tutorialTargetId}'");
                target.ExtendedButton?.onRightClick?.Invoke();
                _waitingForInput = false;
            }
        };

        inputReader.OnRightClick += onRightClick;
        while (_waitingForInput)
        {
            if (target != null) PositionButtonHighlight(target);
            yield return null;
        }
        inputReader.OnRightClick -= onRightClick;
        HideAll();
    }

    IEnumerator RunGuidedLeftClickExtendedButton(PlotNode node)
    {
        EnsureBattleReferences();
        HideAll();
        blockerPanel.SetActive(true);

        TutorialTarget target = null;
        yield return StartCoroutine(WaitForReadyTarget(node.tutorialTargetId, node.targetWaitTimeout, t => target = t));

        if (target == null || inputReader == null)
        {
            Debug.LogError($"[PlotSequencer] GuidedLeftClick: target '{node.tutorialTargetId}' " +
                           $"not found after {node.targetWaitTimeout}s.");
            yield break;
        }

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        PositionButtonHighlight(target);
        var btn = tutorialButton.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();

        _waitingForInput = true;

        btn.onClick.AddListener(() =>
        {
            // Fire the actual cell click in the game
            if (interactionSystem != null && interactionSystem.stateMachine != null)
            {
                // Guard: If state machine is not in UnitSelectedState, do not advance
                if (interactionSystem.stateMachine.currentState is not UnitSelectedState)
                {
                    Debug.LogWarning($"[PlotSequencer] GuidedCellClick on {node.targetCell} ignored - StateMachine is in {interactionSystem.stateMachine.currentState?.GetType().Name}, not UnitSelectedState.");
                    if (matchSession != null && matchSession.ownedReadyUnitIds != null && matchSession.ownedReadyUnitIds.Count > 0)
                    {
                        int uId = matchSession.selectedUnitId != -1 ? matchSession.selectedUnitId : matchSession.ownedReadyUnitIds[0];
                        interactionSystem.stateMachine.GoToUnitSelected(uId);
                    }
                    else
                    {
                        return;
                    }
                }

                var targetCell = new Vector3Int(node.targetCell.x, node.targetCell.y, 0);
                interactionSystem.stateMachine.OnTileHover(targetCell);
                interactionSystem.HandleTileClick(targetCell);
                interactionSystem.stateMachine.OnTileHover(targetCell);

                if (matchSession != null && (!matchSession.isTargetLocked || matchSession.currentPreviewCell.x != node.targetCell.x || matchSession.currentPreviewCell.y != node.targetCell.y))
                {
                    Debug.LogWarning($"[PlotSequencer] GuidedCellClick on {targetCell} failed to lock target - remaining on current step.");
                    return;
                }
            }
            _waitingForInput = false;
        });

        Action onLeftClick = () =>
        {
            if (!_waitingForInput) return;
            var buttonRect = tutorialButton.GetComponent<RectTransform>();
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            if ((buttonRect != null && RectTransformUtility.RectangleContainsScreenPoint(buttonRect, mousePos)) ||
                (target.RectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(target.RectTransform, mousePos)))
            {
                Debug.Log($"[PlotSequencer] GuidedLeftClick: left-click detected on target '{node.tutorialTargetId}'");
                target.ExtendedButton?.onLeftClick?.Invoke();
                _waitingForInput = false;
            }
        };

        inputReader.OnLeftClick += onLeftClick;
        while (_waitingForInput)
        {
            if (target != null) PositionButtonHighlight(target);
            yield return null;
        }
        inputReader.OnLeftClick -= onLeftClick;
        HideAll();
    }
    // -------------------------------------------------------
    // GuidedCellClick - highlight a world-space grid cell
    // -------------------------------------------------------

    IEnumerator RunGuidedCellClick(PlotNode node)
    {
        EnsureBattleReferences();
        HideAll();
        blockerPanel.SetActive(true);

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        yield return null;

        // Convert cell (x,y) to world position then to screen position
        // Assumes cell center is at (x + 0.5, y + 0.5, 0) - adjust if your grid differs
        var cam = gameCamera != null ? gameCamera : Camera.main;
        Vector3 worldPos = new Vector3(node.targetCell.x + 0.5f, node.targetCell.y + 0.5f, 0f);
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);


        // Position highlight at screen position
        var highlightRect = tutorialButton.GetComponent<RectTransform>();
        var highlighterRect = highlighter.GetComponent<RectTransform>();
        highlightRect.position = screenPos;
        highlighterRect.position = screenPos;
        highlightRect.sizeDelta = new Vector2(160f, 160f);     // one cell size approx
        highlighterRect.sizeDelta = new Vector2(260f, 260f);   // padded ring around cell
        tutorialButton.SetActive(true);
        ShowHighlighter();

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        // Wait for player to click the cell
        // The highlight button acts as the click proxy
        var btn = tutorialButton.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        _waitingForInput = true;
        btn.onClick.AddListener(() =>
        {
            // Fire the actual cell click in the game
            if (interactionSystem != null && interactionSystem.stateMachine != null)
            {
                // Guard: If state machine is not in UnitSelectedState, do not advance
                if (interactionSystem.stateMachine.currentState is not UnitSelectedState)
                {
                    Debug.LogWarning($"[PlotSequencer] GuidedCellClick on {node.targetCell} ignored - StateMachine is in {interactionSystem.stateMachine.currentState?.GetType().Name}, not UnitSelectedState.");
                    if (matchSession != null && matchSession.ownedReadyUnitIds != null && matchSession.ownedReadyUnitIds.Count > 0)
                    {
                        int uId = matchSession.selectedUnitId != -1 ? matchSession.selectedUnitId : matchSession.ownedReadyUnitIds[0];
                        interactionSystem.stateMachine.GoToUnitSelected(uId);
                    }
                    else
                    {
                        return;
                    }
                }

                var targetCell = new Vector3Int(node.targetCell.x, node.targetCell.y, 0);
                interactionSystem.stateMachine.OnTileHover(targetCell);
                interactionSystem.HandleTileClick(targetCell);
                interactionSystem.stateMachine.OnTileHover(targetCell);

                if (matchSession != null && (!matchSession.isTargetLocked || matchSession.currentPreviewCell.x != node.targetCell.x || matchSession.currentPreviewCell.y != node.targetCell.y))
                {
                    Debug.LogWarning($"[PlotSequencer] GuidedCellClick on {targetCell} failed to lock target - remaining on current step.");
                    return;
                }
            }
            _waitingForInput = false;
        });

        while (_waitingForInput)
        {
            screenPos = cam.WorldToScreenPoint(worldPos);
            highlightRect.position = screenPos;
            highlighterRect.position = screenPos;
            if (textBoxPanel.activeSelf)
                PositionTextBoxAwayFrom(screenPos.y);
            yield return null;
        }

        HideAll();
    }

    // -------------------------------------------------------
    // InspectCellClick - highlight a world-space grid cell,
    // fire HandleTileClick and advance immediately WITHOUT
    // requiring isTargetLocked (used for inspecting a unit
    // already standing on that cell, e.g. "view his loadout").
    // -------------------------------------------------------

    IEnumerator RunInspectCellClick(PlotNode node)
    {
        EnsureBattleReferences();
        HideAll();
        blockerPanel.SetActive(true);

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        yield return null;

        var cam = gameCamera != null ? gameCamera : Camera.main;
        Vector3 worldPos = new Vector3(node.targetCell.x + 0.5f, node.targetCell.y + 0.5f, 0f);
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        var highlightRect = tutorialButton.GetComponent<RectTransform>();
        var highlighterRect = highlighter.GetComponent<RectTransform>();
        highlightRect.position = screenPos;
        highlighterRect.position = screenPos;
        highlightRect.sizeDelta = new Vector2(160f, 160f);
        highlighterRect.sizeDelta = new Vector2(260f, 260f);
        tutorialButton.SetActive(true);
        ShowHighlighter();

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        var btn = tutorialButton.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        _waitingForInput = true;
        btn.onClick.AddListener(() =>
        {
            if (interactionSystem != null && interactionSystem.stateMachine != null)
            {
                var targetCell = new Vector3Int(node.targetCell.x, node.targetCell.y, 0);
                interactionSystem.stateMachine.OnTileHover(targetCell);
                interactionSystem.HandleTileClick(targetCell);
                Debug.Log($"[PlotSequencer] InspectCellClick: fired HandleTileClick on {targetCell}, advancing immediately.");
            }
            // Advance regardless - this node does NOT require isTargetLocked
            _waitingForInput = false;
        });

        while (_waitingForInput)
        {
            screenPos = cam.WorldToScreenPoint(worldPos);
            highlightRect.position = screenPos;
            highlighterRect.position = screenPos;
            if (textBoxPanel.activeSelf)
                PositionTextBoxAwayFrom(screenPos.y);
            yield return null;
        }

        HideAll();
    }

    // -------------------------------------------------------
    // CompleteAndExit - final button, no real button onClick copied
    // -------------------------------------------------------

    IEnumerator RunCompleteAndExit(PlotNode node)
    {
        HideAll();

        blockerPanel.SetActive(true);

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        if (string.IsNullOrEmpty(node.tutorialTargetId))
        {
            // Case 1: node.text has content -> player must actively click Next (elevated above blocker) to confirm!
            if (!string.IsNullOrEmpty(node.text))
            {
                if (nextButton != null)
                {
                    var nbImg = nextButton.GetComponent<Image>();
                    if (nbImg != null)
                        nbImg.color = new Color(0f, 0f, 0f, 0f);

                    nextButton.gameObject.SetActive(true);

                    // Ensure nextButton CanvasGroup is interactive and click-blocking (transparent)
                    var nbCanvasGroup = nextButton.GetComponent<CanvasGroup>();
                    if (nbCanvasGroup != null)
                    {
                        nbCanvasGroup.interactable = true;
                        nbCanvasGroup.blocksRaycasts = true;
                    }

                    // Ensure nextButton Canvas is elevated above blockerPanel (sortingOrder 1000)
                    var nbCanvas = nextButton.GetComponent<Canvas>();
                    if (nbCanvas != null)
                    {
                        nbCanvas.overrideSorting = true;
                        nbCanvas.sortingOrder = 1002;
                    }

                    _waitingForInput = true;
                    nextButton.onClick.RemoveAllListeners();
                    nextButton.onClick.AddListener(() =>
                    {
                        HideAll();
                        _waitingForInput = false;
                        _onComplete?.Invoke();
                    });

                    // Wait for player to actively click (NO auto-timeout cutoff!)
                    while (_waitingForInput)
                    {
                        yield return null;
                    }
                }
                else
                {
                    // Fallback to tutorialButton if nextButton reference is missing
                    tutorialButton.SetActive(true);
                    var btn = tutorialButton.GetComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                    _waitingForInput = true;
                    btn.onClick.AddListener(() =>
                    {
                        HideAll();
                        _waitingForInput = false;
                        _onComplete?.Invoke();
                    });

                    while (_waitingForInput)
                    {
                        yield return null;
                    }
                }
            }
            else
            {
                // Case 2: No text and no tutorialTargetId -> auto-advance after delay
                float delayTime = node.delay > 0 ? node.delay : 1.0f;
                yield return new WaitForSeconds(delayTime);
                HideAll();
                _onComplete?.Invoke();
            }
        }
        else
        {
            TutorialTarget target = null;
            yield return StartCoroutine(WaitForReadyTarget(node.tutorialTargetId, node.targetWaitTimeout, t => target = t));

            if (target == null)
            {
                Debug.LogError($"[PlotSequencer] CompleteAndExit: target '{node.tutorialTargetId}' " +
                               $"not found after {node.targetWaitTimeout}s.");
                yield break;
            }

            PositionButtonHighlight(target);

            var btn = tutorialButton.GetComponent<Button>();
            bool buttonClicked = false;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                HideAll();
                buttonClicked = true;
                _onComplete?.Invoke();
            });

            while (!buttonClicked)
            {
                if (target != null) PositionButtonHighlight(target);
                yield return null;
            }
        }
    }

    // -------------------------------------------------------
    // AutoAdvance
    // -------------------------------------------------------

    IEnumerator RunAutoAdvance(PlotNode node)
    {
        HideAll();
        yield return new WaitForSeconds(node.delay);
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    private readonly Vector3[] _corners = new Vector3[4];

    private void CalculateScreenRect(TutorialTarget target, out Vector2 centerScreen, out Vector2 sizeScreen)
    {
        var targetRect = target.RectTransform;
        targetRect.GetWorldCorners(_corners);

        Canvas targetCanvas = target.GetComponentInParent<Canvas>();
        Camera targetCam = (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? (targetCanvas.worldCamera != null ? targetCanvas.worldCamera : (gameCamera != null ? gameCamera : Camera.main))
            : null;

        Vector2 minScreen = RectTransformUtility.WorldToScreenPoint(targetCam, _corners[0]);
        Vector2 maxScreen = minScreen;
        for (int i = 1; i < 4; i++)
        {
            Vector2 sp = RectTransformUtility.WorldToScreenPoint(targetCam, _corners[i]);
            minScreen = Vector2.Min(minScreen, sp);
            maxScreen = Vector2.Max(maxScreen, sp);
        }

        centerScreen = (minScreen + maxScreen) * 0.5f;
        sizeScreen = new Vector2(Mathf.Abs(maxScreen.x - minScreen.x), Mathf.Abs(maxScreen.y - minScreen.y));
    }

    void PositionButtonHighlight(TutorialTarget target)
    {
        if (target == null || target.RectTransform == null) return;
        RebuildTargetLayout(target);

        var buttonRect = tutorialButton.GetComponent<RectTransform>();
        var highlighterRect = highlighter.GetComponent<RectTransform>();
        var parentRect = buttonRect.parent as RectTransform;

        CalculateScreenRect(target, out Vector2 centerScreen, out Vector2 sizeScreen);

        if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, centerScreen, null, out Vector2 localPoint))
        {
            buttonRect.anchoredPosition = localPoint;
            highlighterRect.anchoredPosition = localPoint;

            float scaleX = parentRect.lossyScale.x > 0 ? parentRect.lossyScale.x : 1f;
            float scaleY = parentRect.lossyScale.y > 0 ? parentRect.lossyScale.y : 1f;
            Vector2 canvasSize = new Vector2(sizeScreen.x / scaleX, sizeScreen.y / scaleY);

            buttonRect.sizeDelta = canvasSize;
            highlighterRect.sizeDelta = canvasSize * highlighterScaleMultiplier + highlighterExtraPadding;
        }
        else
        {
            buttonRect.position = target.RectTransform.position;
            buttonRect.sizeDelta = target.RectTransform.rect.size;
            highlighterRect.position = target.RectTransform.position;
            highlighterRect.sizeDelta = target.RectTransform.rect.size * highlighterScaleMultiplier + highlighterExtraPadding;
        }

        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        highlighterRect.pivot = new Vector2(0.5f, 0.5f);

        tutorialButton.SetActive(true);
        ShowHighlighter();

        // Move textbox away from target if it's visible
        if (textBoxPanel.activeSelf)
            PositionTextBoxAwayFrom(centerScreen.y);
    }

    void PositionTargetHighlight(TutorialTarget target)
    {
        if (target == null || target.RectTransform == null) return;
        RebuildTargetLayout(target);

        var highlighterRect = highlighter.GetComponent<RectTransform>();
        var parentRect = highlighterRect.parent as RectTransform;

        CalculateScreenRect(target, out Vector2 centerScreen, out Vector2 sizeScreen);

        if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, centerScreen, null, out Vector2 localPoint))
        {
            highlighterRect.anchoredPosition = localPoint;

            float scaleX = parentRect.lossyScale.x > 0 ? parentRect.lossyScale.x : 1f;
            float scaleY = parentRect.lossyScale.y > 0 ? parentRect.lossyScale.y : 1f;
            Vector2 canvasSize = new Vector2(sizeScreen.x / scaleX, sizeScreen.y / scaleY);

            highlighterRect.sizeDelta = canvasSize * highlighterScaleMultiplier + highlighterExtraPadding;
        }
        else
        {
            highlighterRect.position = target.RectTransform.position;
            highlighterRect.sizeDelta = target.RectTransform.rect.size * highlighterScaleMultiplier + highlighterExtraPadding;
        }

        highlighterRect.pivot = new Vector2(0.5f, 0.5f);
        ShowHighlighter();

        if (textBoxPanel.activeSelf)
            PositionTextBoxAwayFrom(centerScreen.y);
    }

    void PositionTextBoxAwayFrom(RectTransform targetRect)
    {
        var textRect = textBoxPanel.GetComponent<RectTransform>();
        float screenMid = Screen.height / 2f;

        // Target below center → textbox above center, and vice versa
        if (targetRect.position.y < screenMid)
            textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, Mathf.Abs(textRect.anchoredPosition.y));
        else
            textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, -Mathf.Abs(textRect.anchoredPosition.y));
    }
    void PositionTextBoxAwayFrom(float targetScreenY)
    {
        var textRect = textBoxPanel.GetComponent<RectTransform>();
        float screenMid = Screen.height / 2f;
        if (targetScreenY < screenMid)
            textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, Mathf.Abs(textRect.anchoredPosition.y));
        else
            textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, -Mathf.Abs(textRect.anchoredPosition.y));
    }

    void HideAll()
    {
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (skipButton != null) skipButton.gameObject.SetActive(false);
        if (textBoxPanel != null) textBoxPanel.SetActive(false);
        if (blockerPanel != null) blockerPanel.SetActive(false);
        if (tutorialButton != null) tutorialButton.SetActive(false);
        if (highlighter != null) highlighter.SetActive(false);
    }

    public void ButtonDebugger()
    {
        Debug.Log($"Button is clicked");
    }
}
