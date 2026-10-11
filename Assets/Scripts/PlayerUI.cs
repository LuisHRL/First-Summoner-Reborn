using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public ManaSystem manaSystem;

    public Slider healthBar;
    public Slider manaBar;

    void Start()
    {
        healthBar.maxValue = playerHealth.maxHealth;
        manaBar.maxValue = manaSystem.maxMana;
    }

    void Update()
    {
        healthBar.value = playerHealth.CurrentHealth;
        manaBar.value = manaSystem.currentMana;
    }
}