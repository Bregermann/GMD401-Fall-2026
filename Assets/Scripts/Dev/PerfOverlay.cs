using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

// Press F3 in any scene (editor, desktop or WebGL build) for a small performance readout:
// FPS, average and worst frame time over the last 2 seconds, memory, garbage made per frame, draw calls,
// and the slowest one-off jobs logged with PerfLog (token frames, board textures...). Starts hidden.
// Draw calls and per-frame garbage only show in the editor and Development Builds
public class PerfOverlay : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        GameObject helper = new GameObject("Perf Overlay") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(helper);
        helper.AddComponent<PerfOverlay>();
    }

    private bool visible;
    private readonly Queue<(float time, float ms)> frames = new Queue<(float, float)>();
    private ProfilerRecorder gcAlloc, drawCalls, totalMemory;
    private readonly StringBuilder text = new StringBuilder(512);
    private float nextText;
    private string shown = "";
    private GUIStyle style;

    void OnEnable()
    {
        gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        totalMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
    }

    void OnDisable()
    {
        gcAlloc.Dispose();
        drawCalls.Dispose();
        totalMemory.Dispose();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) visible = !visible;

        float now = Time.realtimeSinceStartup;
        frames.Enqueue((now, Time.unscaledDeltaTime * 1000f));
        while (frames.Count > 0 && frames.Peek().time < now - 2f) frames.Dequeue();

        if (!visible || now < nextText) return;
        nextText = now + 0.25f;

        float sum = 0f, worst = 0f;
        foreach ((float _, float ms) in frames) { sum += ms; worst = Mathf.Max(worst, ms); }
        float average = frames.Count > 0 ? sum / frames.Count : 0f;

        text.Clear();
        text.Append("FPS ").Append(average > 0f ? (1000f / average).ToString("0") : "-")
            .Append("   avg ").Append(average.ToString("0.0")).Append(" ms   worst ").Append(worst.ToString("0.0")).Append(" ms\n");
        if (totalMemory.Valid) text.Append("Memory ").Append((totalMemory.LastValue / (1024f * 1024f)).ToString("0")).Append(" MB   ");
        if (gcAlloc.Valid) text.Append("Garbage/frame ").Append((gcAlloc.LastValue / 1024f).ToString("0.0")).Append(" KB   ");
        if (drawCalls.Valid) text.Append("Draw calls ").Append(drawCalls.LastValue);
        text.Append('\n');
        foreach ((string label, float ms) in PerfLog.Slowest(4))
            text.Append(label).Append(": ").Append(ms.ToString("0")).Append(" ms\n");
        shown = text.ToString();
    }

    void OnGUI()
    {
        if (!visible) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = false };
            style.normal.textColor = Color.white;
        }
        GUI.Box(new Rect(10, 10, 430, 110), shown, style);
    }
}

// Times one-off jobs (making textures etc.) so the overlay and the console can show the slow ones.
// using (PerfLog.Time("Token frame Arcturus")) { ... }
public static class PerfLog
{
    static readonly List<(string label, float ms)> entries = new List<(string, float)>();

    public static Scope Time(string label) => new Scope(label);

    public static void Add(string label, float ms)
    {
        entries.Add((label, ms));
        if (entries.Count > 200) entries.RemoveAt(0);
        if (ms > 30f && (Application.isEditor || Debug.isDebugBuild)) Debug.Log($"[Perf] {label}: {ms:0} ms"); // Editor and Development Builds only
    }

    public static IEnumerable<(string, float)> Slowest(int count)
    {
        List<(string label, float ms)> sorted = new List<(string, float)>(entries);
        sorted.Sort((a, b) => b.ms.CompareTo(a.ms));
        for (int i = 0; i < Mathf.Min(count, sorted.Count); i++) yield return sorted[i];
    }

    public readonly struct Scope : System.IDisposable
    {
        readonly string label;
        readonly long start;
        public Scope(string label) { this.label = label; start = System.Diagnostics.Stopwatch.GetTimestamp(); }
        public void Dispose() => Add(label, (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000f / System.Diagnostics.Stopwatch.Frequency);
    }
}
