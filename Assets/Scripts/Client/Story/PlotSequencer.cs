using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

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

    private int _currentIndex = 0;
    private bool _waitingForInput = false;
    private System.Action _onComplete;

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

    public void StartPlotWithTriggers(System.Collections.Generic.List<PlotEntry> plots)
    {
        _pendingPlots = new System.Collections.Generic.Queue<PlotEntry>(plots);
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
        switch (node.type)
        {
            case PlotNodeType.TextBox:
                yield return StartCoroutine(RunTextBox(node));
                break;

            case PlotNodeType.GuidedClick:
                yield return StartCoroutine(RunGuidedClick(node));
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
        nextButton.gameObject.SetActive(true);
        skipButton.gameObject.SetActive(true);

        textBoxPanel.SetActive(true);
        dialogueText.text = node.text;

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        // Wait for target to register — reload scene if timeout exceeded
        TutorialTarget target = null;
        if (!string.IsNullOrEmpty(node.tutorialTargetId))
        {
            float waited = 0f;
            while (target == null && waited < node.targetWaitTimeout)
            {
                target = TutorialTargetRegistry.Get(node.tutorialTargetId);
                if (target == null)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
            }

            if (target == null)
            {
                Debug.LogError($"[PlotSequencer] TextNode: target '{node.tutorialTargetId}' " +
                               $"not found after {node.targetWaitTimeout}s — reloading scene.");
                // SceneManager.LoadScene("3_MainMenu");
                // yield break;
            }
            else
            {
                PositionTargetHighlight(target);
            }
        }

        _waitingForInput = true;
        nextButton.onClick.RemoveAllListeners();
        nextButton.onClick.AddListener(() => _waitingForInput = false);

        yield return new WaitUntil(() => !_waitingForInput);
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

        // Wait for target to register — reload scene if timeout exceeded
        TutorialTarget target = null;
        float waited = 0f;
        while (target == null && waited < node.targetWaitTimeout)
        {
            target = TutorialTargetRegistry.Get(node.tutorialTargetId);
            if (target == null)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        if (target == null)
        {
            Debug.LogError($"[PlotSequencer] GuidedClick: target '{node.tutorialTargetId}' " +
                           $"not found after {node.targetWaitTimeout}s — reloading scene.");
            // SceneManager.LoadScene("3_MainMenu");
            yield break;
        }

        PositionButtonHighlight(target);

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }
        else if (!string.IsNullOrEmpty(node.text))
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

        yield return new WaitUntil(() => !_waitingForInput);

        HideAll();
    }

    // -------------------------------------------------------
    // GuidedCellClick — highlight a world-space grid cell
    // -------------------------------------------------------

    IEnumerator RunGuidedCellClick(PlotNode node)
    {
        HideAll();
        blockerPanel.SetActive(true);

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        yield return null;
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
                interactionSystem.HandleTileClick(new Vector3Int(node.targetCell.x, node.targetCell.y, 0));
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

        // Wait for target to register — reload scene if timeout exceeded
        TutorialTarget target = null;
        float waited = 0f;
        while (target == null && waited < node.targetWaitTimeout)
        {
            target = TutorialTargetRegistry.Get(node.tutorialTargetId);
            if (target == null)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        if (target == null)
        {
            Debug.LogError($"[PlotSequencer] CompleteAndExit: target '{node.tutorialTargetId}' " +
                           $"not found after {node.targetWaitTimeout}s — reloading scene.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            yield break;
        }

        PositionButtonHighlight(target);

        var btn = tutorialButton.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            HideAll();
            _onComplete?.Invoke();
        });

        yield return new WaitUntil(() => !tutorialButton.activeSelf);
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
        var targetRect = target.RectTransform;
        if (targetRect == null)
        {
            Debug.LogError($"[PlotSequencer] PositionTargetHighlight: target has no RectTransform.");
            return;
        }
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