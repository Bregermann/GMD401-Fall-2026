using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// Plays the visual side of combat: projectiles, hit particles, flash and shake, floating numbers,
// status effect visuals and the defeat effect. Put one on the CombatManager object.
// Uses PlaceholderEffects unless an attack has its own effect prefabs. TurnManager waits while IsPlaying.
public class CombatEffects : MonoBehaviour
{
    public static CombatEffects Instance { get; private set; }
    public static bool IsPlaying => Instance != null && Time.time < Instance.busyUntil;

    [Header("Timing")]
    [SerializeField] private float projectileSpeed = 8f;  // World units per second
    [SerializeField] private float hitDuration = 0.4f;    // Flash and shake length
    [SerializeField] private float textDuration = 1f;     // How long floating numbers last

    [Header("Look")]
    [SerializeField] private float numberSize = 3f;       // Floating number font size. Raise it if they're too small
    [SerializeField] private float shakeAmount = 0.05f;
    [SerializeField] private float effectHeight = 0.3f;   // How far above the card effects spawn
    [SerializeField] private float textRise = 0.6f;       // How far floating text drifts up

    // Effects that get a looping visual on the card while it has them
    private static readonly StatusEffectType[] AuraEffects =
    {
        StatusEffectType.Burn, StatusEffectType.Poison, StatusEffectType.Regeneration,
        StatusEffectType.Stagger, StatusEffectType.Stun
    };

    private float busyUntil;
    private float nextAuraCheck;
    private Camera cam;
    private readonly Dictionary<CardInstance, float> hitTimes = new Dictionary<CardInstance, float>(); // When a projectile reaches each card
    private readonly Dictionary<(CardInstance, StatusEffectType), GameObject> auras = new Dictionary<(CardInstance, StatusEffectType), GameObject>();
    private MaterialPropertyBlock flashBlock;

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

    // ---- Attacks ----

    void OnAttack(CardUnit attacker, AttackData attack, CardUnit target, AttackResult result)
    {
        if (attacker == null || attack == null || result == null) return;
        Color color = PlaceholderEffects.ColorFor(attack.damageType);

        // Projectile when the attacker isn't right next to the target
        float travel = 0f;
        bool aroundSelf = attack.area == AttackArea.AroundSelf;
        if (!aroundSelf && target != null && target != attacker && BoardDistance(attacker.Instance, target.Instance) > 1)
        {
            Vector3 start = Above(attacker.transform);
            Vector3 end = Above(target.transform);
            travel = Vector3.Distance(start, end) / projectileSpeed;
            StartCoroutine(Projectile(attack, start, end, color, travel));
        }

        // Pulse out from the user for attacks centred on itself
        if (aroundSelf) PlaceholderEffects.Pulse(Above(attacker.transform), color, 1.5f + attack.areaSize * 1.2f);

        foreach (AttackHit hit in result.hits)
        {
            hitTimes[hit.card] = Time.time + travel;
            StartCoroutine(PlayHit(CombatManager.Instance.GetUnit(hit.card), hit, attack, color, travel));
        }

        // Knockback into a wall or card
        if (result.hitObstacle && result.collisionDamage > 0)
        {
            StartCoroutine(DelayedText(target, $"-{result.collisionDamage}", PlaceholderEffects.PhysicalColor, 1, travel + 0.2f));
            if (result.collidedWith != null && result.collidedWith.owner != attacker.Instance.owner)
                StartCoroutine(DelayedText(CombatManager.Instance.GetUnit(result.collidedWith), $"-{result.collisionDamage}", PlaceholderEffects.PhysicalColor, 0, travel + 0.2f));
        }

        // Effects the attacker gave itself
        if (attack.selfStatusEffects != null)
        {
            int line = 0;
            foreach (StatusEffectApplication effect in attack.selfStatusEffects)
                StartCoroutine(DelayedText(attacker, EffectName(effect.type, attacker.Instance), EffectColor(effect.type), line++, travel, 0.6f));
        }

        busyUntil = Mathf.Max(busyUntil, Time.time + travel + hitDuration);
    }

    IEnumerator Projectile(AttackData attack, Vector3 start, Vector3 end, Color color, float travel)
    {
        GameObject custom = attack.projectilePrefab != null ? Instantiate(attack.projectilePrefab, start, Quaternion.LookRotation(end - start)) : null;
        GameObject placeholder = custom == null ? PlaceholderEffects.Projectile(start, attack.damageType) : null;
        Transform mover = custom != null ? custom.transform : placeholder.transform;

        for (float t = 0f; t < travel; t += Time.deltaTime)
        {
            if (mover == null) yield break;
            mover.position = Vector3.Lerp(start, end, t / travel);
            yield return null;
        }

        if (custom != null) Destroy(custom);
        else PlaceholderEffects.Stop(placeholder);
    }

    // Particles, flash, shake and numbers on one card, once the projectile arrives
    IEnumerator PlayHit(CardUnit unit, AttackHit hit, AttackData attack, Color color, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (unit == null) yield break;

        Vector3 position = Above(unit.transform);
        if (attack.hitEffectPrefab != null)
            Destroy(Instantiate(attack.hitEffectPrefab, position, Quaternion.identity), 3f);
        else if (attack.damageType == DamageType.Heal)
            PlaceholderEffects.HealHit(position);
        else if (attack.damageType == DamageType.Magic)
            PlaceholderEffects.MagicHit(position);
        else
            PlaceholderEffects.PhysicalHit(position);

        StartCoroutine(Flash(unit, color));
        if (hit.damage > 0) StartCoroutine(Shake(unit.transform));

        int line = 0;
        if (hit.damage > 0) ShowText(unit, $"-{hit.damage}", PlaceholderEffects.PhysicalColor, line++);
        if (hit.healed > 0) ShowText(unit, $"+{hit.healed}", PlaceholderEffects.HealColor, line++);

        // Names of the effects it was given, e.g. "Magic Defense Down"
        if (attack.targetStatusEffects != null && hit.card.IsAlive)
            foreach (StatusEffectApplication effect in attack.targetStatusEffects)
                ShowText(unit, EffectName(effect.type, hit.card), EffectColor(effect.type), line++, 0.6f);
    }

    // ---- Status effects (from TurnManager) ----

    void OnBurned(CardUnit unit, int amount)
    {
        if (unit == null) return;
        PlaceholderEffects.Burn(Above(unit.transform), false);
        StatusHit(unit, $"-{amount}", PlaceholderEffects.BurnColor, true);
    }

    void OnPoisoned(CardUnit unit, int amount)
    {
        if (unit == null) return;
        PlaceholderEffects.Poison(Above(unit.transform), false);
        StatusHit(unit, $"-{amount}", PlaceholderEffects.PoisonColor, true);
    }

    void OnRegenerated(CardUnit unit, int amount)
    {
        if (unit == null) return;
        PlaceholderEffects.Regeneration(Above(unit.transform), false);
        StatusHit(unit, $"+{amount}", PlaceholderEffects.HealColor, false);
    }

    void OnStunned(CardUnit unit)
    {
        if (unit == null) return;
        PlaceholderEffects.Stars(Above(unit.transform), false, false);
        StartCoroutine(Flash(unit, PlaceholderEffects.StunColor));
        StartCoroutine(FloatText(unit.transform, "STUNNED", PlaceholderEffects.StunColor, 0, 1.2f, textDuration * 1.5f));
    }

    void StatusHit(CardUnit unit, string text, Color color, bool shake)
    {
        if (unit.Instance != null) hitTimes[unit.Instance] = Time.time;
        StartCoroutine(Flash(unit, color));
        if (shake) StartCoroutine(Shake(unit.transform));
        ShowText(unit, text, color, 0);
    }

    // Looping visuals that match each card's current status effects
    void UpdateAuras()
    {
        // Remove ones whose card is gone or lost the effect
        List<(CardInstance, StatusEffectType)> finished = new List<(CardInstance, StatusEffectType)>();
        foreach (KeyValuePair<(CardInstance, StatusEffectType), GameObject> aura in auras)
        {
            CardInstance card = aura.Key.Item1;
            if (aura.Value == null || !card.IsAlive || !CombatManager.Instance.Board.IsOnBoard(card) || !card.statusEffects.Has(aura.Key.Item2))
                finished.Add(aura.Key);
        }
        foreach ((CardInstance, StatusEffectType) key in finished)
        {
            PlaceholderEffects.Stop(auras[key]);
            auras.Remove(key);
        }

        // Add any that are missing
        foreach (CardInstance card in CombatManager.Instance.Board.GetAllCards())
        {
            CardUnit unit = CombatManager.Instance.GetUnit(card);
            if (unit == null) continue;

            foreach (StatusEffectType type in AuraEffects)
            {
                if (card.statusEffects.Has(type) && !auras.ContainsKey((card, type)))
                    auras[(card, type)] = MakeAura(type, unit.transform);
            }
        }
    }

    GameObject MakeAura(StatusEffectType type, Transform card)
    {
        Vector3 position = card.position + Vector3.up * effectHeight * 0.5f;
        GameObject aura;
        switch (type)
        {
            case StatusEffectType.Burn:         aura = PlaceholderEffects.Burn(position, true); break;
            case StatusEffectType.Poison:       aura = PlaceholderEffects.Poison(position, true); break;
            case StatusEffectType.Regeneration: aura = PlaceholderEffects.Regeneration(position, true); break;
            case StatusEffectType.Stun:         aura = PlaceholderEffects.Stars(position, true, false); break;
            default:                            aura = PlaceholderEffects.Stars(position, true, true); break; // Stagger, faint
        }
        aura.transform.SetParent(card, true); // Follows the card when it moves
        return aura;
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

    // Shatter particles, then the card spins and shrinks away before CombatManager removes it
    IEnumerator PlayDefeat(Transform card, float delay, float duration)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (card == null) yield break;

        PlaceholderEffects.Shatter(Above(card));
        Vector3 startScale = card.localScale;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (card == null) yield break;
            card.localScale = startScale * (1f - t / duration);
            card.Rotate(Vector3.up, 360f * Time.deltaTime, Space.World);
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

    IEnumerator DelayedText(CardUnit unit, string text, Color color, int line, float delay, float sizeScale = 1f)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        ShowText(unit, text, color, line, sizeScale);
    }

    // Drifts up the screen and fades out
    IEnumerator FloatText(Transform card, string text, Color color, int line, float sizeScale, float duration)
    {
        Vector3 start = Above(card) + Vector3.up * effectHeight - cam.transform.up * 0.3f * line;
        TextMeshPro tmp = PlaceholderEffects.Text(text, start, color, numberSize * sizeScale);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (tmp == null) yield break;
            float progress = t / duration;
            tmp.transform.position = start + cam.transform.up * textRise * progress;
            color.a = progress < 0.6f ? 1f : 1f - (progress - 0.6f) / 0.4f;
            tmp.color = color;
            yield return null;
        }
        if (tmp != null) Destroy(tmp.gameObject);
    }

    // ---- Helpers ----

    Vector3 Above(Transform card) => card.position + Vector3.up * effectHeight;

    int BoardDistance(CardInstance a, CardInstance b)
    {
        if (a == null || b == null) return 0;
        return Mathf.Abs(a.row - b.row) + Mathf.Abs(a.column - b.column);
    }

    // "MagicDefenseDown" -> "Magic Defense Down". Stun shows whether it landed or only staggered
    string EffectName(StatusEffectType type, CardInstance card)
    {
        if (type == StatusEffectType.Stun)
            return card != null && card.statusEffects.Has(StatusEffectType.Stun) ? "Stunned!" : "Staggered";

        string name = type.ToString();
        StringBuilder spaced = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i])) spaced.Append(' ');
            spaced.Append(name[i]);
        }
        return spaced.ToString();
    }

    Color EffectColor(StatusEffectType type)
    {
        if (type == StatusEffectType.Stun || type == StatusEffectType.Stagger) return PlaceholderEffects.StunColor;
        return type.IsBuff() ? PlaceholderEffects.BuffColor : PlaceholderEffects.DebuffColor;
    }
}
