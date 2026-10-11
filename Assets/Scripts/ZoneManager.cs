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
        zoneCompleteText.SetActive(false);
        returnMenuButton.SetActive(false);
    }

    void Update()
    {
        if (zoneComplete)
            return;

        if (enemySpawner.IsWaveFinished &&
            FindAnyObjectByType<EnemyHealth>() == null)
        {
            CompleteZone();
        }
    }

    void CompleteZone()
    {
        zoneComplete = true;
        zoneCompleteText.SetActive(true);
        returnMenuButton.SetActive(true);
    }

    public void ReturnToMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}