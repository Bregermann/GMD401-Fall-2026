using UnityEditor;
using UnityEngine;

// Card inspector with a helper for the board token portrait: the card art with a gold box showing what the token
// will show, and the cropped result next to it. Click or drag on the art to move the focus
[CustomEditor(typeof(CardData))]
public class CardDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CardData data = (CardData)target;
        Sprite art = data.artwork;
        if (art == null) return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Board Token Portrait Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Click or drag on the art to centre the portrait. Use Portrait Zoom above to get closer.", MessageType.None);

        // Art on the left, the cropped portrait on the right (same shape as this class's crown window)
        Rect texRect = art.textureRect;
        Texture texture = art.texture;
        Rect uv = new Rect(texRect.x / texture.width, texRect.y / texture.height, texRect.width / texture.width, texRect.height / texture.height);
        Rect box = ClassFrames.PortraitBox(data.characterClass);
        float artAspect = texRect.width / texRect.height, portraitAspect = box.width / box.height;

        float height = 260f;
        Rect row = GUILayoutUtility.GetRect(10f, height);
        Rect artArea = new Rect(row.x, row.y, height * artAspect, height);
        Rect portraitArea = new Rect(artArea.xMax + 12f, row.y, height * portraitAspect, height);
        GUI.DrawTextureWithTexCoords(artArea, texture, uv);

        // Gold box over the art where the crop is
        Rect crop = UnitTokens.PortraitCrop(data, artAspect, portraitAspect);
        Rect cropArea = new Rect(artArea.x + crop.x * artArea.width, artArea.y + (1f - crop.yMax) * artArea.height,
                                 crop.width * artArea.width, crop.height * artArea.height);
        Handles.DrawSolidRectangleWithOutline(cropArea, new Color(1f, 0.85f, 0.4f, 0.08f), new Color(1f, 0.85f, 0.4f, 1f));

        // The cropped portrait on its own
        Rect cropUV = new Rect(uv.x + crop.x * uv.width, uv.y + crop.y * uv.height, crop.width * uv.width, crop.height * uv.height);
        GUI.DrawTextureWithTexCoords(portraitArea, texture, cropUV);
        Handles.DrawSolidRectangleWithOutline(portraitArea, Color.clear, ClassFrames.ColourOf(data.characterClass));

        // Click or drag on the art to set the focus
        Event e = Event.current;
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && artArea.Contains(e.mousePosition))
        {
            Undo.RecordObject(data, "Move Portrait Focus");
            data.portraitFocus = new Vector2((e.mousePosition.x - artArea.x) / artArea.width,
                                             1f - (e.mousePosition.y - artArea.y) / artArea.height);
            EditorUtility.SetDirty(data);
            e.Use();
        }
    }
}
