using UnityEngine;

// Directions for things drawn flat on the board (tiles, borders, shadows), so they line up with the screen
// whatever angle the camera looks from. Uses CameraRig's resting angle, so its little mouse sway doesn't turn them.
public static class BoardView
{
    // The camera's angle without sway (the main camera's own angle if there's no CameraRig)
    public static Quaternion CameraRotation
    {
        get
        {
            if (CameraRig.HasRestingRotation) return CameraRig.RestingRotation;
            Camera cam = Camera.main;
            return cam != null ? cam.transform.rotation : Quaternion.Euler(90f, 0f, 0f);
        }
    }

    // Along the board, pointing right on screen
    public static Vector3 Right => Flatten(CameraRotation * Vector3.right, Vector3.right);

    // Along the board, pointing up on screen (away from the viewer)
    public static Vector3 Up => Flatten(CameraRotation * Vector3.up, Vector3.forward);

    // For a sprite lying flat on the board that reads upright on screen
    public static Quaternion Flat => Quaternion.LookRotation(Vector3.down, Up);

    // Width and height on screen of something lying flat on the board, from its world size
    public static Vector2 Size(Vector3 world)
    {
        Vector3 right = Right, up = Up;
        return new Vector2(Mathf.Abs(right.x) * world.x + Mathf.Abs(right.z) * world.z,
                           Mathf.Abs(up.x) * world.x + Mathf.Abs(up.z) * world.z);
    }

    static Vector3 Flatten(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : fallback;
    }
}
