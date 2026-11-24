using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Balance report for the real cards (placeholders and test cards are left out). Only runs when asked for
// (Test Runner, or -testFilter BalanceSimulation) because it plays thousands of battles. Writes Logs/balance-report.txt.
//  1. Card and attack table
//  2. Turns to kill: each card attacking each other card, standing in range, using its best attacks with the cooldown
//     rule (can't use the same attack twice in a row) and status effects ticking
//  3. 1v1 duels on the real 8x8 board with movement, both cards played by the computer opponent's planner
//  4. Random 3v3 teams and random 6v6 splits, placed and played by the computer opponent
// Everything uses the real combat code (CombatResolver, statuses, cooldowns, knockback) and the real card data
public class BalanceSimulation
{
    const int DuelsPerPair = 12, TeamGames3 = 1500, TeamGames6 = 600, MaxRounds = 40;

    class Tally
    {
        public int games, wins, draws, survived, kills;
        public long damage, healing, damageTaken;
        public float WinRate => games == 0 ? 0f : (wins + 0.5f * draws) / games;
    }

    [Test, Explicit("Plays thousands of battles; run on purpose")]
    public void BalanceReport()
    {
        Random.InitState(20261121);
        List<CardData> cards = LoadRealCards();
        Assert.Greater(cards.Count, 1, "Need at least two real cards");
        StringBuilder report = new StringBuilder();
        report.AppendLine($"STRATAFIELD BALANCE REPORT  ({cards.Count} cards)\n");

        CardTable(cards, report);
        TurnsToKill(cards, report);
        Duels(cards, report);
        Teams(cards, 3, TeamGames3, report);
        Teams(cards, 6, TeamGames6, report);

        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "balance-report.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, report.ToString());
        Debug.Log($"Balance report written to {path}\n{report}");
    }

    // ---- Cards ----

    static List<CardData> LoadRealCards()
    {
        List<CardData> cards = new List<CardData>();
        foreach (string guid in AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data/Cards" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Placeholder") || Path.GetFileName(path).StartsWith("test")) continue;
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card != null) cards.Add(card);
        }
        return cards.OrderBy(c => c.characterClass).ThenBy(c => c.cardName).ToList();
    }

    static AttackData[] Attacks(CardData card) => new[] { card.attack1, card.attack2, card.attack3, card.attack4 };

    static void CardTable(List<CardData> cards, StringBuilder report)
    {
        report.AppendLine("== 1. CARDS ==");
        report.AppendLine($"{"Card",-10} {"Class",-9} {"HP",5} {"PDef",4} {"MDef",4} {"Spd",4} {"Mv",2} Diag");
        foreach (CardData c in cards)
            report.AppendLine($"{c.cardName,-10} {c.characterClass,-9} {c.maxHP,5} {c.physicalDefense,4} {c.magicDefense,4} {c.speed,4} {c.moveSpaces,2} {(c.canMoveDiagonally ? "yes" : "")}");

        report.AppendLine("\nAttacks (dmg type range area | effects | extras):");
        foreach (CardData c in cards)
        {
            report.AppendLine($"  {c.cardName}");
            foreach (AttackData a in Attacks(c))
            {
                if (a == null) { report.AppendLine("    (empty slot)"); continue; }
                List<string> extras = new List<string>();
                if (a.hasKnockback) extras.Add($"knockback({a.knockbackCollisionDamage})");
                if (a.ignoresDefense) extras.Add("ignores defense");
                if (a.executeAtOrBelowHP > 0) extras.Add($"execute<={a.executeAtOrBelowHP}");
                if (a.whileOnCooldown != CooldownPassive.None) extras.Add($"passive {a.whileOnCooldown}({a.passiveDamage})");
                if (a.positionalBonus != PositionalBonus.None) extras.Add($"{a.positionalBonus} x{a.positionalMultiplier}");
                if (a.hasClassBonus) extras.Add($"vs {a.classBonusTarget} x{a.classMultiplier}");
                if (a.bonuses != null) foreach (AttackBonus b in a.bonuses) if (b != null) extras.Add($"bonus +{b.bonusDamage} if {b.condition}");
                string effects = string.Join(",", (a.targetStatusEffects ?? new StatusEffectApplication[0]).Select(e => e.type.ToString()))
                               + ((a.selfStatusEffects?.Length ?? 0) > 0 ? " self:" + string.Join(",", a.selfStatusEffects.Select(e => e.type.ToString())) : "");
                int range = a.range > 0 ? a.range : a.damageType == DamageType.Heal ? CombatResolver.HealRange : c.characterClass == CharacterClass.Ranged ? CombatResolver.RangedRange : CombatResolver.MeleeRange;
                string area = a.area == AttackArea.Single ? "" : $"{a.area}{a.areaSize}";
                report.AppendLine($"    {a.attackName,-22} {a.damage,4} {a.damageType,-8} r{range,-3} {area,-14}| {effects,-28}| {string.Join("; ", extras)}");
            }
        }
        report.AppendLine();
    }

    // ---- Turns to kill ----

    // The attacker stands in range and uses its most damaging allowed attack each turn (cooldown rule applies,
    // statuses tick). Movement, the defender fighting back and range are ignored: this is raw killing power
    static void TurnsToKill(List<CardData> cards, StringBuilder report)
    {
        report.AppendLine("== 2. TURNS TO KILL (row attacks column; fewer = deadlier) ==");
        report.Append($"{"",-10}");
        foreach (CardData d in cards) report.Append($"{Short(d),6}");
        report.AppendLine($"{"avg",7}");

        Dictionary<CardData, float> taken = cards.ToDictionary(c => c, c => 0f);
        foreach (CardData attacker in cards)
        {
            report.Append($"{attacker.cardName,-10}");
            float sum = 0f;
            foreach (CardData defender in cards)
            {
                int turns = TurnsToKill(attacker, defender);
                sum += turns;
                taken[defender] += turns;
                report.Append($"{(turns >= 99 ? "-" : turns.ToString()),6}");
            }
            report.AppendLine($"{sum / cards.Count,7:0.0}");
        }
        report.AppendLine("\nAverage turns to be killed (higher = tankier):");
        foreach (KeyValuePair<CardData, float> pair in taken.OrderByDescending(p => p.Value))
            report.AppendLine($"  {pair.Key.cardName,-10} {pair.Value / cards.Count,5:0.0}");
        report.AppendLine();
    }

    static string Short(CardData card) => card.cardName.Length > 5 ? card.cardName.Substring(0, 5) : card.cardName;

    static int TurnsToKill(CardData attackerData, CardData defenderData)
    {
        BoardState board = new BoardState();
        CardInstance attacker = new CardInstance(attackerData, 1, -1, -1);
        CardInstance defender = new CardInstance(defenderData, 2, -1, -1);
        board.Place(attacker, 3, 3);
        board.Place(defender, 4, 3); // Adjacent, so melee and ranged can both reach
        for (int turn = 1; turn <= 30; turn++)
        {
            attacker.StartTurn();
            AttackData best = null;
            int bestDamage = -1;
            foreach (AttackData attack in Attacks(attackerData))
            {
                if (attack == null || attack.damageType == DamageType.Heal || CombatResolver.UsedOnSelf(attack) && attack.damage == 0) continue;
                CardInstance target = CombatResolver.UsedOnSelf(attack) ? attacker : defender;
                AttackResult preview = CombatResolver.PreviewAttack(attacker, attack, target, board);
                if (preview == null) continue;
                int damage = preview.hits.Where(h => h.card == defender).Sum(h => h.damage) + (preview.hits.Any(h => h.card == defender && h.executed) ? 99999 : 0);
                if (damage > bestDamage) { bestDamage = damage; best = attack; }
            }
            if (best != null) CombatResolver.ResolveAttack(attacker, best, CombatResolver.UsedOnSelf(best) ? attacker : defender, board);
            else attacker.SkipAttack();
            attacker.EndTurn();
            defender.StartTurn(); defender.EndTurn(); // Burn and poison tick on the defender's own turn
            if (!defender.IsAlive) return turn;
            if (!board.IsOnBoard(defender) || !board.IsOnBoard(attacker)) // Knocked apart: put them back next to each other
            {
                board.Remove(defender); board.Remove(attacker);
                board.Place(attacker, 3, 3); board.Place(defender, 4, 3);
            }
            else if (Mathf.Abs(attacker.row - defender.row) + Mathf.Abs(attacker.column - defender.column) != 1)
            {
                board.Remove(defender);
                if (!board.Place(defender, attacker.row + 1, attacker.column)) board.Place(defender, attacker.row - 1, attacker.column);
            }
        }
        return 99;
    }

    // ---- Duels ----

    static void Duels(List<CardData> cards, StringBuilder report)
    {
        Dictionary<CardData, Tally> tally = cards.ToDictionary(c => c, c => new Tally());
        Dictionary<(CardData, CardData), int> beat = new Dictionary<(CardData, CardData), int>(); // (x, y) = times x beat y
        foreach (CardData a in cards)
            foreach (CardData b in cards)
            {
                if (a == b) continue;
                for (int i = 0; i < DuelsPerPair / 2; i++) // Each pair is played both ways round (a as player 1, then b)
                {
                    int winner = Battle(new List<CardData> { a }, new List<CardData> { b }, tally);
                    if (winner == 0) continue;
                    (CardData, CardData) key = winner == 1 ? (a, b) : (b, a);
                    beat[key] = beat.TryGetValue(key, out int w) ? w + 1 : 1;
                }
            }

        report.AppendLine($"== 3. 1v1 DUELS (computer plays both, {DuelsPerPair} per pair, with movement) ==");
        WriteTally(report, tally);
        report.AppendLine("\nDuel win % (row beats column):");
        report.Append($"{"",-10}");
        foreach (CardData d in cards) report.Append($"{Short(d),6}");
        report.AppendLine();
        foreach (CardData a in cards)
        {
            report.Append($"{a.cardName,-10}");
            foreach (CardData b in cards)
            {
                if (a == b) { report.Append($"{"",6}"); continue; }
                int wins = beat.TryGetValue((a, b), out int w) ? w : 0;
                report.Append($"{100 * wins / DuelsPerPair,5}%");
            }
            report.AppendLine();
        }
        report.AppendLine();
    }

    // ---- Teams ----

    static void Teams(List<CardData> cards, int size, int games, StringBuilder report)
    {
        if (cards.Count < size * 2) return;
        Dictionary<CardData, Tally> tally = cards.ToDictionary(c => c, c => new Tally());
        for (int g = 0; g < games; g++)
        {
            List<CardData> shuffled = cards.OrderBy(_ => Random.value).ToList();
            Battle(shuffled.Take(size).ToList(), shuffled.Skip(size).Take(size).ToList(), tally);
        }
        report.AppendLine($"== 4. RANDOM {size}v{size} TEAMS ({games} games, computer places and plays both) ==");
        WriteTally(report, tally);
        report.AppendLine();
    }

    static void WriteTally(StringBuilder report, Dictionary<CardData, Tally> tally)
    {
        report.AppendLine($"{"Card",-10} {"Class",-9} {"Win%",5} {"Games",6} {"Dmg/g",6} {"Heal/g",6} {"Taken/g",7} {"Kills/g",7} {"Alive%",6}");
        foreach (KeyValuePair<CardData, Tally> pair in tally.OrderByDescending(p => p.Value.WinRate))
        {
            Tally t = pair.Value;
            float g = Mathf.Max(1, t.games);
            report.AppendLine($"{pair.Key.cardName,-10} {pair.Key.characterClass,-9} {100 * t.WinRate,4:0}% {t.games,6} {t.damage / g,6:0} {t.healing / g,6:0} {t.damageTaken / g,7:0} {t.kills / g,7:0.00} {100 * t.survived / g,5:0}%");
        }
    }

    // ---- Playing a battle ----

    // Places both teams (computer placement, alternating, player 1 first) and plays until one side is gone.
    // Returns the winner (1 or 2), or 0 for a draw after MaxRounds. Adds every card's stats to the tally
    static int Battle(List<CardData> team1, List<CardData> team2, Dictionary<CardData, Tally> tally)
    {
        BoardState board = new BoardState();
        List<CardInstance> all = new List<CardInstance>();
        Dictionary<CardInstance, float> flips = new Dictionary<CardInstance, float>();
        Dictionary<CardInstance, (long dmg, long heal, long taken, int kills)> stats = new Dictionary<CardInstance, (long, long, long, int)>();

        List<CardInstance>[] unplaced = { team1.Select(d => new CardInstance(d, 1, -1, -1)).ToList(), team2.Select(d => new CardInstance(d, 2, -1, -1)).ToList() };
        foreach (List<CardInstance> side in unplaced) foreach (CardInstance c in side) { all.Add(c); flips[c] = Random.value; stats[c] = (0, 0, 0, 0); }

        int placing = 0;
        while (unplaced[0].Count + unplaced[1].Count > 0)
        {
            if (unplaced[placing].Count == 0) placing = 1 - placing;
            List<CardInstance> side = unplaced[placing];
            CardInstance card = side[ComputerDrafter.ChooseCardToPlace(side.Select(c => c.data).ToList())];
            List<Vector2Int> squares = new List<Vector2Int>();
            for (int r = 0; r < BoardState.Rows; r++)
                for (int col = 0; col < BoardState.Columns; col++)
                    if (BoardState.IsInPlacementZone(r, card.owner) && board.IsEmpty(r, col)) squares.Add(new Vector2Int(r, col));
            Vector2Int square = ComputerDrafter.ChooseSquare(card.data, card.owner, board, squares);
            board.Place(card, square.x, square.y);
            side.Remove(card);
            placing = 1 - placing;
        }

        int winner = 0;
        for (int round = 0; round < MaxRounds && winner == 0; round++)
        {
            HashSet<CardInstance> acted = new HashSet<CardInstance>();
            while (winner == 0)
            {
                CardInstance next = null;
                foreach (CardInstance c in board.GetAllCards())
                    if (c.IsAlive && !acted.Contains(c) && (next == null || TurnOrder.GoesBefore(c, next, flips[c], flips[next]))) next = c;
                if (next == null) break;
                acted.Add(next);
                TakeTurn(next, board, stats);
                foreach (CardInstance c in board.GetAllCards()) if (!c.IsAlive) board.Remove(c);
                winner = TurnManager.WinnerOf(board, true); // Same rule as the game: wiped out, or three cards behind
                if (board.GetCards(1).Count == 0 && board.GetCards(2).Count == 0) break;
            }
        }

        foreach (CardInstance c in all)
        {
            Tally t = tally[c.data];
            t.games++;
            if (winner == c.owner) t.wins++;
            if (winner == 0) t.draws++;
            if (c.IsAlive) t.survived++;
            t.damage += stats[c].dmg; t.healing += stats[c].heal; t.damageTaken += stats[c].taken; t.kills += stats[c].kills;
        }
        return winner;
    }

    static void TakeTurn(CardInstance card, BoardState board, Dictionary<CardInstance, (long dmg, long heal, long taken, int kills)> stats)
    {
        int before = card.currentHP;
        card.StartTurn();
        AddTaken(stats, card, before - card.currentHP);
        if (!card.IsAlive) return;

        if (!card.IsStunned)
        {
            ComputerPlanner.Plan plan = ComputerPlanner.Choose(card, board, Moves(card, board), 0.03f);
            if (plan.moveTo.HasValue) board.MoveCard(card, plan.moveTo.Value.x, plan.moveTo.Value.y);
            if (plan.attackIndex >= 0 && plan.target != null && plan.target.IsAlive)
            {
                AttackData attack = card.GetAttacks()[plan.attackIndex];
                Dictionary<CardInstance, int> hpBefore = board.GetAllCards().ToDictionary(c => c, c => c.currentHP);
                AttackResult result = CombatResolver.ResolveAttack(card, attack, plan.target, board);
                if (result != null)
                    foreach (KeyValuePair<CardInstance, int> pair in hpBefore)
                    {
                        int change = pair.Value - pair.Key.currentHP;
                        if (change > 0)
                        {
                            AddTaken(stats, pair.Key, change);
                            if (pair.Key.owner != card.owner) { var s = stats[card]; s.dmg += change; if (!pair.Key.IsAlive) s.kills++; stats[card] = s; }
                        }
                        else if (change < 0 && pair.Key.owner == card.owner) { var s = stats[card]; s.heal += -change; stats[card] = s; }
                    }
                else card.SkipAttack();
            }
            else card.SkipAttack();
        }

        before = card.currentHP;
        card.EndTurn();
        AddTaken(stats, card, before - card.currentHP);
    }

    static void AddTaken(Dictionary<CardInstance, (long dmg, long heal, long taken, int kills)> stats, CardInstance card, int amount)
    {
        if (amount <= 0) return;
        var s = stats[card]; s.taken += amount; stats[card] = s;
    }

    // Same movement rules as CardGrid: straight lines up to MoveSpaces, diagonals only if the card allows, no passing through cards
    static List<Vector2Int> Moves(CardInstance card, BoardState board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                if (dr != 0 && dc != 0 && !card.data.canMoveDiagonally) continue;
                for (int step = 1; step <= card.MoveSpaces; step++)
                {
                    int r = card.row + dr * step, c = card.column + dc * step;
                    if (!board.IsEmpty(r, c)) break;
                    moves.Add(new Vector2Int(r, c));
                }
            }
        return moves;
    }
}
