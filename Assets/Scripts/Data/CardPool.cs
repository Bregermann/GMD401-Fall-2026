using System.Collections.Generic;
using UnityEngine;

// Every card in the game. The draft picks its random pool from this list.
[CreateAssetMenu(fileName = "CardPool", menuName = "Stratafield/Card Pool")]
public class CardPool : ScriptableObject
{
    public List<CardData> cards = new List<CardData>();

    // Random cards for one draft with at least perClass of every class, no duplicates.
    // If a class doesn't have enough cards, the gap is filled with random ones.
    public List<CardData> GetDraftPool(int size, int perClass)
    {
        List<CardData> remaining = new List<CardData>();
        foreach (CardData card in cards)
            if (card != null && !remaining.Contains(card)) remaining.Add(card);
        Shuffle(remaining);

        List<CardData> pool = new List<CardData>();

        // Guaranteed cards from each class first
        foreach (CharacterClass characterClass in System.Enum.GetValues(typeof(CharacterClass)))
        {
            List<CardData> ofClass = remaining.FindAll(c => c.characterClass == characterClass);
            if (ofClass.Count < perClass)
                Debug.LogWarning($"Card Pool only has {ofClass.Count} {characterClass} card(s), the draft wants {perClass}");

            for (int i = 0; i < perClass && i < ofClass.Count && pool.Count < size; i++)
            {
                pool.Add(ofClass[i]);
                remaining.Remove(ofClass[i]);
            }
        }

        // Fill the rest randomly
        for (int i = 0; i < remaining.Count && pool.Count < size; i++)
            pool.Add(remaining[i]);

        if (pool.Count < size)
            Debug.LogWarning($"Card Pool only has {pool.Count} cards, the draft wants {size}");

        Shuffle(pool);
        return pool;
    }

    private static void Shuffle(List<CardData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
