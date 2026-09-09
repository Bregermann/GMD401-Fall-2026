using System;
using UnityEngine;

namespace LunaWolfStudios.ScriptableSheets.Samples.SerializeReference
{
	// Abstract base for a hero's active ability. Every concrete subtype shares ChargeCount but adds its own fields.
	[Serializable]
	public abstract class HeroAbilityActiveData : PolymorphicData
	{
		[SerializeField]
		private int m_ChargeCount;
		public int ChargeCount { get => m_ChargeCount; set => m_ChargeCount = value; }
	}

	[Serializable]
	public class HeroAbilityActiveDataDirectAttack : HeroAbilityActiveData
	{
		[SerializeField]
		private int m_Damage;
		public int Damage { get => m_Damage; set => m_Damage = value; }
	}

	[Serializable]
	public class HeroAbilityActiveDataDamageObjects : HeroAbilityActiveData
	{
		[SerializeField]
		private int m_CardCount;
		public int CardCount { get => m_CardCount; set => m_CardCount = value; }

		[SerializeField]
		private int m_Damage;
		public int Damage { get => m_Damage; set => m_Damage = value; }
	}

	[Serializable]
	public class HeroAbilityActiveDataAddCardsToField : HeroAbilityActiveData
	{
		[SerializeField]
		private int m_CardCount;
		public int CardCount { get => m_CardCount; set => m_CardCount = value; }
	}

	[Serializable]
	public class HeroAbilityActiveDataSuitDebuff : HeroAbilityActiveData
	{
		[SerializeField]
		private float m_DebuffStrength = 0.15f;
		public float DebuffStrength { get => m_DebuffStrength; set => m_DebuffStrength = value; }
	}

	[Serializable]
	public class HeroAbilityActiveDataComboAttack : HeroAbilityActiveData
	{
		[SerializeField]
		private int[] m_DamagePerHit;
		public int[] DamagePerHit { get => m_DamagePerHit; set => m_DamagePerHit = value; }

		[SerializeField]
		private AbilityModifier m_Modifier;
		public AbilityModifier Modifier { get => m_Modifier; set => m_Modifier = value; }
	}

	[Serializable]
	public class AbilityModifier
	{
		[SerializeField]
		private float m_Multiplier = 1f;
		public float Multiplier { get => m_Multiplier; set => m_Multiplier = value; }

		[SerializeField]
		private string m_Note;
		public string Note { get => m_Note; set => m_Note = value; }
	}
}
