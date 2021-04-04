using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Voxelizer.Rendering
{
    /// <summary>
    /// Encapsulates creation and update of VoxelsData
    /// </summary>
    public static class VoxelizationUtils
    {
        private static int TEMP_RT = Shader.PropertyToID("_RenderTargetVoxl");

        /// <summary>
        /// Initialize VoxelsData
        /// </summary>
        /// <param name="bounds">AABB of voxels volume</param>
        /// <param name="resolution">Number of voxels in the largest bounds dimension</param>
        /// <param name="debugName">Name of 3D render texture resource</param>
        /// <returns></returns>
        public static VoxelsData CreateVoxelData(Bounds bounds, int resolution, string debugName = "")
        {
            var fullExtent = bounds.size;
            var largestDimension = Mathf.Max(Mathf.Max(fullExtent.x, fullExtent.y), fullExtent.z);
            var voxelSize = largestDimension / Mathf.Max(resolution, 1);
            var textureSizeVec3 = fullExtent / voxelSize;
            var textureSizeInt3 = Vector3Int.CeilToInt(textureSizeVec3);
            textureSizeInt3 = Vector3Int.Max(textureSizeInt3, Vector3Int.one);

            var voxelsRt = new RenderTexture(textureSizeInt3.x, textureSizeInt3.y, 0, RenderTextureFormat.ARGBFloat);
            voxelsRt.enableRandomWrite = true;
            voxelsRt.volumeDepth = textureSizeInt3.z;
            voxelsRt.dimension = TextureDimension.Tex3D;
            voxelsRt.name = debugName;
            voxelsRt.Create();

            return new VoxelsData(voxelsRt, voxelSize);
        }


        /// <summary>
        /// Voxelize the surface of a mesh. Only color is constructed at the moment.
        /// </summary>
        /// <param name="commandBuffer"></param>
        /// <param name="data"></param>
        /// <param name="mesh"></param>
        public static void VoxelizeSurface(CommandBuffer commandBuffer, VoxelizationResources resources, VoxelsData data, Mesh mesh)
        {
            commandBuffer.Clear();
            commandBuffer.ClearRandomWriteTargets();

            commandBuffer.DisableScissorRect();

            // TODO: find out how to set null render target
            //       viewport is implicitly set
            var vp = data.LargestDimenstion2D; 
            commandBuffer.GetTemporaryRT(TEMP_RT, vp.x, vp.y, 0, FilterMode.Point, RenderTextureFormat.ARGBFloat);
            commandBuffer.SetRenderTarget(TEMP_RT);

            // set pixel shader UAV
            commandBuffer.SetRandomWriteTarget(1, data.VoxelsRTId);

            // transform scene in voxelization volume space
            // [-1, 1] in XYZ
            var volumeSize = data.VolumeSize;
            var view = Matrix4x4.Scale(new Vector3(2.0f / volumeSize.x, 2.0f / volumeSize.y, 2.0f / volumeSize.z)) *
                       Matrix4x4.Translate(-mesh.bounds.center - new Vector3(0, 0, 0.5f * volumeSize.z));

            // geometry shader reprojects each triangles
            // opengl vs directx depth needs to be handled correctly
            // hence a simple orthogonal projection
            var proj = Matrix4x4.Ortho(-1, 1, -1, 1, -1, 1);
            commandBuffer.SetViewProjectionMatrices(view, proj);

            commandBuffer.SetGlobalVector(VoxelizationResources.VOLUME_SIZE, 
                new Vector4(data.Voxels.width,
                            data.Voxels.height,
                            data.Voxels.volumeDepth));
            commandBuffer.SetGlobalVector(VoxelizationResources.VIEWPORT_ST, 
                new Vector4(1.0f / vp.x, 
                            1.0f / vp.y, 
                            -0.5f / vp.x,
                            -0.5f / vp.y));

            // trigger rasterization to fill highest resolution voxels
            commandBuffer.DrawMesh(mesh, Matrix4x4.identity, resources.VoxelizationMaterial);

            // generate voxels mips
            // TODO

            commandBuffer.ReleaseTemporaryRT(TEMP_RT);
            var fence = commandBuffer.CreateGraphicsFence(GraphicsFenceType.CPUSynchronisation,
                SynchronisationStageFlags.PixelProcessing);
        }
    }
}