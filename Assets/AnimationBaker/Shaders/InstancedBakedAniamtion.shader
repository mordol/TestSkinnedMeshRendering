Shader "Unlit/InstancedBakedAniamtion"
{
    // https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@10.2/manual/writing-shaders-urp-unlit-texture.html
    
    Properties
    {
        _BaseMap("Base Map", 2D) = "white"
        
        _AnimMap ("Baked animation", 2D) = "black"
        _UVStepForBone ("UV Step for bone", Vector) = (0, 0, 0, 0)
        _Frame ("Animation frame", Float) = 0.0
    }
    SubShader
    {
        // SubShader Tags define when and under which conditions a SubShader block or
        // a pass is executed.
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            // The HLSL code block. Unity SRP uses the HLSL language.
            HLSLPROGRAM
            // This line defines the name of the vertex shader. 
            #pragma vertex vert
            // This line defines the name of the fragment shader. 
            #pragma fragment frag

            #pragma target 3.0

            // For SRP Batcher
            #pragma enable_cbuffer

            // The Core.hlsl file contains definitions of frequently used HLSL
            // macros and functions, and also contains #include references to other
            // HLSL files (for example, Common.hlsl, SpaceTransforms.hlsl, etc.).
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"            
            
            // The structure definition defines which variables it contains.
            // This example uses the Attributes structure as an input structure in
            // the vertex shader.
            struct Attributes
            {
                // The positionOS variable contains the vertex positions in object
                // space.
                float4 positionOS   : POSITION;
                // The uv variable contains the UV coordinate on the texture for the
                // given vertex.
                float2 uv           : TEXCOORD0;

                uint4 blendIndices  : BLENDINDICES;
                float4 blendWeights : BLENDWEIGHTS;
            };

            struct Varyings
            {
                // The positions in this struct must have the SV_POSITION semantic.
                float4 positionHCS  : SV_POSITION;
                
                // The uv variable contains the UV coordinate on the texture for the
                // given vertex.
                float2 uv           : TEXCOORD0;
            };
            
            // This macro declares _BaseMap as a Texture2D object.
            TEXTURE2D(_BaseMap);
            // This macro declares the sampler for the _BaseMap texture.
            SAMPLER(sampler_BaseMap);
            
            TEXTURE2D(_AnimMap);
            SAMPLER(sampler_AnimMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _AnimMap_ST;
                float _Frame;
                float2 _UVStepForBone;
            CBUFFER_END

            // Get skin matrix function
            half4x4 GetSkinMatrix(uint blendIndex, float frameIndex)
            {
	            half4x4 mat;
	            // Shift to mid uv position (uvStep * 0.5)
	            float2 uv = float2((float)blendIndex * 3.0 * _UVStepForBone.x, frameIndex * _UVStepForBone.y) + _UVStepForBone * 0.5;
                float2 uvStep = float2(_UVStepForBone.x, 0);
                
                return half4x4(
                    SAMPLE_TEXTURE2D_LOD(_AnimMap, sampler_AnimMap, TRANSFORM_TEX(uv, _AnimMap), 0),
                    SAMPLE_TEXTURE2D_LOD(_AnimMap, sampler_AnimMap, TRANSFORM_TEX(uv + uvStep, _AnimMap), 0),
                    SAMPLE_TEXTURE2D_LOD(_AnimMap, sampler_AnimMap, TRANSFORM_TEX(uv + uvStep + uvStep, _AnimMap), 0),
                    half4(0, 0, 0, 1));
            }

            // The vertex shader definition with properties defined in the Varyings 
            // structure. The type of the vert function must match the type (struct)
            // that it returns.
            Varyings vert(Attributes IN)
            {
                // Calc skin matrix for 4 weight
                half4x4 skinMatrix;
                skinMatrix = GetSkinMatrix(IN.blendIndices.x, _Frame) * IN.blendWeights.x;
                skinMatrix += GetSkinMatrix(IN.blendIndices.y, _Frame) * IN.blendWeights.y;
                skinMatrix += GetSkinMatrix(IN.blendIndices.z, _Frame) * IN.blendWeights.z;
                skinMatrix += GetSkinMatrix(IN.blendIndices.w, _Frame) * IN.blendWeights.w;
                
                float4 position = mul(skinMatrix, IN.positionOS);
                
                // Declaring the output object (OUT) with the Varyings struct.
                Varyings OUT;

                // The TransformObjectToHClip function transforms vertex positions
                // from object space to homogenous clip space.
                OUT.positionHCS = TransformObjectToHClip(position.xyz);
                
                // The TRANSFORM_TEX macro performs the tiling and offset
                // transformation.
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);

                return OUT;
            }

            // The fragment shader definition.            
            half4 frag(Varyings IN) : SV_Target
            {
                // The SAMPLE_TEXTURE2D marco samples the texture with the given
                // sampler.
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                return color;
            }
            ENDHLSL
        }
    }
}
