using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Localization.Settings;

public class MainMenu : MonoBehaviour
{
    public GameObject mainButtonsPanel;
    public GameObject missionPanel;
    public GameObject cardsPanel;
    public GameObject settingsPanel;
    public GameObject aboutPanel;

    void Start()
    {
        // Recharge le choix de contrôle sauvegardé la dernière fois.
        ControlSettings.LoadSavedMode();

        mainButtonsPanel.SetActive(true);
        missionPanel.SetActive(false);
        cardsPanel.SetActive(false);
        settingsPanel.SetActive(false);
        aboutPanel.SetActive(false);
    }

    public void OpenMissions()
    {
        mainButtonsPanel.SetActive(false);
        missionPanel.SetActive(true);
    }
    public void OpenAbout()
    {
        mainButtonsPanel.SetActive(false);
        aboutPanel.SetActive(true);
    }
    public void OpenCards()
    {
        mainButtonsPanel.SetActive(false);
        cardsPanel.SetActive(true);
    }

    public void OpenSettings()
    {
        mainButtonsPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void BackToMainMenu()
    {
        missionPanel.SetActive(false);
        cardsPanel.SetActive(false);
        settingsPanel.SetActive(false);
        mainButtonsPanel.SetActive(true);
        aboutPanel.SetActive(false);
    }

    public void StartNormalMission()
    {
        GameSession.SelectedDifficulty = 1;
        SceneManager.LoadScene("GameScene");
    }

    public void StartHardMission()
    {
        GameSession.SelectedDifficulty = 2;
        SceneManager.LoadScene("GameScene");
    }

    public void OpenHero()
    {
        Debug.Log("Gestion du héros : à créer.");
    }

    // A brancher sur un bouton "Tap to Move" dans SettingsPanel.
    public void UseTapToMove()
    {
        ControlSettings.SetMode(ControlMode.TapToMove);
    }

    // A brancher sur un bouton "Joystick" dans SettingsPanel.
    public void UseVirtualJoystick()
    {
        ControlSettings.SetMode(ControlMode.VirtualJoystick);
    }
    public void OpenGitHub()
    {
        Application.OpenURL("https://github.com/LuisHRL/First-Summoner-Reborn");
    }
    public void ChangeLanguage(int languageIndex)
    {
        StartCoroutine(SetLanguage(languageIndex));
    }

    private System.Collections.IEnumerator SetLanguage(int languageIndex)
    {
        yield return LocalizationSettings.InitializationOperation;

        if (languageIndex >= 0 &&
            languageIndex < LocalizationSettings.AvailableLocales.Locales.Count)
        {
            LocalizationSettings.SelectedLocale =
                LocalizationSettings.AvailableLocales.Locales[languageIndex];
        }
    }
}