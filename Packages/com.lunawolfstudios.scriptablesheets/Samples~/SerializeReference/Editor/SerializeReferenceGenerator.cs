using LunaWolfStudios.ScriptableSheets.Samples.SerializeReference;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Samples.SerializeReference
{
	public class SerializeReferenceGenerator
	{
		[MenuItem("Assets/Create/Scriptable Sheets/Sample Hero Data")]
		public static void CreateSampleHeroData()
		{
			CreateHero("HeroDataDirectAttack", CardSuit.Hearts, new HeroAbilityActiveDataDirectAttack { ChargeCount = 1, Damage = 10 });
			CreateHero("HeroDataDamageObjects", CardSuit.Clubs, new HeroAbilityActiveDataDamageObjects { ChargeCount = 2, CardCount = 3, Damage = 5 });
			CreateHero("HeroDataAddCardsToField", CardSuit.Diamonds, new HeroAbilityActiveDataAddCardsToField { ChargeCount = 1, CardCount = 2 });
			CreateHero("HeroDataSuitDebuff", CardSuit.Spades, new HeroAbilityActiveDataSuitDebuff { ChargeCount = 3, DebuffStrength = 0.25f });
			CreateHero("HeroDataComboAttack", CardSuit.Hearts, new HeroAbilityActiveDataComboAttack { ChargeCount = 2, DamagePerHit = new[] { 3, 5, 8 }, Modifier = new AbilityModifier { Multiplier = 1.5f, Note = "Finisher" } });
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		// Managed references inside a list on a ScriptableObject.
		[MenuItem("Assets/Create/Scriptable Sheets/Sample Hero Deck")]
		public static void CreateSampleHeroDeck()
		{
			var heroDeck = ScriptableObject.CreateInstance<HeroDeck>();
			heroDeck.Suit = CardSuit.Hearts;
			heroDeck.Abilities = new List<HeroAbilityActiveData>
			{
				new HeroAbilityActiveDataDirectAttack { ChargeCount = 1, Damage = 10 },
				new HeroAbilityActiveDataDamageObjects { ChargeCount = 2, CardCount = 3, Damage = 5 },
				new HeroAbilityActiveDataAddCardsToField { ChargeCount = 1, CardCount = 2 },
				new HeroAbilityActiveDataSuitDebuff { ChargeCount = 3, DebuffStrength = 0.25f },
				new HeroAbilityActiveDataComboAttack { ChargeCount = 2, DamagePerHit = new[] { 3, 5, 8 }, Modifier = new AbilityModifier { Multiplier = 1.5f, Note = "Finisher" } },
			};
			var path = AssetDatabase.GenerateUniqueAssetPath("Assets/HeroDeck.asset");
			AssetDatabase.CreateAsset(heroDeck, path);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		// Managed references on a Prefab MonoBehaviour Component.
		[MenuItem("Assets/Create/Scriptable Sheets/Sample Hero Ability Prefab")]
		public static void CreateSampleHeroAbilityPrefab()
		{
			var gameObject = new GameObject("HeroAbilityPrefab");
			try
			{
				var heroAbility = gameObject.AddComponent<HeroAbilityComponent>();
				heroAbility.Suit = CardSuit.Clubs;
				heroAbility.AbilityActive = new HeroAbilityActiveDataDirectAttack { ChargeCount = 2, Damage = 15 };
				heroAbility.Abilities = new List<HeroAbilityActiveData>
				{
					new HeroAbilityActiveDataSuitDebuff { ChargeCount = 1, DebuffStrength = 0.15f },
					new HeroAbilityActiveDataComboAttack { ChargeCount = 3, DamagePerHit = new[] { 4, 6 }, Modifier = new AbilityModifier { Multiplier = 2f, Note = "Charged" } },
				};
				var path = AssetDatabase.GenerateUniqueAssetPath("Assets/HeroAbilityPrefab.prefab");
				PrefabUtility.SaveAsPrefabAsset(gameObject, path);
			}
			finally
			{
				Object.DestroyImmediate(gameObject);
			}
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		// Collection Tables and a managed reference from within a Sub Asset.
		[MenuItem("Assets/Create/Scriptable Sheets/Sample Ability Book")]
		public static void CreateSampleAbilityBook()
		{
			var abilityBook = ScriptableObject.CreateInstance<AbilityBook>();
			abilityBook.Description = "A book of ability cards, each a Sub Asset with its own managed reference and collections.";
			abilityBook.Suit = CardSuit.Hearts;

			var path = AssetDatabase.GenerateUniqueAssetPath("Assets/AbilityBook.asset");
			AssetDatabase.CreateAsset(abilityBook, path);

			abilityBook.Cards = new[]
			{
				CreateCard(abilityBook, "FireballCard", "Fireball",
					new HeroAbilityActiveDataDirectAttack { ChargeCount = 1, Damage = 12 },
					new List<int> { 12, 6, 3 },
					new List<AbilityModifier> { new AbilityModifier { Multiplier = 1.5f, Note = "Burn" } }),
				CreateCard(abilityBook, "ShuffleCard", "Shuffle",
					new HeroAbilityActiveDataAddCardsToField { ChargeCount = 2, CardCount = 3 },
					new List<int>(),
					new List<AbilityModifier> { new AbilityModifier { Multiplier = 1f, Note = "Draw" }, new AbilityModifier { Multiplier = 1.25f, Note = "Bonus" } }),
				CreateCard(abilityBook, "ComboCard", "Combo",
					new HeroAbilityActiveDataComboAttack { ChargeCount = 2, DamagePerHit = new[] { 2, 4, 6 }, Modifier = new AbilityModifier { Multiplier = 2f, Note = "Finisher" } },
					new List<int> { 2, 4, 6 },
					new List<AbilityModifier> { new AbilityModifier { Multiplier = 1.75f, Note = "Chain" } }),
			};

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		private static void CreateHero(string name, CardSuit suit, HeroAbilityActiveData abilityActive)
		{
			var heroData = ScriptableObject.CreateInstance<HeroData>();
			heroData.Suit = suit;
			heroData.AbilityActive = abilityActive;
			var path = AssetDatabase.GenerateUniqueAssetPath($"Assets/{name}.asset");
			AssetDatabase.CreateAsset(heroData, path);
		}

		private static AbilityCard CreateCard(AbilityBook book, string name, string title, HeroAbilityActiveData ability, List<int> damagePerTurn, List<AbilityModifier> modifiers)
		{
			var card = ScriptableObject.CreateInstance<AbilityCard>();
			card.name = name;
			card.Title = title;
			card.Ability = ability;
			card.DamagePerTurn = damagePerTurn;
			card.Modifiers = modifiers;
			AssetDatabase.AddObjectToAsset(card, book);
			return card;
		}
	}
}
