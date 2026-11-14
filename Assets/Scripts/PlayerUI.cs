using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public ManaSystem manaSystem;

    public Slider healthBar;
    public Slider manaBar;
    [Header("Optional animated mana visuals")]
    public AnimatedFillEffect manaBarAnimation;
    public AnimatedFillEffect manaIconAnimation;
    public Image manaGainFx;
    [Min(0.01f)] public float manaGainFxDuration = 0.35f;
    private int previousMana;
    private float gainFxRemaining;

    void Start()
    {
        healthBar.maxValue = playerHealth.maxHealth;
        manaBar.maxValue = manaSystem.maxMana;
        previousMana = manaSystem.currentMana;
        if (manaBarAnimation != null) manaBarAnimation.SetValue((float)manaSystem.currentMana / Mathf.Max(1, manaSystem.maxMana), true);
        if (manaIconAnimation != null) manaIconAnimation.SetValue(manaSystem.FractionalManaProgress, true);
        if (manaGainFx != null) manaGainFx.gameObject.SetActive(false);
    }

    void Update()
    {
        healthBar.value = playerHealth.CurrentHealth;
        manaBar.value = manaSystem.currentMana;
        if (manaBarAnimation != null) manaBarAnimation.SetValue((float)manaSystem.currentMana / Mathf.Max(1, manaSystem.maxMana), true);
        if (manaIconAnimation != null) manaIconAnimation.SetValue(manaSystem.FractionalManaProgress);
        if (manaSystem.currentMana > previousMana && manaGainFx != null)
        {
            gainFxRemaining = manaGainFxDuration;
            manaGainFx.gameObject.SetActive(true);
        }
        previousMana = manaSystem.currentMana;
        if (gainFxRemaining > 0f)
        {
            gainFxRemaining -= Time.deltaTime;
            if (gainFxRemaining <= 0f && manaGainFx != null)
                manaGainFx.gameObject.SetActive(false);
        }
    }
}