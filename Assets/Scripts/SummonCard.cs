using UnityEngine;

public class SummonCard : MonoBehaviour
{
    public ManaSystem manaSystem;
    public GameObject minionPrefab;
    public Transform player;
    public int manaCost = 25;
    public AudioClip summonSound;

    public void PlayCard()
    {
        if (!manaSystem.SpendMana(manaCost))
        {
            Debug.Log("Pas assez de mana");
            return;
        }

        Vector3 spawnPosition = player.position + player.forward * 2f;
        if (summonSound != null)
        {
            AudioSource.PlayClipAtPoint(summonSound, spawnPosition);
        }
        Instantiate(minionPrefab, spawnPosition, Quaternion.identity);
    }
}