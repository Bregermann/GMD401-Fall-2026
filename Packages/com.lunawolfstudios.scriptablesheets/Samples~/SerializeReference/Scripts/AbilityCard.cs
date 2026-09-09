using System.Collections.Generic;
using UnityEngine;

namespace LunaWolfStudios.ScriptableSheets.Samples.SerializeReference
{
	// A Sub Asset that combines a polymorphic serialize reference with serializable collections. The managed reference
	// lives inside the Sub Asset, while the collections can be flipped into their own Collection Tables.
	[System.Serializable]
	public class AbilityCard : ScriptableObject
	{
		[SerializeField]
		private string m_Title;
		public string Title { get => m_Title; set => m_Title = value; }

		// Fully qualified because this sample's namespace ends in SerializeReference.
		[UnityEngine.SerializeReference]
		private HeroAbilityActiveData m_Ability;
		public HeroAbilityActiveData Ability { get => m_Ability; set => m_Ability = value; }

		[SerializeField]
		private List<int> m_DamagePerTurn = new List<int>();
		public List<int> DamagePerTurn { get => m_DamagePerTurn; set => m_DamagePerTurn = value; }

		[SerializeField]
		private List<AbilityModifier> m_Modifiers = new List<AbilityModifier>();
		public List<AbilityModifier> Modifiers { get => m_Modifiers; set => m_Modifiers = value; }
	}
}
