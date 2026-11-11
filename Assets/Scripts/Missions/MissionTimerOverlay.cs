using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Automatically displays the active mission timer in the gameplay scene.</summary>
public sealed class MissionTimerOverlay : MonoBehaviour
{
    private TMP_Text clockText;

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
        if (FindFirstObjectByType<MissionTimerOverlay>() != null) return;
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

    private void Update()
    {
        if (GameSession.MissionTimerRunning)
            GameSession.TickMissionTimer(Time.deltaTime);
        if (clockText == null) return;
        int total = Mathf.FloorToInt(GameSession.MissionElapsedSeconds);
        clockText.text = string.Format("{0:00}:{1:00}", total / 60, total % 60);
    }
}
