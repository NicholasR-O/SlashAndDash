Shader "SlashAndDash/Skybox/LatLong"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (1, 1, 1, 1)
        _Exposure ("Exposure", Range(0, 8)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
        _MainTex ("Spherical (HDR)", 2D) = "grey" {}
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _Tint;
            half _Exposure;
            float _Rotation;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            float3 RotateAroundY(float3 direction, float degrees)
            {
                float radians = degrees * UNITY_PI / 180.0;
                float sine;
                float cosine;
                sincos(radians, sine, cosine);
                return float3(
                    direction.x * cosine - direction.z * sine,
                    direction.y,
                    direction.x * sine + direction.z * cosine
                );
            }

            float2 DirectionToLatLong(float3 direction)
            {
                direction = normalize(direction);
                float longitude = atan2(direction.x, direction.z);
                float latitude = asin(direction.y);
                return float2(0.5 + longitude / (2.0 * UNITY_PI), 0.5 + latitude / UNITY_PI);
            }

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = RotateAroundY(input.vertex.xyz, _Rotation);
                return output;
            }

            half4 frag(v2f input) : SV_Target
            {
                half4 color = tex2D(_MainTex, DirectionToLatLong(input.direction));
                return half4(color.rgb * _Tint.rgb * _Exposure, 1);
            }
            ENDCG
        }
    }
}
