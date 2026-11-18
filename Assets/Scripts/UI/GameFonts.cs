using TMPro;
using UnityEngine;

// Fonts shared by the game, all in Assets/Resources/Fonts. Screen UI is sleek sci-fi like the logo and the UI art;
// things on the board are chunky cartoon like the units:
// - Title: Audiowide, wide squared letters close to the STRATAFIELD logo. Screen UI headings, banners, buttons. Free (OFL), see Audiowide_License.txt
// - Body: Varela Round, a rounded font that stays readable when small. All small and long text. Free (OFL), see VarelaRound_License.txt
// - Display: Lilita One, chunky cartoon letters for the board (unit names, stat gems, damage numbers, comic words). Free (OFL), see LilitaOne_License.txt
// - Heading: Gunship, no longer used (non-commercial licence, see Gunship_License.txt). PixelifySans.ttf isn't used either
// Also works in rich text: <font="Audiowide">TITLE</font>, <font="LilitaOne">BONK!</font> or <font="VarelaRound">text</font>
public static class GameFonts
{
    public const float SmallestHeading = 22f; // UseLabel: labels this size and up get the Title font, smaller ones Body

    private static TMP_FontAsset heading, body, display, title;
    private static bool headingLoaded, bodyLoaded, displayLoaded, titleLoaded;

    // Null if the font file is missing, so callers keep the default font
    public static TMP_FontAsset Heading => Load("Gunship", ref heading, ref headingLoaded);
    public static TMP_FontAsset Body => Load("VarelaRound", ref body, ref bodyLoaded);
    // Lilita One: chunky cartoon letters for unit names, stat gems and damage numbers (inked style). Free (OFL)
    public static TMP_FontAsset Display => Load("LilitaOne", ref display, ref displayLoaded);
    // Audiowide: the logo-style font for screen UI headings. Free (OFL)
    public static TMP_FontAsset Title => Load("Audiowide", ref title, ref titleLoaded);

    // Inked look: Display font, white with a thick dark outline. outline is in TMP units (0-1)
    public static void UseInked(TMP_Text text, float outline = 0.3f)
    {
        if (text == null) return;
        if (Display != null) text.font = Display;
        text.fontStyle &= ~FontStyles.Bold;
        text.outlineWidth = outline;
        text.outlineColor = InkArt.Ink;
    }

    // Screen UI headings: Title font with an ink outline
    public static void UseTitle(TMP_Text text, float outline = 0.2f)
    {
        if (text == null) return;
        if (Title != null) text.font = Title;
        text.fontStyle &= ~FontStyles.Bold;
        text.outlineWidth = outline;
        text.outlineColor = InkArt.Ink;
    }

    // For screen UI labels: big ones use the Title font, small ones use Body
    public static void UseLabel(TMP_Text text)
    {
        if (text == null) return;
        if (text.fontSize < SmallestHeading) UseBody(text);
        else UseTitle(text, 0.2f);
    }

    public static void UseHeading(TMP_Text text)
    {
        if (text == null || Heading == null) return;
        text.font = Heading;
        text.fontStyle &= ~FontStyles.Bold; // Already heavy, fake bold would blur it
    }

    public static void UseBody(TMP_Text text)
    {
        if (text == null || Body == null) return;
        text.font = Body;
        text.fontStyle &= ~FontStyles.Bold; // Fake bold smudges the pixels
    }

    static TMP_FontAsset Load(string file, ref TMP_FontAsset asset, ref bool loaded)
    {
        if (loaded) return asset;
        loaded = true;

        Font font = Resources.Load<Font>("Fonts/" + file);
        if (font == null)
        {
            Debug.LogWarning($"GameFonts: Resources/Fonts/{file}.ttf not found, using the default font");
            return null;
        }

        // Built while the game runs, so no font asset has to be made in the editor
        asset = TMP_FontAsset.CreateFontAsset(font);
        if (asset == null) return null;
        asset.name = file;
        asset.hashCode = TMP_TextUtilities.GetHashCode(file);
        // TMP works this out before the font has a name, so every runtime font gets the same one. Two fonts then clash
        asset.materialHashCode = TMP_TextUtilities.GetSimpleHashCode(file + " Atlas Material");

        // Lets <font="..."> find it. Only rich text needs this, so a problem here mustn't stop the UI being built
        try { MaterialReferenceManager.AddFontAsset(asset); }
        catch (System.Exception e) { Debug.LogWarning($"GameFonts: couldn't register {file} for <font> tags: {e.Message}"); }
        return asset;
    }
}
