using UnityEngine;

namespace MagicMR
{
    [CreateAssetMenu(menuName = "Magic MR/Lighter Model Settings")]
    public sealed class LighterModelSettings : ScriptableObject
    {
        public GameObject modelPrefab;
        [Tooltip("Rotate the imported model so its height is +Y and its broad front is +Z.")]
        public Vector3 rotationDegrees;
        [Min(.00001f), Tooltip("Meters per authored unit: 1 for meters, 0.01 for centimeters, 0.001 for millimeters.")]
        public float metersPerUnit = 1;
    }
}
