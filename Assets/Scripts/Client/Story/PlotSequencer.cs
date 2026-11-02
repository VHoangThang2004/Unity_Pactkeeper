using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.InputSystem;
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

    [Header("TextBox UI")]
    [SerializeField] private GameObject textBoxPanel;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button skipButton;

    [Header("GuidedClick UI")]
    [SerializeField] private GameObject blockerPanel;
    [SerializeField] private GameObject tutorialButton;
    [SerializeField] private GameObject highlighter;

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
        HideAll();
    }

    void OnDestroy()
    {
        PlotDirector.OnTrigger -= HandleTrigger;
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

        // 2. Wait until snapshot sync, eye animation, effect resolve replay, and LockedInputState are all finished.
        float syncWait = 0f;
        while ((matchSession.SyncState != SyncStateValue.Idle ||
                matchSession.IsSceneSyncing ||
                interactionSystem.decisionSpamRoutine != null ||
                (interactionSystem.stateMachine?.currentState is LockedInputState)) &&
               syncWait < 12f)
        {
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
        if (targetId == "END")
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

        // For ready unit queue slots, ensure the unit icon sprite has been assigned
        if (targetId == "Unit1" || targetId == "Unit2")
        {
            var img = target.GetComponent<Image>();
            if (img != null && img.sprite == null)
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
    // Multi-plot entry point — for scenes like C0_S1 where
    // different plots start on different game triggers.
    // C0_S2/C0_S3 use StartPlot() directly — this is additive only.
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

            case PlotNodeType.AutoAdvance:
                yield return StartCoroutine(RunAutoAdvance(node));
                break;

            case PlotNodeType.CompleteAndExit:
                yield return StartCoroutine(RunCompleteAndExit(node));
                yield break;
        }
    }

    // -------------------------------------------------------
    // TextBox — show text, wait for Next click
    // -------------------------------------------------------

    IEnumerator RunTextBox(PlotNode node)
    {
        HideAll();
        if (nextButton != null) nextButton.gameObject.SetActive(true);
        if (skipButton != null)
            skipButton.gameObject.SetActive(allowSkip && !ContainsInteractiveBattleNodes(plot));

        textBoxPanel.SetActive(true);
        dialogueText.text = node.text;

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

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
    }

    // -------------------------------------------------------
    // GuidedClick — block everything, highlight target UI button
    // -------------------------------------------------------

    IEnumerator RunGuidedClick(PlotNode node)
    {
        HideAll();

        blockerPanel.SetActive(true);

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        TutorialTarget target = null;
        yield return StartCoroutine(WaitForReadyTarget(node.tutorialTargetId, node.targetWaitTimeout, t => target = t));

        if (target == null)
        {
            Debug.LogError($"[PlotSequencer] GuidedClick: target '{node.tutorialTargetId}' " +
                           $"not found after {node.targetWaitTimeout}s.");
            yield break;
        }

        PositionButtonHighlight(target);

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        var btn = tutorialButton.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();

        if (target.Button != null)
        {
            var persistent = target.Button.onClick;
            btn.onClick.AddListener(() => persistent.Invoke());
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

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        TutorialTarget target = null;
        yield return StartCoroutine(WaitForReadyTarget(node.tutorialTargetId, node.targetWaitTimeout, t => target = t));

        if (target == null || inputReader == null)
        {
            Debug.LogError($"[PlotSequencer] GuidedRightClick: target '{node.tutorialTargetId}' " +
                           $"not found after {node.targetWaitTimeout}s.");
            yield break;
        }

        PositionButtonHighlight(target);
        var btn = tutorialButton.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();

        _waitingForInput = true;

        Action onRightClick = () =>
        {
            if (!_waitingForInput) return;
            if (RectTransformUtility.RectangleContainsScreenPoint(
                target.RectTransform, Mouse.current.position.ReadValue()))
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

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        TutorialTarget target = null;
        yield return StartCoroutine(WaitForReadyTarget(node.tutorialTargetId, node.targetWaitTimeout, t => target = t));

        if (target == null || inputReader == null)
        {
            Debug.LogError($"[PlotSequencer] GuidedLeftClick: target '{node.tutorialTargetId}' " +
                           $"not found after {node.targetWaitTimeout}s.");
            yield break;
        }

        PositionButtonHighlight(target);
        var btn = tutorialButton.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();

        _waitingForInput = true;

        Action onLeftClick = () =>
        {
            if (!_waitingForInput) return;
            if (RectTransformUtility.RectangleContainsScreenPoint(
                target.RectTransform, Mouse.current.position.ReadValue()))
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
    // GuidedCellClick — highlight a world-space grid cell
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
        // Assumes cell center is at (x + 0.5, y + 0.5, 0) — adjust if your grid differs
        var cam = gameCamera != null ? gameCamera : Camera.main;
        Vector3 worldPos = new Vector3(node.targetCell.x + 0.5f, node.targetCell.y + 0.5f, 0f);
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);


        // Position highlight at screen position
        var highlightRect = tutorialButton.GetComponent<RectTransform>();
        var highlighterRect = highlighter.GetComponent<RectTransform>();
        highlightRect.position = screenPos;
        highlighterRect.position = screenPos;
        highlightRect.sizeDelta = new Vector2(160f, 160f);     // one cell size approx
        highlighterRect.sizeDelta = new Vector2(192f, 192f);   // 1.2x
        tutorialButton.SetActive(true);
        highlighter.SetActive(true);

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
            if (interactionSystem != null)
            {
                var targetCell = new Vector3Int(node.targetCell.x, node.targetCell.y, 0);
                interactionSystem.stateMachine.OnTileHover(targetCell);
                interactionSystem.HandleTileClick(targetCell);
                interactionSystem.stateMachine.OnTileHover(targetCell);
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
    // CompleteAndExit — final button, no real button onClick copied
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
            // Fallback to simple "Next" button if no target ID is specified
            nextButton.gameObject.SetActive(true);
            _waitingForInput = true;
            
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() =>
            {
                HideAll();
                _waitingForInput = false;
                _onComplete?.Invoke();
            });

            if (node.delay > 0)
            {
                float timer = 0f;
                while (_waitingForInput && timer < node.delay)
                {
                    timer += Time.deltaTime;
                    yield return null;
                }
                if (_waitingForInput) // Timer expired before click
                {
                    HideAll();
                    _waitingForInput = false;
                    _onComplete?.Invoke();
                }
            }
            else
            {
                yield return new WaitUntil(() => !_waitingForInput);
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

            if (node.delay > 0)
            {
                float timer = 0f;
                while (!buttonClicked && timer < node.delay)
                {
                    if (target != null) PositionButtonHighlight(target);
                    timer += Time.deltaTime;
                    yield return null;
                }
                if (!buttonClicked) // Timer expired before click
                {
                    HideAll();
                    _onComplete?.Invoke();
                }
            }
            else
            {
                while (!buttonClicked)
                {
                    if (target != null) PositionButtonHighlight(target);
                    yield return null;
                }
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

    void PositionButtonHighlight(TutorialTarget target)
    {
        if (target == null || target.RectTransform == null) return;
        RebuildTargetLayout(target);

        var buttonRect = tutorialButton.GetComponent<RectTransform>();
        var targetRect = target.RectTransform;
        var highlighterRect = highlighter.GetComponent<RectTransform>();

        buttonRect.position = targetRect.position;
        buttonRect.sizeDelta = targetRect.rect.size;
        highlighterRect.position = targetRect.position;
        highlighterRect.sizeDelta = targetRect.rect.size * 1.2f;
        buttonRect.pivot = targetRect.pivot;
        highlighterRect.pivot = targetRect.pivot;

        tutorialButton.SetActive(true);
        highlighter.SetActive(true);

        // Move textbox away from target if it's visible
        if (textBoxPanel.activeSelf)
            PositionTextBoxAwayFrom(targetRect);
    }

    void PositionTargetHighlight(TutorialTarget target)
    {
        if (target == null) return;
        var targetRect = target.RectTransform;
        if (targetRect == null)
        {
            Debug.LogError($"[PlotSequencer] PositionTargetHighlight: target has no RectTransform.");
            return;
        }
        RebuildTargetLayout(target);
        var highlighterRect = highlighter.GetComponent<RectTransform>();
        highlighterRect.position = targetRect.position;
        highlighterRect.sizeDelta = targetRect.rect.size * 1.2f;
        highlighterRect.pivot = targetRect.pivot;

        highlighter.SetActive(true);

        if (textBoxPanel.activeSelf)
            PositionTextBoxAwayFrom(targetRect);
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