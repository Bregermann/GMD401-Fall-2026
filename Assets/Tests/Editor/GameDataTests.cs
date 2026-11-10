using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Checks every real card and attack asset for setup mistakes.
// Each test lists every problem it finds, so one run shows everything to fix.
public class GameDataTests
{
    static List<T> LoadAll<T>(string folder) where T : Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null)
            .ToList();
    }

    static string PathOf(Object asset) => AssetDatabase.GetAssetPath(asset);

    // Placeholder and test cards are skipped, they're meant to be replaced
    static bool IsPlaceholder(Object asset) => PathOf(asset).Contains("/Placeholder/") || PathOf(asset).Contains("/Test") || asset.name.StartsWith("test_");

    static void Report(List<string> problems)
    {
        if (problems.Count > 0) Assert.Fail($"{problems.Count} problem(s):\n" + string.Join("\n", problems));
    }

    [Test]
    public void Cards_AreSetUpCorrectly()
    {
        List<string> problems = new List<string>();
        foreach (CardData card in LoadAll<CardData>("Assets/Data/Cards").Where(c => !IsPlaceholder(c)))
        {
            string where = $"{card.name} ({PathOf(card)})";
            if (string.IsNullOrWhiteSpace(card.cardName)) problems.Add($"{where}: no card name");
            if (card.maxHP <= 0) problems.Add($"{where}: max HP is {card.maxHP}");
            if (card.speed <= 0) problems.Add($"{where}: speed is {card.speed}");
            if (card.moveSpaces < 1) problems.Add($"{where}: move spaces is {card.moveSpaces}");
            if (card.physicalDefense < 0 || card.magicDefense < 0) problems.Add($"{where}: negative defense");

            AttackData[] attacks = { card.attack1, card.attack2, card.attack3, card.attack4 };
            for (int i = 0; i < attacks.Length; i++)
                if (attacks[i] == null) problems.Add($"{where}: attack {i + 1} is empty");
            if (attacks.Where(a => a != null).Distinct().Count() != attacks.Count(a => a != null))
                problems.Add($"{where}: the same attack is used twice");
        }
        Report(problems);
    }

    [Test]
    public void Attacks_AreSetUpCorrectly()
    {
        List<string> problems = new List<string>();
        foreach (AttackData attack in LoadAll<AttackData>("Assets/Data/Attacks").Where(a => !IsPlaceholder(a)))
        {
            string where = $"{attack.name} ({PathOf(attack)})";
            if (string.IsNullOrWhiteSpace(attack.attackName)) problems.Add($"{where}: no attack name");
            if (attack.damage < 0) problems.Add($"{where}: negative damage");
            if (attack.range < 0) problems.Add($"{where}: negative range");

            bool usesArea = attack.area == AttackArea.AroundTarget || attack.area == AttackArea.AroundSelf;
            if (usesArea && attack.areaSize < 1) problems.Add($"{where}: area attack with area size {attack.areaSize}");

            // A damaging attack that only lands on your own team hurts you
            bool ownSide = attack.area == AttackArea.Self || (attack.area == AttackArea.AroundSelf && attack.areaAffects == AreaAffects.Allies);
            if (ownSide && attack.damageType != DamageType.Heal && attack.damage > 0)
                problems.Add($"{where}: deals {attack.damage} damage to its own side");

            if (attack.positionalBonus != PositionalBonus.None && attack.positionalMultiplier <= 0) problems.Add($"{where}: positional multiplier is {attack.positionalMultiplier}");
            if (attack.hasClassBonus && attack.classMultiplier <= 0) problems.Add($"{where}: class multiplier is {attack.classMultiplier}");
            if (attack.whileOnCooldown == CooldownPassive.DamageAttackers && attack.passiveDamage <= 0) problems.Add($"{where}: Damage Attackers passive with no damage");
            if (attack.executeAtOrBelowHP < 0) problems.Add($"{where}: negative execute HP");

            CheckEffects(problems, where, "target effect", attack.targetStatusEffects);
            CheckEffects(problems, where, "self effect", attack.selfStatusEffects);

            if (attack.bonuses == null) continue;
            for (int i = 0; i < attack.bonuses.Length; i++)
            {
                AttackBonus bonus = attack.bonuses[i];
                string which = $"{where}: bonus {i + 1}";
                if (bonus == null) { problems.Add($"{which} is empty"); continue; }

                if (bonus.condition == BonusCondition.AllyInPlay && bonus.ally == null) problems.Add($"{which} (Ally In Play) has no ally set");
                if (bonus.condition == BonusCondition.AttackOnCooldown && bonus.otherAttack == null) problems.Add($"{which} (Attack On Cooldown) has no attack set");
                if (bonus.condition == BonusCondition.FromSide && bonus.side == PositionalBonus.None) problems.Add($"{which} (From Side) has no side set");
                if ((bonus.condition == BonusCondition.AttackerHPBelow || bonus.condition == BonusCondition.AttackerHPAbove) && bonus.hp <= 0)
                    problems.Add($"{which} (HP) has no HP set");

                bool hasReward = bonus.bonusDamage > 0 || (bonus.extraTargetEffects?.Length ?? 0) > 0 || (bonus.extraSelfEffects?.Length ?? 0) > 0;
                if (!hasReward) problems.Add($"{which} gives nothing (no bonus damage or extra effects)");

                CheckEffects(problems, which, "extra target effect", bonus.extraTargetEffects);
                CheckEffects(problems, which, "extra self effect", bonus.extraSelfEffects);
            }
        }
        Report(problems);
    }

    static void CheckEffects(List<string> problems, string where, string label, StatusEffectApplication[] effects)
    {
        if (effects == null) return;
        foreach (StatusEffectApplication effect in effects)
        {
            if (effect == null) { problems.Add($"{where}: empty {label}"); continue; }
            if (effect.amount < 0 || effect.duration < 0) problems.Add($"{where}: {label} {effect.type} has a negative amount or duration");
        }
    }

    [Test]
    public void CardPool_HasNoEmptyOrDuplicateEntries()
    {
        List<string> problems = new List<string>();
        foreach (CardPool pool in LoadAll<CardPool>("Assets/Data"))
        {
            if (pool.cards.Any(c => c == null)) problems.Add($"{pool.name}: has empty entries");
            foreach (var duplicate in pool.cards.Where(c => c != null).GroupBy(c => c).Where(g => g.Count() > 1))
                problems.Add($"{pool.name}: {duplicate.Key.name} is in it {duplicate.Count()} times");
        }
        Report(problems);
    }

    [Test]
    public void CardPool_IncludesEveryRealCard()
    {
        List<CardData> realCards = LoadAll<CardData>("Assets/Data/Cards").Where(c => !IsPlaceholder(c)).ToList();
        List<string> problems = new List<string>();
        foreach (CardPool pool in LoadAll<CardPool>("Assets/Data"))
            foreach (CardData card in realCards.Where(c => !pool.cards.Contains(c)))
                problems.Add($"{pool.name}: missing {card.name} (use Add All Cards In Project)");
        Report(problems);
    }

    [Test]
    public void EffectLibrary_HasNoMissingPrefabs()
    {
        List<string> problems = new List<string>();
        foreach (EffectLibrary library in LoadAll<EffectLibrary>("Assets/Data"))
        {
            // A slot that was set but points at a deleted asset still has its guid saved in the file
            string text = System.IO.File.ReadAllText(PathOf(library));
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, @"guid: ([0-9a-f]{32}), type: 3"))
            {
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(match.Groups[1].Value)))
                    problems.Add($"{library.name}: a slot points at a missing prefab ({match.Groups[1].Value})");
            }
        }
        Report(problems);
    }
}
