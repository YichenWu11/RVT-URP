Shader "RVT/RenderPageTable" 
{
	Properties { }
	
	SubShader {
		Pass {
			Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline"}
			Cull Off
			HLSLPROGRAM

			// #define TARGET45
			
			#if defined(TARGET45)
				#pragma target 4.5
			#else
				#pragma target 3.0
			#endif
			#pragma multi_compile_instancing
			
            #pragma vertex RenderPageTableVertex
            #pragma fragment RenderPageTableFragment
		
			#include "RenderPageTable.hlsl"
            ENDHLSL
		}
	}
}