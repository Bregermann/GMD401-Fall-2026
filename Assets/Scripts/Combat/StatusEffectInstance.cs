// One active status effect on a card and how many of that card's turns it has left.
public class StatusEffectInstance
{
    public StatusEffectType type;
    public int turnsRemaining;

    public StatusEffectInstance(StatusEffectType type, int turnsRemaining)
    {
        this.type = type;
        this.turnsRemaining = turnsRemaining;
    }
}
