Shader "RVT/RenderTile" 
{
	Properties {
		[HideInInspector] _Control("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Diffuse3("Layer 3 (A)", 2D) = "grey" {}
        [HideInInspector] _Diffuse2("Layer 2 (B)", 2D) = "grey" {}
        [HideInInspector] _Diffuse1("Layer 1 (G)", 2D) = "grey" {}
        [HideInInspector] _Diffuse0("Layer 0 (R)", 2D) = "grey" {}
        [HideInInspector] _Normal3("Normal 3 (A)", 2D) = "bump" {}
        [HideInInspector] _Normal2("Normal 2 (B)", 2D) = "bump" {}
        [HideInInspector] _Normal1("Normal 1 (G)", 2D) = "bump" {}
        [HideInInspector] _Normal0("Normal 0 (R)", 2D) = "bump" {}
	}
	
	SubShader {
		// Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline"}
		Cull Off
		Pass {
			Name "RenderTile"
			HLSLPROGRAM
			// #define TARGET45
			#if defined(TARGET45)
				#pragma target 4.5
			#else
				#pragma target 3.0
			#endif
			
			#pragma multi_compile_instancing

            #pragma vertex RenderTileVertex
            #pragma fragment RenderTileFragment
		
			#include "RenderTile.hlsl"
            ENDHLSL
		}
		
		Pass {
			Name "RenderTileSingle"
			HLSLPROGRAM
			
			#pragma target 3.0

            #pragma vertex RenderTileVertex
            #pragma fragment RenderTileFragment
		
			#include "RenderTileSingle.hlsl"
            ENDHLSL
		}
		
		Pass {
			
			Name "AddPass"
			Blend One One
			HLSLPROGRAM
			
			#pragma target 3.0

            #pragma vertex RenderTileVertex
            #pragma fragment RenderTileFragment
			#define ADD_PASS
			#include "RenderTileSingle.hlsl"
            ENDHLSL
		}

	}
}