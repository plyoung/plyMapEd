Shader "Hidden/plyMapEd_Grid"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_Tint("Tint Colour", Color) = (1, 1, 1, 1)
		_Back("Back Colour", Color) = (0, 0, 0, 0)
		_Size("Size", float) = 0.5
		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4 // deafult to "LessEqual"
		_Offset("Offset", float) = -1
	}

	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
			"PreviewType" = "Plane"
		}

		Blend srcAlpha oneMinusSrcAlpha
		Cull Off
		ZWrite Off
		ZTest [_ZTest]
		Offset[_Offset],[_Offset]

		Pass
		{
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 fade : TEXCOORD1;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _Tint;
			fixed4 _Back;
			float _Size;

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.fade = float4(_MainTex_ST.xyz * 0.5, _MainTex_ST.x * _Size);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				// blend the background colour and grid
				fixed4 c1 = _Back;
				fixed4 c2 = tex2D(_MainTex, i.uv) * _Tint;
				fixed4 col = fixed4(lerp(c1.rgb, c2.rgb, c2.a).rgb, lerp(c1.a, c2.a, c2.a));

				// create a circular fade
				float3 offset = distance(i.fade, i.uv);
				float range = dot(offset, offset) / (i.fade.w * i.fade.w);
				col.a *= saturate(1.0f - range);

				return col;
			}

			ENDCG
		}
	}
}
