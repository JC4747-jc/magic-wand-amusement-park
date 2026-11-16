using UnityEngine;

namespace MagicMR
{
    // Authored bind-pose meshes reuse the existing five branch bones and fixed photogrammetric trunk.
    public sealed class WillowCanopyData : ScriptableObject
    {
        public Mesh leaves;
        public Mesh branches;
        public Material leafMaterial;
        public int fronds,leafCount;
    }
}
