using System.Collections.Generic;
using UnityEngine;

namespace LunaWolfStudios.ScriptableSheets.Samples.SerializeReference
{
	// A deck whose abilities are stored as a list of polymorphic serialize references on a single ScriptableObject.
	// Each element can be a different concrete subtype, which Scriptable Sheets flattens into a per-element type
	// dropdown plus a column for every subtype field.
	[CreateAssetMenu(fileName = "HeroDeck", menuName = "Scriptable Sheets/Hero Deck")]
	public class HeroDeck : ScriptableObject
	{
		[SerializeField]
		private CardSuit m_Suit;
		public CardSuit Suit { get => m_Suit; set => m_Suit = value; }

		// Fully qualified because this sample's namespace ends in SerializeReference.
		[UnityEngine.SerializeReference]
		private List<HeroAbilityActiveData> m_Abilities = new List<HeroAbilityActiveData>();
		public List<HeroAbilityActiveData> Abilities { get => m_Abilities; set => m_Abilities = value; }
	}
}
