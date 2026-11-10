using UnityEngine;

// Which effect prefab plays for each combat moment. Give CombatEffects one of these.
// Any slot left empty uses the built-in placeholder from PlaceholderEffects.
// An attack's own Hit Effect / Projectile prefab still wins over these.
[CreateAssetMenu(fileName = "EffectLibrary", menuName = "Stratafield/Effect Library")]
public class EffectLibrary : ScriptableObject
{
    [Header("Hits")]
    public EffectEntry physicalHit;
    public EffectEntry magicHit;
    public EffectEntry healHit;
    public EffectEntry areaBlast;          // Extra effect at the centre of AroundTarget attacks

    [Header("Projectiles")]
    public EffectEntry physicalProjectile;
    public EffectEntry magicProjectile;
    public EffectEntry healProjectile;

    [Header("Status Ticks")]
    public EffectEntry burnTick;
    public EffectEntry poisonTick;
    public EffectEntry regenerationTick;
    public EffectEntry stunTick;

    [Header("Status Auras (loop while the card has the effect)")]
    public EffectEntry burnAura;
    public EffectEntry poisonAura;
    public EffectEntry regenerationAura;
    public EffectEntry stunAura;
    public EffectEntry staggerAura;
    public EffectEntry hinderedAura;
    public EffectEntry attackUpAura;       // Physical or Magic Attack Up
    public EffectEntry attackDownAura;     // Physical or Magic Attack Down
    public EffectEntry defenseUpAura;      // Physical or Magic Defense Up
    public EffectEntry defenseDownAura;    // Physical or Magic Defense Down
    public EffectEntry speedUpAura;        // Speed Up or Haste
    public EffectEntry speedDownAura;
    public EffectEntry healBlockAura;

    [Header("Other")]
    public EffectEntry defeat;
}

// One effect slot. Asset store effects are usually character-sized, cards are about 1 unit, hence the scale
[System.Serializable]
public class EffectEntry
{
    public GameObject prefab;      // Empty = use the placeholder
    public float scale = 0.1f;     // Size multiplier on the prefab
    public bool tiltToScreen;      // Turns the effect's "up" to point up the screen, for effects made to be seen from the side
    public float lifetime = 2f;    // One-off effects are removed after this. Ignored for auras and projectiles

    public bool IsSet => prefab != null;
}
