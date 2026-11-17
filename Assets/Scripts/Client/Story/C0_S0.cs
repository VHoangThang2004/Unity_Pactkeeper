using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Chapter 0 Scene 0 ("0.0 - Signing the First Pact").
/// Mandatory Name Input screen for newly logged-in users.
/// Validates that the entered name is non-empty, saves it via PATCH /api/auth/username,
/// and then completes C0_S0 to transition into C0_S1 (which plays the 4-slide Visual Novel intro).
/// </summary>
public class C0_S0 : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig sceneConfig;

    [Header("UI References (Auto-resolved if left null)")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text feedbackText;

    private bool _isSubmitting = false;

    void Start()
    {
        PlotSequencer.HasShownChapter0IntroSlides = false;

        if (nameInputField == null)
            nameInputField = FindAnyObjectByType<TMP_InputField>();

        if (confirmButton == null && nameInputField != null && nameInputField.transform.parent != null)
            confirmButton = nameInputField.transform.parent.GetComponentInChildren<Button>();

        // Pre-fill if a non-empty username is already present, or leave ready for mandatory input
        if (nameInputField != null)
        {
            nameInputField.onValueChanged.AddListener(OnNameInputValueChanged);
            UpdateConfirmButtonState(nameInputField.text);
        }
    }

    private void OnNameInputValueChanged(string currentValue)
    {
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);

        UpdateConfirmButtonState(currentValue);
    }

    private void UpdateConfirmButtonState(string rawText)
    {
        if (confirmButton != null && !_isSubmitting)
            confirmButton.interactable = !string.IsNullOrWhiteSpace(rawText);
    }

    public void CompleteAndContinue()
    {
        if (_isSubmitting) return;

        if (nameInputField == null)
            nameInputField = FindAnyObjectByType<TMP_InputField>();

        string enteredName = nameInputField != null ? nameInputField.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(enteredName))
        {
            ShowFeedback("Please enter your name before continuing.");
            if ( nameInputField != null && nameInputField.placeholder is TMP_Text placeholderTmp )
            {
                placeholderTmp.text = "Name is required!";
                placeholderTmp.color = new Color(1f, 0.45f, 0.45f, 0.9f);
            }
            return;
        }

        StartCoroutine(SubmitNameAndAdvanceRoutine(enteredName));
    }

    private IEnumerator SubmitNameAndAdvanceRoutine(string enteredName)
    {
        _isSubmitting = true;
        if (confirmButton != null)
            confirmButton.interactable = false;

        // Save the chosen name to backend (PATCH /api/auth/username) if changed or newly entered
        if (config != null && !string.IsNullOrEmpty(PlayerSession.Token))
        {
            string payload = JsonUtility.ToJson(new ChangeUsernamePayload { newUsername = enteredName });
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);

            using var req = new UnityWebRequest($"{config.backendUrl}/api/auth/username", "PATCH");
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            config.SetHeaders(req, PlayerSession.Token);
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                string serverMsg = req.downloadHandler != null && !string.IsNullOrWhiteSpace(req.downloadHandler.text)
                    ? req.downloadHandler.text.Trim('"')
                    : "Could not save name. Please try another name.";
                Debug.LogWarning($"[C0_S0] Failed to update username: {serverMsg}");
                ShowFeedback(serverMsg);
                _isSubmitting = false;
                UpdateConfirmButtonState(enteredName);
                yield break;
            }
        }

        PlayerSession.Username = enteredName;
        PlotSequencer.HasShownChapter0IntroSlides = false;

        yield return StartCoroutine(StoryClient.CompleteAndAdvance(
            config, this,
            next => StoryClient.RouteToStoryScene(next, config, this),
            () =>
            {
                Debug.LogWarning("[C0_S0] Story advance failed.");
                _isSubmitting = false;
                UpdateConfirmButtonState(enteredName);
            },
            chapterId: 0,
            sceneId: 0
        ));
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText == null && nameInputField != null && nameInputField.transform.parent != null)
        {
            var go = new GameObject("ValidationFeedbackText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(nameInputField.transform.parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -90f);
            rt.sizeDelta = new Vector2(900f, 50f);

            feedbackText = go.GetComponent<TextMeshProUGUI>();
            if (nameInputField.textComponent != null)
                feedbackText.font = nameInputField.textComponent.font;
            feedbackText.fontSize = 28f;
            feedbackText.alignment = TextAlignmentOptions.Center;
            feedbackText.color = new Color(1f, 0.4f, 0.4f, 1f);
            feedbackText.raycastTarget = false;
        }

        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.gameObject.SetActive(true);
        }
    }

    [System.Serializable]
    private class ChangeUsernamePayload
    {
        public string newUsername;
    }
}
