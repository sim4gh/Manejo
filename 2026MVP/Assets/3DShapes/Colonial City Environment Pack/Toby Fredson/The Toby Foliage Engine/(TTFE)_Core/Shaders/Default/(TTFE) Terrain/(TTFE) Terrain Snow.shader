// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Toby Fredson/The Toby Foliage Engine/(TTFE) Terrain Snow"
{
	Properties
	{
		[TTFE_DrawerTitle]_TTFELIGHTTERRAINSNOWSHADER("(TTFE-LIGHT) TERRAIN SNOW SHADER", Float) = 0
		[TTFE_DrawerFeatureBorder][Space (5)]_TEXTUREMAPS("TEXTURE MAPS", Float) = 0
		[NoScaleOffset][Space (5)]_ColorMapProjection("Color Map Projection", 2D) = "white" {}
		[NoScaleOffset][Normal]_NormalMap("Normal Map", 2D) = "bump" {}
		[TTFE_DrawerDivider]_DIVIDER_2("DIVIDER_01", Float) = 0
		[TTFE_DrawerFeatureBorder]_TEXTURESETTINGS("TEXTURE SETTINGS", Float) = 0
		[Header(_____________________________________________________)][Header(Texture Settings)][Header((Color Map))]_ColorMapIntensity("Color Map Intensity", Range( 0 , 1)) = 1
		[Toggle]_ColorMap("Color Map", Float) = 1
		[Header((Normal))]_NormalIntensity("Normal Intensity", Range( 0 , 1)) = 1
		[Toggle]_TerrainNormalMap("Terrain Normal Map", Float) = 0
		_SmoothnessIntensity("Smoothness Intensity", Range( 0 , 1)) = 0
		[Header((Specular))]_SpecularOffset("Specular Offset", Float) = 0.02
		_SpecularPower("Specular Power", Range( 0 , 1)) = 1
		[TTFE_DrawerDivider]_DIVIDER_01("DIVIDER_01", Float) = 0
		[Toggle(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)] _EnableInstancedPerPixelNormal("Enable Instanced Per-Pixel Normal", Float) = 0
		[HideInInspector]_TerrainHolesTexture("_TerrainHolesTexture", 2D) = "white" {}
		[HideInInspector]_Control("Control", 2D) = "white" {}
		[HideInInspector]_Splat0("Splat0", 2D) = "white" {}
		[HideInInspector]_Normal0("Normal0", 2D) = "bump" {}
		[HideInInspector]_NormalScale0("NormalScale0", Float) = 1
		[HideInInspector]_Mask0("Mask0", 2D) = "gray" {}
		[HideInInspector]_Metallic0("Metallic0", Range( 0 , 1)) = 0
		[HideInInspector]_Smoothness0("Smoothness 0", Range( 0 , 1)) = 0
		[HideInInspector]_Splat1("Splat1", 2D) = "white" {}
		[HideInInspector]_Normal1("Normal1", 2D) = "bump" {}
		[HideInInspector]_NormalScale1("NormalScale1", Float) = 1
		[HideInInspector]_Mask1("Mask1", 2D) = "gray" {}
		[HideInInspector]_Metallic1("Metallic1", Range( 0 , 1)) = 0
		[HideInInspector]_Smoothness1("Smoothness1", Range( 0 , 1)) = 0
		[HideInInspector]_Splat2("Splat2", 2D) = "white" {}
		[HideInInspector]_Normal2("Normal2", 2D) = "bump" {}
		[HideInInspector]_NormalScale2("NormalScale2", Float) = 1
		[HideInInspector]_Mask2("Mask2", 2D) = "gray" {}
		[HideInInspector]_Metallic2("Metallic2", Range( 0 , 1)) = 0
		[HideInInspector]_Smoothness2("Smoothness2", Range( 0 , 1)) = 0
		[HideInInspector]_Splat3("Splat3", 2D) = "white" {}
		[HideInInspector]_Normal3("Normal3", 2D) = "bump" {}
		[HideInInspector]_NormalScale3("_NormalScale3", Float) = 1
		[HideInInspector]_Mask3("Mask3", 2D) = "gray" {}
		[HideInInspector]_Metallic3("Metallic3", Range( 0 , 1)) = 0
		[HideInInspector]_Smoothness3("Smoothness3", Range( 0 , 1)) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry-100" "IsEmissive" = "true"  "TerrainCompatible"="True" }
		Cull Back
		ZTest LEqual
		CGPROGRAM
		#include "UnityStandardUtils.cginc"
		#pragma target 3.5
		#pragma multi_compile_instancing
		#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd
		#pragma shader_feature_local _TERRAIN_INSTANCED_PERPIXEL_NORMAL
		#define ASE_VERSION 19801
		#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd
		#pragma multi_compile_local __ _NORMALMAP
		#pragma shader_feature_local _MASKMAP
		#pragma multi_compile_local_fragment __ _ALPHATEST_ON
		#pragma multi_compile_fog
		#pragma editor_sync_compilation
		#pragma target 3.0
		#pragma exclude_renderers gles
		#define TERRAIN_SPLATMAP_COMMON_CGINC_INCLUDED
		#include "UnityPBSLighting.cginc"
		#include "TerrainSplatmapCommon.cginc"
		#define TERRAIN_STANDARD_SHADER
		#define TERRAIN_SURFACE_OUTPUT SurfaceOutputStandard
		#define ASE_USING_SAMPLING_MACROS 1
		#if defined(SHADER_API_D3D11) || defined(SHADER_API_XBOXONE) || defined(UNITY_COMPILER_HLSLCC) || defined(SHADER_API_PSSL) || (defined(SHADER_TARGET_SURFACE_ANALYSIS) && !defined(SHADER_TARGET_SURFACE_ANALYSIS_MOJOSHADER))//ASE Sampler Macros
		#define SAMPLE_TEXTURE2D(tex,samplerTex,coord) tex.Sample(samplerTex,coord)
		#else//ASE Sampling Macros
		#define SAMPLE_TEXTURE2D(tex,samplerTex,coord) tex2D(tex,coord)
		#endif//ASE Sampling Macros

		#pragma surface surf StandardSpecular keepalpha vertex:vertexDataFunc  finalcolor:SplatmapFinalColor
		struct Input
		{
			float2 vertexToFrag286_g33;
			float2 uv_texcoord;
		};

		#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
			sampler2D _TerrainHeightmapTexture;//ASE Terrain Instancing
			sampler2D _TerrainNormalmapTexture;//ASE Terrain Instancing
		#endif//ASE Terrain Instancing
		UNITY_INSTANCING_BUFFER_START( Terrain )//ASE Terrain Instancing
			UNITY_DEFINE_INSTANCED_PROP( float4, _TerrainPatchInstanceData )//ASE Terrain Instancing
		UNITY_INSTANCING_BUFFER_END( Terrain)//ASE Terrain Instancing
		CBUFFER_START( UnityTerrain)//ASE Terrain Instancing
			#ifdef UNITY_INSTANCING_ENABLED//ASE Terrain Instancing
				float4 _TerrainHeightmapRecipSize;//ASE Terrain Instancing
				float4 _TerrainHeightmapScale;//ASE Terrain Instancing
			#endif//ASE Terrain Instancing
		CBUFFER_END//ASE Terrain Instancing
		uniform float _TerrainNormalMap;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_Control);
		uniform float4 _Control_ST;
		SamplerState sampler_Control;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_Normal0);
		UNITY_DECLARE_TEX2D_NOSAMPLER(_Splat0);
		uniform float4 _Splat0_ST;
		SamplerState sampler_Normal0;
		uniform half _NormalScale0;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_Normal1);
		UNITY_DECLARE_TEX2D_NOSAMPLER(_Splat1);
		uniform float4 _Splat1_ST;
		uniform half _NormalScale1;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_Normal2);
		UNITY_DECLARE_TEX2D_NOSAMPLER(_Splat2);
		uniform float4 _Splat2_ST;
		uniform half _NormalScale2;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_Normal3);
		UNITY_DECLARE_TEX2D_NOSAMPLER(_Splat3);
		uniform float4 _Splat3_ST;
		uniform half _NormalScale3;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_NormalMap);
		SamplerState sampler_NormalMap;
		uniform float _NormalIntensity;
		uniform float _ColorMap;
		SamplerState sampler_Splat0;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_TerrainHolesTexture);
		uniform float4 _TerrainHolesTexture_ST;
		SamplerState sampler_TerrainHolesTexture;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_ColorMapProjection);
		SamplerState sampler_ColorMapProjection;
		uniform float _ColorMapIntensity;
		uniform float _TTFELIGHTTERRAINSNOWSHADER;
		uniform float _TEXTUREMAPS;
		uniform float _DIVIDER_01;
		uniform float _TEXTURESETTINGS;
		uniform float _DIVIDER_2;
		uniform float _Metallic0;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_Mask0);
		SamplerState sampler_Mask0;
		uniform float _Metallic1;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_Mask1);
		uniform float _Metallic2;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_Mask2);
		uniform float _Metallic3;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_Mask3);
		uniform float _Smoothness0;
		uniform float _Smoothness1;
		uniform float _Smoothness2;
		uniform float _Smoothness3;
		uniform float _SpecularOffset;
		uniform float _SpecularPower;
		uniform float _SmoothnessIntensity;


		void SplatmapFinalColor( Input SurfaceIn, SurfaceOutputStandardSpecular SurfaceOut, inout fixed4 FinalColor )
		{
			FinalColor *= SurfaceOut.Alpha;
		}


		void ApplyMeshModification( inout appdata_full v )
		{
			#if defined(UNITY_INSTANCING_ENABLED) && !defined(SHADER_API_D3D11_9X)
				float2 patchVertex = v.vertex.xy;
				float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
				
				float4 uvscale = instanceData.z * _TerrainHeightmapRecipSize;
				float4 uvoffset = instanceData.xyxy * uvscale;
				uvoffset.xy += 0.5f * _TerrainHeightmapRecipSize.xy;
				float2 sampleCoords = (patchVertex.xy * uvscale.xy + uvoffset.xy);
				
				float hm = UnpackHeightmap(tex2Dlod(_TerrainHeightmapTexture, float4(sampleCoords, 0, 0)));
				v.vertex.xz = (patchVertex.xy + instanceData.xy) * _TerrainHeightmapScale.xz * instanceData.z;
				v.vertex.y = hm * _TerrainHeightmapScale.y;
				v.vertex.w = 1.0f;
				
				v.texcoord.xy = (patchVertex.xy * uvscale.zw + uvoffset.zw);
				v.texcoord3 = v.texcoord2 = v.texcoord1 = v.texcoord;
				
				#ifdef TERRAIN_INSTANCED_PERPIXEL_NORMAL
					v.normal = float3(0, 1, 0);
					//data.tc.zw = sampleCoords;
				#else
					float3 nor = tex2Dlod(_TerrainNormalmapTexture, float4(sampleCoords, 0, 0)).xyz;
					v.normal = 2.0f * nor - 1.0f;
				#endif
			#endif
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			ApplyMeshModification(v);;
			float localCalculateTangentsStandard706_g33 = ( 0.0 );
			{
			v.tangent.xyz = cross ( v.normal, float3( 0, 0, 1 ) );
			v.tangent.w = -1;
			}
			float3 ase_normalOS = v.normal.xyz;
			float3 temp_output_707_0_g33 = ( localCalculateTangentsStandard706_g33 + ase_normalOS );
			v.normal = temp_output_707_0_g33;
			float4 appendResult704_g33 = (float4(cross( ase_normalOS , float3(0,0,1) ) , -1.0));
			v.tangent = appendResult704_g33;
			float2 break291_g33 = _Control_ST.zw;
			float2 appendResult293_g33 = (float2(( break291_g33.x + 0.001 ) , ( break291_g33.y + 0.0001 )));
			o.vertexToFrag286_g33 = ( ( v.texcoord.xy * _Control_ST.xy ) + appendResult293_g33 );
		}

		void surf( Input i , inout SurfaceOutputStandardSpecular o )
		{
			float4 tex2DNode283_g33 = SAMPLE_TEXTURE2D( _Control, sampler_Control, i.vertexToFrag286_g33 );
			float dotResult278_g33 = dot( tex2DNode283_g33 , half4(1,1,1,1) );
			float localSplatClip276_g33 = ( dotResult278_g33 );
			float SplatWeight276_g33 = dotResult278_g33;
			{
			#if !defined(SHADER_API_MOBILE) && defined(TERRAIN_SPLAT_ADDPASS)
				clip(SplatWeight276_g33 == 0.0f ? -1 : 1);
			#endif
			}
			float4 Control26_g33 = ( tex2DNode283_g33 / ( localSplatClip276_g33 + 0.001 ) );
			float2 uv_Splat0 = i.uv_texcoord * _Splat0_ST.xy + _Splat0_ST.zw;
			float4 Normal0341_g33 = SAMPLE_TEXTURE2D( _Normal0, sampler_Normal0, uv_Splat0 );
			float2 uv_Splat1 = i.uv_texcoord * _Splat1_ST.xy + _Splat1_ST.zw;
			float4 Normal1378_g33 = SAMPLE_TEXTURE2D( _Normal1, sampler_Normal0, uv_Splat1 );
			float2 uv_Splat2 = i.uv_texcoord * _Splat2_ST.xy + _Splat2_ST.zw;
			float4 Normal2356_g33 = SAMPLE_TEXTURE2D( _Normal2, sampler_Normal0, uv_Splat2 );
			float2 uv_Splat3 = i.uv_texcoord * _Splat3_ST.xy + _Splat3_ST.zw;
			float4 Normal3398_g33 = SAMPLE_TEXTURE2D( _Normal3, sampler_Normal0, uv_Splat3 );
			float4 weightedBlendVar473_g33 = Control26_g33;
			float3 weightedBlend473_g33 = ( weightedBlendVar473_g33.x*UnpackScaleNormal( Normal0341_g33, _NormalScale0 ) + weightedBlendVar473_g33.y*UnpackScaleNormal( Normal1378_g33, _NormalScale1 ) + weightedBlendVar473_g33.z*UnpackScaleNormal( Normal2356_g33, _NormalScale2 ) + weightedBlendVar473_g33.w*UnpackScaleNormal( Normal3398_g33, _NormalScale3 ) );
			float3 break513_g33 = weightedBlend473_g33;
			float3 appendResult514_g33 = (float3(break513_g33.x , break513_g33.y , ( break513_g33.z + 0.001 )));
			#ifdef _TERRAIN_INSTANCED_PERPIXEL_NORMAL
				float3 staticSwitch503_g33 = appendResult514_g33;
			#else
				float3 staticSwitch503_g33 = appendResult514_g33;
			#endif
			float3 NormalOut80 = staticSwitch503_g33;
			float2 uv_NormalMap97 = i.uv_texcoord;
			float3 NormalMap_Top196 = UnpackScaleNormal( SAMPLE_TEXTURE2D( _NormalMap, sampler_NormalMap, uv_NormalMap97 ), _NormalIntensity );
			float3 NormalSnow_Output136 = (( _TerrainNormalMap )?( BlendNormals( NormalMap_Top196 , NormalOut80 ) ):( NormalOut80 ));
			o.Normal = NormalSnow_Output136;
			float4 tex2DNode414_g33 = SAMPLE_TEXTURE2D( _Splat0, sampler_Splat0, uv_Splat0 );
			float3 Splat0342_g33 = (tex2DNode414_g33).rgb;
			float3 Splat1379_g33 = (SAMPLE_TEXTURE2D( _Splat1, sampler_Splat0, uv_Splat1 )).rgb;
			float3 Splat2357_g33 = (SAMPLE_TEXTURE2D( _Splat2, sampler_Splat0, uv_Splat2 )).rgb;
			float3 Splat3390_g33 = (SAMPLE_TEXTURE2D( _Splat3, sampler_Splat0, uv_Splat3 )).rgb;
			float4 weightedBlendVar9_g33 = Control26_g33;
			float3 weightedBlend9_g33 = ( weightedBlendVar9_g33.x*Splat0342_g33 + weightedBlendVar9_g33.y*Splat1379_g33 + weightedBlendVar9_g33.z*Splat2357_g33 + weightedBlendVar9_g33.w*Splat3390_g33 );
			float3 localClipHoles453_g33 = ( weightedBlend9_g33 );
			float2 uv_TerrainHolesTexture = i.uv_texcoord * _TerrainHolesTexture_ST.xy + _TerrainHolesTexture_ST.zw;
			float Hole453_g33 = SAMPLE_TEXTURE2D( _TerrainHolesTexture, sampler_TerrainHolesTexture, uv_TerrainHolesTexture ).r;
			{
			#ifdef _ALPHATEST_ON
				clip(Hole453_g33 == 0.0f ? -1 : 1);
			#endif
			}
			float3 BaseColorOut79 = localClipHoles453_g33;
			float2 uv_ColorMapProjection203 = i.uv_texcoord;
			float4 ColorMap_Top204 = ( unity_ColorSpaceDouble * SAMPLE_TEXTURE2D( _ColorMapProjection, sampler_ColorMapProjection, uv_ColorMapProjection203 ) );
			float4 lerpResult221 = lerp( float4( BaseColorOut79 , 0.0 ) , ( ColorMap_Top204 * float4( BaseColorOut79 , 0.0 ) ) , _ColorMapIntensity);
			float4 AlbedoSnow_Output135 = (( _ColorMap )?( lerpResult221 ):( float4( BaseColorOut79 , 0.0 ) ));
			o.Albedo = AlbedoSnow_Output135.rgb;
			float3 temp_cast_9 = (( _TTFELIGHTTERRAINSNOWSHADER + _TEXTUREMAPS + _DIVIDER_01 + _TEXTURESETTINGS + _DIVIDER_2 )).xxx;
			o.Emission = temp_cast_9;
			float4 tex2DNode416_g33 = SAMPLE_TEXTURE2D( _Mask0, sampler_Mask0, uv_Splat0 );
			float Mask0R334_g33 = tex2DNode416_g33.r;
			float4 tex2DNode422_g33 = SAMPLE_TEXTURE2D( _Mask1, sampler_Mask0, uv_Splat1 );
			float Mask1R370_g33 = tex2DNode422_g33.r;
			float4 tex2DNode419_g33 = SAMPLE_TEXTURE2D( _Mask2, sampler_Mask0, uv_Splat2 );
			float Mask2R359_g33 = tex2DNode419_g33.r;
			float4 tex2DNode425_g33 = SAMPLE_TEXTURE2D( _Mask3, sampler_Mask0, uv_Splat3 );
			float Mask3R388_g33 = tex2DNode425_g33.r;
			float4 weightedBlendVar536_g33 = Control26_g33;
			float weightedBlend536_g33 = ( weightedBlendVar536_g33.x*max( _Metallic0 , Mask0R334_g33 ) + weightedBlendVar536_g33.y*max( _Metallic1 , Mask1R370_g33 ) + weightedBlendVar536_g33.z*max( _Metallic2 , Mask2R359_g33 ) + weightedBlendVar536_g33.w*max( _Metallic3 , Mask3R388_g33 ) );
			float4 appendResult1168_g33 = (float4(_Smoothness0 , _Smoothness1 , _Smoothness2 , _Smoothness3));
			float Splat0A435_g33 = tex2DNode414_g33.a;
			float Mask1A369_g33 = tex2DNode422_g33.a;
			float Mask2A360_g33 = tex2DNode419_g33.a;
			float Mask3A391_g33 = tex2DNode425_g33.a;
			float4 appendResult1169_g33 = (float4(Splat0A435_g33 , Mask1A369_g33 , Mask2A360_g33 , Mask3A391_g33));
			float dotResult1166_g33 = dot( ( appendResult1168_g33 * appendResult1169_g33 ) , Control26_g33 );
			float temp_output_198_45 = dotResult1166_g33;
			float3 temp_cast_11 = (saturate( ( weightedBlend536_g33 + ( temp_output_198_45 * _SpecularOffset * _SpecularPower ) ) )).xxx;
			o.Specular = temp_cast_11;
			float SmoothOut157 = ( _SmoothnessIntensity * temp_output_198_45 );
			float Smoothness_Output138 = SmoothOut157;
			o.Smoothness = Smoothness_Output138;
			float Mask0G409_g33 = tex2DNode416_g33.g;
			float Mask1G371_g33 = tex2DNode422_g33.g;
			float Mask2G358_g33 = tex2DNode419_g33.g;
			float Mask3G389_g33 = tex2DNode425_g33.g;
			float4 weightedBlendVar602_g33 = Control26_g33;
			float weightedBlend602_g33 = ( weightedBlendVar602_g33.x*saturate( ( ( ( Mask0G409_g33 - 0.5 ) * 0.25 ) + ( 1.0 - 0.25 ) ) ) + weightedBlendVar602_g33.y*saturate( ( ( ( Mask1G371_g33 - 0.5 ) * 0.25 ) + ( 1.0 - 0.25 ) ) ) + weightedBlendVar602_g33.z*saturate( ( ( ( Mask2G358_g33 - 0.5 ) * 0.25 ) + ( 1.0 - 0.25 ) ) ) + weightedBlendVar602_g33.w*saturate( ( ( ( Mask3G389_g33 - 0.5 ) * 0.25 ) + ( 1.0 - 0.25 ) ) ) );
			o.Occlusion = saturate( weightedBlend602_g33 );
			o.Alpha = dotResult278_g33;
		}

		ENDCG
		UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
		UsePass "Hidden/Nature/Terrain/Utilities/SELECTION"
	}

	Dependency "BaseMapShader"="Toby Fredson/The Toby Foliage Engine/(TTFE) Terrain Snow BasePass"
	Dependency "AddPassShader"="Toby Fredson/The Toby Foliage Engine/(TTFE) Terrain Snow AddPass"
	Fallback "Nature/Terrain/Diffuse"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.CommentaryNode;90;-1824,896;Inherit;False;2341.918;1079.072;Settings for season control, specular, and others.;4;234;232;233;230;Mesh Settings;0,1,0.1098039,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;230;-1744,1024;Inherit;False;1045.935;683.8665;;7;203;97;204;196;212;201;211;Main Maps;1,1,1,1;0;0
Node;AmplifyShaderEditor.ColorSpaceDouble;211;-1328,1072;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;203;-1392,1264;Inherit;True;Property;_ColorMapProjection;Color Map Projection;2;1;[NoScaleOffset];Create;True;3;______________(TTFE) TERRAIN BASIC________________;_____________________________________________________;Texture Maps;0;0;False;1;Space (5);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;201;-1696,1536;Inherit;False;Property;_NormalIntensity;Normal Intensity;8;1;[Header];Create;True;1;(Normal);0;0;False;0;False;1;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;212;-1088,1184;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.FunctionNode;198;-384,128;Inherit;False;(TTFE) Terrain 4 Layer;14;;33;cbdebdb4ac1110445ac18699eb78ddd1;2,504,1,102,1;0;8;FLOAT3;0;FLOAT3;14;FLOAT;56;FLOAT;45;FLOAT;200;FLOAT;282;FLOAT3;709;FLOAT4;701
Node;AmplifyShaderEditor.CommentaryNode;232;-608,1024;Inherit;False;1094.198;332.2637;;7;152;206;135;208;221;213;205;Albedo_Final;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;79;64,48;Inherit;False;BaseColorOut;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;204;-928,1184;Inherit;False;ColorMap_Top;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;97;-1392,1488;Inherit;True;Property;_NormalMap;Normal Map;3;2;[NoScaleOffset];[Normal];Create;True;0;0;0;False;0;False;-1;None;None;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.CommentaryNode;233;-608,1408;Inherit;False;1011.998;280.6313;;6;197;153;251;136;202;168;Normal_Final;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;80;64,144;Inherit;False;NormalOut;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;223;-512,-80;Inherit;False;Property;_SmoothnessIntensity;Smoothness Intensity;10;0;Create;True;0;0;0;False;0;False;0;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;196;-1072,1488;Inherit;False;NormalMap_Top;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;206;-560,1072;Inherit;False;204;ColorMap_Top;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;152;-560,1152;Inherit;False;79;BaseColorOut;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;222;-112,-112;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;205;-336,1088;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;213;-560,1232;Inherit;False;Property;_ColorMapIntensity;Color Map Intensity;6;1;[Header];Create;True;3;_____________________________________________________;Texture Settings;(Color Map);0;0;False;0;False;1;0.3;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;153;-544,1584;Inherit;False;80;NormalOut;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;197;-560,1488;Inherit;False;196;NormalMap_Top;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;234;-608,1744;Inherit;False;530.5502;156.5347;;2;138;155;Smoothness_Final;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;157;64,-48;Inherit;False;SmoothOut;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;247;-64,464;Inherit;False;Property;_SpecularOffset;Specular Offset;11;1;[Header];Create;True;1;(Specular);0;0;False;0;False;0.02;0.02;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;248;-144,544;Inherit;False;Property;_SpecularPower;Specular Power;12;0;Create;True;0;0;0;False;0;False;1;0.017;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;221;-176,1136;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.BlendNormalsNode;168;-336,1488;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode;251;-160,1616;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;252;-400.516,-609.4368;Inherit;False;500;476;;6;258;257;256;255;254;253;DRAWERS;0,0,0,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;246;168.9545,425.7959;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode;208;0,1232;Inherit;False;Property;_ColorMap;Color Map;7;0;Create;True;0;0;0;False;0;False;1;True;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ToggleSwitchNode;202;-112,1520;Inherit;False;Property;_TerrainNormalMap;Terrain Normal Map;9;0;Create;True;0;0;0;False;0;False;0;True;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;155;-560,1808;Inherit;False;157;SmoothOut;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;254;-256.516,-481.4368;Inherit;False;Property;_TEXTUREMAPS;TEXTURE MAPS;1;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (5);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;255;-256.516,-401.4368;Inherit;False;Property;_DIVIDER_01;DIVIDER_01;13;0;Create;True;0;0;0;False;1;TTFE_DrawerDivider;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;256;-291.5033,-318.9207;Inherit;False;Property;_TEXTURESETTINGS;TEXTURE SETTINGS;5;1;[Header];Create;True;0;0;0;False;1;TTFE_DrawerFeatureBorder;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;257;-259.5033,-238.9206;Inherit;False;Property;_DIVIDER_2;DIVIDER_01;4;0;Create;True;0;0;0;False;1;TTFE_DrawerDivider;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;253;-352.516,-561.4368;Inherit;False;Property;_TTFELIGHTTERRAINSNOWSHADER;(TTFE-LIGHT) TERRAIN SNOW SHADER;0;0;Create;True;0;0;0;False;1;TTFE_DrawerTitle;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;250;304,320;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;135;224,1232;Inherit;False;AlbedoSnow_Output;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;136;144,1520;Inherit;False;NormalSnow_Output;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;138;-368,1808;Inherit;False;Smoothness_Output;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;258;-64.51599,-497.4368;Inherit;False;5;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StickyNoteNode;50;656,528;Inherit;False;757.9001;428.2;BIRP First Pass;;0,0,0,1;Additional Directives:$$#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd$#pragma multi_compile_local __ _NORMALMAP$#pragma shader_feature_local _MASKMAP$#pragma multi_compile_local_fragment __ _ALPHATEST_ON$#pragma multi_compile_fog$#pragma editor_sync_compilation$#pragma target 3.0$#pragma exclude_renderers gles$#define TERRAIN_SPLATMAP_COMMON_CGINC_INCLUDED$#include UnityPBSLighting.cginc$#include TerrainSplatmapCommon.cginc$#define TERRAIN_STANDARD_SHADER$#define TERRAIN_SURFACE_OUTPUT SurfaceOutputStandard$$$Custom SubShader Tags:$$TerrainCompatible = True$;0;0
Node;AmplifyShaderEditor.StickyNoteNode;52;642.0813,-247.3737;Inherit;False;262;102;SplatmapFinalColor;;0,0,0,1;Additional Surface Options:$finalcolor:SplatmapFinalColor;0;0
Node;AmplifyShaderEditor.GetLocalVarNode;193;304,48;Inherit;False;136;NormalSnow_Output;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;192;304,-48;Inherit;False;135;AlbedoSnow_Output;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;194;304,144;Inherit;False;138;Smoothness_Output;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.CustomExpressionNode;45;643.3015,-133.2568;Float;False;FinalColor *= SurfaceOut.Alpha@;7;Create;3;True;SurfaceIn;OBJECT;0;In;Input;Float;False;True;SurfaceOut;OBJECT;0;In;SurfaceOutputStandardSpecular;Float;False;True;FinalColor;OBJECT;0;InOut;fixed4;Float;False;SplatmapFinalColor;False;True;0;;False;4;0;FLOAT;0;False;1;OBJECT;0;False;2;OBJECT;0;False;3;OBJECT;0;False;2;FLOAT;0;OBJECT;4
Node;AmplifyShaderEditor.SaturateNode;249;432,320;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;259;544,-336;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;645.0565,40.11808;Float;False;True;-1;3;;0;0;StandardSpecular;Toby Fredson/The Toby Foliage Engine/(TTFE) Terrain Snow;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;;3;False;;False;0;False;;0;False;;False;0;Custom;0.5;True;False;-100;True;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;Nature/Terrain/Diffuse;42;-1;-1;-1;1;TerrainCompatible=True;False;2;BaseMapShader=Toby Fredson/The Toby Foliage Engine/(TTFE) Terrain Snow BasePass;AddPassShader=Toby Fredson/The Toby Foliage Engine/(TTFE) Terrain Snow AddPass;0;False;;-1;0;False;;13;Pragma;instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd;False;;Custom;False;0;0;;Pragma;multi_compile_local __ _NORMALMAP;False;;Custom;False;0;0;;Pragma;shader_feature_local _MASKMAP;False;;Custom;False;0;0;;Pragma;multi_compile_local_fragment __ _ALPHATEST_ON;False;;Custom;False;0;0;;Pragma;multi_compile_fog;False;;Custom;False;0;0;;Pragma;editor_sync_compilation;False;;Custom;False;0;0;;Pragma;target 3.0;False;;Custom;False;0;0;;Pragma;exclude_renderers gles;False;;Custom;False;0;0;;Define;TERRAIN_SPLATMAP_COMMON_CGINC_INCLUDED;False;;Custom;False;0;0;;Include;UnityPBSLighting.cginc;False;;Custom;False;0;0;;Include;TerrainSplatmapCommon.cginc;False;;Custom;False;0;0;;Define;TERRAIN_STANDARD_SHADER;False;;Custom;False;0;0;;Define;TERRAIN_SURFACE_OUTPUT SurfaceOutputStandard;False;;Custom;False;0;0;;1;finalcolor:SplatmapFinalColor;0;True;0.1;False;;0;False;;True;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
Node;AmplifyShaderEditor.CommentaryNode;235;-1824,688;Inherit;False;2380.247;100;;0;MESH SETTINGS;1,1,1,1;0;0
WireConnection;212;0;211;0
WireConnection;212;1;203;0
WireConnection;79;0;198;0
WireConnection;204;0;212;0
WireConnection;97;5;201;0
WireConnection;80;0;198;14
WireConnection;196;0;97;0
WireConnection;222;0;223;0
WireConnection;222;1;198;45
WireConnection;205;0;206;0
WireConnection;205;1;152;0
WireConnection;157;0;222;0
WireConnection;221;0;152;0
WireConnection;221;1;205;0
WireConnection;221;2;213;0
WireConnection;168;0;197;0
WireConnection;168;1;153;0
WireConnection;251;0;153;0
WireConnection;246;0;198;45
WireConnection;246;1;247;0
WireConnection;246;2;248;0
WireConnection;208;0;152;0
WireConnection;208;1;221;0
WireConnection;202;0;251;0
WireConnection;202;1;168;0
WireConnection;250;0;198;56
WireConnection;250;1;246;0
WireConnection;135;0;208;0
WireConnection;136;0;202;0
WireConnection;138;0;155;0
WireConnection;258;0;253;0
WireConnection;258;1;254;0
WireConnection;258;2;255;0
WireConnection;258;3;256;0
WireConnection;258;4;257;0
WireConnection;249;0;250;0
WireConnection;259;0;258;0
WireConnection;0;0;192;0
WireConnection;0;1;193;0
WireConnection;0;2;259;0
WireConnection;0;3;249;0
WireConnection;0;4;194;0
WireConnection;0;5;198;200
WireConnection;0;9;198;282
WireConnection;0;12;198;709
WireConnection;0;16;198;701
ASEEND*/
//CHKSM=BB57637CCD8564CDB082B0D7F1A5181CE65F463C