using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Virtual input devices for the playtest bot: a keyboard, a gamepad and a mouse added to the Input System,
    /// so the game reads the bot exactly like a player (move A/D, hold S or the left stick down to drop through a platform, jump Space, hold R to rewind, P to pause,
    /// 1-4 to pick an upgrade, Escape to skip; aim with the gamepad's right stick; click menu buttons with the
    /// mouse). Nothing exists until <see cref="Create"/>; <see cref="Dispose"/> removes the devices.
    /// </summary>
    public sealed class PlaytestInput
    {
        public static PlaytestInput Current { get; private set; }

        Keyboard keyboard;
        Gamepad gamepad;
        Mouse mouse;
        Touchscreen touchscreen;
        int touchId;
        readonly HashSet<Key> held = new HashSet<Key>();
        Vector2 leftStick;
        Vector2 rightStick;
        Vector2 mousePosition;
        bool mouseDown;

        public static PlaytestInput Create()
        {
            Current?.Dispose();
            var input = new PlaytestInput
            {
                keyboard = InputSystem.AddDevice<Keyboard>("HawkesPlaytestKeyboard"),
                gamepad = InputSystem.AddDevice<Gamepad>("HawkesPlaytestGamepad"),
                mouse = InputSystem.AddDevice<Mouse>("HawkesPlaytestMouse"),
                touchscreen = InputSystem.AddDevice<Touchscreen>("HawkesPlaytestTouchscreen"),
            };
            Current = input;
            return input;
        }

        public bool Alive => keyboard != null && keyboard.added;

        public bool IsHeld(Key key) => held.Contains(key);

        public void SetKey(Key key, bool down)
        {
            if (!Alive || (down ? !held.Add(key) : !held.Remove(key)))
                return;
            var state = new KeyboardState();
            foreach (var k in held)
                state.Set(k, true);
            InputSystem.QueueStateEvent(keyboard, state);
            keyboard.MakeCurrent();
        }

        /// <summary>Horizontal movement (-1..1) on A/D.</summary>
        public void SetMoveX(float x)
        {
            SetKey(Key.A, x < -0.5f);
            SetKey(Key.D, x > 0.5f);
        }

        /// <summary>Holds S (down: drops through a one-way platform he stands on) while 'down' is true.</summary>
        public void SetDown(bool down) => SetKey(Key.S, down);

        /// <summary>The gamepad's left stick (move; pulled down it drops through a one-way platform).</summary>
        public void SetMoveStick(Vector2 stick)
        {
            stick = stick.sqrMagnitude > 1f ? stick.normalized : stick;
            if (!Alive || stick == leftStick)
                return;
            leftStick = stick;
            SendGamepad();
        }

        /// <summary>Aims (and fires) with the gamepad's right stick; zero stops firing.</summary>
        public void SetAim(Vector2 direction)
        {
            direction = direction.sqrMagnitude > 1f ? direction.normalized : direction;
            if (!Alive || direction == rightStick)
                return;
            rightStick = direction;
            SendGamepad();
        }

        void SendGamepad()
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = leftStick, rightStick = rightStick });
            gamepad.MakeCurrent();
        }

        public void MoveMouse(Vector2 screenPosition)
        {
            if (!Alive)
                return;
            mousePosition = screenPosition;
            SendMouse();
        }

        public void SetMouseButton(bool down)
        {
            if (!Alive || mouseDown == down)
                return;
            mouseDown = down;
            SendMouse();
        }

        void SendMouse()
        {
            var state = new MouseState { position = mousePosition };
            if (mouseDown)
                state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state);
            mouse.MakeCurrent();
        }

        /// <summary>Lets go of everything.</summary>
        /// <summary>A finger goes down at a screen point (a touchscreen state event; EnhancedTouch's onFingerDown sees it). Returns its id for <see cref="TouchUp"/>.</summary>
        public int TouchDown(Vector2 screenPosition)
        {
            if (touchscreen == null || !touchscreen.added)
                return 0;
            int id = ++touchId;
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Began, position = screenPosition, pressure = 1f });
            return id;
        }

        /// <summary>The finger <paramref name="id"/> slides to a screen point.</summary>
        public void TouchMove(int id, Vector2 screenPosition)
        {
            if (touchscreen == null || !touchscreen.added || id == 0)
                return;
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = screenPosition, pressure = 1f });
        }

        /// <summary>The finger <paramref name="id"/> lifts.</summary>
        public void TouchUp(int id, Vector2 screenPosition)
        {
            if (touchscreen == null || !touchscreen.added || id == 0)
                return;
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = screenPosition });
        }

        public void ReleaseAll()
        {
            if (!Alive)
                return;
            held.Clear();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            leftStick = rightStick = Vector2.zero;
            SendGamepad();
            mouseDown = false;
            SendMouse();
        }

        public void Dispose()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (touchscreen != null && touchscreen.added) InputSystem.RemoveDevice(touchscreen);
            touchscreen = null;
            keyboard = null;
            gamepad = null;
            mouse = null;
            held.Clear();
            if (Current == this)
                Current = null;
        }
    }
}
