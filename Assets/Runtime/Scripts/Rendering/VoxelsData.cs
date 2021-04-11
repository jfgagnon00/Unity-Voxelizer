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
        public Vector3 VolumeCenter { get; private set; }
        public Vector3 VolumeSize => new Vector3(Voxels.width, Voxels.height, Voxels.volumeDepth) * VoxelSize;
        public Bounds VolumeBounds => new Bounds(VolumeCenter, VolumeSize);

        // 2 most largest dimension value (used by rasterization)
        public Vector2Int ViewportSize
        {
            get
            {
                int[] dimensions = { Voxels.width, Voxels.height, Voxels.volumeDepth };
                Array.Sort(dimensions);
                return new Vector2Int(dimensions[2], dimensions[2]);
            }
        }

        public VoxelsData(RenderTexture voxels, 
            ComputeBuffer filledVoxelInstances, 
            float voxelSize,
            Vector3 volumeCenter)
        {
            Voxels = voxels;
            VoxelsRTId = new RenderTargetIdentifier(Voxels);
            VoxelSize = voxelSize;
            VolumeCenter = volumeCenter;
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