using UnityEngine;
using UnityEngine.InputSystem;

// Desktop scale/walkthrough companion scene; independent of the PICO tracking rig.
[RequireComponent(typeof(CharacterController))]
public sealed class ForestPreviewWalker : MonoBehaviour
{
    public Transform view;
    public ForestPreviewStation[] stations;
    public Vector3[] route;
    public bool scriptedReview;
    public float speed = 2.2f;
    float verticalSpeed, pitch;
    CharacterController body;
    Vector3 spawn;
    Quaternion heading;
    public Vector3 SpawnPosition => spawn;
    public CharacterController Body => body ? body : (body = GetComponent<CharacterController>());
    void Awake() { spawn = transform.position; heading = transform.rotation; }
    void Update()
    {
        if (scriptedReview) return;
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            var delta = mouse.delta.ReadValue();
            transform.Rotate(0, delta.x * .12f, 0);
            pitch = Mathf.Clamp(pitch - delta.y * .12f, -70, 70);
            view.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
        Vector2 move = Vector2.zero;
        if (keyboard != null)
        {
            move.x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            move.y = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            if (keyboard.eKey.wasPressedThisFrame) Interact();
            if (keyboard.rKey.wasPressedThisFrame) ResetWalk();
        }
        MoveWorld(transform.TransformDirection(new Vector3(move.x, 0, move.y)), Time.deltaTime);
    }
    public void MoveWorld(Vector3 direction, float dt)
    {
        dt = Mathf.Clamp(dt, 0, .05f);
        direction.y = 0;
        if (Body.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
        verticalSpeed = Mathf.Max(-20, verticalSpeed - 9.81f * dt);
        Body.Move((Vector3.ClampMagnitude(direction, 1) * speed + Vector3.up * verticalSpeed) * dt);
        if (transform.position.y < -5) ResetWalk();
    }
    public bool Interact()
    {
        ForestPreviewStation nearest = null;
        float distance = 2.4f;
        foreach (var station in stations)
        {
            if (!station) continue;
            float d = Vector3.Distance(view.position, station.transform.position + Vector3.up * .8f);
            if (d < distance) { nearest = station; distance = d; }
        }
        if (!nearest) return false;
        nearest.Toggle(); return true;
    }
    public void ResetWalk()
    {
        Body.enabled = false; transform.SetPositionAndRotation(spawn, heading); Body.enabled = true;
        verticalSpeed = pitch = 0; view.localRotation = Quaternion.identity;
        foreach (var station in stations) if (station) station.SetActive(false);
    }
    void OnGUI()
    {
        GUI.Box(new Rect(16, 16, 510, 80), "FOREST | scale and collision blockout");
        GUI.Label(new Rect(30, 42, 490, 25), "WASD walk   Hold RMB look   E inspect station   R reset");
        int visited = 0; foreach (var s in stations) if (s && s.Active) visited++;
        GUI.Label(new Rect(30, 64, 490, 25), "Eye 1.65 m | body 1.8 m | route 3.2 m | stations " + visited + "/" + stations.Length);
    }
}
