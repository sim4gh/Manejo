// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Toby Fredson/The Toby Foliage Engine/(TTFE) Simple Cliff Masked"
{
	Properties
	{
		[TTFE_DrawerTitle]_TTFELIGHTSIMPLECLIFFMASKED("(TTFE-LIGHT) SIMPLE CLIFF MASKED", Float) = 0
		[TTFE_DrawerFeatureBorder][Space (5)]_BASELAYER(" BASE LAYER", Float) = 0
		[Header(Texture Maps)][NoScaleOffset][TTFE_Drawer_SingleLineTexture][Space (5)]_DetailAlbedo("Detail Albedo", 2D) = "gray" {}
		[NoScaleOffset][Normal][TTFE_Drawer_SingleLineTexture]_DetailNormal("Detail Normal", 2D) = "bump" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture]_DetailMask("Detail Mask", 2D) = "white" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture]_DetailLayerMask("Detail Layer Mask", 2D) = "white" {}
		[Header(Detail Texture Settings)]_DetailNormalIntensity("Detail Normal Intensity", Range( -3 , 3)) = 0
		_SpecularPower("Specular Power", Range( 0 , 1)) = 1
		_MaskAoIntensity("Mask Ao Intensity", Range( 0 , 1)) = 0
		_Float5("Main Ao Intensity", Range( 0 , 1)) = 0
		[TTFE_DrawerDivider]_DIVIDER_01("DIVIDER_01", Float) = 0
		[TTFE_DrawerFeatureBorder]_ROCKLAYER("ROCK LAYER", Float) = 0
		[Header(Texture Maps)][NoScaleOffset][TTFE_Drawer_SingleLineTexture][Space (5)]_RockAlbedo("Rock Albedo", 2D) = "white" {}
		[NoScaleOffset][Normal][TTFE_Drawer_SingleLineTexture]_RockNormal("Rock Normal", 2D) = "bump" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture]_RockMetallicAoGloss("Rock Metallic/Ao/Gloss", 2D) = "white" {}
		[Header(Rock Texture Settings)][Header((Tiling and Offset))]_RockTiling("Rock Tiling", Vector) = (1,1,0,0)
		_RockOffset("Rock Offset", Vector) = (1,1,0,0)
		_RockColor("Rock Color", Color) = (1,1,1,0)
		_RockNormalIntensity("Rock Normal Intensity", Range( -3 , 3)) = 1
		[TTFE_DrawerDivider]_DIVIDER_02("DIVIDER_02", Float) = 0
		[TTFE_DrawerFeatureBorder]_COVERLAYER("COVER LAYER", Float) = 0
		[Header(Texture Maps)][NoScaleOffset][TTFE_Drawer_SingleLineTexture][Space (5)]_CoverAlbedo("Cover Albedo", 2D) = "white" {}
		[NoScaleOffset][Normal][TTFE_Drawer_SingleLineTexture]_CoverNormal("Cover Normal", 2D) = "bump" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture]_CoverMetallicAoGloss("Cover Metallic/Ao/Gloss", 2D) = "white" {}
		[Header(Cover Texture Settings)][Header((Tiling and Offset))]_CoverTiling("Cover Tiling", Vector) = (1,1,0,0)
		_CoverOffset("Cover Offset", Vector) = (1,1,0,0)
		_CoverageAmount("Coverage Amount", Range( 0 , 5)) = 1
		_CoverColor("Cover Color", Color) = (1,1,1,0)
		_SlopeMax1("Slope Max", Float) = 0
		_SlopeMin1("Slope Min", Float) = 0
		[Toggle]_PreserveScaling("Preserve Scaling", Float) = 1
		_WorldScale("World Scale", Float) = 0.01
		_CoverageSpecularAngle("Coverage Specular Angle", Float) = 0
		[TTFE_DrawerDivider]_DIVIDER_03("DIVIDER_03", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Back
		CGINCLUDE
		#include "UnityStandardUtils.cginc"
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 4.5
		#define ASE_VERSION 19801
		#ifdef UNITY_PASS_SHADOWCASTER
			#undef INTERNAL_DATA
			#undef WorldReflectionVector
			#undef WorldNormalVector
			#define INTERNAL_DATA half3 internalSurfaceTtoW0; half3 internalSurfaceTtoW1; half3 internalSurfaceTtoW2;
			#define WorldReflectionVector(data,normal) reflect (data.worldRefl, half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal)))
			#define WorldNormalVector(data,normal) half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal))
		#endif
		struct Input
		{
			float2 uv_texcoord;
			float3 worldNormal;
			INTERNAL_DATA
		};

		uniform sampler2D _DetailNormal;
		uniform float _DetailNormalIntensity;
		uniform sampler2D _RockNormal;
		uniform float2 _RockTiling;
		uniform float2 _RockOffset;
		uniform float _RockNormalIntensity;
		uniform sampler2D _CoverNormal;
		uniform float _PreserveScaling;
		uniform float2 _CoverTiling;
		uniform float2 _CoverOffset;
		uniform float _WorldScale;
		uniform sampler2D _DetailLayerMask;
		uniform float _CoverageAmount;
		uniform float _SlopeMax1;
		uniform float _SlopeMin1;
		uniform sampler2D _DetailAlbedo;
		uniform sampler2D _RockAlbedo;
		uniform float4 _RockColor;
		uniform sampler2D _CoverAlbedo;
		uniform float4 _CoverColor;
		uniform float _TTFELIGHTSIMPLECLIFFMASKED;
		uniform float _BASELAYER;
		uniform float _DIVIDER_01;
		uniform float _ROCKLAYER;
		uniform float _DIVIDER_02;
		uniform float _COVERLAYER;
		uniform float _DIVIDER_03;
		uniform float _CoverageSpecularAngle;
		uniform sampler2D _CoverMetallicAoGloss;
		uniform float _SpecularPower;
		uniform sampler2D _DetailMask;
		uniform sampler2D _RockMetallicAoGloss;
		uniform float _MaskAoIntensity;
		uniform float _Float5;

		void surf( Input i , inout SurfaceOutputStandardSpecular o )
		{
			float2 uv_DetailNormal578 = i.uv_texcoord;
			float2 uv_TexCoord572 = i.uv_texcoord * _RockTiling + _RockOffset;
			float2 uv_TexCoord583 = i.uv_texcoord * _CoverTiling + _CoverOffset;
			float3 ase_parentObjectScale = (1.0/float3( length( unity_WorldToObject[ 0 ].xyz ), length( unity_WorldToObject[ 1 ].xyz ), length( unity_WorldToObject[ 2 ].xyz ) ));
			float2 appendResult645 = (float2(ase_parentObjectScale.x , ase_parentObjectScale.z));
			float2 uv_TexCoord648 = i.uv_texcoord * ( appendResult645 * _WorldScale );
			float2 uv_DetailLayerMask585 = i.uv_texcoord;
			float3 ase_normalWS = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float3 temp_output_618_0 = abs( ase_normalWS );
			float dotResult620 = dot( temp_output_618_0 , float3(1,1,1) );
			float temp_output_635_0 = saturate( ( tex2D( _DetailLayerMask, uv_DetailLayerMask585 ).g * ( saturate( ( ase_normalWS.y * _CoverageAmount ) ) * saturate( (( temp_output_618_0 / dotResult620 ).y*_SlopeMax1 + _SlopeMin1) ) ) ) );
			float3 lerpResult597 = lerp( UnpackScaleNormal( tex2D( _RockNormal, uv_TexCoord572 ), _RockNormalIntensity ) , UnpackNormal( tex2D( _CoverNormal, (( _PreserveScaling )?( uv_TexCoord648 ):( uv_TexCoord583 )) ) ) , temp_output_635_0);
			o.Normal = BlendNormals( UnpackScaleNormal( tex2D( _DetailNormal, uv_DetailNormal578 ), _DetailNormalIntensity ) , lerpResult597 );
			float2 uv_DetailAlbedo579 = i.uv_texcoord;
			float4 lerpResult592 = lerp( ( tex2D( _DetailAlbedo, uv_DetailAlbedo579 ) * unity_ColorSpaceDouble * tex2D( _RockAlbedo, uv_TexCoord572 ) * _RockColor ) , ( tex2D( _CoverAlbedo, (( _PreserveScaling )?( uv_TexCoord648 ):( uv_TexCoord583 )) ) * _CoverColor ) , temp_output_635_0);
			o.Albedo = lerpResult592.rgb;
			float3 temp_cast_1 = (( _TTFELIGHTSIMPLECLIFFMASKED + _BASELAYER + _DIVIDER_01 + _ROCKLAYER + _DIVIDER_02 + _COVERLAYER + _DIVIDER_03 )).xxx;
			o.Emission = temp_cast_1;
			float4 tex2DNode576 = tex2D( _CoverMetallicAoGloss, (( _PreserveScaling )?( uv_TexCoord648 ):( uv_TexCoord583 )) );
			float3 temp_cast_2 = (saturate( ( saturate( ( ( _CoverageSpecularAngle * tex2DNode576.a ) + ( 1.0 - temp_output_635_0 ) ) ) * ( 0.04 * 1.0 * _SpecularPower ) ) )).xxx;
			o.Specular = temp_cast_2;
			float2 uv_DetailMask577 = i.uv_texcoord;
			float4 tex2DNode577 = tex2D( _DetailMask, uv_DetailMask577 );
			float4 tex2DNode568 = tex2D( _RockMetallicAoGloss, uv_TexCoord572 );
			float lerpResult601 = lerp( tex2DNode568.a , tex2DNode576.a , temp_output_635_0);
			o.Smoothness = ( tex2DNode577.a * lerpResult601 );
			float saferPower660 = abs( tex2DNode577.g );
			float lerpResult600 = lerp( tex2DNode568.g , tex2DNode576.g , temp_output_635_0);
			float saferPower178 = abs( lerpResult600 );
			o.Occlusion = ( pow( saferPower660 , _MaskAoIntensity ) * pow( saferPower178 , _Float5 ) );
			o.Alpha = 1;
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf StandardSpecular keepalpha fullforwardshadows 

		ENDCG
		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			ZWrite On
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.5
			#pragma multi_compile_shadowcaster
			#pragma multi_compile UNITY_PASS_SHADOWCASTER
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#include "HLSLSupport.cginc"
			#if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
				#define CAN_SKIP_VPOS
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			struct v2f
			{
				V2F_SHADOW_CASTER;
				float2 customPack1 : TEXCOORD1;
				float4 tSpace0 : TEXCOORD2;
				float4 tSpace1 : TEXCOORD3;
				float4 tSpace2 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			v2f vert( appdata_full v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_INITIALIZE_OUTPUT( v2f, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				Input customInputData;
				float3 worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
				half3 worldNormal = UnityObjectToWorldNormal( v.normal );
				half3 worldTangent = UnityObjectToWorldDir( v.tangent.xyz );
				half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				half3 worldBinormal = cross( worldNormal, worldTangent ) * tangentSign;
				o.tSpace0 = float4( worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x );
				o.tSpace1 = float4( worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y );
				o.tSpace2 = float4( worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z );
				o.customPack1.xy = customInputData.uv_texcoord;
				o.customPack1.xy = v.texcoord;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
				return o;
			}
			half4 frag( v2f IN
			#if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
			) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				Input surfIN;
				UNITY_INITIALIZE_OUTPUT( Input, surfIN );
				surfIN.uv_texcoord = IN.customPack1.xy;
				float3 worldPos = float3( IN.tSpace0.w, IN.tSpace1.w, IN.tSpace2.w );
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldNormal = float3( IN.tSpace0.z, IN.tSpace1.z, IN.tSpace2.z );
				surfIN.internalSurfaceTtoW0 = IN.tSpace0.xyz;
				surfIN.internalSurfaceTtoW1 = IN.tSpace1.xyz;
				surfIN.internalSurfaceTtoW2 = IN.tSpace2.xyz;
				SurfaceOutputStandardSpecular o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutputStandardSpecular, o )
				surf( surfIN, o );
				#if defined( CAN_SKIP_VPOS )
				float2 vpos = IN.pos;
				#endif
				SHADOW_CASTER_FRAGMENT( IN )
			}
			ENDCG
		}
	}
	Fallback "Diffuse"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.CommentaryNode;616;-2736,4080;Inherit;False;1359.01;482.7809;;10;626;625;624;623;622;621;620;619;618;617;Slope Function v2;1,1,1,1;0;0
Node;AmplifyShaderEditor.WorldNormalVector;617;-2672,4192;Inherit;False;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.CommentaryNode;588;-3248,2304;Inherit;False;1836.785;1026.24;;8;611;565;574;575;576;654;580;642;Coverage Layer Mask;1,1,1,1;0;0
Node;AmplifyShaderEditor.AbsOpNode;618;-2480,4208;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector3Node;619;-2624,4368;Float;False;Constant;_Vector1;Vector 0;-1;0;Create;True;0;0;0;False;0;False;1,1,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.CommentaryNode;642;-3216,2800;Inherit;False;851.9214;313.2019;Keep the same UV Scale regardless of the object scaling.;5;650;648;647;645;643;Scale Independent Position;1,1,1,1;0;0
Node;AmplifyShaderEditor.DotProductOpNode;620;-2384,4304;Inherit;True;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;670;-2064,3680;Inherit;False;676;339;;4;613;612;615;614;;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;621;-2160,4176;Inherit;True;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ObjectScaleNode;643;-3168,2848;Inherit;False;True;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.CommentaryNode;580;-2816,2400;Inherit;False;444.205;362.8944;;3;583;582;581;Texture Coordinates Cover;1,1,1,1;0;0
Node;AmplifyShaderEditor.BreakToComponentsNode;622;-1936,4192;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RangedFloatNode;623;-1952,4400;Inherit;False;Property;_SlopeMin1;Slope Min;29;0;Create;True;0;0;0;False;0;False;0;-1.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;624;-1952,4320;Inherit;False;Property;_SlopeMax1;Slope Max;28;0;Create;True;0;0;0;False;0;False;0;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WorldNormalVector;613;-1920,3728;Inherit;False;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;612;-2016,3904;Float;False;Property;_CoverageAmount;Coverage Amount;26;0;Create;True;0;0;0;False;0;False;1;5;0;5;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;645;-2976,2880;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;650;-2976,3008;Inherit;False;Property;_WorldScale;World Scale;31;0;Create;True;0;0;0;False;0;False;0.01;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;625;-1776,4176;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;4;False;2;FLOAT;-1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;615;-1728,3824;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;647;-2784,2944;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;582;-2784,2448;Float;False;Property;_CoverTiling;Cover Tiling;24;1;[Header];Create;True;2;Cover Texture Settings;(Tiling and Offset);0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;581;-2784,2608;Float;False;Property;_CoverOffset;Cover Offset;25;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.CommentaryNode;586;-2208,400;Inherit;False;727.0039;935.4727;;5;578;610;579;577;585;Detail Layer Maps;1,1,1,1;0;0
Node;AmplifyShaderEditor.SaturateNode;626;-1536,4176;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;614;-1568,3824;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;648;-2608,2912;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;583;-2592,2528;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;587;-2560,1408;Inherit;False;1066;803;;5;569;573;568;566;567;Rock Layer Maps;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;632;-1296,3984;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode;654;-2288,2704;Inherit;False;Property;_PreserveScaling;Preserve Scaling;30;0;Create;True;0;0;0;False;0;False;1;True;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;585;-1792,1104;Inherit;True;Property;_DetailLayerMask;Detail Layer Mask;5;1;[NoScaleOffset];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.CommentaryNode;569;-2512,1680;Inherit;False;444.205;362.8944;;3;572;571;570;Texture Coordinates Rock;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;633;-1104,2208;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;576;-2000,2848;Inherit;True;Property;_CoverMetallicAoGloss;Cover Metallic/Ao/Gloss;23;1;[NoScaleOffset];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;e3e17f7ee6882624bbec7de8d04946e2;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.Vector2Node;570;-2480,1888;Float;False;Property;_RockOffset;Rock Offset;16;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;571;-2480,1728;Float;False;Property;_RockTiling;Rock Tiling;15;1;[Header];Create;True;2;Rock Texture Settings;(Tiling and Offset);0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SaturateNode;635;-912,2224;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;684;-912,3152;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;682;-896,2128;Inherit;False;Property;_CoverageSpecularAngle;Coverage Specular Angle;32;0;Create;True;0;0;0;False;0;False;0;0.015;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;572;-2304,1808;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.OneMinusNode;652;-720,2240;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;683;-528,2160;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;666;-1024,2944;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;679;-368,2176;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;565;-1920,3072;Inherit;False;Property;_CoverColor;Cover Color;27;0;Create;True;0;0;0;False;0;False;1,1,1,0;1,1,1,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;448;-656,3040;Inherit;False;Constant;_Float9;Float 9;8;0;Create;True;0;0;0;False;0;False;0.04;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;447;-624,3136;Inherit;False;Constant;_Float1;Float 1;34;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;449;-688,3264;Inherit;False;Property;_SpecularPower;Specular Power;7;1;[Header];Create;True;0;0;0;False;0;False;1;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;577;-1792,880;Inherit;True;Property;_DetailMask;Detail Mask;4;1;[NoScaleOffset];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;568;-1824,1952;Inherit;True;Property;_RockMetallicAoGloss;Rock Metallic/Ao/Gloss;14;1;[NoScaleOffset];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;ce054b904524a4346a8c8d77a8d067ad;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;574;-2000,2384;Inherit;True;Property;_CoverAlbedo;Cover Albedo;21;2;[Header];[NoScaleOffset];Create;True;1;Texture Maps;0;0;False;2;TTFE_Drawer_SingleLineTexture;Space (5);False;-1;None;f342feadc66e9a04b9941beb835befdd;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.CommentaryNode;673;-16,2976;Inherit;False;404;187;;2;651;653;Specular_Output;1,0.4,0,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;573;-2336,2080;Inherit;False;Property;_RockNormalIntensity;Rock Normal Intensity;18;0;Create;True;0;0;0;False;0;False;1;1;-3;3;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;600;-720,2352;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;158;-768,2592;Float;False;Property;_Float5;Main Ao Intensity;9;0;Create;False;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;658;-752,2704;Inherit;False;Property;_MaskAoIntensity;Mask Ao Intensity;8;1;[Header];Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;664;-1008,2752;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;665;-960,2992;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;685;-144,2176;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;611;-1632,2752;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;446;-400,3072;Inherit;True;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;579;-1792,448;Inherit;True;Property;_DetailAlbedo;Detail Albedo;2;2;[Header];[NoScaleOffset];Create;True;1;Texture Maps;0;0;False;2;TTFE_Drawer_SingleLineTexture;Space (5);False;-1;None;None;True;0;False;gray;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;566;-1824,1504;Inherit;True;Property;_RockAlbedo;Rock Albedo;12;2;[Header];[NoScaleOffset];Create;True;1;Texture Maps;0;0;False;2;TTFE_Drawer_SingleLineTexture;Space (5);False;-1;None;7786648498cc37049804e1da3f3e0808;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.CommentaryNode;699;-224,-496;Inherit;False;564;659;;8;692;688;694;693;695;696;697;698;Custom DRAWERS;0,0,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;675;-64,1776;Inherit;False;228;187;;1;656;Smoothness_Output;1,0.4,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;672;48,2576;Inherit;False;228;187;;1;657;Ao_Output;1,0.4,0,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;610;-2160,784;Inherit;False;Property;_DetailNormalIntensity;Detail Normal Intensity;6;1;[Header];Create;True;1;Detail Texture Settings;0;0;False;0;False;0;0;-3;3;0;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;662;-1296,352;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorSpaceDouble;603;-1296,432;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;608;-1296,624;Inherit;False;Property;_RockColor;Rock Color;17;0;Create;True;0;0;0;False;0;False;1,1,1,0;1,1,1,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.WireNode;663;-1120,1440;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.PowerNode;178;-448,2400;Inherit;True;True;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;660;-448,2736;Inherit;False;True;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;678;-112,1168;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;667;-1104,2720;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;601;-624,1872;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;651;48,3040;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;567;-1824,1728;Inherit;True;Property;_RockNormal;Rock Normal;13;2;[NoScaleOffset];[Normal];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;5af57fde76c871440aa83608609d6cd1;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;575;-2000,2608;Inherit;True;Property;_CoverNormal;Cover Normal;22;2;[NoScaleOffset];[Normal];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;2b9b807f14b1a1740954e8305a3bcce9;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.CommentaryNode;677;-432,656;Inherit;False;311;304;;1;592;Albedo_Output;1,0.4,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;676;-432,1040;Inherit;False;334;304;;1;606;Normal Map_Output;1,0.4,0,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;605;-1008,368;Inherit;True;4;4;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;657;96,2624;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;656;0,1840;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;686;-864,928;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.WireNode;687;-704,1184;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;597;-624,1616;Inherit;True;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SaturateNode;653;208,3056;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;692;-48,-368;Inherit;False;Property;_BASELAYER; BASE LAYER;1;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (5);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;694;-48,-288;Inherit;False;Property;_DIVIDER_01;DIVIDER_01;10;0;Create;True;0;0;0;False;1;TTFE_DrawerDivider;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;693;-48,-208;Inherit;False;Property;_ROCKLAYER;ROCK LAYER;11;1;[Header];Create;True;0;0;0;False;1;TTFE_DrawerFeatureBorder;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;695;-48,-128;Inherit;False;Property;_DIVIDER_02;DIVIDER_02;19;0;Create;True;0;0;0;False;1;TTFE_DrawerDivider;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;696;-48,-32;Inherit;False;Property;_COVERLAYER;COVER LAYER;20;0;Create;True;0;0;0;False;1;TTFE_DrawerFeatureBorder;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;697;-48,48;Inherit;False;Property;_DIVIDER_03;DIVIDER_03;33;0;Create;True;0;0;0;False;1;TTFE_DrawerDivider;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;578;-1792,672;Inherit;True;Property;_DetailNormal;Detail Normal;3;2;[NoScaleOffset];[Normal];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;None;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;688;-176,-448;Inherit;False;Property;_TTFELIGHTSIMPLECLIFFMASKED;(TTFE-LIGHT) SIMPLE CLIFF MASKED;0;0;Create;True;0;0;0;False;1;TTFE_DrawerTitle;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;674;496,1856;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;671;560,2496;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;655;704,2960;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BlendNormalsNode;606;-368,1088;Inherit;True;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;592;-368,720;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;698;208,-272;Inherit;False;7;7;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;976,736;Float;False;True;-1;5;;0;0;StandardSpecular;Toby Fredson/The Toby Foliage Engine/(TTFE) Simple Cliff Masked;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
Node;AmplifyShaderEditor.CommentaryNode;476;-2784,3504;Inherit;False;1370.785;100;;0;GLOBAL PROPERTIES;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;477;-3958.464,-2760.383;Inherit;False;4397.492;100;;0;TRIPLANAR LAYERS;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;479;-624,320;Inherit;False;935.5962;100;;0;FINAL OUTPUTS;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;661;-2784,64;Inherit;False;1370.785;100;;0;MATERIAL MAPS;1,1,1,1;0;0
WireConnection;618;0;617;0
WireConnection;620;0;618;0
WireConnection;620;1;619;0
WireConnection;621;0;618;0
WireConnection;621;1;620;0
WireConnection;622;0;621;0
WireConnection;645;0;643;1
WireConnection;645;1;643;3
WireConnection;625;0;622;1
WireConnection;625;1;624;0
WireConnection;625;2;623;0
WireConnection;615;0;613;2
WireConnection;615;1;612;0
WireConnection;647;0;645;0
WireConnection;647;1;650;0
WireConnection;626;0;625;0
WireConnection;614;0;615;0
WireConnection;648;0;647;0
WireConnection;583;0;582;0
WireConnection;583;1;581;0
WireConnection;632;0;614;0
WireConnection;632;1;626;0
WireConnection;654;0;583;0
WireConnection;654;1;648;0
WireConnection;633;0;585;2
WireConnection;633;1;632;0
WireConnection;576;1;654;0
WireConnection;635;0;633;0
WireConnection;684;0;576;4
WireConnection;572;0;571;0
WireConnection;572;1;570;0
WireConnection;652;0;635;0
WireConnection;683;0;682;0
WireConnection;683;1;684;0
WireConnection;666;0;576;2
WireConnection;679;0;683;0
WireConnection;679;1;652;0
WireConnection;568;1;572;0
WireConnection;574;1;654;0
WireConnection;600;0;568;2
WireConnection;600;1;666;0
WireConnection;600;2;635;0
WireConnection;664;0;577;2
WireConnection;665;0;576;4
WireConnection;685;0;679;0
WireConnection;611;0;574;0
WireConnection;611;1;565;0
WireConnection;446;0;448;0
WireConnection;446;1;447;0
WireConnection;446;2;449;0
WireConnection;566;1;572;0
WireConnection;662;0;579;0
WireConnection;663;0;566;0
WireConnection;178;0;600;0
WireConnection;178;1;158;0
WireConnection;660;0;664;0
WireConnection;660;1;658;0
WireConnection;678;0;577;4
WireConnection;667;0;611;0
WireConnection;601;0;568;4
WireConnection;601;1;665;0
WireConnection;601;2;635;0
WireConnection;651;0;685;0
WireConnection;651;1;446;0
WireConnection;567;1;572;0
WireConnection;567;5;573;0
WireConnection;575;1;654;0
WireConnection;605;0;662;0
WireConnection;605;1;603;0
WireConnection;605;2;663;0
WireConnection;605;3;608;0
WireConnection;657;0;660;0
WireConnection;657;1;178;0
WireConnection;656;0;678;0
WireConnection;656;1;601;0
WireConnection;686;0;667;0
WireConnection;687;0;635;0
WireConnection;597;0;567;0
WireConnection;597;1;575;0
WireConnection;597;2;635;0
WireConnection;653;0;651;0
WireConnection;578;5;610;0
WireConnection;674;0;656;0
WireConnection;671;0;657;0
WireConnection;655;0;653;0
WireConnection;606;0;578;0
WireConnection;606;1;597;0
WireConnection;592;0;605;0
WireConnection;592;1;686;0
WireConnection;592;2;687;0
WireConnection;698;0;688;0
WireConnection;698;1;692;0
WireConnection;698;2;694;0
WireConnection;698;3;693;0
WireConnection;698;4;695;0
WireConnection;698;5;696;0
WireConnection;698;6;697;0
WireConnection;0;0;592;0
WireConnection;0;1;606;0
WireConnection;0;2;698;0
WireConnection;0;3;655;0
WireConnection;0;4;674;0
WireConnection;0;5;671;0
ASEEND*/
//CHKSM=F266A28450BB0D3FA08232C2D2DBB61696139EA1