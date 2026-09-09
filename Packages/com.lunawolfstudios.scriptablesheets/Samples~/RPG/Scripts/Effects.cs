using System.Collections.Generic;
using UnityEngine;

namespace LunaWolfStudios.ScriptableSheets.Samples.RPG
{
	[System.Serializable]
	public class Effects
	{
		[SerializeField]
		private List<string> m_ConsumableEffects;
		public List<string> ConsumableEffects { get => m_ConsumableEffects; set => m_ConsumableEffects = value; }

		[SerializeField]
		private List<string> m_WeaponEffects;
		public List<string> WeaponEffects { get => m_WeaponEffects; set => m_WeaponEffects = value; }
	}
}
