Shader "Voxelizer/Voxelization"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "ForceNoShadowCasting"="True" }
        
        Pass
        {
            // disable alpha blend, depth test and culling
            Blend Off
            Cull Off
            ZTest Always

            // disable color and depth write
            ColorMask 0
            ZWrite Off

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
                // gs: normalized voxelization volume space
                // ps: clip space
                float4 position: SV_POSITION;
                float4 color : COLOR0;
            };

            float4 _VolumeSize; // in pixels
            float4 _ViewportST;
          
            GsPsInput vsMain(VsInput vsIn)
            {
                GsPsInput output;

                output.position.xyz = UnityObjectToViewPos(vsIn.position);
                output.position.w = 1.0;
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
                float3 e12 = points[2].position - points[1].position;
                float3 n = normalize(cross(e01, e12));
                return n;
            }

            [maxvertexcount(3)]
            void gsMain(triangle GsPsInput gsIn[3], inout TriangleStream<GsPsInput> gsOut)
            {
                float3 n = GetTriangleNormal(gsIn);

                float4 p0;
                float4 p1;
                float4 p2;
                float axis;

                // find plane giving largest area and project on it
                n = abs(n);
                if (n.x >= n.y && n.x >= n.z)
                {
                    p0 = PemuteXZ(gsIn[0].position);
                    p1 = PemuteXZ(gsIn[1].position);
                    p2 = PemuteXZ(gsIn[2].position);
                    axis = 0;
                }
                else if (n.y >= n.x && n.y >= n.z)
                {
                    p0 = PemuteYZ(gsIn[0].position);
                    p1 = PemuteYZ(gsIn[1].position);
                    p2 = PemuteYZ(gsIn[2].position);
                    axis = 1;
                }
                else
                {
                    p0 = gsIn[0].position;
                    p1 = gsIn[1].position;
                    p2 = gsIn[2].position;
                    axis = 2;
                }

                // send new triangle to rasterization
                gsIn[0].position = mul(UNITY_MATRIX_P, p0);
                gsIn[0].color.a = axis;
                gsOut.Append(gsIn[0]);

                gsIn[1].position = mul(UNITY_MATRIX_P, p1);
                gsIn[1].color.a = axis;
                gsOut.Append(gsIn[1]);

                gsIn[2].position = mul(UNITY_MATRIX_P, p2);
                gsIn[2].color.a = axis;
                gsOut.Append(gsIn[2]);

                gsOut.RestartStrip();
            }

            // ===========================================

            fixed4 psMain(GsPsInput psIn) : SV_Target
            {
                float3 color = psIn.color.rgb;

                // x and y are pixel centers but z is still
                // in clip coordinate at this point; must 
                // adjust psIn.position so all components
                // are in same space
                float4 position = psIn.position;
                position.xy = position.xy * _ViewportST.xy + _ViewportST.zw;

                // compensate for largest area projetion
                if (psIn.color.a == 0)
                    position = PemuteXZ(position);
                else if (psIn.color.a == 1)
                    position = PemuteYZ(position);

                position.xyz *= _VolumeSize.xyz;
                position.xyz += 0.5;

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
