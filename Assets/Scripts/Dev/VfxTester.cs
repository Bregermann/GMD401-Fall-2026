using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Dev tool for checking the attack effects without playing a match. In Play mode it shows a panel (top middle):
// click a theme to fire its whole sequence (wind-up, projectile, impact, comic word) from one unit to another,
// or a signature, a status aura, a status tick or the KO. Uses the first two units on the board, or the unit under
// the mouse as the target. Add it from the menu: Stratafield > VFX > Add VFX Tester. Effects only, no damage
public class VfxTester : MonoBehaviour
{
    [SerializeField] private bool showPanel = true;
    [SerializeField] private float intensity = 1f; // 0.6 = small hit, 1.6 = huge hit

    private Vector2 scroll;
    private int tab;
    private readonly string[] tabs = { "Themes", "Signatures", "Auras", "Ticks / KO" };

    void OnGUI()
    {
        if (!showPanel) return;
        GUILayout.BeginArea(new Rect(Screen.width / 2f - 230f, 10f, 460f, 270f), GUI.skin.box);
        GUILayout.Label("<b>VFX Tester</b>  (target = unit under the mouse, or the second unit)", new GUIStyle(GUI.skin.label) { richText = true });
        tab = GUILayout.Toolbar(tab, tabs);
        intensity = GUILayout.HorizontalSlider(intensity, 0.6f, 1.6f);
        scroll = GUILayout.BeginScrollView(scroll);

        if (!FindUnits(out CardUnit from, out CardUnit to))
            GUILayout.Label("Place at least one unit on the board first.");
        else if (tab == 0) Grid(System.Enum.GetNames(typeof(VfxTheme)), 1, i => StartCoroutine(PlayTheme((VfxTheme)i, VfxSignature.None, from, to)));
        else if (tab == 1) Grid(System.Enum.GetNames(typeof(VfxSignature)), 1, i => StartCoroutine(PlayTheme(ThemeFor((VfxSignature)i), (VfxSignature)i, from, to)));
        else if (tab == 2) Grid(System.Enum.GetNames(typeof(ThemedEffects.AuraKind)), 0, i => StartCoroutine(ShowAura((ThemedEffects.AuraKind)i, to)));
        else
        {
            Grid(new[] { "Burn", "Poison", "Regeneration", "Stun" }, 0, i => ThemedEffects.Tick((ThemedEffects.AuraKind)i, to));
            if (GUILayout.Button("KO! (effect only)")) ThemedEffects.Defeat(to);
            if (GUILayout.Button("Hit flash")) UnitTokens.Flash(to, Color.white, 0.4f);
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // Buttons in rows of 4, starting from 'first' (to skip Default / None)
    void Grid(string[] names, int first, System.Action<int> onClick)
    {
        for (int i = first; i < names.Length; i += 4)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + 4, names.Length); j++)
                if (GUILayout.Button(names[j], GUILayout.Width(105f))) onClick(j);
            GUILayout.EndHorizontal();
        }
    }

    // The same order CombatEffects plays a real attack in
    IEnumerator PlayTheme(VfxTheme theme, VfxSignature signature, CardUnit from, CardUnit to)
    {
        const float chargeTime = 0.25f, speed = 9f;
        Vector3 start = ThemedEffects.OnUnit(from, 0.5f), end = ThemedEffects.OnUnit(to, 0.5f);
        float travel = from == to ? 0f : Vector3.Distance(start, end) / speed;
        float impact = chargeTime + travel;

        ThemedEffects.Charge(theme, start, chargeTime);
        if (signature != VfxSignature.None) ThemedEffects.Signature(signature, theme, from, from == to ? null : to, impact, this);
        yield return new WaitForSeconds(chargeTime);

        if (travel > 0f)
        {
            GameObject projectile = ThemedEffects.Projectile(theme, signature, start);
            for (float t = 0f; t < travel; t += Time.deltaTime)
            {
                if (projectile != null) projectile.transform.position = Vector3.Lerp(start, end, t / travel);
                yield return null;
            }
            if (projectile != null) InkFx.Stop(projectile, 1.5f);
        }

        Vector3 direction = end - start;
        direction.y = 0f;
        ThemedEffects.Impact(theme, end, direction, intensity, theme == VfxTheme.Food);
        if (!ThemedEffects.HasOwnWord(signature)) ThemedEffects.Word(theme, ThemedEffects.OnUnit(to, 1.05f), intensity);
        UnitTokens.Flash(to, Color.white, 0.4f);
    }

    IEnumerator ShowAura(ThemedEffects.AuraKind kind, CardUnit unit)
    {
        GameObject aura = ThemedEffects.Aura(kind, unit);
        yield return new WaitForSeconds(3f);
        InkFx.Stop(aura, 1.5f);
    }

    static VfxTheme ThemeFor(VfxSignature signature)
    {
        switch (signature)
        {
            case VfxSignature.Starfall:
            case VfxSignature.LunarBeam: return VfxTheme.Lunar;
            case VfxSignature.GlacialShatter: return VfxTheme.Ice;
            case VfxSignature.BuzzsawSpinner: return VfxTheme.Tech;
            case VfxSignature.RadiantPunishment: return VfxTheme.Light;
            case VfxSignature.HurricaneSlash: return VfxTheme.Wind;
            case VfxSignature.SweepingTide: return VfxTheme.Water;
            case VfxSignature.Roar: return VfxTheme.Rally;
            case VfxSignature.GardeniaBloom: return VfxTheme.Nature;
            case VfxSignature.AllInSlam: return VfxTheme.Blunt;
            default: return VfxTheme.Default;
        }
    }

    // Attacker = the unit taking its turn (or the first on the board); target = the unit under the mouse (or another one)
    static bool FindUnits(out CardUnit from, out CardUnit to)
    {
        from = to = null;
        if (CombatManager.Instance == null) return false;
        List<CardUnit> units = new List<CardUnit>();
        foreach (CardInstance card in CombatManager.Instance.Board.GetAllCards())
        {
            CardUnit unit = CombatManager.Instance.GetUnit(card);
            if (unit != null) units.Add(unit);
        }
        if (units.Count == 0) return false;

        CardUnit attacker = TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != null ? TurnManager.Instance.ActiveUnit : units[0];
        CardUnit hovered = CardHover.UnderMouse();
        from = attacker;
        to = hovered != null && hovered != attacker ? hovered : units.Find(u => u != attacker) ?? attacker;
        return true;
    }
}
