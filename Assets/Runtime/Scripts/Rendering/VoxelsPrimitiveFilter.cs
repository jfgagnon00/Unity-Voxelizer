using UnityEngine;

namespace Voxelizer.Rendering
{
    /// <summary>
    /// Responsible for rendering properties of VoxelsPrimitive.
    /// </summary>
    public class VoxelsPrimitiveFilter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Voxels to render")]
        private VoxelsPrimitive _voxelsPrimitive;

        [SerializeField]
        [Tooltip("Material used to render Voxels Primitive")]
        private Material _material;
    }
}