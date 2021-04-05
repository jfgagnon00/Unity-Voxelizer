using UnityEngine;

namespace Voxelizer.Rendering
{
    /// <summary>
    /// Encapsulate common resources related to shaders for
    /// voxelization creation and visualization
    /// </summary>
    [CreateAssetMenu(menuName = "Voxelizer/VoxelizationResources")]
    public class VoxelizationResources : ScriptableObject
    {
        // _voxelizationShader uniforms name
        public static int VOLUME_SIZE = Shader.PropertyToID("_VolumeSize");
        public static int VIEWPORT_ST = Shader.PropertyToID("_ViewportST");

        // _voxelizationPostProcessShader values
        public static string FIND_FILLED_VOXELS_KERNEL = "FindFilledVoxels";
        public static int VOXELS = Shader.PropertyToID("_Voxels");
        public static int FILLED_VOXELS_INSTANCES = Shader.PropertyToID("_FilledVoxelInstances");
        public static int INDEX_TO_POSITION = Shader.PropertyToID("_IndexToPosition");

        // _filledVoxelInstanceShader values
        public static int VOLUME_LOCAL_TO_WORLD = Shader.PropertyToID("_VolumeLocalToWorld");

        [SerializeField]
        [Tooltip("Shader to use for voxelization")]
        private Shader _voxelizationShader = null;
        public Shader VoxelizationShader => _voxelizationShader;

        private Material _voxelizationMaterial;
        public Material VoxelizationMaterial
        {
            get
            {
                if (_voxelizationMaterial == null) 
                    _voxelizationMaterial = new Material(_voxelizationShader);
                return _voxelizationMaterial;
            }
        }

        [SerializeField]
        [Tooltip("Compute shader to use for processing voxelization")]
        private ComputeShader _voxelizationPostProcessShader = null;
        public ComputeShader VoxelizationPostProcessShader => _voxelizationPostProcessShader;

        public int FindFilledVoxelsKernel
        {
            get
            {
                if (_voxelizationPostProcessShader != null &&
                    _voxelizationPostProcessShader.HasKernel(FIND_FILLED_VOXELS_KERNEL))
                {
                    return _voxelizationPostProcessShader.FindKernel(FIND_FILLED_VOXELS_KERNEL);
                }
                else
                {
                    return -1;
                }
            }
        }

        public (int, int, int) FindFilledVoxelsThreadGroupsSize
        {
            get
            {
                var kernel = FindFilledVoxelsKernel;
                if (kernel != -1)
                {
                    uint x, y, z;
                    _voxelizationPostProcessShader.GetKernelThreadGroupSizes(kernel,
                        out x,
                        out y,
                        out z);
                    return ((int)x, (int)y, (int)z);
                }
                else
                {
                    return (0, 0, 0);
                }
            }
        }

        [SerializeField]
        [Tooltip("Mesh used to visualize voxels")]
        private Mesh _filledVoxelInstanceMesh = null;
        public Mesh FilledVoxelInstanceMesh => _filledVoxelInstanceMesh;

        [SerializeField]
        [Tooltip("Shader used to visualize voxels")]
        private Shader _filledVoxelInstanceShader = null;
        public Shader FilledVoxelInstanceShader => _filledVoxelInstanceShader;
    }
}