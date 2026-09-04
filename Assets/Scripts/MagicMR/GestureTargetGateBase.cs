using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Optional Bridge/study gate: decide whether a detected gesture may apply
    /// to the current RealityEditor target. Does not own spatial pose.
    /// When no gate exists in the scene, <see cref="GestureManager"/> accepts all.
    /// </summary>
    public abstract class GestureTargetGateBase : MonoBehaviour
    {
        /// <summary>
        /// Return true to allow the gesture to reach RealityEditor.
        /// </summary>
        public abstract bool Allow(
            EditDimension dimension,
            string gestureName,
            Vector3 queryWorldPosition,
            bool hasQueryPosition);
    }
}
