using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Automatically displays the active mission timer in the gameplay scene.</summary>
public sealed class MissionTimerOverlay : MonoBehaviour
{
    private TMP_Text clockText;
    private GameObject resultsPanel;
    private TMP_Text resultsText;
    private bool resultsShown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (GameSession.SelectedMission == null || scene.name != "GameScene") return;
        if (FindAnyObjectByType<MissionTimerOverlay>() != null) return;
        var root = new GameObject("MissionTimerOverlay", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(MissionTimerOverlay));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var label = new GameObject("ElapsedTime", typeof(RectTransform),
            typeof(TextMeshProUGUI));
        label.transform.SetParent(root.transform, false);
        var rect = label.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-28f, -36f);
        rect.sizeDelta = new Vector2(250f, 90f);
        var text = label.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Right;
        text.fontSize = 44;
        text.color = Color.white;
        text.raycastTarget = false;
        root.GetComponent<MissionTimerOverlay>().clockText = text;
        GameSession.StartMissionTimer();
    }


    private void ShowResults()
    {
        resultsShown = true;
        bool won = !GameSession.MissionTimedOut;
        var root = GetComponent<Canvas>().transform;
        resultsPanel = new GameObject("MissionResults", typeof(RectTransform), typeof(Image));
        resultsPanel.transform.SetParent(root, false);
        var rect = resultsPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.32f);
        rect.anchorMax = new Vector2(0.9f, 0.68f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        resultsPanel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.94f);
        var textObject = new GameObject("ResultText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(rect, false);
        var labelRect = textObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.35f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        resultsText = textObject.GetComponent<TextMeshProUGUI>();
        resultsText.alignment = TextAlignmentOptions.Center;
        resultsText.fontSize = 38f;
        resultsText.color = Color.white;
        int elapsed = Mathf.CeilToInt(GameSession.MissionElapsedSeconds);
        int stars = won ? MissionProgress.StarsForTime(GameSession.SelectedMission, GameSession.MissionElapsedSeconds) : 0;
        float best = won ? MissionProgress.BestTime(GameSession.SelectedMission,
            (MissionProgress.Difficulty)GameSession.SelectedDifficulty) : -1f;
        resultsText.text = (won ? "VICTORY" : "DEFEAT") + "\\n" +
            string.Format("{0:00}:{1:00}", elapsed / 60, elapsed % 60) + "\\n" +
            (won ? new string('★', stars) + new string('☆', 3 - stars) : "☆☆☆") +
            (best >= 0f ? "\\nBest: " + Mathf.CeilToInt(best) + "s" : "");
        CreateResultButton(rect, "Retry", -0.26f, RetryMission);
        CreateResultButton(rect, "Missions", 0.26f, ReturnToMissions);
    }

    private void CreateResultButton(RectTransform parent, string label, float x, UnityEngine.Events.UnityAction action)
    {
        var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f + x, 0.17f);
        rect.sizeDelta = new Vector2(220f, 80f);
        obj.GetComponent<Image>().color = new Color(0.3f, 0.18f, 0.13f, 1f);
        obj.GetComponent<Button>().onClick.AddListener(action);
        var child = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        child.transform.SetParent(obj.transform, false);
        var childRect = child.GetComponent<RectTransform>();
        childRect.anchorMin = Vector2.zero;
        childRect.anchorMax = Vector2.one;
        childRect.offsetMin = childRect.offsetMax = Vector2.zero;
        var text = child.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 28f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }

    private void RetryMission()
    {
        SceneManager.LoadScene(gameObject.scene.name);
    }

    private void ReturnToMissions()
    {
        string returnScene = GameSession.MissionReturnSceneName;
        if (string.IsNullOrEmpty(returnScene) || !Application.CanStreamedLevelBeLoaded(returnScene))
        {
            Debug.LogError("Mission return scene is unavailable in Build Settings.");
            return;
        }
        GameSession.ClearMissionSelection();
        GameSession.RequestMissionMenuReturn();
        SceneManager.LoadScene(returnScene);
    }

    private void Update()
    {
        if (GameSession.MissionTimerRunning)
            GameSession.TickMissionTimer(Time.deltaTime);
        if (clockText == null) return;
        if (!resultsShown && (GameSession.MissionTimedOut ||
            (!GameSession.MissionTimerRunning && GameSession.SelectedMission != null)))
            ShowResults();
        int total = Mathf.CeilToInt(GameSession.MissionTimeRemaining);
        clockText.text = GameSession.MissionTimedOut ? "00:00 - Failed" :
            string.Format("{0:00}:{1:00}", total / 60, total % 60);
    }
}
