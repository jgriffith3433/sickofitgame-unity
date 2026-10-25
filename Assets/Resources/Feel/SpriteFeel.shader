// Sick Of It: a sprite's hit flash, squash and recoil (Core/Fx/SpriteFeel.cs, game.json "feel").
// Sprites-Default plus three per-renderer values: _FlashAmount blends the sprite toward _FlashColor (a white silhouette),
// _Squash.xy scales it about its pivot and _Squash.zw moves it, in the sprite's own space. The transform and the collider
// never move. Batching is off so the vertices stay in the sprite's own space; only sprites mid-hit wear this material.
Shader "SickOfIt/SpriteFeel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
        _Squash ("Squash (scale xy, offset zw)", Vector) = (1,1,0,0)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "DisableBatching"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex FeelVert
            #pragma fragment FeelFrag
            #pragma target 2.0
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 _FlashColor;
            fixed _FlashAmount;
            float4 _Squash;

            v2f FeelVert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 pos = UnityFlipSprite(IN.vertex, _Flip);
                pos.xy = pos.xy * _Squash.xy + _Squash.zw;
                OUT.vertex = UnityObjectToClipPos(pos);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;
                return OUT;
            }

            fixed4 FeelFrag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                c.rgb = lerp(c.rgb, _FlashColor.rgb, _FlashAmount);
                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
