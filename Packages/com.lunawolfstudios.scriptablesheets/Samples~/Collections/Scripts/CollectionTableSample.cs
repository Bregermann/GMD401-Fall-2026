using System;
using System.Collections.Generic;
using UnityEngine;

namespace LunaWolfStudios.ScriptableSheets.Samples.Collections
{
	[Serializable]
	public class CollectionTableSample : ScriptableObject
	{
		[SerializeField]
		private List<string> m_Tags;
		public List<string> Tags { get => m_Tags; set => m_Tags = value; }

		[SerializeField]
		private int[] m_Scores;
		public int[] Scores { get => m_Scores; set => m_Scores = value; }

		[SerializeField]
		private List<float> m_Multipliers;
		public List<float> Multipliers { get => m_Multipliers; set => m_Multipliers = value; }

		[SerializeField]
		private List<LootDrop> m_Loot;
		public List<LootDrop> Loot { get => m_Loot; set => m_Loot = value; }
	}

	[Serializable]
	public class LootDrop
	{
		[SerializeField]
		private string m_Name;
		public string Name { get => m_Name; set => m_Name = value; }

		[SerializeField]
		private int m_Quantity;
		public int Quantity { get => m_Quantity; set => m_Quantity = value; }

		[SerializeField]
		private float m_DropChance;
		public float DropChance { get => m_DropChance; set => m_DropChance = value; }

		[SerializeField]
		private LootRarity m_Rarity;
		public LootRarity Rarity { get => m_Rarity; set => m_Rarity = value; }
	}

	public enum LootRarity
	{
		Common,
		Uncommon,
		Rare,
		Epic,
		Legendary,
	}
}
