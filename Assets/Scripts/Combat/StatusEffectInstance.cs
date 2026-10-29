// One active status effect on a card, how strong it is, and how many of that card's turns it has left.
public class StatusEffectInstance
{
    public StatusEffectType type;
    public int turnsRemaining;
    public int amount; // e.g. 50 for -50 magic defense, or Burn damage. 0 for effects with no amount (Stun)

    public StatusEffectInstance(StatusEffectType type, int turnsRemaining, int amount)
    {
        this.type = type;
        this.turnsRemaining = turnsRemaining;
        this.amount = amount;
    }
}
