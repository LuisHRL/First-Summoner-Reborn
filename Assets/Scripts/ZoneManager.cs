using UnityEngine;
using UnityEngine.SceneManagement;

public class ZoneManager : MonoBehaviour
{
    public EnemySpawner enemySpawner;
    public GameObject zoneCompleteText;
    public GameObject returnMenuButton;

    private bool zoneComplete;

    void Start()
    {
        if (zoneCompleteText != null) zoneCompleteText.SetActive(false);
        if (returnMenuButton != null) returnMenuButton.SetActive(false);
    }

    void Update()
    {
        if (zoneComplete || (GameSession.SelectedMission != null &&
            (!GameSession.MissionTimerRunning || GameSession.MissionTimedOut)))
            return;

        if (enemySpawner == null) return;

        if (enemySpawner.IsWaveFinished &&
            FindAnyObjectByType<EnemyHealth>() == null)
        {
            CompleteZone();
        }
    }

    void CompleteZone()
    {
        if (GameSession.SelectedMission != null)
        {
            if (!GameSession.CompleteSelectedMission()) return;
            zoneComplete = true;
            return; // The mission results overlay handles the victory UI.
        }

        zoneComplete = true;
        if (zoneCompleteText != null) zoneCompleteText.SetActive(true);
        if (returnMenuButton != null) returnMenuButton.SetActive(true);
    }

    public void ReturnToMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}