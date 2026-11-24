using UnityEngine;
using UnityEngine.EventSystems;

// One place to check whether gameplay should ignore the mouse and keyboard right now
public static class InputGate
{
    // The options menu pauses the game by setting timeScale to 0. The counter wheel popup also holds input while it's open
    public static bool IsPaused => Time.timeScale == 0f || CounterWheel.IsOpen || BlackHoleTransition.IsPlaying;

    // Mouse is over UI (menu buttons, game log, win panel), so clicks shouldn't reach the cards underneath
    public static bool PointerOverUI => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
}
