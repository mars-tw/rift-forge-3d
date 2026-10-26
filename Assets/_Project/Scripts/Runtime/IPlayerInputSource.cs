using UnityEngine;

namespace RiftForge
{
    /// <summary>
    /// Provides a device-independent two-dimensional movement value to the player controller.
    /// </summary>
    public interface IPlayerInputSource
    {
        /// <summary>
        /// Reads horizontal and forward movement in the normalized range of -1 to 1.
        /// </summary>
        /// <returns>The current movement input, where X is horizontal and Y is forward.</returns>
        Vector2 ReadMove();
    }
}
