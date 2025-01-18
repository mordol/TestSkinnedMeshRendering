Shader "Unlit/BasicBRGShader"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        [HideInInspector] _BaseColor ("Base Colour", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100

        Pass
        {
            Tags
            {
                "LightMode"="UniversalForward"
            }

            Cull Back
            
            // The HLSL code block. Unity SRP uses the HLSL language.
            HLSLPROGRAM
            // This line defines the name of the vertex shader. 
            #pragma vertex vert
            // This line defines the name of the fragment shader. 
            #pragma fragment frag

            // For DOTS Instancing
            #pragma target 4.5
            //#pragma exclude_renderers gles gles3 glcore
            //#pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            //#pragma instancing_options renderinglayer

            // For SRP Batcher
            //#pragma enable_cbuffer

            // The Core.hlsl file contains definitions of frequently used HLSL
            // macros and functions, and also contains #include references to other
            // HLSL files (for example, Common.hlsl, SpaceTransforms.hlsl, etc.).
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            //#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
            
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

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                // The positions in this struct must have the SV_POSITION semantic.
                float4 positionHCS  : SV_POSITION;
                
                // The uv variable contains the UV coordinate on the texture for the
                // given vertex.
                float2 uv           : TEXCOORD0;
                
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            // This macro declares _BaseMap as a Texture2D object.
            TEXTURE2D(_BaseMap);
            // This macro declares the sampler for the _BaseMap texture.
            SAMPLER(sampler_BaseMap);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
            CBUFFER_END

            // DOTS Instanced properties are completely separate from regular material properties, and you can give them the same name as another regular material property.
            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
                    // UNITY_DOTS_INSTANCED_PROP_OVERRIDE_REQUIRED(float4, EmissionColor) // The property is always loaded from the unity_DOTSInstanceData buffer, and no dynamic branch is ever emitted when accessing the property.
                UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

                #define _BaseColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor)
                //#define _AnimText_ST            UNITY_ACCESS_DOTS_INSTANCED_PROP_FROM_MACRO(float4 , Metadata_AnimText_ST)
            #endif

            // The vertex shader definition with properties defined in the Varyings 
            // structure. The type of the vert function must match the type (struct)
            // that it returns.
            Varyings vert(Attributes IN, uint instanceID: SV_InstanceID)
            {
                // Declaring the output object (OUT) with the Varyings struct.
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float4 position = IN.positionOS;

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
                UNITY_SETUP_INSTANCE_ID(IN);
                
                // The SAMPLE_TEXTURE2D marco samples the texture with the given
                // sampler.
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                return color * _BaseColor;
            }
            ENDHLSL
        }
    }
}
