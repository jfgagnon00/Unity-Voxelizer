using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Voxelizer.Rendering
{
    /// <summary>
    /// Encapsulates creation and update of VoxelsData
    /// </summary>
    public static class VoxelizationUtils
    {
        private const string FILL_VOXELS = "FillVoxelsPass";
        private const string FIND_FILLED_VOXELS = "FindFilledVoxelsPass";
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
            voxelsRt.name = debugName + "_Voxels";
            voxelsRt.Create();

            var filledVoxels = new ComputeBuffer(textureSizeInt3.x * textureSizeInt3.y * textureSizeInt3.z,
                Marshal.SizeOf<Vector3>() * 2, 
                ComputeBufferType.Counter | ComputeBufferType.Structured);
            filledVoxels.name = debugName + "_FilledVoxels";

            return new VoxelsData(voxelsRt, filledVoxels, voxelSize);
        }

        /// <summary>
        /// Voxelize the surface of a mesh. Only color is constructed at the moment.
        /// </summary>
        /// <param name="commandBuffer"></param>
        /// <param name="data"></param>
        /// <param name="mesh"></param>
        public static void VoxelizeSurface(CommandBuffer commandBuffer, VoxelizationResources resources, VoxelsData data, Mesh mesh)
        {
            Begin(commandBuffer, resources, data, mesh);
            FillVoxels(commandBuffer, resources, data, mesh);
            FindFilledVoxelInstances(commandBuffer, resources, data, mesh);

            // generate voxels mips
            // TODO

            End(commandBuffer, resources, data, mesh);
        }

        private static void Begin(CommandBuffer commandBuffer, VoxelizationResources resources, VoxelsData data, Mesh mesh)
        {
            commandBuffer.Clear();
            commandBuffer.DisableScissorRect();

            // TODO: find out how to set null render target
            //       viewport is implicitly set
            var vp = data.LargestDimenstion2D;
            commandBuffer.GetTemporaryRT(TEMP_RT, vp.x, vp.y, 0, FilterMode.Point, RenderTextureFormat.ARGBFloat);
            commandBuffer.SetRenderTarget(TEMP_RT);

            // set pixel shader UAV
            commandBuffer.SetRandomWriteTarget(1, data.VoxelsRTId);

            // transform mesh space in voxelization volume space
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
        }

        private static void End(CommandBuffer commandBuffer, VoxelizationResources resources, VoxelsData data, Mesh mesh)
        {
            commandBuffer.ReleaseTemporaryRT(TEMP_RT);
        }

        private static void FillVoxels(CommandBuffer commandBuffer, VoxelizationResources resources, VoxelsData data, Mesh mesh)
        {
            // trigger rasterization to fill highest resolution voxels
            commandBuffer.BeginSample(FILL_VOXELS);
            commandBuffer.DrawMesh(mesh, Matrix4x4.identity, resources.VoxelizationMaterial);
            commandBuffer.EndSample(FILL_VOXELS);
        }

        private static void FindFilledVoxelInstances(CommandBuffer commandBuffer, VoxelizationResources resources, VoxelsData data, Mesh mesh)
        {
            data.FilledVoxelInstances.SetCounterValue(0);

            // fetch filled voxels
            var cs = resources.VoxelizationPostProcessShader;
            var kernel = resources.FindFilledVoxelsKernel;
            cs.SetTexture(kernel, VoxelizationResources.VOXELS, data.Voxels);
            cs.SetBuffer(kernel, VoxelizationResources.FILLED_VOXELS_INSTANCES, data.FilledVoxelInstances);
            cs.SetInts(VoxelizationResources.VOLUME_SIZE,
                new int[] { data.Voxels.width, data.Voxels.height, data.Voxels.volumeDepth });

            var volumeSize = data.VolumeSize;

            // transform index space to mesh local space
            // lhs system; matrix must be read from 
            // last to first
            var indexToPosition =

                // finally mesh local space
                Matrix4x4.Translate(mesh.bounds.center) *

                // [-0.5, 0.5] space [-0.5 * volumeSize, 0.5 * volumeSize]
                Matrix4x4.Scale(new Vector3(volumeSize.x,
                                            volumeSize.y,
                                            volumeSize.z)) *

                // [0, 1] space to [-0.5, 0.5] space
                Matrix4x4.Translate(new Vector3(-0.5f, -0.5f, -0.5f)) *

                // voxel space to [0, 1] space
                Matrix4x4.Scale(new Vector3(1.0f / data.Voxels.width,
                                            1.0f / data.Voxels.height,
                                            1.0f / data.Voxels.volumeDepth)) *

                // move by half a voxel
                Matrix4x4.Translate(new Vector3(0.5f, 0.5f, 0.5f));
            cs.SetMatrix(VoxelizationResources.INDEX_TO_POSITION, indexToPosition);

            (var tgX, var tgY, var tgZ) = resources.FindFilledVoxelsThreadGroupsSize;
            commandBuffer.BeginSample(FIND_FILLED_VOXELS);
            commandBuffer.ClearRandomWriteTargets();
            commandBuffer.DispatchCompute(cs,
                kernel,
                NumGroup(data.Voxels.width, tgX),
                NumGroup(data.Voxels.height, tgY),
                NumGroup(data.Voxels.volumeDepth, tgZ));
            commandBuffer.EndSample(FIND_FILLED_VOXELS);
        }

        private static int NumGroup(int count, int size)
        {
            return Math.Max(1, count / size);
        }
    }
}