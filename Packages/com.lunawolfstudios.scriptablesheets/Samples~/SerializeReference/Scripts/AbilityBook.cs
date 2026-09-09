using UnityEngine;

namespace LunaWolfStudios.ScriptableSheets.Samples.SerializeReference
{
	// A main asset that holds AbilityCard Sub Assets. Each Sub Asset carries its own managed reference and collections,
	// showing how Scriptable Sheets handles serialize references and Collection Tables sourced from Sub Assets.
	[System.Serializable]
	public class AbilityBook : ScriptableObject
	{
		[SerializeField]
		private string m_Description;
		public string Description { get => m_Description; set => m_Description = value; }

		[SerializeField]
		private CardSuit m_Suit;
		public CardSuit Suit { get => m_Suit; set => m_Suit = value; }

		[SerializeField]
		private AbilityCard[] m_Cards;
		public AbilityCard[] Cards { get => m_Cards; set => m_Cards = value; }
	}
}
