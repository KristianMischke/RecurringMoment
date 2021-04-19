// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/TimeMachineShader"
{
    Properties
    {
        _MainColor ("Color", Color) = (1,1,1,1)
        _MainTex ("Mask", 2D) = "white" {}
    }
    SubShader
    {
        Tags {
            "Queue" = "Transparent"
		    "RenderType" = "Transparent"
		    "PreviewType" = "Plane"
            }
        LOD 100
		Cull Off
		Lighting Off
		ZWrite Off
		Fog { Color(0, 0, 0, 0) }

        Pass
		{
		    Blend SrcAlpha OneMinusSrcAlpha

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
			};

			sampler2D _MainTex;
			float4 _MainColor;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float4 col = tex2D(_MainTex, i.uv);
				col.a = tex2D(_MainTex, i.uv).r;
                col *= _MainColor;
				return col;
			}
			ENDCG
		}
    }
}
