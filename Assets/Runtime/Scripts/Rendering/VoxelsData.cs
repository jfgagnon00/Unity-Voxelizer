using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Voxelizer.Rendering
{
    /// <summary>
    /// Represents data unique to a particular voxelization.
    /// </summary>
    public class VoxelsData : IDisposable
    {
        // 3D voxelization texture: contains color (RGB) and a flag indicating if voxel is filled (A != 0)
        public RenderTexture Voxels { get; private set; }
        public RenderTargetIdentifier VoxelsRTId { get; private set; }

        // Buffer of filled voxel instances (voxel center and color)
        public ComputeBuffer FilledVoxelInstances { get; private set; }

        // physical size of 1 voxel
        public float VoxelSize { get; private set; }

        // full physical size of volume
        public Vector3 VolumeSize => new Vector3(Voxels.width, Voxels.height, Voxels.volumeDepth) * VoxelSize;

        // 2 most largest dimension value (used by rasterization)
        public Vector2Int LargestDimenstion2D
        {
            get
            {
                int[] dimensions = { Voxels.width, Voxels.height, Voxels.volumeDepth };
                Array.Sort(dimensions);
                return new Vector2Int(dimensions[2], dimensions[1]);
            }
        }

        public VoxelsData(RenderTexture voxels, 
            ComputeBuffer filledVoxelInstances, 
            float voxelSize)
        {
            Voxels = voxels;
            VoxelsRTId = new RenderTargetIdentifier(Voxels);
            VoxelSize = voxelSize;
            FilledVoxelInstances = filledVoxelInstances;
        }

        public void Dispose()
        {
            if (Voxels != null) Voxels.Release();
            Voxels = null;

            if (FilledVoxelInstances != null) FilledVoxelInstances.Release();
            FilledVoxelInstances = null;
        }
    }
}