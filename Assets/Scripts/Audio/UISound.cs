using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Hover and click sounds for every button, toggle, slider and dropdown in every scene (menus, settings, HUD).
// Added automatically: a hidden helper looks for new UI once a second, so no scene needs editing.
// Put a UISound on something by hand and untick playSounds to keep it quiet
public class UISound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public bool playSounds = true;
    public string clickCue = "ui.click"; // e.g. "ui.confirm" for big buttons

    private Selectable selectable;

    void Awake() => selectable = GetComponent<Selectable>();

    bool Usable => playSounds && (selectable == null || selectable.IsInteractable());

    public void OnPointerEnter(PointerEventData eventData) { if (Usable) GameSfx.Play("ui.hover"); }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Usable && !(selectable is Slider)) GameSfx.Play(clickCue); // Sliders tick while dragging instead
    }

    // ---- Auto-adding ----

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        GameObject helper = new GameObject("UI Sound Scanner") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(helper);
        helper.AddComponent<Scanner>();
    }

    class Scanner : MonoBehaviour
    {
        float next;

        void OnEnable() => SceneManager.sceneLoaded += OnLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnLoaded;
        void OnLoaded(Scene scene, LoadSceneMode mode) => next = 0f;

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            foreach (Selectable selectable in FindObjectsByType<Selectable>(FindObjectsInactive.Include))
            {
                if (selectable.GetComponent<UISound>() != null) continue;
                selectable.gameObject.AddComponent<UISound>();
                if (selectable is Slider slider) slider.onValueChanged.AddListener(_ => GameSfx.Play("ui.slider"));
            }
        }
    }
}
