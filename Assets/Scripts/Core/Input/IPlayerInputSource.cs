using UnityEngine;

namespace SickOfIt.Core.Input
{
    /// <summary>
    /// Input that replaces the devices for PlayerController (see PlayerController.InputOverride).
    /// Used by the automated playtest bot.
    /// </summary>
    public interface IPlayerInputSource
    {
        /// <summary>Stick direction, -1..1 on each axis.</summary>
        Vector2 Move { get; }
        /// <summary>Direction to fire in; zero = not firing.</summary>
        Vector2 FireDirection { get; }
        /// <summary>True once per requested jump.</summary>
        bool ConsumeJump();
    }
}
