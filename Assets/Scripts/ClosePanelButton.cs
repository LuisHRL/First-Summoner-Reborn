
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ClosePanelButton : MonoBehaviour
{
    public enum CloseAction
    {
        MainMenu,
        Hero,
        Settings,
        Cards
    }

    [SerializeField]
    private CloseAction closeAction = CloseAction.MainMenu;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(ClosePanel);
    }

    private void ClosePanel()
    {
        MainMenu menu = FindAnyObjectByType<MainMenu>();

        if (menu == null)
        {
            Debug.LogWarning("ClosePanelButton : MainMenu introuvable.");
            return;
        }

        switch (closeAction)
        {
            case CloseAction.MainMenu:
            case CloseAction.Hero:
                menu.BackToHero();
                break;
            case CloseAction.Settings:
                menu.BackToSettings();
                break;
            case CloseAction.Cards:
                CardDetailsUI details = GetComponentInParent<CardDetailsUI>(true);

                if (details != null)
                {
                    details.Hide();
                }
                break;
        }
    }
}
