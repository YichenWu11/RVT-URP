Shader "RVT/FeedbackDebug" {
	Properties {
		
	}
	SubShader {
		Pass {
			
			Cull Off
			HLSLPROGRAM
        	    #pragma target 5.0
				
        	    #pragma vertex FeedbackDebugVertex
        	    #pragma fragment FeedbackDebugFragment
	
				float4x4 _ImageMVP;
				struct Attributes
				{
					float4 positionOS : POSITION;
				    half2 texcoord : TEXCOORD0;
				
				};
				
				struct Varyings
				{
				    float4 pos : SV_POSITION;
				    half2 uv : TEXCOORD0;
				};

				StructuredBuffer<uint> _DebugBuffer;
				// _FeedBackParam x: scaleFactor, y: height, z:width w: mask
				float4 _FeedBackParam;
				
				Varyings FeedbackDebugVertex(Attributes v)
				{
					Varyings o;
					o.uv = v.texcoord;
					o.pos = mul(_ImageMVP, v.positionOS);
					return o;
				}
				half4 FeedbackDebugFragment(Varyings IN) :SV_TARGET
				{
					uint screenX = IN.pos.x;
					uint screenY = IN.pos.y;

					int request = _DebugBuffer[screenY * _FeedBackParam.z + screenX];
					int mipLevel = request >> 24;
					int pageX = (request & 0xffffff) >> 12;
					int pageY = (request & 0xfff);
					
					return half4(pageX/512.0f, pageY/512.0f, mipLevel/10.0f, 1.0f);
					// return half4(pageX/512.0f, 0, 0, 1.0f);
					// return half4(0, 0, mipLevel/10.0f, 1.0f);
				}
			ENDHLSL
		}
	}
}