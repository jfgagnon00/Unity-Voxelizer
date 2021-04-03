using UnityEngine;

namespace Voxelizer.Rendering
{
    /// <summary>
    /// Represents data unique to a particular voxelization.
    /// </summary>
    [CreateAssetMenu(menuName = "Voxelizer/VoxelsPrimitive")]
    public class VoxelsPrimitive : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Physical size of voxels volume. Note the volume is a cube hence using a scalar for size.")]
        [Min(0.1f)]
        private float _volumeSize = 1.0f;
        public float VolumeSize 
        { 
            get => _volumeSize;
            set { _volumeSize = Mathf.Max(value, 0.1f); } 
        }

        [SerializeField]
        [Tooltip("Physical size of voxels volume")]
        [Range(2, 4096)]
        private int _resolution = 2;
        public int Resolution 
        { 
            get => _resolution;
            set { _resolution = Mathf.Clamp(value, 2, 4096); } 
        }

        // Index used to access Voxels content
        public GraphicsBuffer Octree { get; private set; }

        // Actual voxel content.
        public Texture3D Voxels { get; private set; }

#if UNITY_EDITOR
        // Need to store some info for custom editor in edited object
        // see https://tinyurl.com/5xfkmsu6 for details
        public bool ShowVolumeSizeGizmo { get; set; }
#endif
    }
}