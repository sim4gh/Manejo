// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Toby Fredson/The Toby Foliage Engine/(TTFE) Tree Foliage"
{
	Properties
	{
		[TTFE_DrawerTitle]_TTFELIGHTTREEFOLIAGESHADER("(TTFE-LIGHT) TREE FOLIAGE SHADER", Float) = 0
		_Cutoff( "Mask Clip Value", Float ) = 0.2
		[TTFE_DrawerFeatureBorder][Space (5)]_TEXTUREMAPS("TEXTURE MAPS", Float) = 0
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture][Space (5)]_AlbedoMap("Albedo Map", 2D) = "white" {}
		[NoScaleOffset][Normal][TTFE_Drawer_SingleLineTexture]_NormalMap("Normal Map", 2D) = "bump" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture]_MaskMapRGBA("Mask Map *RGB(A)", 2D) = "white" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture]_SpecularMap("Specular Map (Grayscale)", 2D) = "white" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture]_NoiseMapGrayscale("Noise Map (Grayscale)", 2D) = "white" {}
		[TTFE_DrawerDivider]_DIVIDER_01("DIVIDER_01", Float) = 0
		[TTFE_DrawerFeatureBorder]_TEXTURESETTINGS("TEXTURE SETTINGS", Float) = 0
		[Header((Albedo))]_AlbedoColor("Albedo Color", Color) = (1,1,1,0)
		[Header((Normal))]_NormalIntenisty("Normal Intenisty", Float) = 1
		[Toggle]_NormalBackFaceFixBranch("Normal Back Face Fix (Branch)", Float) = 0
		[Header((Smoothness))]_SmoothnessIntensity("Smoothness Intensity", Range( 0 , 3)) = 1
		[Header((Ambient Occlusion))]_AmbientOcclusionIntensity("Ambient Occlusion Intensity", Range( 0 , 1)) = 1
		[Header((Specular))]_SpecularPower("Specular Power", Range( 0 , 1)) = 1
		[Toggle(_SPECULARBACKFACEOCCLUSION1_ON)] _SpecularBackfaceOcclusion1("Specular Backface Occlusion", Float) = 0
		_SpecularBias("Specular Bias", Float) = 1
		_SpecularScale("Specular Scale", Float) = -5
		_SpecularStrength("Specular Strength", Float) = 2
		_SpecularMapScale("Specular Map Scale", Float) = 1
		_SpecularMapOffset("Specular Map Offset", Float) = 0
		[Header((Translucency))]_TranslucencyPower("Translucency Power", Range( 1 , 10)) = 1
		_TranslucencyDirectIntensity("Translucency Direct Intensity", Float) = 1
		_TranslucencyFalloff("Translucency Falloff", Float) = 0
		_TranslucencyMapScale("Translucency Map Scale", Float) = 1
		_TranslucencyMapOffset("Translucency Map Offset", Float) = 0
		[TTFE_DrawerDivider]_DIVIDER_02("DIVIDER_02", Float) = 0
		[TTFE_DrawerFeatureBorder]_LIGHTINGSETTINGS("LIGHTING SETTINGS", Float) = 0
		[Header((Self Shading))]_VertexLighting("Vertex Lighting", Float) = 0
		_VertexShadow("Vertex Shadow", Float) = 0
		[Toggle(_SELFSHADINGVERTEXCOLOR_ON)] _SelfShadingVertexColor("Self Shading (Vertex Color)", Float) = 0
		[Toggle]_LightDetectBackface("Light Detect (Back face)", Float) = 1
		[Toggle]_WorldUp("World Up", Float) = 0
		[TTFE_DrawerDivider]_DIVIDER_03("DIVIDER_03", Float) = 0
		[TTFE_DrawerFeatureBorder]_SEASONSETTINGS("SEASON SETTINGS", Float) = 0
		[Header((Season Control))]_ColorVariation("Color Variation", Range( 0 , 1)) = 1
		_RandomColorScale("Random Color Scale", Float) = 1
		_DryLeafColor("Dry Leaf Color", Color) = (0.5568628,0.3730685,0.1764706,0)
		_DryLeavesScale("Dry Leaves - Scale", Float) = 0
		_DryLeavesOffset("Dry Leaves - Offset", Float) = 0
		_SeasonChangeGlobal("Season Change - Global", Range( -2 , 2)) = 0
		[Toggle]_BranchMaskR("Branch Mask *(R)", Float) = 0
		[TTFE_DrawerDivider]_DIVIDER_04("DIVIDER_04", Float) = 0
		[TTFE_DrawerFeatureBorder]_TEXTUREMAPS("WIND SETTINGS", Float) = 0
		[Header((Global Wind Settings))]_GlobalWindStrength("Global Wind Strength", Range( 0 , 1)) = 1
		_StrongWindSpeed("Strong Wind Speed", Range( 1 , 3)) = 1
		[KeywordEnum(GentleBreeze,WindOff)] _WindType("Wind Type", Float) = 0
		[Header((Trunk and Branch))]_BranchWindLarge("Branch Wind Large", Range( 0 , 20)) = 1
		_BranchWindSmall("Branch Wind Small", Range( 0 , 20)) = 1
		[Toggle(_LEAFFLUTTER_ON)] _LeafFlutter("Leaf Flutter", Float) = 1
		_GlobalFlutterIntensity("Global Flutter Intensity", Range( 0 , 20)) = 1
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture]_WindNoise("Wind Noise Map", 2D) = "white" {}
		[Toggle]_PivotSway("Pivot Sway", Float) = 0
		_PivotSwayPower("Pivot Sway Power", Float) = 1
		[TTFE_DrawerDivider]_DIVIDER_05("DIVIDER_05", Float) = 0
		[TTFE_DrawerFeatureBorder]_WINDMASKSETTINGS("WIND MASK SETTINGS", Float) = 0
		[Header((Wind Mask))]_Radius("Radius", Float) = 1
		_Hardness("Hardness", Float) = 1
		[Toggle]_CenterofMass("Center of Mass", Float) = 0
		[Toggle]_SwitchVGreenToRGBA("Switch VGreen To RGBA", Float) = 0
		[TTFE_DrawerDivider]_DIVIDER_06("DIVIDER_06", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "TransparentCutout"  "Queue" = "AlphaTest+0" "DisableBatching" = "True" "IsEmissive" = "true"  }
		Cull Off
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#include "UnityCG.cginc"
		#include "UnityStandardUtils.cginc"
		#pragma target 3.0
		#pragma shader_feature_local _WINDTYPE_GENTLEBREEZE _WINDTYPE_WINDOFF
		#pragma shader_feature_local _LEAFFLUTTER_ON
		#pragma shader_feature_local _SELFSHADINGVERTEXCOLOR_ON
		#pragma shader_feature_local _SPECULARBACKFACEOCCLUSION1_ON
		#define ASE_VERSION 19801
		#pragma surface surf StandardSpecular keepalpha addshadow fullforwardshadows dithercrossfade vertex:vertexDataFunc 
		struct Input
		{
			float3 worldPos;
			float2 uv_texcoord;
			half ASEIsFrontFacing : VFACE;
			float4 ase_positionOS4f;
			float3 worldNormal;
			INTERNAL_DATA
		};

		uniform float _GlobalWindStrength;
		uniform float _Radius;
		uniform float _Hardness;
		uniform float _BranchWindLarge;
		uniform float _CenterofMass;
		uniform float _BranchWindSmall;
		uniform float _StrongWindSpeed;
		uniform float _SwitchVGreenToRGBA;
		uniform sampler2D _WindNoise;
		uniform float _GlobalFlutterIntensity;
		uniform float _PivotSway;
		uniform float _PivotSwayPower;
		uniform float _TEXTUREMAPS;
		uniform float _DIVIDER_05;
		uniform float _WINDMASKSETTINGS;
		uniform float _DIVIDER_06;
		uniform float _WorldUp;
		uniform float _LightDetectBackface;
		uniform sampler2D _NormalMap;
		uniform float _NormalBackFaceFixBranch;
		uniform float _NormalIntenisty;
		uniform float4 _AlbedoColor;
		uniform float _TEXTURESETTINGS;
		uniform float _DIVIDER_01;
		uniform float _DIVIDER_02;
		uniform float _SEASONSETTINGS;
		uniform float _DIVIDER_03;
		uniform float _LIGHTINGSETTINGS;
		uniform float _DIVIDER_04;
		uniform sampler2D _AlbedoMap;
		uniform float4 _DryLeafColor;
		uniform sampler2D _NoiseMapGrayscale;
		uniform float _RandomColorScale;
		uniform float _SeasonChangeGlobal;
		uniform float _DryLeavesScale;
		uniform float _DryLeavesOffset;
		uniform float _ColorVariation;
		uniform float _BranchMaskR;
		uniform sampler2D _MaskMapRGBA;
		uniform float _VertexLighting;
		uniform float _VertexShadow;
		uniform float _TranslucencyFalloff;
		uniform float _TranslucencyDirectIntensity;
		uniform float _TranslucencyMapScale;
		uniform float _TranslucencyMapOffset;
		uniform float _TranslucencyPower;
		uniform float _TTFELIGHTTREEFOLIAGESHADER;
		uniform float _SpecularPower;
		uniform sampler2D _SpecularMap;
		uniform float _SpecularMapScale;
		uniform float _SpecularMapOffset;
		uniform float _SpecularBias;
		uniform float _SpecularScale;
		uniform float _SpecularStrength;
		uniform float _SmoothnessIntensity;
		uniform float _AmbientOcclusionIntensity;
		uniform float _Cutoff = 0.2;


		float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }

		float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }

		float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }

		float snoise( float2 v )
		{
			const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
			float2 i = floor( v + dot( v, C.yy ) );
			float2 x0 = v - i + dot( i, C.xx );
			float2 i1;
			i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
			float4 x12 = x0.xyxy + C.xxzz;
			x12.xy -= i1;
			i = mod2D289( i );
			float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
			float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
			m = m * m;
			m = m * m;
			float3 x = 2.0 * frac( p * C.www ) - 1.0;
			float3 h = abs( x ) - 0.5;
			float3 ox = floor( x + 0.5 );
			float3 a0 = x - ox;
			m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
			float3 g;
			g.x = a0.x * x0.x + h.x * x0.y;
			g.yz = a0.yz * x12.xz + h.yz * x12.yw;
			return 130.0 * dot( m, g );
		}


		inline float noise_randomValue (float2 uv) { return frac(sin(dot(uv, float2(12.9898, 78.233)))*43758.5453); }

		inline float noise_interpolate (float a, float b, float t) { return (1.0-t)*a + (t*b); }

		inline float valueNoise (float2 uv)
		{
			float2 i = floor(uv);
			float2 f = frac( uv );
			f = f* f * (3.0 - 2.0 * f);
			uv = abs( frac(uv) - 0.5);
			float2 c0 = i + float2( 0.0, 0.0 );
			float2 c1 = i + float2( 1.0, 0.0 );
			float2 c2 = i + float2( 0.0, 1.0 );
			float2 c3 = i + float2( 1.0, 1.0 );
			float r0 = noise_randomValue( c0 );
			float r1 = noise_randomValue( c1 );
			float r2 = noise_randomValue( c2 );
			float r3 = noise_randomValue( c3 );
			float bottomOfGrid = noise_interpolate( r0, r1, f.x );
			float topOfGrid = noise_interpolate( r2, r3, f.x );
			float t = noise_interpolate( bottomOfGrid, topOfGrid, f.y );
			return t;
		}


		float SimpleNoise(float2 UV)
		{
			float t = 0.0;
			float freq = pow( 2.0, float( 0 ) );
			float amp = pow( 0.5, float( 3 - 0 ) );
			t += valueNoise( UV/freq )*amp;
			freq = pow(2.0, float(1));
			amp = pow(0.5, float(3-1));
			t += valueNoise( UV/freq )*amp;
			freq = pow(2.0, float(2));
			amp = pow(0.5, float(3-2));
			t += valueNoise( UV/freq )*amp;
			return t;
		}


		float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
		{
			original -= center;
			float C = cos( angle );
			float S = sin( angle );
			float t = 1 - C;
			float m00 = t * u.x * u.x + C;
			float m01 = t * u.x * u.y - S * u.z;
			float m02 = t * u.x * u.z + S * u.y;
			float m10 = t * u.x * u.y + S * u.z;
			float m11 = t * u.y * u.y + C;
			float m12 = t * u.y * u.z - S * u.x;
			float m20 = t * u.x * u.z - S * u.y;
			float m21 = t * u.y * u.z + S * u.x;
			float m22 = t * u.z * u.z + C;
			float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
			return mul( finalMatrix, original ) + center;
		}


		struct Gradient
		{
			int type;
			int colorsLength;
			int alphasLength;
			float4 colors[8];
			float2 alphas[8];
		};


		Gradient NewGradient(int type, int colorsLength, int alphasLength, 
		float4 colors0, float4 colors1, float4 colors2, float4 colors3, float4 colors4, float4 colors5, float4 colors6, float4 colors7,
		float2 alphas0, float2 alphas1, float2 alphas2, float2 alphas3, float2 alphas4, float2 alphas5, float2 alphas6, float2 alphas7)
		{
			Gradient g;
			g.type = type;
			g.colorsLength = colorsLength;
			g.alphasLength = alphasLength;
			g.colors[ 0 ] = colors0;
			g.colors[ 1 ] = colors1;
			g.colors[ 2 ] = colors2;
			g.colors[ 3 ] = colors3;
			g.colors[ 4 ] = colors4;
			g.colors[ 5 ] = colors5;
			g.colors[ 6 ] = colors6;
			g.colors[ 7 ] = colors7;
			g.alphas[ 0 ] = alphas0;
			g.alphas[ 1 ] = alphas1;
			g.alphas[ 2 ] = alphas2;
			g.alphas[ 3 ] = alphas3;
			g.alphas[ 4 ] = alphas4;
			g.alphas[ 5 ] = alphas5;
			g.alphas[ 6 ] = alphas6;
			g.alphas[ 7 ] = alphas7;
			return g;
		}


		float4 SampleGradient( Gradient gradient, float time )
		{
			float3 color = gradient.colors[0].rgb;
			UNITY_UNROLL
			for (int c = 1; c < 8; c++)
			{
			float colorPos = saturate((time - gradient.colors[c-1].w) / ( 0.00001 + (gradient.colors[c].w - gradient.colors[c-1].w)) * step(c, (float)gradient.colorsLength-1));
			color = lerp(color, gradient.colors[c].rgb, lerp(colorPos, step(0.01, colorPos), gradient.type));
			}
			#ifndef UNITY_COLORSPACE_GAMMA
			color = half3(GammaToLinearSpaceExact(color.r), GammaToLinearSpaceExact(color.g), GammaToLinearSpaceExact(color.b));
			#endif
			float alpha = gradient.alphas[0].x;
			UNITY_UNROLL
			for (int a = 1; a < 8; a++)
			{
			float alphaPos = saturate((time - gradient.alphas[a-1].y) / ( 0.00001 + (gradient.alphas[a].y - gradient.alphas[a-1].y)) * step(a, (float)gradient.alphasLength-1));
			alpha = lerp(alpha, gradient.alphas[a].x, lerp(alphaPos, step(0.01, alphaPos), gradient.type));
			}
			return float4(color, alpha);
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float3 ase_positionWS = mul( unity_ObjectToWorld, v.vertex );
			float3 normalizeResult710_g2884 = normalize( ase_positionWS );
			float mulTime716_g2884 = _Time.y * 0.25;
			float simplePerlin2D714_g2884 = snoise( ( normalizeResult710_g2884 + mulTime716_g2884 ).xy*0.43 );
			float WindMask_LargeB725_g2884 = ( simplePerlin2D714_g2884 * 1.5 );
			float3 ase_positionOS = v.vertex.xyz;
			float3 appendResult820_g2884 = (float3(0.0 , 0.0 , saturate( ase_positionOS ).z));
			float3 break862_g2884 = ase_positionOS;
			float3 appendResult819_g2884 = (float3(break862_g2884.x , ( break862_g2884.y * 0.15 ) , 0.0));
			float mulTime849_g2884 = _Time.y * 2.1;
			float3 temp_output_573_0_g2884 = ( ( ase_positionOS - float3(0,-1,0) ) / _Radius );
			float dotResult574_g2884 = dot( temp_output_573_0_g2884 , temp_output_573_0_g2884 );
			float temp_output_577_0_g2884 = pow( saturate( dotResult574_g2884 ) , _Hardness );
			float SphearicalMaskCM735_g2884 = saturate( temp_output_577_0_g2884 );
			float3 temp_cast_1 = (ase_positionOS.y).xxx;
			float2 appendResult810_g2884 = (float2(ase_positionOS.x , ase_positionOS.z));
			float3 temp_output_869_0_g2884 = ( cross( temp_cast_1 , float3( appendResult810_g2884 ,  0.0 ) ) * 0.005 );
			float3 appendResult813_g2884 = (float3(0.0 , ase_positionOS.y , 0.0));
			float3 break845_g2884 = ase_positionOS;
			float3 appendResult843_g2884 = (float3(break845_g2884.x , 0.0 , ( break845_g2884.z * 0.15 )));
			float mulTime850_g2884 = _Time.y * 2.3;
			float dotResult730_g2884 = dot( (ase_positionOS*0.02 + 0.0) , ase_positionOS );
			float CeneterOfMassThickness_Mask734_g2884 = saturate( dotResult730_g2884 );
			float3 appendResult854_g2884 = (float3(ase_positionOS.x , 0.0 , 0.0));
			float3 break857_g2884 = ase_positionOS;
			float3 appendResult842_g2884 = (float3(0.0 , ( break857_g2884.y * 0.2 ) , ( break857_g2884.z * 0.4 )));
			float mulTime851_g2884 = _Time.y * 2.0;
			float3 normalizeResult1560_g2884 = normalize( ase_positionOS );
			float CenterOfMassTrunkUP_C1561_g2884 = saturate( distance( normalizeResult1560_g2884 , float3(0,1,0) ) );
			float3 normalizeResult718_g2884 = normalize( ase_positionWS );
			float mulTime723_g2884 = _Time.y * 0.26;
			float simplePerlin2D722_g2884 = snoise( ( normalizeResult718_g2884 + mulTime723_g2884 ).xy*0.7 );
			float WindMask_LargeC726_g2884 = ( simplePerlin2D722_g2884 * 1.5 );
			float mulTime795_g2884 = _Time.y * 3.2;
			float3 worldToObj796_g2884 = mul( unity_WorldToObject, float4( ase_positionOS, 1 ) ).xyz;
			float3 temp_output_763_0_g2884 = ( mulTime795_g2884 + float3(0.4,0.3,0.1) + ( worldToObj796_g2884.x * 0.02 ) + ( 0.14 * worldToObj796_g2884.y ) + ( worldToObj796_g2884.z * 0.16 ) );
			float3 normalizeResult581_g2884 = normalize( ase_positionOS );
			float CenterOfMassTrunkUP586_g2884 = saturate( (distance( normalizeResult581_g2884 , float3(0,1,0) )*1.0 + -0.05) );
			float3 ase_objectScale = float3( length( unity_ObjectToWorld[ 0 ].xyz ), length( unity_ObjectToWorld[ 1 ].xyz ), length( unity_ObjectToWorld[ 2 ].xyz ) );
			float mulTime794_g2884 = _Time.y * 2.3;
			float3 worldToObj797_g2884 = mul( unity_WorldToObject, float4( ase_positionOS, 1 ) ).xyz;
			float3 temp_output_757_0_g2884 = ( mulTime794_g2884 + ( 0.2 * worldToObj797_g2884 ) + float3(0.4,0.3,0.1) );
			float mulTime793_g2884 = _Time.y * 3.6;
			float3 temp_cast_5 = (ase_positionOS.x).xxx;
			float3 worldToObj799_g2884 = mul( unity_WorldToObject, float4( temp_cast_5, 1 ) ).xyz;
			float temp_output_787_0_g2884 = ( mulTime793_g2884 + ( 0.2 * worldToObj799_g2884.x ) );
			float3 normalizeResult647_g2884 = normalize( ase_positionOS );
			float CenterOfMass651_g2884 = saturate( (distance( normalizeResult647_g2884 , float3(0,1,0) )*2.0 + 0.0) );
			float SphericalMaskProxySphere655_g2884 = (( _CenterofMass )?( ( temp_output_577_0_g2884 * CenterOfMass651_g2884 ) ):( temp_output_577_0_g2884 ));
			float StrongWindSpeed994_g2884 = _StrongWindSpeed;
			float2 appendResult1379_g2884 = (float2(ase_positionWS.x , ase_positionWS.z));
			float3 worldToObj1380_g2884 = mul( unity_WorldToObject, float4( float3( appendResult1379_g2884 ,  0.0 ), 1 ) ).xyz;
			float simpleNoise1430_g2884 = SimpleNoise( ( ( StrongWindSpeed994_g2884 * _Time.y ) + worldToObj1380_g2884 ).xy*4.0 );
			simpleNoise1430_g2884 = simpleNoise1430_g2884*2 - 1;
			float4 ase_tangentOS = v.tangent;
			float3 worldToObj1376_g2884 = mul( unity_WorldToObject, float4( ase_positionOS, 1 ) ).xyz;
			float mulTime1321_g2884 = _Time.y * 10.0;
			float3 temp_output_1316_0_g2884 = ( sin( ( ( worldToObj1376_g2884 * ( 1.0 * 10.0 * ase_objectScale ) ) + mulTime1321_g2884 + 1.0 ) ) * 0.028 );
			float3 MotionFlutterConstant1481_g2884 = ( temp_output_1316_0_g2884 * 33 );
			float4 temp_cast_12 = (v.color.g).xxxx;
			float4 LeafVertexColor_Main1540_g2884 = (( _SwitchVGreenToRGBA )?( v.color ):( temp_cast_12 ));
			float mulTime1349_g2884 = _Time.y * 0.4;
			float3 worldToObj1443_g2884 = mul( unity_WorldToObject, float4( ase_tangentOS.xyz, 1 ) ).xyz;
			float2 panner1354_g2884 = ( mulTime1349_g2884 * float2( 1,1 ) + ( worldToObj1443_g2884 * 0.1 ).xy);
			float2 uv_TexCoord1355_g2884 = v.texcoord.xy * float2( 0.2,0.2 ) + panner1354_g2884;
			float3 normalizeResult589_g2884 = normalize( ase_positionWS );
			float mulTime590_g2884 = _Time.y * 0.2;
			float simplePerlin2D592_g2884 = snoise( ( normalizeResult589_g2884 + mulTime590_g2884 ).xy*0.4 );
			float WindMask_LargeA595_g2884 = ( simplePerlin2D592_g2884 * 1.5 );
			float3 worldToObjDir1435_g2884 = mul( unity_WorldToObject, float4( ( tex2Dlod( _WindNoise, float4( uv_TexCoord1355_g2884, 0, 0.0) ) * WindMask_LargeA595_g2884 * WindMask_LargeC726_g2884 ).rgb, 0.0 ) ).xyz;
			float dotResult4_g2885 = dot( float2( 0.2,0.2 ) , float2( 12.9898,78.233 ) );
			float lerpResult10_g2885 = lerp( 0.0 , 0.35 , frac( ( sin( dotResult4_g2885 ) * 43758.55 ) ));
			float2 appendResult1454_g2884 = (float2(ase_positionWS.x , ase_positionWS.z));
			float simpleNoise1455_g2884 = SimpleNoise( ( appendResult1454_g2884 + ( StrongWindSpeed994_g2884 * _Time.y ) )*4.0 );
			simpleNoise1455_g2884 = simpleNoise1455_g2884*2 - 1;
			float simplePerlin2D1395_g2884 = snoise( ( ( StrongWindSpeed994_g2884 * _Time.y ) + ( ase_tangentOS.xyz * 1.0 ) ).xy );
			#ifdef _LEAFFLUTTER_ON
				float4 staticSwitch1263_g2884 = ( ( ( ( simpleNoise1430_g2884 * 0.9 ) * float4( float3(-1,-0.5,-1) , 0.0 ) * float4( ase_tangentOS.xyz , 0.0 ) * saturate( ase_positionOS.y ) * float4( MotionFlutterConstant1481_g2884 , 0.0 ) * WindMask_LargeC726_g2884 * LeafVertexColor_Main1540_g2884 ) + ( ( float4( worldToObjDir1435_g2884 , 0.0 ) * float4( float3(-1,-1,-1) , 0.0 ) * saturate( ase_positionOS.y ) * LeafVertexColor_Main1540_g2884 * float4( ase_objectScale , 0.0 ) ) * 1 ) + ( ( float4( float3(-1,-1,-1) , 0.0 ) * lerpResult10_g2885 * simpleNoise1455_g2884 * saturate( ase_positionOS.y ) * LeafVertexColor_Main1540_g2884 * float4( ase_tangentOS.xyz , 0.0 ) ) * 2 ) + ( ( simplePerlin2D1395_g2884 * 0.11 ) * float4( float3(5.9,5.9,5.9) , 0.0 ) * float4( ase_tangentOS.xyz , 0.0 ) * saturate( ase_positionOS.y ) * WindMask_LargeA595_g2884 * LeafVertexColor_Main1540_g2884 ) + ( ( float4( temp_output_1316_0_g2884 , 0.0 ) * saturate( ase_positionOS.y ) * LeafVertexColor_Main1540_g2884 ) * 3 ) ) * _GlobalFlutterIntensity );
			#else
				float4 staticSwitch1263_g2884 = float4( 0,0,0,0 );
			#endif
			float3 worldToObj1580_g2884 = mul( unity_WorldToObject, float4( ase_positionOS, 1 ) ).xyz;
			float mulTime1587_g2884 = _Time.y * 4.0;
			float mulTime1579_g2884 = _Time.y * 0.2;
			float2 appendResult1576_g2884 = (float2(ase_positionWS.x , ase_positionWS.z));
			float2 normalizeResult1578_g2884 = normalize( appendResult1576_g2884 );
			float simpleNoise1588_g2884 = SimpleNoise( ( mulTime1579_g2884 + normalizeResult1578_g2884 )*1.0 );
			float WindMask_SimpleSway1593_g2884 = ( ( simpleNoise1588_g2884 * 1.5 ) * _PivotSwayPower );
			float3 rotatedValue1599_g2884 = RotateAroundAxis( float3( 0,0,0 ), ase_positionOS, normalize( float3(0.6,1,0.1) ), ( ( cos( ( ( worldToObj1580_g2884 * 0.02 ) + mulTime1587_g2884 + ( float3(0.6,1,0.8) * 0.3 * worldToObj1580_g2884 ) ) ) * 0.1 ) * WindMask_SimpleSway1593_g2884 * saturate( ase_objectScale ) ).x );
			float4 temp_cast_27 = (0.0).xxxx;
			#if defined( _WINDTYPE_GENTLEBREEZE )
				float4 staticSwitch1496_g2884 = ( ( float4( ( ( WindMask_LargeB725_g2884 * ( ( ( ( ( appendResult820_g2884 + ( appendResult819_g2884 * cos( mulTime849_g2884 ) ) + ( cross( float3(1.2,0.6,1) , ( float3(0.7,1,0.8) * appendResult819_g2884 ) ) * sin( mulTime849_g2884 ) ) ) * SphearicalMaskCM735_g2884 * temp_output_869_0_g2884 ) * 0.08 ) + ( ( ( appendResult813_g2884 + ( appendResult843_g2884 * cos( mulTime850_g2884 ) ) + ( cross( float3(0.9,1,1.2) , ( float3(1,1,1) * appendResult843_g2884 ) ) * sin( mulTime850_g2884 ) ) ) * SphearicalMaskCM735_g2884 * CeneterOfMassThickness_Mask734_g2884 * temp_output_869_0_g2884 ) * 0.1 ) + ( ( ( appendResult854_g2884 + ( appendResult842_g2884 * cos( mulTime851_g2884 ) ) + ( cross( float3(1.1,1.3,0.8) , ( float3(1.4,0.8,1.1) * appendResult842_g2884 ) ) * sin( mulTime851_g2884 ) ) ) * SphearicalMaskCM735_g2884 * temp_output_869_0_g2884 ) * 0.05 ) ) * _BranchWindLarge ) ) * CenterOfMassTrunkUP_C1561_g2884 ) , 0.0 ) + float4( ( ( ( WindMask_LargeC726_g2884 * ( ( ( ( cos( temp_output_763_0_g2884 ) * sin( temp_output_763_0_g2884 ) * CenterOfMassTrunkUP586_g2884 * SphearicalMaskCM735_g2884 * CeneterOfMassThickness_Mask734_g2884 * saturate( ase_objectScale ) ) * 0.2 ) + ( ( cos( temp_output_757_0_g2884 ) * sin( temp_output_757_0_g2884 ) * CenterOfMassTrunkUP586_g2884 * CeneterOfMassThickness_Mask734_g2884 * SphearicalMaskCM735_g2884 * saturate( ase_objectScale ) ) * 0.2 ) + ( ( sin( temp_output_787_0_g2884 ) * cos( temp_output_787_0_g2884 ) * SphericalMaskProxySphere655_g2884 * CeneterOfMassThickness_Mask734_g2884 * CenterOfMassTrunkUP586_g2884 ) * 0.2 ) ) * _BranchWindSmall ) ) * 0.3 ) * CenterOfMassTrunkUP_C1561_g2884 ) , 0.0 ) + ( staticSwitch1263_g2884 * 0.3 ) + float4( (( _PivotSway )?( ( ( rotatedValue1599_g2884 - ase_positionOS ) * 0.4 ) ):( float3( 0,0,0 ) )) , 0.0 ) ) * saturate( ase_positionOS.y ) );
			#elif defined( _WINDTYPE_WINDOFF )
				float4 staticSwitch1496_g2884 = temp_cast_27;
			#else
				float4 staticSwitch1496_g2884 = ( ( float4( ( ( WindMask_LargeB725_g2884 * ( ( ( ( ( appendResult820_g2884 + ( appendResult819_g2884 * cos( mulTime849_g2884 ) ) + ( cross( float3(1.2,0.6,1) , ( float3(0.7,1,0.8) * appendResult819_g2884 ) ) * sin( mulTime849_g2884 ) ) ) * SphearicalMaskCM735_g2884 * temp_output_869_0_g2884 ) * 0.08 ) + ( ( ( appendResult813_g2884 + ( appendResult843_g2884 * cos( mulTime850_g2884 ) ) + ( cross( float3(0.9,1,1.2) , ( float3(1,1,1) * appendResult843_g2884 ) ) * sin( mulTime850_g2884 ) ) ) * SphearicalMaskCM735_g2884 * CeneterOfMassThickness_Mask734_g2884 * temp_output_869_0_g2884 ) * 0.1 ) + ( ( ( appendResult854_g2884 + ( appendResult842_g2884 * cos( mulTime851_g2884 ) ) + ( cross( float3(1.1,1.3,0.8) , ( float3(1.4,0.8,1.1) * appendResult842_g2884 ) ) * sin( mulTime851_g2884 ) ) ) * SphearicalMaskCM735_g2884 * temp_output_869_0_g2884 ) * 0.05 ) ) * _BranchWindLarge ) ) * CenterOfMassTrunkUP_C1561_g2884 ) , 0.0 ) + float4( ( ( ( WindMask_LargeC726_g2884 * ( ( ( ( cos( temp_output_763_0_g2884 ) * sin( temp_output_763_0_g2884 ) * CenterOfMassTrunkUP586_g2884 * SphearicalMaskCM735_g2884 * CeneterOfMassThickness_Mask734_g2884 * saturate( ase_objectScale ) ) * 0.2 ) + ( ( cos( temp_output_757_0_g2884 ) * sin( temp_output_757_0_g2884 ) * CenterOfMassTrunkUP586_g2884 * CeneterOfMassThickness_Mask734_g2884 * SphearicalMaskCM735_g2884 * saturate( ase_objectScale ) ) * 0.2 ) + ( ( sin( temp_output_787_0_g2884 ) * cos( temp_output_787_0_g2884 ) * SphericalMaskProxySphere655_g2884 * CeneterOfMassThickness_Mask734_g2884 * CenterOfMassTrunkUP586_g2884 ) * 0.2 ) ) * _BranchWindSmall ) ) * 0.3 ) * CenterOfMassTrunkUP_C1561_g2884 ) , 0.0 ) + ( staticSwitch1263_g2884 * 0.3 ) + float4( (( _PivotSway )?( ( ( rotatedValue1599_g2884 - ase_positionOS ) * 0.4 ) ):( float3( 0,0,0 ) )) , 0.0 ) ) * saturate( ase_positionOS.y ) );
			#endif
			float4 FinalWind_Output163_g2884 = ( ( _GlobalWindStrength * staticSwitch1496_g2884 ) + _TEXTUREMAPS + _DIVIDER_05 + _WINDMASKSETTINGS + _DIVIDER_06 );
			v.vertex.xyz += FinalWind_Output163_g2884.rgb;
			v.vertex.w = 1;
			float3 ase_normalOS = v.normal.xyz;
			#if defined(LIGHTMAP_ON) && UNITY_VERSION < 560 //aseld
			float3 ase_lightDirWS = 0;
			#else //aseld
			float3 ase_lightDirWS = Unity_SafeNormalize( UnityWorldSpaceLightDir( ase_positionWS ) );
			#endif //aseld
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_positionWS );
			float3 ase_viewDirWS = normalize( ase_viewVectorWS );
			float3 ase_normalWS = UnityObjectToWorldNormal( v.normal );
			float3 ase_normalWSNorm = normalize( ase_normalWS );
			float dotResult494_g2958 = dot( ase_viewDirWS , ase_normalWSNorm );
			float2 uv_NormalMap789_g2958 = v.texcoord;
			float3 ifLocalVar497_g2958 = 0;
			if( dotResult494_g2958 > 0.0 )
				ifLocalVar497_g2958 = UnpackScaleNormal( -tex2Dlod( _NormalMap, float4( uv_NormalMap789_g2958, 0, 0.0) ), -1.0 );
			else if( dotResult494_g2958 == 0.0 )
				ifLocalVar497_g2958 = UnpackScaleNormal( -tex2Dlod( _NormalMap, float4( uv_NormalMap789_g2958, 0, 0.0) ), -1.0 );
			else if( dotResult494_g2958 < 0.0 )
				ifLocalVar497_g2958 = -ase_normalOS;
			float4 transform500_g2958 = mul(unity_ObjectToWorld,float4( ifLocalVar497_g2958 , 0.0 ));
			float dotResult504_g2958 = dot( float4( ase_lightDirWS , 0.0 ) , transform500_g2958 );
			float3 ifLocalVar511_g2958 = 0;
			if( dotResult504_g2958 >= 0.0 )
				ifLocalVar511_g2958 = ifLocalVar497_g2958;
			else
				ifLocalVar511_g2958 = -ifLocalVar497_g2958;
			float3 break514_g2958 = ifLocalVar511_g2958;
			float3 temp_cast_34 = (dotResult504_g2958).xxx;
			float4 appendResult525_g2958 = (float4(break514_g2958.x , ( break514_g2958.y + saturate( ( 1.0 - ( ( distance( float3( 0,0,0 ) , temp_cast_34 ) - 0.2 ) / max( 0.2 , 1E-05 ) ) ) ) ) , break514_g2958.z , 0.0));
			float4 LightDetectBackface595_g2958 = appendResult525_g2958;
			float4 LightDetect_Output597_g2958 = (( _WorldUp )?( float4( float3(0,1,0) , 0.0 ) ):( (( _LightDetectBackface )?( LightDetectBackface595_g2958 ):( float4( ase_normalOS , 0.0 ) )) ));
			v.normal = LightDetect_Output597_g2958.xyz;
			float4 ase_positionOS4f = v.vertex;
			o.ase_positionOS4f = ase_positionOS4f;
		}

		void surf( Input i , inout SurfaceOutputStandardSpecular o )
		{
			float2 uv_NormalMap531_g2958 = i.uv_texcoord;
			float3 tex2DNode531_g2958 = UnpackScaleNormal( tex2D( _NormalMap, uv_NormalMap531_g2958 ), _NormalIntenisty );
			float3 break539_g2958 = tex2DNode531_g2958;
			float3 appendResult552_g2958 = (float3(break539_g2958.x , break539_g2958.y , ( break539_g2958.z * i.ASEIsFrontFacing )));
			float3 Normal_Output557_g2958 = (( _NormalBackFaceFixBranch )?( appendResult552_g2958 ):( tex2DNode531_g2958 ));
			o.Normal = Normal_Output557_g2958;
			float CustomDRAWERS867_g2958 = ( _TEXTUREMAPS + _TEXTURESETTINGS + _DIVIDER_01 + _DIVIDER_02 + _SEASONSETTINGS + _DIVIDER_03 + _LIGHTINGSETTINGS + _DIVIDER_04 );
			float2 uv_AlbedoMap513_g2958 = i.uv_texcoord;
			float2 uv_AlbedoMap662_g2958 = i.uv_texcoord;
			float4 tex2DNode662_g2958 = tex2D( _AlbedoMap, uv_AlbedoMap662_g2958 );
			float2 uv_NoiseMapGrayscale669_g2958 = i.uv_texcoord;
			float4 transform894_g2958 = mul(unity_ObjectToWorld,float4( 1,1,1,1 ));
			float4 break889_g2958 = transform894_g2958;
			float RandomColorFix893_g2958 = floor( ( ( break889_g2958.x + break889_g2958.z ) * _RandomColorScale ) );
			float2 temp_cast_0 = (RandomColorFix893_g2958).xx;
			float dotResult4_g2960 = dot( temp_cast_0 , float2( 12.9898,78.233 ) );
			float lerpResult10_g2960 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g2960 ) * 43758.55 ) ));
			float3 ase_positionOS = i.ase_positionOS4f.xyz;
			float3 normalizeResult439_g2958 = normalize( ase_positionOS );
			float DryLeafPositionMask443_g2958 = ( (distance( normalizeResult439_g2958 , float3( 0,0.8,0 ) )*1.0 + 0.0) * 1 );
			float4 lerpResult677_g2958 = lerp( ( _DryLeafColor * ( tex2DNode662_g2958.g * 2 ) ) , tex2DNode662_g2958 , saturate( (( ( tex2D( _NoiseMapGrayscale, uv_NoiseMapGrayscale669_g2958 ).r * saturate( lerpResult10_g2960 ) * DryLeafPositionMask443_g2958 ) - _SeasonChangeGlobal )*_DryLeavesScale + _DryLeavesOffset) ));
			float4 SeasonControl_Output676_g2958 = lerpResult677_g2958;
			Gradient gradient752_g2958 = NewGradient( 0, 2, 2, float4( 1, 0.276868, 0, 0 ), float4( 0, 1, 0.7818019, 1 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
			float2 temp_cast_1 = (RandomColorFix893_g2958).xx;
			float dotResult4_g2961 = dot( temp_cast_1 , float2( 12.9898,78.233 ) );
			float lerpResult10_g2961 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g2961 ) * 43758.55 ) ));
			float4 lerpResult515_g2958 = lerp( SeasonControl_Output676_g2958 , ( ( SeasonControl_Output676_g2958 * 0.5 ) + ( SampleGradient( gradient752_g2958, saturate( lerpResult10_g2961 ) ) * SeasonControl_Output676_g2958 ) ) , _ColorVariation);
			float2 uv_MaskMapRGBA505_g2958 = i.uv_texcoord;
			float4 lerpResult521_g2958 = lerp( tex2D( _AlbedoMap, uv_AlbedoMap513_g2958 ) , lerpResult515_g2958 , (( _BranchMaskR )?( tex2D( _MaskMapRGBA, uv_MaskMapRGBA505_g2958 ).r ):( 1.0 )));
			float3 temp_output_465_0_g2958 = ( ( ase_positionOS * float3( 2,1.3,2 ) ) / 25.0 );
			float dotResult471_g2958 = dot( temp_output_465_0_g2958 , temp_output_465_0_g2958 );
			float saferPower480_g2958 = abs( saturate( dotResult471_g2958 ) );
			float3 normalizeResult457_g2958 = normalize( ase_positionOS );
			float SelfShading601_g2958 = saturate( (( pow( saferPower480_g2958 , 1.5 ) + ( ( 1.0 - (distance( normalizeResult457_g2958 , float3( 0,0.8,0 ) )*0.5 + 0.0) ) * 0.6 ) )*0.92 + -0.16) );
			#ifdef _SELFSHADINGVERTEXCOLOR_ON
				float4 staticSwitch618_g2958 = ( lerpResult521_g2958 * (SelfShading601_g2958*_VertexLighting + _VertexShadow) );
			#else
				float4 staticSwitch618_g2958 = lerpResult521_g2958;
			#endif
			float4 GrassColorVariation_Output586_g2958 = staticSwitch618_g2958;
			float3 ase_positionWS = i.worldPos;
			#if defined(LIGHTMAP_ON) && UNITY_VERSION < 560 //aseld
			float3 ase_lightDirWS = 0;
			#else //aseld
			float3 ase_lightDirWS = Unity_SafeNormalize( UnityWorldSpaceLightDir( ase_positionWS ) );
			#endif //aseld
			float3 normalizeResult857_g2958 = normalize( ( ase_lightDirWS + _WorldSpaceLightPos0.xyz ) );
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_positionWS );
			float3 normalizeResult860_g2958 = normalize( ase_viewVectorWS );
			float dotResult851_g2958 = dot( normalizeResult857_g2958 , normalizeResult860_g2958 );
			float temp_output_854_0_g2958 = saturate( max( -dotResult851_g2958 , 0.0 ) );
			float temp_output_844_0_g2958 = ( temp_output_854_0_g2958 * temp_output_854_0_g2958 );
			float temp_output_845_0_g2958 = ( temp_output_844_0_g2958 * temp_output_844_0_g2958 );
			float2 uv_MaskMapRGBA516_g2958 = i.uv_texcoord;
			#if defined(LIGHTMAP_ON) && ( UNITY_VERSION < 560 || ( defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK) && defined(SHADOWS_SCREEN) ) )//aselc
			float4 ase_lightColor = 0;
			#else //aselc
			float4 ase_lightColor = _LightColor0;
			#endif //aselc
			float TobyTranslucency526_g2958 = ( saturate( ( ( ( temp_output_845_0_g2958 * temp_output_845_0_g2958 ) + _TranslucencyFalloff ) * _TranslucencyDirectIntensity ) ) * (tex2D( _MaskMapRGBA, uv_MaskMapRGBA516_g2958 ).b*_TranslucencyMapScale + _TranslucencyMapOffset) * max( ase_lightColor.a , 0.0 ) );
			float TranslucencyIntensity616_g2958 = _TranslucencyPower;
			float4 Albedo_Output613_g2958 = ( ( ( _AlbedoColor + CustomDRAWERS867_g2958 ) * GrassColorVariation_Output586_g2958 ) * (1.0 + (TobyTranslucency526_g2958 - 0.0) * (TranslucencyIntensity616_g2958 - 1.0) / (1.0 - 0.0)) );
			o.Albedo = Albedo_Output613_g2958.rgb;
			float3 temp_cast_3 = (_TTFELIGHTTREEFOLIAGESHADER).xxx;
			o.Emission = temp_cast_3;
			float temp_output_809_0_g2958 = ( 0.2 * _SpecularPower );
			float2 uv_SpecularMap702_g2958 = i.uv_texcoord;
			float3 ase_tangentWS = WorldNormalVector( i, float3( 1, 0, 0 ) );
			float3 ase_normalWS = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float3 ase_bitangentWS = WorldNormalVector( i, float3( 0, 1, 0 ) );
			float3x3 ase_tangentToWorldFast = float3x3(ase_tangentWS.x,ase_bitangentWS.x,ase_normalWS.x,ase_tangentWS.y,ase_bitangentWS.y,ase_normalWS.y,ase_tangentWS.z,ase_bitangentWS.z,ase_normalWS.z);
			float fresnelNdotV835_g2958 = dot( mul(ase_tangentToWorldFast,ase_tangentWS), ase_lightDirWS );
			float fresnelNode835_g2958 = ( _SpecularBias + _SpecularScale * pow( max( 1.0 - fresnelNdotV835_g2958 , 0.0001 ), _SpecularStrength ) );
			float SpecRecalculate829_g2958 = saturate( fresnelNode835_g2958 );
			#ifdef _SPECULARBACKFACEOCCLUSION1_ON
				float staticSwitch790_g2958 = ( temp_output_809_0_g2958 * saturate( (tex2D( _SpecularMap, uv_SpecularMap702_g2958 ).r*_SpecularMapScale + _SpecularMapOffset) ) * SpecRecalculate829_g2958 );
			#else
				float staticSwitch790_g2958 = temp_output_809_0_g2958;
			#endif
			float Specular_Output570_g2958 = staticSwitch790_g2958;
			float3 temp_cast_4 = (Specular_Output570_g2958).xxx;
			o.Specular = temp_cast_4;
			float2 uv_MaskMapRGBA535_g2958 = i.uv_texcoord;
			float4 tex2DNode535_g2958 = tex2D( _MaskMapRGBA, uv_MaskMapRGBA535_g2958 );
			float Smoothness_Output558_g2958 = saturate( ( tex2DNode535_g2958.a * _SmoothnessIntensity ) );
			o.Smoothness = Smoothness_Output558_g2958;
			float AoMapBase538_g2958 = tex2DNode535_g2958.g;
			float saferPower580_g2958 = abs( AoMapBase538_g2958 );
			float AmbientOcclusion_Output582_g2958 = ( pow( saferPower580_g2958 , _AmbientOcclusionIntensity ) * ( 1.5 / ( ( saturate( TobyTranslucency526_g2958 ) * TranslucencyIntensity616_g2958 ) + 1.5 ) ) );
			o.Occlusion = AmbientOcclusion_Output582_g2958;
			o.Alpha = 1;
			float2 uv_AlbedoMap555_g2958 = i.uv_texcoord;
			float Opacity_Output559_g2958 = tex2D( _AlbedoMap, uv_AlbedoMap555_g2958 ).a;
			clip( Opacity_Output559_g2958 - _Cutoff );
		}

		ENDCG
	}
	Fallback "Diffuse"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.RangedFloatNode;3035;928,48;Inherit;False;Property;_TTFELIGHTTREEFOLIAGESHADER;(TTFE-LIGHT) TREE FOLIAGE SHADER;0;0;Create;True;0;0;0;False;1;TTFE_DrawerTitle;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;3049;928,384;Inherit;False;(TTFE) Tree Foliage_Wind System;45;;2884;ccec0b38fced125459cc01da4402fa7a;0;0;1;COLOR;0
Node;AmplifyShaderEditor.FunctionNode;3068;960,144;Inherit;False;(TTFE) Tree Foliage_Shading;2;;2958;32f9493bbb6c2d44ab3d59bde623860f;0;0;7;COLOR;152;FLOAT3;153;FLOAT;24;FLOAT;27;FLOAT;25;FLOAT;26;FLOAT4;28
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;1338.528,120.0737;Float;False;True;-1;2;;0;0;StandardSpecular;Toby Fredson/The Toby Foliage Engine/(TTFE) Tree Foliage;False;False;False;False;False;False;False;False;False;False;False;False;True;True;False;False;False;False;False;False;True;Off;0;False;;0;False;;False;0;False;;0;False;;False;0;Masked;0.2;True;True;0;False;TransparentCutout;;AlphaTest;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;0;0;3068;152
WireConnection;0;1;3068;153
WireConnection;0;2;3035;0
WireConnection;0;3;3068;24
WireConnection;0;4;3068;27
WireConnection;0;5;3068;25
WireConnection;0;10;3068;26
WireConnection;0;11;3049;0
WireConnection;0;12;3068;28
ASEEND*/
//CHKSM=A60D74123C1CF4A2CE2218CB440511489BFDFE66