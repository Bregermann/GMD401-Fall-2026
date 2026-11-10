// Who acts first: highest current speed, then class priority (TurnPriority), then a coin flip.
// Speed is checked live, so SpeedUp/SpeedDown reshuffle the order straight away.
public static class TurnOrder
{
    // True if a should act before b. The flips break full ties, higher goes first
    public static bool GoesBefore(CardInstance a, CardInstance b, float aFlip, float bFlip)
    {
        if (a.Speed != b.Speed) return a.Speed > b.Speed;

        int aPriority = a.data.characterClass.TurnPriority();
        int bPriority = b.data.characterClass.TurnPriority();
        if (aPriority != bPriority) return aPriority < bPriority;

        return aFlip > bFlip;
    }
}
