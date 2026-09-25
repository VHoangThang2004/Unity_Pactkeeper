using System.Collections;
using UnityEngine;
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
    [SerializeField] private GameObject blockerPanel;        // Full-screen raycast blocker
    [SerializeField] private GameObject highlightButton;    // Invisible button placed over target
    [SerializeField] private GameObject highlighter;    // Visible highlight guideline placed over target


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

    IEnumerator RunPlot()
    {
        while (_currentIndex < plot.nodes.Count)
        {
            var node = plot.nodes[_currentIndex];
            yield return StartCoroutine(RunNode(node));
            _currentIndex++;
        }

        HideAll();
        _onComplete?.Invoke();
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

            case PlotNodeType.AutoAdvance:
                yield return StartCoroutine(RunAutoAdvance(node));
                break;

            case PlotNodeType.CompleteAndExit:
                yield return StartCoroutine(RunCompleteAndExit(node));
                yield break; // stop the plot loop — onComplete handles everything
        }
    }

    // -------------------------------------------------------
    // TextBox — show text, wait for Next click
    // -------------------------------------------------------

    IEnumerator RunTextBox(PlotNode node)
    {
        HideAll();
        textBoxPanel.SetActive(true);
        dialogueText.text = node.text;

        _waitingForInput = true;
        nextButton.onClick.RemoveAllListeners();
        nextButton.onClick.AddListener(() => _waitingForInput = false);

        yield return new WaitUntil(() => !_waitingForInput);
    }

    // -------------------------------------------------------
    // GuidedClick — block everything, highlight target button
    // -------------------------------------------------------

    IEnumerator RunGuidedClick(PlotNode node)
    {
        HideAll();

        // Show hint text if provided
        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        // Wait a frame for the additive scene to finish registering targets
        yield return null;
        yield return null;

        var target = TutorialTargetRegistry.Get(node.targetButtonId);
        if (target == null)
        {
            Debug.LogError($"[PlotSequencer] GuidedClick: target '{node.targetButtonId}' not found in registry. " +
                           "Make sure TutorialTarget component is on the button with matching targetId.");
            yield break;
        }

        // Enable full-screen blocker
        blockerPanel.SetActive(true);

        // Position the invisible highlight button over the real target
        PositionHighlightOver(target);

        // Show hint text in the same textBox panel
        if (!string.IsNullOrEmpty(node.highlightHint))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.highlightHint;
        }
        else if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        // Wire highlight button: fire original button's onClick + advance plot
        var btn = highlightButton.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();

        // Copy original button's onClick listeners
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
    // CompleteAndExit — final button, highlights real button,
    // on click fires onComplete WITHOUT copying real button's onClick.
    // Story router handles all navigation from here.
    // -------------------------------------------------------

    IEnumerator RunCompleteAndExit(PlotNode node)
    {
        HideAll();

        if (!string.IsNullOrEmpty(node.text))
        {
            textBoxPanel.SetActive(true);
            dialogueText.text = node.text;
        }

        yield return null;
        yield return null;

        var target = TutorialTargetRegistry.Get(node.targetButtonId);
        if (target == null)
        {
            Debug.LogError($"[PlotSequencer] CompleteAndExit: target '{node.targetButtonId}' not found.");
            HideAll();
            _onComplete?.Invoke();
            yield break;
        }

        blockerPanel.SetActive(true);
        PositionHighlightOver(target);

        var btn = highlightButton.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        // Do NOT copy real button's onClick — story router owns navigation
        btn.onClick.AddListener(() =>
        {
            HideAll();
            _onComplete?.Invoke();
        });

        yield return new WaitUntil(() => !highlightButton.activeSelf);
    }

    // -------------------------------------------------------
    // AutoAdvance — just wait the delay, no input needed
    // -------------------------------------------------------

    IEnumerator RunAutoAdvance(PlotNode node)
    {
        HideAll();
        yield return new WaitForSeconds(node.delay);
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    void PositionHighlightOver(TutorialTarget target)
    {
        var highlightRect = highlightButton.GetComponent<RectTransform>();
        var targetRect = target.RectTransform;
        var highlighterRect = highlighter.GetComponent<RectTransform>();

        // Match size and position in screen space
        highlightRect.position = targetRect.position;
        highlightRect.sizeDelta = targetRect.rect.size;
        highlighterRect.position = targetRect.position;
        highlighterRect.sizeDelta = targetRect.rect.size * 1.2f; // Slightly larger for visual effect
        highlightRect.pivot = targetRect.pivot;
        highlighterRect.pivot = targetRect.pivot;

        highlightButton.SetActive(true);
        highlighter.SetActive(true);
    }

    void HideAll()
    {
        if (textBoxPanel != null) textBoxPanel.SetActive(false);
        if (blockerPanel != null) blockerPanel.SetActive(false);
        if (highlightButton != null) highlightButton.SetActive(false);
        if (highlighter != null) highlighter.SetActive(false);
    }
    public void ButtonDebugger()
    {
        Debug.Log($"Button is clicked");
    }
}