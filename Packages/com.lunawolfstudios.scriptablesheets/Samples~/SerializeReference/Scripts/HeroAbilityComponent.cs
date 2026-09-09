using System.Collections.Generic;
using UnityEngine;

namespace LunaWolfStudios.ScriptableSheets.Samples.SerializeReference
{
	// A MonoBehaviour that stores polymorphic abilities as serialize references. Attach it to a Prefab to see how
	// Scriptable Sheets edits managed references on Prefab Components, not just ScriptableObjects.
	public class HeroAbilityComponent : MonoBehaviour
	{
		[SerializeField]
		private CardSuit m_Suit;
		public CardSuit Suit { get => m_Suit; set => m_Suit = value; }

		// Fully qualified because this sample's namespace ends in SerializeReference.
		[UnityEngine.SerializeReference]
		private HeroAbilityActiveData m_AbilityActive;
		public HeroAbilityActiveData AbilityActive { get => m_AbilityActive; set => m_AbilityActive = value; }

		[UnityEngine.SerializeReference]
		private List<HeroAbilityActiveData> m_Abilities = new List<HeroAbilityActiveData>();
		public List<HeroAbilityActiveData> Abilities { get => m_Abilities; set => m_Abilities = value; }
	}
}
