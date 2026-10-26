using UnityEngine;
using UnityEngine.InputSystem;

namespace RiftForge
{
    /// <summary>
    /// Reads player movement from WASD keys or the active gamepad left stick via the Input System.
    /// </summary>
    public sealed class InputSystemPlayerInputSource : IPlayerInputSource
    {
        /// <inheritdoc />
        public Vector2 ReadMove()
        {
            Vector2 keyboardMove = ReadKeyboardMove();
            Vector2 gamepadMove = Gamepad.current?.leftStick.ReadValue() ?? Vector2.zero;

            Vector2 strongestInput = gamepadMove.sqrMagnitude > keyboardMove.sqrMagnitude
                ? gamepadMove
                : keyboardMove;

            return Vector2.ClampMagnitude(strongestInput, 1f);
        }

        private static Vector2 ReadKeyboardMove()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            float horizontal = (keyboard.dKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed ? 1f : 0f);
            float vertical = (keyboard.wKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed ? 1f : 0f);

            return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        }
    }
}
