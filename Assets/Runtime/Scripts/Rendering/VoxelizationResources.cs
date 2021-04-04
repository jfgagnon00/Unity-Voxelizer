using UnityEngine;

namespace Voxelizer.Rendering
{
    /// <summary>
    /// Wraps all shaders and constant related to shaders for voxelization
    /// </summary>
    [CreateAssetMenu(menuName = "Voxelizer/VoxelizationResources")]
    public class VoxelizationResources : ScriptableObject
    {
        // _voxelizationShader uniforms name
        public static int VOLUME_SIZE = Shader.PropertyToID("_VolumeSize");
        public static int VIEWPORT_ST = Shader.PropertyToID("_ViewportST");

        [SerializeField]
        [Tooltip("Shader to use for voxelizarion")]
        private Shader _voxelizationShader = null;
        public Shader VoxelizationShader => _voxelizationShader;

        private Material _voxelizationMaterial;
        public Material VoxelizationMaterial 
        { 
            get
            {
                if (_voxelizationMaterial == null) _voxelizationMaterial = new Material(_voxelizationShader);
                return _voxelizationMaterial;
            }
        }
    }
}