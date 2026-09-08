using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class InputReader : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public float ZoomInput { get; private set; }
    public Vector2 MouseScreenPosition { get; private set; }
    public bool IsFastMove { get; private set; }

    public event Action OnLeftClick;

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        if (keyboard != null)
        {
            Vector2 move = Vector2.zero;

            if (keyboard.wKey.isPressed) move.y += 1;
            if (keyboard.sKey.isPressed) move.y -= 1;
            if (keyboard.aKey.isPressed) move.x -= 1;
            if (keyboard.dKey.isPressed) move.x += 1;

            MoveInput = move.normalized;
            IsFastMove = keyboard.leftShiftKey.isPressed;
        }

        if (mouse != null)
        {
            MouseScreenPosition = mouse.position.ReadValue();
            ZoomInput = mouse.scroll.ReadValue().y;

            if (mouse.leftButton.wasPressedThisFrame)
                OnLeftClick?.Invoke();
        }
    }
}