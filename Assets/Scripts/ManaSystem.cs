using UnityEngine;

public class ManaSystem : MonoBehaviour
{
    public int maxMana = 100;
    public int currentMana;

    public float manaRegenPerSecond = 5f;

    // Ce "buffer" garde en mémoire les petits bouts de mana
    // (ex: 0.4) en attendant qu'ils s'accumulent jusqu'à 1 mana entier.
    private float manaBuffer;

    void Start()
    {
        currentMana = maxMana;
    }

    void Update()
    {
        // La régénération passive utilise maintenant la même
        // méthode que les récompenses de kill (voir GainMana).
        GainMana(manaRegenPerSecond * Time.deltaTime);
    }

    public bool SpendMana(int amount)
    {
        if (currentMana < amount)
            return false;

        currentMana -= amount;
        return true;
    }

    public void RestoreMana(int amount)
    {
        currentMana = Mathf.Min(currentMana + amount, maxMana);
    }

    // Ajoute un gain de mana qui peut être fractionnaire (ex: 0.4).
    // Utilisable pour la régén passive ET pour les récompenses
    // (kill d'ennemi, objet ramassé, etc.)
    public void GainMana(float amount)
    {
        if (currentMana >= maxMana)
            return;

        manaBuffer += amount;

        int manaToRestore = Mathf.FloorToInt(manaBuffer);

        if (manaToRestore > 0)
        {
            RestoreMana(manaToRestore);
            manaBuffer -= manaToRestore;
        }
    }
}
