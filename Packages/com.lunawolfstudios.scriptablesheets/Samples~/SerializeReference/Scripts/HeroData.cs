using UnityEngine;

namespace LunaWolfStudios.ScriptableSheets.Samples.SerializeReference
{
	// A hero whose active ability is stored as a polymorphic serialize reference. Different heroes can use different
	// concrete ability subtypes, which Scriptable Sheets displays as a per-row type dropdown plus a column per subtype field.
	[CreateAssetMenu(fileName = "HeroData", menuName = "Scriptable Sheets/Hero Data")]
	public class HeroData : ScriptableObject
	{
		[SerializeField]
		private CardSuit m_Suit;
		public CardSuit Suit { get => m_Suit; set => m_Suit = value; }

		// Fully qualified because this sample's namespace ends in SerializeReference.
		[UnityEngine.SerializeReference]
		private HeroAbilityActiveData m_AbilityActive;
		public HeroAbilityActiveData AbilityActive { get => m_AbilityActive; set => m_AbilityActive = value; }

		[UnityEngine.SerializeReference]
		private HeroAbilityActiveData m_SecondaryAbility;
		public HeroAbilityActiveData SecondaryAbility { get => m_SecondaryAbility; set => m_SecondaryAbility = value; }
	}
}
