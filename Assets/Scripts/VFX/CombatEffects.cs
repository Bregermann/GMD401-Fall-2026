using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Plays the visual side of combat: projectiles, hit particles, flash and shake, floating numbers,
// status effect visuals and the defeat effect. Put one on the CombatManager object.
// Effects come from the attack's own prefabs first, then the Effect Library, then PlaceholderEffects.
// TurnManager waits while IsPlaying.
public class CombatEffects : MonoBehaviour
{
    public static CombatEffects Instance { get; private set; }
    public static bool IsPlaying => Instance != null && Time.time < Instance.busyUntil;

    [Header("Effects")]
    [SerializeField] private EffectLibrary library; // Effect prefabs for each moment. Empty slots use the placeholders

    [Header("Timing")]
    [SerializeField] private float projectileSpeed = 9f;      // World units per second
    [SerializeField] private float hitDuration = 0.4f;        // Flash and shake length
    [SerializeField] private float textDuration = 1f;         // How long floating numbers last
    [SerializeField] private float chargeTime = 0.25f;        // Wind-up glow before ranged, magic and self attacks
    [SerializeField] private float lungeTime = 0.12f;         // Melee attacker moving in (coming back takes a bit longer)
    [SerializeField] private float lungeDistance = 0.35f;
    [SerializeField] private float knockbackSlideTime = 0.18f;

    [Header("Impact")]
    [SerializeField] private bool screenShake = true;
    [SerializeField] private float screenShakeStrength = 0.15f; // Biggest camera offset, for the biggest hits
    [SerializeField] private bool hitStop = true;
    [SerializeField] private float hitStopTime = 0.08f;         // Longest freeze in real seconds, for the biggest hits
    [SerializeField] private float bigHitDamage = 400f;         // Damage that counts as a full-size hit when scaling effects

    [Header("Look")]
    [SerializeField] private float numberSize = 3f;       // Floating number font size. Raise it if they're too small
    [SerializeField] private float shakeAmount = 0.05f;   // How much a hit card shakes
    [SerializeField] private float effectHeight = 0.3f;   // How far above the card effects spawn
    [SerializeField] private float textRise = 0.6f;       // How far floating text drifts up

    private const float FrozenTimeScale = 0.02f; // Hit-stop slows time to this instead of 0, so the options menu pause (0) still wins

    // Looping visuals a card can have while it has a status effect. Several effects can share one (e.g. both Defense Ups)
    private enum AuraSlot { Burn, Poison, Regeneration, Stun, Stagger, Hindered, AttackUp, AttackDown, DefenseUp, DefenseDown, SpeedUp, SpeedDown, HealBlock }
    private static readonly AuraSlot[] AuraSlots = (AuraSlot[])System.Enum.GetValues(typeof(AuraSlot));

    private float busyUntil;
    private float nextAuraCheck;
    private Camera cam;
    private readonly Dictionary<CardInstance, float> hitTimes = new Dictionary<CardInstance, float>(); // When a projectile reaches each card
    private readonly Dictionary<(CardInstance, AuraSlot), GameObject> auras = new Dictionary<(CardInstance, AuraSlot), GameObject>();
    private MaterialPropertyBlock flashBlock;
    private float trauma;          // Screen shake amount, fades over time
    private Vector3 cameraRest;    // Where the camera sits when it isn't shaking

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        cam = Camera.main;

        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.AttackResolved += OnAttack;
            CombatManager.Instance.CardDefeated += OnDefeated;
        }
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.CardBurned += OnBurned;
            TurnManager.Instance.CardPoisoned += OnPoisoned;
            TurnManager.Instance.CardRegenerated += OnRegenerated;
            TurnManager.Instance.CardStunned += OnStunned;
        }
    }

    void OnDestroy()
    {
        if (Time.timeScale == FrozenTimeScale) Time.timeScale = 1f; // Don't leave the game slowed if destroyed mid hit-stop
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.AttackResolved -= OnAttack;
            CombatManager.Instance.CardDefeated -= OnDefeated;
        }
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.CardBurned -= OnBurned;
            TurnManager.Instance.CardPoisoned -= OnPoisoned;
            TurnManager.Instance.CardRegenerated -= OnRegenerated;
            TurnManager.Instance.CardStunned -= OnStunned;
        }
    }

    void Update()
    {
        if (Time.time < nextAuraCheck || CombatManager.Instance == null) return;
        nextAuraCheck = Time.time + 0.25f;
        UpdateAuras();
    }

    // Screen shake: offsets the camera while there's trauma, then puts it back exactly
    void LateUpdate()
    {
        if (cam == null) return;
        if (trauma <= 0f)
        {
            cameraRest = cam.transform.position; // Follows anything else that moves the camera
            return;
        }

        trauma = Mathf.Max(0f, trauma - Time.unscaledDeltaTime * 2.5f);
        float strength = screenShakeStrength * trauma * trauma; // Squared so small hits barely shake
        float time = Time.unscaledTime * 30f;
        Vector3 offset = cam.transform.right * (Mathf.PerlinNoise(time, 0f) * 2f - 1f) + cam.transform.up * (Mathf.PerlinNoise(0f, time) * 2f - 1f);
        cam.transform.position = cameraRest + offset * strength;
    }

    // Adds screen shake (0-1). Big hits stack up to a full shake
    void AddShake(float amount)
    {
        if (screenShake) trauma = Mathf.Clamp01(trauma + amount);
    }

    // Hit-stop: a split-second slow-down on impact. Skipped if the game is paused or already frozen
    IEnumerator Freeze(float seconds)
    {
        if (!hitStop || seconds <= 0f || Time.timeScale != 1f) yield break;
        Time.timeScale = FrozenTimeScale;
        yield return new WaitForSecondsRealtime(seconds);
        if (Time.timeScale == FrozenTimeScale) Time.timeScale = 1f; // The options menu may have paused meanwhile
    }

    // 0.6 for small hits up to 1.6 for big ones
    float Intensity(int amount) => Mathf.Lerp(0.6f, 1.6f, Mathf.Clamp01(amount / bigHitDamage));

    // ---- Attacks ----

    // Wind-up, then impact, then fade-out. Melee attackers lunge in; ranged, magic and self attacks charge up first
    void OnAttack(CardUnit attacker, AttackData attack, CardUnit target, AttackResult result)
    {
        if (attacker == null || attack == null || result == null) return;
        Color color = PlaceholderEffects.ColorFor(attack.damageType);

        bool onSelf = CombatResolver.UsedOnSelf(attack);
        bool hasTarget = target != null && target != attacker;
        bool ranged = !onSelf && hasTarget && BoardDistance(attacker.Instance, target.Instance) > 1;
        Vector3 toTarget = hasTarget ? Flat(target.transform.position - attacker.transform.position) : cam.transform.up;

        // Wind-up
        float windup;
        if (ranged || onSelf)
        {
            windup = chargeTime;
            PlaceholderEffects.Charge(Above(attacker.transform), attack.damageType, chargeTime);
        }
        else
        {
            windup = lungeTime;
            StartCoroutine(Lunge(attacker.transform, toTarget));
        }

        // Projectile flies after the charge
        float travel = 0f;
        if (ranged)
        {
            Vector3 start = Above(attacker.transform);
            Vector3 end = Above(target.transform);
            travel = Vector3.Distance(start, end) / projectileSpeed;
            StartCoroutine(Projectile(attack, start, end, travel, windup));
        }
        float impact = windup + travel;

        // Area effects at the moment of impact
        if (attack.area == AttackArea.AroundSelf)
            StartCoroutine(Delayed(impact, () => PlaceholderEffects.Pulse(Above(attacker.transform), color, 1.5f + attack.areaSize * 1.2f)));
        if (attack.area == AttackArea.AroundTarget && target != null)
            StartCoroutine(DelayedEffect(Slot(l => l.areaBlast), Above(target.transform), impact));

        // Each card hit. Effects scale with the biggest number in the attack
        int biggest = 0;
        foreach (AttackHit hit in result.hits) biggest = Mathf.Max(biggest, Mathf.Max(hit.damage, hit.healed));
        float k = Intensity(biggest);

        foreach (AttackHit hit in result.hits)
        {
            hitTimes[hit.card] = Time.time + impact;
            bool slides = result.wasKnockedBack && target != null && hit.card == target.Instance;
            Vector3 direction = hit.card == attacker.Instance ? cam.transform.up : Flat(CombatManager.Instance.GetUnit(hit.card).transform.position - attacker.transform.position);
            StartCoroutine(PlayHit(CombatManager.Instance.GetUnit(hit.card), hit, attack, attacker.Instance.owner, color, impact, direction, k, slides));
        }

        // The impact moment: shake and a split-second freeze, both scaled by damage
        int biggestDamage = 0;
        foreach (AttackHit hit in result.hits) biggestDamage = Mathf.Max(biggestDamage, hit.damage);
        if (biggestDamage > 0)
        {
            float power = Mathf.Clamp01(biggestDamage / bigHitDamage);
            StartCoroutine(Delayed(impact, () =>
            {
                AddShake(0.25f + 0.55f * power);
                StartCoroutine(Freeze(hitStopTime * (0.4f + 0.6f * power)));
            }));
        }

        // Knocked-back card slides to its new slot instead of teleporting
        if (result.wasKnockedBack && target != null)
            StartCoroutine(KnockbackSlide(target.transform, CombatManager.Instance.LastKnockbackFrom, impact, toTarget));

        // Knockback into a wall or card
        if (result.hitObstacle && result.collisionDamage > 0)
        {
            StartCoroutine(DelayedText(target, $"-{result.collisionDamage}", PlaceholderEffects.PhysicalColor, 2, impact + 0.2f));
            if (result.collidedWith != null && result.collidedWith.owner != attacker.Instance.owner)
                StartCoroutine(DelayedText(CombatManager.Instance.GetUnit(result.collidedWith), $"-{result.collisionDamage}", PlaceholderEffects.PhysicalColor, 0, impact + 0.2f));
        }

        // Damage the attacker took back (Hangry) and knockback that didn't work (Anchored Stance)
        if (result.reflectedDamage > 0)
        {
            StartCoroutine(DelayedText(attacker, $"-{result.reflectedDamage}", PlaceholderEffects.PhysicalColor, 1, impact + 0.3f));
            StartCoroutine(DelayedShake(attacker.transform, impact + 0.3f));
        }
        if (result.knockbackBlocked)
            StartCoroutine(DelayedText(target, "Anchored", PlaceholderEffects.BuffColor, 2, impact + 0.2f, 0.6f));

        // Effects the attacker gave itself
        if (attack.selfStatusEffects != null)
        {
            int line = 0;
            foreach (StatusEffectApplication effect in attack.selfStatusEffects)
                StartCoroutine(DelayedText(attacker, EffectName(effect.type, attacker.Instance), EffectColor(effect.type), line++, impact, 0.6f));
        }

        busyUntil = Mathf.Max(busyUntil, Time.time + impact + hitDuration);
    }

    // Melee attacker darts toward the target, then eases back to exactly where it started
    IEnumerator Lunge(Transform card, Vector3 direction)
    {
        Vector3 start = card.position;
        Vector3 peak = start + direction.normalized * lungeDistance;

        for (float t = 0f; t < lungeTime; t += Time.deltaTime)
        {
            if (card == null) yield break;
            float p = t / lungeTime;
            card.position = Vector3.Lerp(start, peak, p * p); // Speeds up into the hit
            yield return null;
        }

        float back = lungeTime * 1.3f;
        for (float t = 0f; t < back; t += Time.deltaTime)
        {
            if (card == null) yield break;
            card.position = Vector3.Lerp(peak, start, Mathf.SmoothStep(0f, 1f, t / back));
            yield return null;
        }
        if (card != null) card.position = start;
    }

    // The card has already moved to its new slot. Put it back where it was until the hit lands, then slide it over
    IEnumerator KnockbackSlide(Transform card, Vector3 from, float delay, Vector3 direction)
    {
        if (card == null) yield break;
        Vector3 to = card.position;
        card.position = from;

        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (card == null) yield break;
        PlaceholderEffects.Dust(Above(card) - Vector3.up * effectHeight * 0.8f, direction);

        for (float t = 0f; t < knockbackSlideTime; t += Time.deltaTime)
        {
            if (card == null) yield break;
            float p = 1f - Mathf.Pow(1f - t / knockbackSlideTime, 3f); // Fast, then settles
            card.position = Vector3.Lerp(from, to, p);
            yield return null;
        }
        if (card != null) card.position = to;
    }

    // The attack's own projectile, then the library's for its damage type, then the placeholder
    IEnumerator Projectile(AttackData attack, Vector3 start, Vector3 end, float travel, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        Quaternion facing = Quaternion.LookRotation(end - start);
        EffectEntry entry = Slot(l => attack.damageType == DamageType.Heal ? l.healProjectile
                                    : attack.damageType == DamageType.Magic ? l.magicProjectile : l.physicalProjectile);

        GameObject projectile;
        if (attack.projectilePrefab != null) projectile = Instantiate(attack.projectilePrefab, start, facing);
        else if (entry != null && entry.IsSet) projectile = SpawnEffect(entry, start, facing);
        else projectile = PlaceholderEffects.Projectile(start, attack.damageType);

        for (float t = 0f; t < travel; t += Time.deltaTime)
        {
            if (projectile == null) yield break;
            projectile.transform.position = Vector3.Lerp(start, end, t / travel);
            yield return null;
        }

        // Stop emitting so trails fade out instead of vanishing
        PlaceholderEffects.Stop(projectile);
    }

    // Particles, flash, shake and numbers on one card when the attack lands
    IEnumerator PlayHit(CardUnit unit, AttackHit hit, AttackData attack, int attackerOwner, Color color, float delay,
                        Vector3 direction, float k, bool slides)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (unit == null) yield break;

        // Buffs on yourself or allies (Roar, Hibernate) don't "hit" - just the aura and effect names show
        bool buffOnly = attack.damageType != DamageType.Heal && hit.damage == 0 && hit.card.owner == attackerOwner;

        if (!buffOnly)
        {
            PlayHitEffect(attack, Above(unit.transform), direction, k);
            StartCoroutine(Flash(unit, color));
        }
        if (hit.damage > 0 && !slides) StartCoroutine(Shake(unit.transform)); // A sliding card already shows the hit

        // Numbers sized by how big the hit is, coloured by damage type
        float size = Mathf.Lerp(0.85f, 1.5f, (k - 0.6f));
        int line = 0;
        if (hit.executed) ShowText(unit, "Executed!", PlaceholderEffects.DebuffColor, line++, 1.2f);
        if (hit.damage > 0) ShowText(unit, $"-{hit.damage}", NumberColor(attack.damageType), line++, size);
        if (hit.healed > 0) ShowText(unit, $"+{hit.healed}", PlaceholderEffects.HealColor, line++, size);
        if (hit.bonusTriggered) ShowText(unit, "Bonus!", PlaceholderEffects.BonusColor, line++, 0.7f);

        // Names of the effects it was given, e.g. "Magic Defense Down"
        if (attack.targetStatusEffects != null && hit.card.IsAlive)
            foreach (StatusEffectApplication effect in attack.targetStatusEffects)
                ShowText(unit, EffectName(effect.type, hit.card), EffectColor(effect.type), line++, 0.6f);
    }

    // Damage number colour: warm for physical, cool for magic
    static Color NumberColor(DamageType type)
    {
        return type == DamageType.Magic ? new Color(0.7f, 0.75f, 1f) : new Color(1f, 0.6f, 0.35f);
    }

    // The attack's own hit effect, then the library's for its damage type, then the placeholder
    void PlayHitEffect(AttackData attack, Vector3 position, Vector3 direction, float k)
    {
        if (attack.hitEffectPrefab != null)
            Destroy(Instantiate(attack.hitEffectPrefab, position, Quaternion.identity), 3f);
        else if (attack.damageType == DamageType.Heal)
            PlayOnce(Slot(l => l.healHit), position, () => PlaceholderEffects.HealHit(position, k));
        else if (attack.damageType == DamageType.Magic)
            PlayOnce(Slot(l => l.magicHit), position, () => PlaceholderEffects.MagicHit(position, direction, k));
        else
            PlayOnce(Slot(l => l.physicalHit), position, () => PlaceholderEffects.PhysicalHit(position, direction, k));
    }

    IEnumerator Delayed(float delay, System.Action action)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        action();
    }

    // Flattened onto the board (no up/down)
    static Vector3 Flat(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }
    // ---- Status effects (from TurnManager) ----

    void OnBurned(CardUnit unit, int amount)
    {
        if (unit == null) return;
        Vector3 position = Above(unit.transform);
        PlayOnce(Slot(l => l.burnTick), position, () => PlaceholderEffects.Burn(position, false));
        StatusHit(unit, $"-{amount}", PlaceholderEffects.BurnColor, true);
    }

    void OnPoisoned(CardUnit unit, int amount)
    {
        if (unit == null) return;
        Vector3 position = Above(unit.transform);
        PlayOnce(Slot(l => l.poisonTick), position, () => PlaceholderEffects.Poison(position, false));
        StatusHit(unit, $"-{amount}", PlaceholderEffects.PoisonColor, true);
    }

    void OnRegenerated(CardUnit unit, int amount)
    {
        if (unit == null) return;
        Vector3 position = Above(unit.transform);
        PlayOnce(Slot(l => l.regenerationTick), position, () => PlaceholderEffects.Regeneration(position, false));
        StatusHit(unit, $"+{amount}", PlaceholderEffects.HealColor, false);
    }

    void OnStunned(CardUnit unit)
    {
        if (unit == null) return;
        Vector3 position = Above(unit.transform);
        PlayOnce(Slot(l => l.stunTick), position, () => PlaceholderEffects.Stars(position, false, false));
        StartCoroutine(Flash(unit, PlaceholderEffects.StunColor));
        StartCoroutine(FloatText(unit.transform, "STUNNED", PlaceholderEffects.StunColor, 0, 1.2f, textDuration * 1.5f));
    }

    void StatusHit(CardUnit unit, string text, Color color, bool shake)
    {
        if (unit.Instance != null) hitTimes[unit.Instance] = Time.time;
        StartCoroutine(Flash(unit, color));
        if (shake)
        {
            StartCoroutine(Shake(unit.transform));
            AddShake(0.2f);
        }
        ShowText(unit, text, color, 0);
    }

    // Looping visuals that match each card's current status effects
    void UpdateAuras()
    {
        // Remove ones whose card is gone or lost the effect
        List<(CardInstance, AuraSlot)> finished = new List<(CardInstance, AuraSlot)>();
        foreach (KeyValuePair<(CardInstance, AuraSlot), GameObject> aura in auras)
        {
            CardInstance card = aura.Key.Item1;
            if (aura.Value == null || !card.IsAlive || !CombatManager.Instance.Board.IsOnBoard(card) || !HasAura(card, aura.Key.Item2))
                finished.Add(aura.Key);
        }
        foreach ((CardInstance, AuraSlot) key in finished)
        {
            PlaceholderEffects.Stop(auras[key]);
            auras.Remove(key);
        }

        // Add any that are missing
        foreach (CardInstance card in CombatManager.Instance.Board.GetAllCards())
        {
            CardUnit unit = CombatManager.Instance.GetUnit(card);
            if (unit == null) continue;

            foreach (AuraSlot slot in AuraSlots)
            {
                if (!HasAura(card, slot) || auras.ContainsKey((card, slot))) continue;
                GameObject aura = MakeAura(slot, unit.transform);
                if (aura != null) auras[(card, slot)] = aura;
            }
        }
    }

    bool HasAura(CardInstance card, AuraSlot slot)
    {
        StatusEffectHandler effects = card.statusEffects;
        switch (slot)
        {
            case AuraSlot.Burn:         return effects.Has(StatusEffectType.Burn);
            case AuraSlot.Poison:       return effects.Has(StatusEffectType.Poison);
            case AuraSlot.Regeneration: return effects.Has(StatusEffectType.Regeneration);
            case AuraSlot.Stun:         return effects.Has(StatusEffectType.Stun);
            case AuraSlot.Stagger:      return effects.Has(StatusEffectType.Stagger);
            case AuraSlot.Hindered:     return effects.Has(StatusEffectType.Hindered);
            case AuraSlot.AttackUp:     return effects.Has(StatusEffectType.PhysicalAttackUp) || effects.Has(StatusEffectType.MagicAttackUp);
            case AuraSlot.AttackDown:   return effects.Has(StatusEffectType.PhysicalAttackDown) || effects.Has(StatusEffectType.MagicAttackDown);
            case AuraSlot.DefenseUp:    return effects.Has(StatusEffectType.PhysicalDefenseUp) || effects.Has(StatusEffectType.MagicDefenseUp);
            case AuraSlot.DefenseDown:  return effects.Has(StatusEffectType.PhysicalDefenseDown) || effects.Has(StatusEffectType.MagicDefenseDown);
            case AuraSlot.SpeedUp:      return effects.Has(StatusEffectType.SpeedUp) || effects.Has(StatusEffectType.Haste);
            case AuraSlot.SpeedDown:    return effects.Has(StatusEffectType.SpeedDown);
            case AuraSlot.HealBlock:    return effects.Has(StatusEffectType.HealBlock);
            default: return false;
        }
    }

    // Library aura if there is one, otherwise the placeholder
    GameObject MakeAura(AuraSlot slot, Transform card)
    {
        Vector3 position = card.position + Vector3.up * effectHeight * 0.5f;
        EffectEntry entry = Slot(l => LibraryAura(l, slot));

        GameObject aura;
        if (entry != null && entry.IsSet) aura = SpawnEffect(entry, position);
        else
        {
            switch (slot)
            {
                case AuraSlot.Burn:         aura = PlaceholderEffects.Burn(position, true); break;
                case AuraSlot.Poison:       aura = PlaceholderEffects.Poison(position, true); break;
                case AuraSlot.Regeneration: aura = PlaceholderEffects.Regeneration(position, true); break;
                case AuraSlot.Stun:         aura = PlaceholderEffects.Stars(position, true, false); break;
                case AuraSlot.Stagger:      aura = PlaceholderEffects.Stars(position, true, true); break;
                case AuraSlot.Hindered:     aura = PlaceholderEffects.Hindered(position); break;
                case AuraSlot.AttackUp:     aura = PlaceholderEffects.AttackUp(position); break;
                case AuraSlot.AttackDown:   aura = PlaceholderEffects.AttackDown(position); break;
                case AuraSlot.DefenseUp:    aura = PlaceholderEffects.DefenseUp(position); break;
                case AuraSlot.DefenseDown:  aura = PlaceholderEffects.DefenseDown(position); break;
                case AuraSlot.SpeedUp:      aura = PlaceholderEffects.SpeedUp(position); break;
                case AuraSlot.SpeedDown:    aura = PlaceholderEffects.SpeedDown(position); break;
                case AuraSlot.HealBlock:    aura = PlaceholderEffects.HealBlock(position); break;
                default:                    return null;
            }
        }
        aura.transform.SetParent(card, true); // Follows the card when it moves
        return aura;
    }

    EffectEntry LibraryAura(EffectLibrary l, AuraSlot slot)
    {
        switch (slot)
        {
            case AuraSlot.Burn:         return l.burnAura;
            case AuraSlot.Poison:       return l.poisonAura;
            case AuraSlot.Regeneration: return l.regenerationAura;
            case AuraSlot.Stun:         return l.stunAura;
            case AuraSlot.Stagger:      return l.staggerAura;
            case AuraSlot.Hindered:     return l.hinderedAura;
            case AuraSlot.AttackUp:     return l.attackUpAura;
            case AuraSlot.AttackDown:   return l.attackDownAura;
            case AuraSlot.DefenseUp:    return l.defenseUpAura;
            case AuraSlot.DefenseDown:  return l.defenseDownAura;
            case AuraSlot.SpeedUp:      return l.speedUpAura;
            case AuraSlot.SpeedDown:    return l.speedDownAura;
            default:                    return l.healBlockAura;
        }
    }

    // ---- Defeat ----

    void OnDefeated(CardUnit unit)
    {
        if (unit == null) return;

        // Wait for the hit to land first if a projectile is still on its way
        float delay = 0f;
        if (unit.Instance != null && hitTimes.TryGetValue(unit.Instance, out float hitTime))
            delay = Mathf.Max(0f, hitTime - Time.time) + hitDuration * 0.5f;

        float duration = Mathf.Max(0.1f, CombatManager.Instance.DefeatDelay - delay - 0.05f);
        StartCoroutine(PlayDefeat(unit.transform, delay, duration));
    }

    // Shatter, then the card flips over (showing its back), shrinks and sinks before CombatManager removes it
    IEnumerator PlayDefeat(Transform card, float delay, float duration)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (card == null) yield break;

        Vector3 position = Above(card);
        PlayOnce(Slot(l => l.defeat), position, () => PlaceholderEffects.Shatter(position));
        AddShake(0.4f);

        Vector3 startScale = card.localScale;
        Vector3 startPosition = card.position;
        Quaternion startRotation = card.rotation;
        Vector3 flipAxis = cam.transform.right;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (card == null) yield break;
            float p = t / duration;
            card.rotation = Quaternion.AngleAxis(180f * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, p * 1.6f)), flipAxis) * startRotation;
            card.localScale = startScale * (1f - p * p); // Holds its size a moment, then shrinks fast
            card.position = startPosition - Vector3.up * 0.3f * p;
            yield return null;
        }
        if (card != null) card.localScale = Vector3.zero;
    }

    // ---- Flash, shake and text ----

    // Tints the card, then fades back to its normal colour.
    // Uses a property block so the card's material isn't copied (Unity never cleans those copies up)
    IEnumerator Flash(CardUnit unit, Color color)
    {
        if (flashBlock == null) flashBlock = new MaterialPropertyBlock();

        List<(Renderer, string, Color)> targets = new List<(Renderer, string, Color)>();
        foreach (Renderer renderer in unit.GetComponentsInChildren<Renderer>())
        {
            if (renderer is ParticleSystemRenderer) continue; // Skip aura particles
            Material material = renderer.sharedMaterial;
            if (material == null) continue;

            string property = material.HasProperty("_BaseColor") ? "_BaseColor" : material.HasProperty("_Color") ? "_Color" : null;
            if (property != null) targets.Add((renderer, property, material.GetColor(property)));
        }

        for (float t = 0f; t < hitDuration; t += Time.deltaTime)
        {
            if (unit == null) yield break;
            foreach ((Renderer renderer, string property, Color baseColor) in targets)
                SetColor(renderer, property, Color.Lerp(color, baseColor, t / hitDuration));
            yield return null;
        }

        if (unit == null) yield break;
        foreach ((Renderer renderer, string property, Color baseColor) in targets)
            SetColor(renderer, property, baseColor);
    }

    void SetColor(Renderer renderer, string property, Color color)
    {
        if (renderer == null) return;
        renderer.GetPropertyBlock(flashBlock);
        flashBlock.SetColor(property, color);
        renderer.SetPropertyBlock(flashBlock);
    }

    IEnumerator Shake(Transform card)
    {
        if (card == null) yield break;
        Vector3 start = card.position;

        for (float t = 0f; t < hitDuration; t += Time.deltaTime)
        {
            if (card == null) yield break;
            float strength = shakeAmount * (1f - t / hitDuration);
            card.position = start + (cam.transform.right * Random.Range(-1f, 1f) + cam.transform.up * Random.Range(-1f, 1f)) * strength;
            yield return null;
        }
        if (card != null) card.position = start;
    }

    // line stacks several texts on the same card so they don't overlap
    void ShowText(CardUnit unit, string text, Color color, int line, float sizeScale = 1f)
    {
        if (unit == null) return;
        StartCoroutine(FloatText(unit.transform, text, color, line, sizeScale, textDuration));
    }

    IEnumerator DelayedShake(Transform card, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        yield return Shake(card);
    }

    IEnumerator DelayedText(CardUnit unit, string text, Color color, int line, float delay, float sizeScale = 1f)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        ShowText(unit, text, color, line, sizeScale);
    }

    // Pops in bigger and settles, drifts up the screen with a little sideways wander, then fades out
    IEnumerator FloatText(Transform card, string text, Color color, int line, float sizeScale, float duration)
    {
        Vector3 start = Above(card) + Vector3.up * effectHeight - cam.transform.up * 0.3f * line;
        Vector3 wander = cam.transform.right * Random.Range(-0.15f, 0.15f);
        TextMeshPro tmp = PlaceholderEffects.Text(text, start, color, numberSize * sizeScale);
        const float popTime = 0.15f;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (tmp == null) yield break;
            float progress = t / duration;
            float rise = 1f - (1f - progress) * (1f - progress); // Quick at first, then slows
            tmp.transform.position = start + (cam.transform.up * textRise + wander) * rise;
            tmp.transform.localScale = Vector3.one * (t < popTime ? Mathf.Lerp(1.6f, 1f, t / popTime) : 1f);
            color.a = progress < 0.6f ? 1f : 1f - (progress - 0.6f) / 0.4f;
            tmp.color = color;
            yield return null;
        }
        if (tmp != null) Destroy(tmp.gameObject);
    }

    // ---- Library effects ----

    // A slot from the library, or null if there's no library
    EffectEntry Slot(System.Func<EffectLibrary, EffectEntry> pick) => library != null ? pick(library) : null;

    // Spawns a library effect at its slot's scale. Hierarchy scaling makes every particle system follow that scale
    GameObject SpawnEffect(EffectEntry entry, Vector3 position, Quaternion? rotation = null)
    {
        Quaternion facing = rotation ?? (entry.tiltToScreen ? cam.transform.rotation : entry.prefab.transform.rotation);
        GameObject effect = Instantiate(entry.prefab, position, facing);
        effect.transform.localScale = entry.prefab.transform.localScale * entry.scale;

        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        return effect;
    }

    // Plays a library effect once, or the placeholder if the slot is empty
    void PlayOnce(EffectEntry entry, Vector3 position, System.Action placeholder)
    {
        if (entry != null && entry.IsSet) Destroy(SpawnEffect(entry, position), entry.lifetime);
        else placeholder?.Invoke();
    }

    IEnumerator DelayedEffect(EffectEntry entry, Vector3 position, float delay)
    {
        if (entry == null || !entry.IsSet) yield break;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Destroy(SpawnEffect(entry, position), entry.lifetime);
    }

    // ---- Helpers ----

    Vector3 Above(Transform card) => card.position + Vector3.up * effectHeight;

    int BoardDistance(CardInstance a, CardInstance b)
    {
        if (a == null || b == null) return 0;
        return Mathf.Abs(a.row - b.row) + Mathf.Abs(a.column - b.column);
    }

    // "Magic Defense Down". Stun shows whether it landed or only staggered
    string EffectName(StatusEffectType type, CardInstance card)
    {
        if (type == StatusEffectType.Stun)
            return card != null && card.statusEffects.Has(StatusEffectType.Stun) ? "Stunned!" : "Staggered";
        return CardText.Name(type);
    }

    Color EffectColor(StatusEffectType type)
    {
        if (type == StatusEffectType.Stun || type == StatusEffectType.Stagger) return PlaceholderEffects.StunColor;
        return type.IsBuff() ? PlaceholderEffects.BuffColor : PlaceholderEffects.DebuffColor;
    }
}
