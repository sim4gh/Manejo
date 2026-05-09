// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Toby Fredson/The Toby Foliage Engine/(TTFE) Grass Foliage"
{
	Properties
	{
		[TTFE_DrawerTitle]_TTFELIGHTGRASSFOLIAGESHADER("(TTFE-LIGHT) GRASS FOLIAGE SHADER", Float) = 0
		_Cutoff( "Mask Clip Value", Float ) = 0.4
		[TTFE_DrawerFeatureBorder][Space (5)]_TEXTUREMAPS("TEXTURE MAPS", Float) = 0
		[NoScaleOffset][Space (5)][TTFE_Drawer_SingleLineTexture]_AlbedoMap("Albedo Map", 2D) = "white" {}
		[NoScaleOffset][Normal][TTFE_Drawer_SingleLineTexture]_NormalMap("Normal Map", 2D) = "bump" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture]_MaskMapRGBA("Mask Map *RGB(A)", 2D) = "white" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture]_SpecularMapGrayscale("Specular Map (Grayscale)", 2D) = "white" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture]_NoiseMapGrayscale("Noise Map (Grayscale)", 2D) = "white" {}
		[TTFE_DrawerDivider]_DIVIDER_01("DIVIDER_01", Float) = 0
		[TTFE_DrawerFeatureBorder]_TEXTURESETTINGS("TEXTURE SETTINGS", Float) = 0
		[Header((Albedo))]_AlbedoColor("Albedo Color", Color) = (1,1,1,0)
		[Header((Normal))]_NormalIntensity("Normal Intensity", Range( -3 , 3)) = 1
		[Header((Smoothness))]_SmoothnessIntensity("Smoothness Intensity", Range( -3 , 3)) = 1
		[Header((Ambient Occlusion))]_AmbientOcclusionIntensity("Ambient Occlusion Intensity", Range( 0 , 1)) = 1
		[Header((Specular))]_SpecularPower("Specular Power", Range( 0 , 1)) = 1
		[Toggle(_SPECULARBACKFACEOCCLUSION_ON)] _SpecularBackfaceOcclusion("Specular Backface Occlusion", Float) = 0
		_SpecularBias("Specular Bias", Float) = 1
		_SpecularScale("Specular Scale", Float) = -5
		_SpecularStrength("Specular Strength", Float) = 2
		_SpecularMapScale("Specular Map Scale", Float) = 1
		_SpecularMapOffset("Specular Map Offset", Float) = 0
		[Header((Translucency))]_TranslucencyPower("Translucency Power", Range( 1 , 10)) = 3
		_TranslucencyDirectIntensity("Translucency Direct Intensity", Float) = 1
		_TranslucencyFalloff("Translucency Falloff", Float) = 0
		_TranslucencyMapScale("Translucency Map Scale", Float) = 1
		_TranslucencyMapOffset("Translucency Map Offset", Float) = 0
		[TTFE_DrawerDivider]_DIVIDER_03("DIVIDER_03", Float) = 0
		[TTFE_DrawerFeatureBorder]_SEASONSETTINGS("SEASON SETTINGS", Float) = 0
		[Header((Season Control))]_ColorVariation("Color Variation", Range( 0 , 1)) = 0
		_RandomColorScale("Random Color Scale", Float) = 1
		_DryLeafColor("Dry Leaf Color", Color) = (0.5568628,0.3730685,0.1764706,0)
		_DryLeavesScale("Dry Leaves - Scale", Float) = 1
		_DryLeavesOffset("Dry Leaves - Offset", Float) = 1
		_SeasonChangeGlobal("Season Change - Global", Range( -2 , 2)) = 0
		[Toggle]_BranchMaskR("Branch Mask *(R)", Float) = 0
		[TTFE_DrawerDivider]_DIVIDER_02("DIVIDER_02", Float) = 0
		[TTFE_DrawerFeatureBorder]_TEXTUREMAPS("WIND SETTINGS", Float) = 0
		[Header((Global Wind Settings))]_GlobalWindStrength("Global Wind Strength", Range( 0 , 1)) = 1
		_StrongWindSpeed("Strong Wind Speed", Range( 1 , 3)) = 1
		[KeywordEnum(GentleBreeze,WindOff)] _WindType("Wind Type", Float) = 0
		[Toggle(_CONTINUOUSWINDGENTLE_ON)] _ContinuousWindGentle("Continuous Wind (Gentle)", Float) = 0
		_GrassSwayPowerPerMaterial("Grass Sway Power (Per Material)", Float) = 1
		[TTFE_DrawerDivider]_DIVIDER_05("DIVIDER_05", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "TransparentCutout"  "Queue" = "AlphaTest+0" "DisableBatching" = "True" "IsEmissive" = "true"  }
		Cull Off
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#include "UnityStandardUtils.cginc"
		#include "UnityCG.cginc"
		#pragma target 3.0
		#pragma shader_feature_local _WINDTYPE_GENTLEBREEZE _WINDTYPE_WINDOFF
		#pragma shader_feature_local _CONTINUOUSWINDGENTLE_ON
		#pragma shader_feature_local _SPECULARBACKFACEOCCLUSION_ON
		#define ASE_VERSION 19801
		#pragma surface surf StandardSpecular keepalpha addshadow fullforwardshadows dithercrossfade vertex:vertexDataFunc 
		struct Input
		{
			float3 worldPos;
			float2 uv_texcoord;
			float4 ase_positionOS4f;
			float3 worldNormal;
			INTERNAL_DATA
		};

		uniform float _GlobalWindStrength;
		uniform float _StrongWindSpeed;
		uniform float _GrassSwayPowerPerMaterial;
		uniform float _TEXTUREMAPS;
		uniform float _DIVIDER_05;
		uniform sampler2D _NormalMap;
		uniform float _NormalIntensity;
		uniform float4 _AlbedoColor;
		uniform float _TEXTURESETTINGS;
		uniform float _DIVIDER_01;
		uniform float _DIVIDER_02;
		uniform float _SEASONSETTINGS;
		uniform float _DIVIDER_03;
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
		uniform float _TranslucencyFalloff;
		uniform float _TranslucencyDirectIntensity;
		uniform float _TranslucencyMapScale;
		uniform float _TranslucencyMapOffset;
		uniform float _TranslucencyPower;
		uniform float _TTFELIGHTGRASSFOLIAGESHADER;
		uniform float _SpecularPower;
		uniform sampler2D _SpecularMapGrayscale;
		uniform float _SpecularMapScale;
		uniform float _SpecularMapOffset;
		uniform float _SpecularBias;
		uniform float _SpecularScale;
		uniform float _SpecularStrength;
		uniform float _SmoothnessIntensity;
		uniform float _AmbientOcclusionIntensity;
		uniform float _Cutoff = 0.4;


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
			float StrongWindSpeed1194_g2679 = _StrongWindSpeed;
			float mulTime1140_g2679 = _Time.y * 1.5;
			float3 ase_positionWS = mul( unity_ObjectToWorld, v.vertex );
			float2 appendResult1144_g2679 = (float2(ase_positionWS.x , ase_positionWS.z));
			float simpleNoise1079_g2679 = SimpleNoise( ( ( StrongWindSpeed1194_g2679 * mulTime1140_g2679 ) + appendResult1144_g2679 )*4.0 );
			float simpleNoise1077_g2679 = SimpleNoise( ( ( StrongWindSpeed1194_g2679 * _Time.y ) + appendResult1144_g2679 ) );
			float temp_output_1083_0_g2679 = ( (simpleNoise1077_g2679*2.2 + -1.05) * 1.6 );
			float3 ase_positionOS = v.vertex.xyz;
			float3 temp_output_1094_0_g2679 = ( ase_positionOS * 0.3 );
			float3 NormaliseRotationAxis1168_g2679 = float3(0,1,0);
			float simplePerlin2D1090_g2679 = snoise( ( ase_positionWS + ( _CosTime.w * 1.5 ) ).xy*1.2 );
			simplePerlin2D1090_g2679 = simplePerlin2D1090_g2679*0.5 + 0.5;
			float3 ase_objectScale = float3( length( unity_ObjectToWorld[ 0 ].xyz ), length( unity_ObjectToWorld[ 1 ].xyz ), length( unity_ObjectToWorld[ 2 ].xyz ) );
			float3 clampResult1155_g2679 = clamp( (ase_objectScale*1.8 + -0.8) , float3( 0,0,0 ) , float3( 1,1,1 ) );
			float3 Scale_MaskB1165_g2679 = clampResult1155_g2679;
			float3 rotatedValue1093_g2679 = RotateAroundAxis( float3( 0,0,0 ), temp_output_1094_0_g2679, NormaliseRotationAxis1168_g2679, ( simplePerlin2D1090_g2679 * 1.0 * Scale_MaskB1165_g2679 ).x );
			float3 worldToObjDir1125_g2679 = mul( unity_WorldToObject, float4( ( ( (simpleNoise1079_g2679*3.09 + -1.5) * 1.6 ) * temp_output_1083_0_g2679 * ( temp_output_1083_0_g2679 * ( temp_output_1094_0_g2679 - rotatedValue1093_g2679 ) ) * _SinTime.x * ase_positionOS.y ), 0.0 ) ).xyz;
			float2 appendResult1154_g2679 = (float2(ase_positionWS.x , ase_positionWS.z));
			float2 BasicWindPositionAndTime1151_g2679 = ( appendResult1154_g2679 + _Time.y );
			float simplePerlin2D1177_g2679 = snoise( BasicWindPositionAndTime1151_g2679*0.6 );
			simplePerlin2D1177_g2679 = simplePerlin2D1177_g2679*0.5 + 0.5;
			float NoiseLarge1179_g2679 = simplePerlin2D1177_g2679;
			float mulTime1100_g2679 = _Time.y * 2.0;
			float3 rotatedValue1109_g2679 = RotateAroundAxis( ( saturate( ase_positionOS.y ) * ase_positionOS ), ( cos( ( ( ase_positionWS * 0.2 ) + mulTime1100_g2679 ) ) * ase_positionOS.y * float3(0.5,0.05,0.5) * _GrassSwayPowerPerMaterial ), NormaliseRotationAxis1168_g2679, NoiseLarge1179_g2679 );
			float3 worldToObjDir1112_g2679 = mul( unity_WorldToObject, float4( rotatedValue1109_g2679, 0.0 ) ).xyz;
			float3 clampResult1161_g2679 = clamp( (ase_objectScale*1.0 + -0.6) , float3( 0,0,0 ) , float3( 1,1,1 ) );
			float3 Scale_MaskA1164_g2679 = clampResult1161_g2679;
			float simplePerlin2D1170_g2679 = snoise( BasicWindPositionAndTime1151_g2679*0.1 );
			float MaskRotation1174_g2679 = saturate( simplePerlin2D1170_g2679 );
			float3 clampResult1157_g2679 = clamp( (ase_objectScale*-0.7 + 2.2) , float3( 0,0,0 ) , float3( 1,1,1 ) );
			float3 Scale_MaskC1166_g2679 = clampResult1157_g2679;
			float simplePerlin2D1172_g2679 = snoise( BasicWindPositionAndTime1151_g2679*0.09995 );
			simplePerlin2D1172_g2679 = simplePerlin2D1172_g2679*0.5 + 0.5;
			float MaskRotation21175_g2679 = saturate( simplePerlin2D1172_g2679 );
			float3 temp_output_1120_0_g2679 = ( ase_positionOS * 0.2 );
			float3 rotatedValue1121_g2679 = RotateAroundAxis( float3( 0,0,0 ), temp_output_1120_0_g2679, NormaliseRotationAxis1168_g2679, ( ( NoiseLarge1179_g2679 * StrongWindSpeed1194_g2679 ) * ase_positionOS.y * Scale_MaskC1166_g2679 * Scale_MaskB1165_g2679 ).x );
			float3 worldToObjDir1130_g2679 = mul( unity_WorldToObject, float4( ( rotatedValue1121_g2679 - temp_output_1120_0_g2679 ), 0.0 ) ).xyz;
			#ifdef _CONTINUOUSWINDGENTLE_ON
				float3 staticSwitch1136_g2679 = worldToObjDir1130_g2679;
			#else
				float3 staticSwitch1136_g2679 = ( MaskRotation21175_g2679 * worldToObjDir1130_g2679 );
			#endif
			float3 temp_cast_3 = (0.0).xxx;
			#if defined( _WINDTYPE_GENTLEBREEZE )
				float3 staticSwitch1183_g2679 = (float3( 0,0,0 ) + (( ( worldToObjDir1125_g2679 - ( worldToObjDir1112_g2679 * ase_positionOS.y * Scale_MaskA1164_g2679 * MaskRotation1174_g2679 * Scale_MaskC1166_g2679 ) ) + staticSwitch1136_g2679 + _TEXTUREMAPS + _DIVIDER_05 ) - float3( 0,0,0 )) * (float3( 1,1,1 ) - float3( 0,0,0 )) / (float3( 1,1,1 ) - float3( 0,0,0 )));
			#elif defined( _WINDTYPE_WINDOFF )
				float3 staticSwitch1183_g2679 = temp_cast_3;
			#else
				float3 staticSwitch1183_g2679 = (float3( 0,0,0 ) + (( ( worldToObjDir1125_g2679 - ( worldToObjDir1112_g2679 * ase_positionOS.y * Scale_MaskA1164_g2679 * MaskRotation1174_g2679 * Scale_MaskC1166_g2679 ) ) + staticSwitch1136_g2679 + _TEXTUREMAPS + _DIVIDER_05 ) - float3( 0,0,0 )) * (float3( 1,1,1 ) - float3( 0,0,0 )) / (float3( 1,1,1 ) - float3( 0,0,0 )));
			#endif
			float3 FinalWind_Output1186_g2679 = ( _GlobalWindStrength * staticSwitch1183_g2679 );
			v.vertex.xyz += FinalWind_Output1186_g2679;
			v.vertex.w = 1;
			float3 LightDetectBackface_Output156_g2722 = float3(0,1,0);
			v.normal = LightDetectBackface_Output156_g2722;
			float4 ase_positionOS4f = v.vertex;
			o.ase_positionOS4f = ase_positionOS4f;
		}

		void surf( Input i , inout SurfaceOutputStandardSpecular o )
		{
			float2 uv_NormalMap189_g2722 = i.uv_texcoord;
			float3 Normal_Output154_g2722 = UnpackScaleNormal( tex2D( _NormalMap, uv_NormalMap189_g2722 ), _NormalIntensity );
			o.Normal = Normal_Output154_g2722;
			float CustomDRAWERS527_g2722 = ( _TEXTUREMAPS + _TEXTURESETTINGS + _DIVIDER_01 + _DIVIDER_02 + _SEASONSETTINGS + _DIVIDER_03 );
			float2 uv_AlbedoMap285_g2722 = i.uv_texcoord;
			float2 uv_AlbedoMap295_g2722 = i.uv_texcoord;
			float4 tex2DNode295_g2722 = tex2D( _AlbedoMap, uv_AlbedoMap295_g2722 );
			float2 uv_NoiseMapGrayscale302_g2722 = i.uv_texcoord;
			float4 transform535_g2722 = mul(unity_ObjectToWorld,float4( 1,1,1,1 ));
			float4 break536_g2722 = transform535_g2722;
			float RandomColorFix541_g2722 = floor( ( ( break536_g2722.x + break536_g2722.z ) * _RandomColorScale ) );
			float2 temp_cast_0 = (RandomColorFix541_g2722).xx;
			float dotResult4_g2723 = dot( temp_cast_0 , float2( 12.9898,78.233 ) );
			float lerpResult10_g2723 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g2723 ) * 43758.55 ) ));
			float3 ase_positionOS = i.ase_positionOS4f.xyz;
			float3 normalizeResult259_g2722 = normalize( ase_positionOS );
			float DryLeafPositionMask263_g2722 = ( (distance( normalizeResult259_g2722 , float3( 0,0.8,0 ) )*1.0 + 0.0) * 1 );
			float4 lerpResult294_g2722 = lerp( ( _DryLeafColor * ( tex2DNode295_g2722.g * 2 ) ) , tex2DNode295_g2722 , saturate( (( ( tex2D( _NoiseMapGrayscale, uv_NoiseMapGrayscale302_g2722 ).r * saturate( lerpResult10_g2723 ) * DryLeafPositionMask263_g2722 ) - _SeasonChangeGlobal )*_DryLeavesScale + _DryLeavesOffset) ));
			float4 SeasonControl_Output311_g2722 = lerpResult294_g2722;
			Gradient gradient271_g2722 = NewGradient( 0, 2, 2, float4( 1, 0.276868, 0, 0 ), float4( 0, 1, 0.7818019, 1 ), 0, 0, 0, 0, 0, 0, float2( 1, 0 ), float2( 1, 1 ), 0, 0, 0, 0, 0, 0 );
			float2 temp_cast_1 = (RandomColorFix541_g2722).xx;
			float dotResult4_g2724 = dot( temp_cast_1 , float2( 12.9898,78.233 ) );
			float lerpResult10_g2724 = lerp( 0.0 , 1.0 , frac( ( sin( dotResult4_g2724 ) * 43758.55 ) ));
			float4 lerpResult282_g2722 = lerp( SeasonControl_Output311_g2722 , ( ( SeasonControl_Output311_g2722 * 0.5 ) + ( SampleGradient( gradient271_g2722, saturate( lerpResult10_g2724 ) ) * SeasonControl_Output311_g2722 ) ) , _ColorVariation);
			float2 uv_MaskMapRGBA313_g2722 = i.uv_texcoord;
			float4 lerpResult283_g2722 = lerp( tex2D( _AlbedoMap, uv_AlbedoMap285_g2722 ) , lerpResult282_g2722 , (( _BranchMaskR )?( tex2D( _MaskMapRGBA, uv_MaskMapRGBA313_g2722 ).r ):( 1.0 )));
			float4 GrassColorVariation_Output109_g2722 = lerpResult283_g2722;
			float3 ase_positionWS = i.worldPos;
			#if defined(LIGHTMAP_ON) && UNITY_VERSION < 560 //aseld
			float3 ase_lightDirWS = 0;
			#else //aseld
			float3 ase_lightDirWS = Unity_SafeNormalize( UnityWorldSpaceLightDir( ase_positionWS ) );
			#endif //aseld
			float3 normalizeResult478_g2722 = normalize( ( ase_lightDirWS + _WorldSpaceLightPos0.xyz ) );
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_positionWS );
			float3 normalizeResult397_g2722 = normalize( ase_viewVectorWS );
			float dotResult395_g2722 = dot( normalizeResult478_g2722 , normalizeResult397_g2722 );
			float temp_output_386_0_g2722 = saturate( max( -dotResult395_g2722 , 0.0 ) );
			float temp_output_377_0_g2722 = ( temp_output_386_0_g2722 * temp_output_386_0_g2722 );
			float temp_output_378_0_g2722 = ( temp_output_377_0_g2722 * temp_output_377_0_g2722 );
			float2 uv_MaskMapRGBA58_g2722 = i.uv_texcoord;
			#if defined(LIGHTMAP_ON) && ( UNITY_VERSION < 560 || ( defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK) && defined(SHADOWS_SCREEN) ) )//aselc
			float4 ase_lightColor = 0;
			#else //aselc
			float4 ase_lightColor = _LightColor0;
			#endif //aselc
			float TobyTranslucency66_g2722 = ( saturate( ( ( ( temp_output_378_0_g2722 * temp_output_378_0_g2722 ) + _TranslucencyFalloff ) * _TranslucencyDirectIntensity ) ) * saturate( (tex2D( _MaskMapRGBA, uv_MaskMapRGBA58_g2722 ).b*_TranslucencyMapScale + _TranslucencyMapOffset) ) * max( ase_lightColor.a , 0.0 ) );
			float TranslucencyIntensity72_g2722 = _TranslucencyPower;
			float4 Albedo_Output160_g2722 = ( ( ( _AlbedoColor + CustomDRAWERS527_g2722 ) * GrassColorVariation_Output109_g2722 ) * (1.0 + (TobyTranslucency66_g2722 - 0.0) * (TranslucencyIntensity72_g2722 - 1.0) / (1.0 - 0.0)) );
			o.Albedo = Albedo_Output160_g2722.rgb;
			float3 temp_cast_3 = (_TTFELIGHTGRASSFOLIAGESHADER).xxx;
			o.Emission = temp_cast_3;
			float temp_output_349_0_g2722 = ( 0.2 * _SpecularPower );
			float2 uv_SpecularMapGrayscale251_g2722 = i.uv_texcoord;
			float3 ase_tangentWS = WorldNormalVector( i, float3( 1, 0, 0 ) );
			float fresnelNdotV373_g2722 = dot( ase_tangentWS, ase_lightDirWS );
			float fresnelNode373_g2722 = ( _SpecularBias + _SpecularScale * pow( max( 1.0 - fresnelNdotV373_g2722 , 0.0001 ), _SpecularStrength ) );
			float SpecRecalculate368_g2722 = saturate( fresnelNode373_g2722 );
			#ifdef _SPECULARBACKFACEOCCLUSION_ON
				float staticSwitch340_g2722 = ( temp_output_349_0_g2722 * saturate( (tex2D( _SpecularMapGrayscale, uv_SpecularMapGrayscale251_g2722 ).r*_SpecularMapScale + _SpecularMapOffset) ) * SpecRecalculate368_g2722 );
			#else
				float staticSwitch340_g2722 = temp_output_349_0_g2722;
			#endif
			float Specular_Output158_g2722 = staticSwitch340_g2722;
			float3 temp_cast_4 = (Specular_Output158_g2722).xxx;
			o.Specular = temp_cast_4;
			float2 uv_MaskMapRGBA89_g2722 = i.uv_texcoord;
			float4 tex2DNode89_g2722 = tex2D( _MaskMapRGBA, uv_MaskMapRGBA89_g2722 );
			float Smoothness_Output159_g2722 = saturate( ( tex2DNode89_g2722.a * _SmoothnessIntensity ) );
			o.Smoothness = Smoothness_Output159_g2722;
			float AoMapBase103_g2722 = tex2DNode89_g2722.g;
			float saferPower118_g2722 = abs( AoMapBase103_g2722 );
			float Ao_Output157_g2722 = ( pow( saferPower118_g2722 , _AmbientOcclusionIntensity ) * ( 1.5 / ( ( saturate( TobyTranslucency66_g2722 ) * TranslucencyIntensity72_g2722 ) + 1.5 ) ) );
			o.Occlusion = Ao_Output157_g2722;
			o.Alpha = 1;
			float2 uv_AlbedoMap86_g2722 = i.uv_texcoord;
			float Opacity_Output155_g2722 = tex2D( _AlbedoMap, uv_AlbedoMap86_g2722 ).a;
			clip( Opacity_Output155_g2722 - _Cutoff );
		}

		ENDCG
	}
	Fallback "Diffuse"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.FunctionNode;4236;-390.4046,278.8535;Inherit;False;(TTFE) Grass Foliage_Wind System;37;;2679;587ec12ad6a469d48af93c852f23db70;0;0;1;FLOAT3;1187
Node;AmplifyShaderEditor.RangedFloatNode;4212;-400,-64;Inherit;False;Property;_TTFELIGHTGRASSFOLIAGESHADER;(TTFE-LIGHT) GRASS FOLIAGE SHADER;0;0;Create;True;0;0;0;False;1;TTFE_DrawerTitle;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;4251;-368,16;Inherit;False;(TTFE) Grass Foliage_Shading;2;;2722;c37c82b3855ad564a8be75e0d3f5f6bc;0;0;7;COLOR;0;FLOAT3;180;FLOAT;186;FLOAT;182;FLOAT;183;FLOAT;184;FLOAT3;188
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;0,0;Float;False;True;-1;2;;0;0;StandardSpecular;Toby Fredson/The Toby Foliage Engine/(TTFE) Grass Foliage;False;False;False;False;False;False;False;False;False;False;False;False;True;True;False;False;False;False;False;False;True;Off;0;False;;0;False;;False;0;False;;0;False;;False;0;Masked;0.4;True;True;0;False;TransparentCutout;;AlphaTest;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;1;-1;-1;-1;0;False;0;0;False;;-1;0;False;_TTFETREEFOLIAGESHADER;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;0;0;4251;0
WireConnection;0;1;4251;180
WireConnection;0;2;4212;0
WireConnection;0;3;4251;186
WireConnection;0;4;4251;182
WireConnection;0;5;4251;183
WireConnection;0;10;4251;184
WireConnection;0;11;4236;1187
WireConnection;0;12;4251;188
ASEEND*/
//CHKSM=CB400223366164324FF6157DFB5CCAE6EEF98CF3