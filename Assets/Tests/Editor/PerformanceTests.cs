using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

// Times the slow one-off jobs that run while the game is playing (making token frames and other textures).
// WebGL runs everything on one thread, so a slow job freezes the game for that long. Results print to the
// test log; the budgets are generous (WebGL is slower than the editor) and only catch big regressions
public class PerformanceTests
{
    const float FramePixels = 1.4f; // Same as UnitTokens

    static float Time(System.Action job)
    {
        Stopwatch watch = Stopwatch.StartNew();
        job();
        return (float)watch.Elapsed.TotalMilliseconds;
    }

    [Test]
    public void TokenFrame_SecondCardOfAClassIsFastAndUnderBudget()
    {
        foreach (CharacterClass characterClass in System.Enum.GetValues(typeof(CharacterClass)))
        {
            Texture2D first = null, second = null;
            float cold = Time(() => first = ClassFrames.PaintFrame(characterClass, InkArt.Player1, p => Color.gray, FramePixels));
            float warm = Time(() => second = ClassFrames.PaintFrame(characterClass, InkArt.Player2, p => Color.gray, FramePixels));
            UnityEngine.Debug.Log($"[Perf] {characterClass} frame: first {cold:0} ms (works out the shape), next {warm:0} ms");
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);

            Assert.Less(warm, 250f, $"{characterClass}: a token frame took {warm:0} ms");
            Assert.IsTrue(ClassFrames.IsReady(characterClass, FramePixels));
        }
    }

    [Test]
    public void ClassShapes_UnderBudget()
    {
        foreach (CharacterClass characterClass in System.Enum.GetValues(typeof(CharacterClass)))
        {
            Texture2D silhouette = null, cracks = null;
            float ms = Time(() =>
            {
                silhouette = ClassFrames.PaintSilhouette(characterClass, FramePixels);
                cracks = ClassFrames.PaintCracks(characterClass, FramePixels);
            });
            UnityEngine.Debug.Log($"[Perf] {characterClass} flash + cracks: {ms:0} ms");
            Object.DestroyImmediate(silhouette);
            Object.DestroyImmediate(cracks);
            Assert.Less(ms, 800f);
        }
    }

    [Test]
    public void InkPainter_256Texture_UnderBudget()
    {
        Texture2D texture = null;
        float ms = Time(() => texture = InkArt.Paint(256, 256, p =>
            InkArt.Outlined(InkArt.Chamfer(p, new Vector2(128f, 128f), new Vector2(120f, 120f), 26f, 7f), 5f, InkArt.Navy)));
        UnityEngine.Debug.Log($"[Perf] 256x256 ink panel: {ms:0} ms");
        Object.DestroyImmediate(texture);
        Assert.Less(ms, 400f);
    }
}
