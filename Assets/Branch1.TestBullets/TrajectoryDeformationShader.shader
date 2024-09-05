// Testing to Compute Trajectory Deformations in Shaders
Shader "Particles/TrajectoryDeformationShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1,1,1,1)
        
        // -------------------------------------
        // Hidden properties - Generic
        //[HideInInspector] _Cull("__cull", Float) = 0.0
        [HideInInspector] _BlendOp("__blendop", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 5.0
        [HideInInspector] _DstBlend("__dst", Float) = 1.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 1.0
        //[HideInInspector] _ZWrite("__zw", Float) = 0.0
        
        // -------------------------------------
        // Test trajectory properties
        _Position("Position", Vector) = (0,0,0,0)
        _TargetPosition("Target Position", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            //"RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            
            // -------------------------------------
            // Render State Commands
            //BlendOp[_BlendOp]
            BlendOp Add
            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            //ZWrite[_ZWrite]
            //Cull[_Cull]
            //BlendOp Multiply
            //BlendOp Add
            //Blend SrcAlpha OneMinusSrcAlpha, One One
            //Blend One OneMinusSrcAlpha
            //Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            Cull Back
            //Cull Off
            Lighting Off
            ZWrite Off
                        
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // For DOTS Instancing
            #pragma target 4.5
            //#pragma exclude_renderers gles gles3 glcore
            //#pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            // The Core.hlsl file contains definitions of frequently used HLSL
            // macros and functions, and also contains #include references to other
            // HLSL files (for example, Common.hlsl, SpaceTransforms.hlsl, etc.).
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            //#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;

                float4 _Position;
                float4 _TargetPosition;
            CBUFFER_END
            
            v2f vert (appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // Test trajectory
                float3 temp = _Position - _TargetPosition;
                float distance = length(temp);
                float3 direction = normalize(temp);
                float3 up = float3(0, 1, 0);
                float3 right = normalize(cross(up, direction));
                float3 newUp = cross(direction, right);
                float3 position = _TargetPosition;

                // Make rotation matrix with right, up, forward
                float3x3 lookAtMatrix = float3x3(
                    right.x, newUp.x, direction.x,
                    right.y, newUp.y, direction.y,
                    right.z, newUp.z, direction.z
                );
                
                float3x3 result = lookAtMatrix;

                // Scale
                float3 scale = float3(0.1, 1, distance);
                
                // Make trajectory matrix
                float4x4 trajectoryMatrix = float4x4(
                    result[0].x * scale.x, result[0].y * scale.y, result[0].z * scale.z, position.x,
                    result[1].x * scale.x, result[1].y * scale.y, result[1].z * scale.z, position.y,
                    result[2].x * scale.x, result[2].y * scale.y, result[2].z * scale.z, position.z,
                    0, 0, 0, 1
                );

                float4 newV = mul(trajectoryMatrix, v.vertex);
                o.vertex = TransformWorldToHClip(newV);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                // TODO: invert target and direction

                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return col * _BaseColor;
            }
            ENDHLSL
        }
    }
}
