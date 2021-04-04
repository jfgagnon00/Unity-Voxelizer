using UnityEngine;
using UnityEngine.Rendering;

namespace Voxelizer.Rendering
{
    /// <summary>
    /// Responsible for rendering properties of VoxelsPrimitive.
    /// </summary>
    public class Voxelization : MonoBehaviour
    {
        private const string VOXELIZATION = "Voxelization";
        private const string DEBUG_SUFFIX = "_Voxels";

        [SerializeField]
        [Tooltip("Voxelization resources")]
        private VoxelizationResources _resources = null;

        [SerializeField]
        [Tooltip("Mesh to voxelize")]
        private Mesh _mesh = null;

        [SerializeField]
        [Tooltip("Number of voxels in largest Mesh dimension")]
        [Range(2, 1024)]
        private int _resolution = 2;

        [SerializeField]
        [Range(0, 10)]
        [Tooltip("Lod bias at rendering")]
        private int _lod = 0;

        [SerializeField]
        [Tooltip("Set to true to voxelize every frame")]
        private bool _continuousVoxelization = false;

        private VoxelsData _voxelsData;
        private CommandBuffer _commandBuffer;

        private void Awake()
        {
            if (_mesh != null)
            {
                _voxelsData = VoxelizationUtils.CreateVoxelData(_mesh.bounds, _resolution, _mesh.name + DEBUG_SUFFIX);

                _commandBuffer = new CommandBuffer();
                _commandBuffer.name = VOXELIZATION;

                if (!_continuousVoxelization)
                {
                    VoxelizationUtils.VoxelizeSurface(_commandBuffer, _resources, _voxelsData, _mesh);
                    Graphics.ExecuteCommandBuffer(_commandBuffer);
                    DisposeCommandBuffer();
                }
            }
        }

        private void OnEnable()
        {
            if (_continuousVoxelization &&
                Camera.main != null &&
                _commandBuffer != null)
            {
                Camera.main.AddCommandBuffer(CameraEvent.AfterEverything, _commandBuffer);
            }
        }

        private void OnDisable()
        {
            if (_continuousVoxelization &&
                Camera.main != null &&
                _commandBuffer != null)
            {
                Camera.main.RemoveCommandBuffer(CameraEvent.AfterEverything, _commandBuffer);
            }
            DisposeAll();
        }

        private void Update()
        {
            if (_continuousVoxelization && _mesh != null)
                VoxelizationUtils.VoxelizeSurface(_commandBuffer, _resources, _voxelsData, _mesh);
        }

        private void DisposeAll()
        {
            if (_voxelsData != null) _voxelsData.Dispose();
            _voxelsData = null;

            DisposeCommandBuffer();
        }

        private void DisposeCommandBuffer()
        {
            if (_commandBuffer != null) _commandBuffer.Release();
            _commandBuffer = null;
        }
    }
}