// Shader responsible to voxelize thin surface using rasterization
// Trick is to use geometry shader to find plane with largest projected
// area and let rasterization do the rest. Pixel shader then writes
// to a UAV.

// There are many problems at the moment as not all voxels are found.
// Area to investigate: conservative rendering, using voxel depth exent
// to ensure all touched voxels are found.
Shader "Voxelizer/Voxelization"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "ForceNoShadowCasting"="True" }
        
        Pass
        {
            Blend Off
            Cull Off
            ZTest Always
            ColorMask false
            ZWrite Off
            Conservative true

            CGPROGRAM

            #pragma require randomwrite compute geometry
            #pragma enable_d3d11_debug_symbols

            #pragma vertex vsMain
            #pragma geometry gsMain
            #pragma fragment psMain

            #include "UnityCG.cginc"

            #define VOXELS_RW_ACCESS
            #include "Voxels.hlsl"

            struct VsInput
            {
                float3 position : POSITION;
                float4 color : COLOR0;
            };

            struct GsPsInput
            {
                // gs: view space
                // ps: clip space
                float4 position: SV_POSITION;
                float4 color : COLOR0;
            };

            float4 _VolumeSize; // xyz -> pixels
            float4x4 _Projections[3];
          
            GsPsInput vsMain(VsInput vsIn)
            {
                GsPsInput output;

                // gs use view space: [0, 1] in all dimensions
                output.position = float4(vsIn.position, 1);
                output.color = vsIn.color;

                return output;
            }

            // ===========================================

            float4 PemuteXZ(float4 p)
            {
                return float4(p.z, p.y, p.x, 1.0);
            }

            float4 PemuteYZ(float4 p)
            {
                return float4(p.x, p.z, p.y, 1.0);
            }

            float3 GetTriangleNormal(GsPsInput points[3])
            {
                float3 e01 = points[1].position - points[0].position;
                float3 e02 = points[2].position - points[0].position;
                float3 n = normalize(cross(e01, e02));
                return n;
            }

            [maxvertexcount(3)]
            void gsMain(triangle GsPsInput gsIn[3], inout TriangleStream<GsPsInput> gsOut)
            {
                float3 n = GetTriangleNormal(gsIn);
                float axis;

                // find plane giving largest area and project on it
                // and adjust triangle positions accordingly
                n = abs(n);
                if (n.x >= n.y && n.x >= n.z)
                {
                    // X has largest area
                    axis = 0;
                }
                else if (n.y >= n.x && n.y >= n.z)
                {
                    // Y has largest area
                    axis = 1;
                }
                else
                {
                    // Z has largest area
                    axis = 2;
                }

                float4x4 proj = _Projections[axis];

                // send new triangle to rasterization
                [unroll]
                for (int i = 0; i < 3; ++i)
                {
                    gsIn[i].position = mul(proj, gsIn[i].position);
                    gsIn[i].color.a = axis;
                    gsOut.Append(gsIn[i]);
                }

                gsOut.RestartStrip();
            }

            // ===========================================

            fixed4 psMain(GsPsInput psIn) : SV_Target
            {
                float3 color = psIn.color.rgb;
                float4 position = psIn.position;

                // compensate for largest area plane projection
                if (psIn.color.a == 0)
                {
                    position.z *= _VolumeSize.x;
                    position = PemuteXZ(position);
                }
                else if (psIn.color.a == 1)
                {
                    position.z *= _VolumeSize.y;
                    position = PemuteYZ(position);
                }
                else
                {
                    position.z *= _VolumeSize.z;
                }

                uint3 index = uint3(position.xyz);

                // store result
                float4 result = float4(color, 100.0);
                _Voxels[index] = result;

                return result;
            }

            ENDCG
        }
    }
}
