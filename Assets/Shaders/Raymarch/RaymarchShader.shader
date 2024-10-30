Shader "PeerPlay/RaymarchShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            
            uniform float4x4 _CamFrustum;
            uniform float4x4 _CamToWorld;
            uniform float _maxDistance;


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 ray : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                half index = v.vertex.z;
                v.vertex.z = 0;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                o.ray = _CamFrustum[(int)index].xyz;
                o.ray /= abs(o.ray.z);
                o.ray = mul(_CamToWorld,o.ray);

                return o;
            }

            //create sphere for raymarching
            float sdSphere(float3 p, float s)
            {
                return length(p)-s;
            }
            
            float distanceField(float3 p)
            {
                float Sphere1 = sdSphere(p-float3(0,0,0),2.0);

                return Sphere1;
            }
            fixed4 raymarching(float3 ro, float3 rd)
            {
                fixed4 result = fixed4(1,1,1,1);
                const int max_iteration = 64;
                //distance travled along the ray direction
                float t=0; 

                for(int i=0; i<max_iteration; i++)
                {
                    if(t > _maxDistance)
                    {
                        //start to draw environment
                        result = fixed4(rd,1);
                        break;
                    }

                    //position
                    float3 p = ro + rd * t;
                    //check for hit in distance field
                    //d <0 inside of sphere
                    //d > 0 outside
                    float d = distanceField(p);

                    //we have hit something
                    if(d<0.01)
                    {
                        result = fixed4(1,1,1,1);
                        break;
                    }

                    t += d;

                  
                }


                return result;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 rayDirection = normalize(i.ray.xyz);
                float3 rayOrigin = _WorldSpaceCameraPos;
                fixed4 result = raymarching(rayOrigin,rayDirection);

                return result;
            }
            ENDCG
        }
    }
}
