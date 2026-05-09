// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Toby Fredson/The Toby Foliage Engine/Utility/(TTFE) Global Controller"
{
	Properties
	{
		[TTFE_DrawerTitle]_TTFELIGHTGLOBALCONTROLLERSHADER("(TTFE-LIGHT) GLOBAL CONTROLLER SHADER", Float) = 0
		[TTFE_DrawerFeatureBorder][Space (5)]_TEXTUREMAPS1("TEXTURE MAPS", Float) = 0
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture][Space (5)]_Albedo("Albedo", 2D) = "white" {}
		[NoScaleOffset][Normal][TTFE_Drawer_SingleLineTexture]_Normal("Normal", 2D) = "bump" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture]_Mask("Mask", 2D) = "white" {}
		[TTFE_DrawerDivider]_DIVIDER_01("DIVIDER_01", Float) = 0
		[TTFE_DrawerFeatureBorder]_TEXTUREMAPS("WIND SETTINGS", Float) = 0
		[Header((Global Wind Settings))]_GlobalWindStrength("Global Wind Strength", Range( 0 , 1)) = 1
		[KeywordEnum(GentleBreeze,WindOff)] _WindType("Wind Type", Float) = 0
		[Header((Trunk and Branch))]_BranchWindLarge("Branch Wind Large", Range( 0 , 20)) = 1
		_BranchWindSmall("Branch Wind Small", Range( 0 , 20)) = 1
		[Toggle]_PivotSway("Pivot Sway", Float) = 0
		_PivotSwayPower("Pivot Sway Power", Float) = 1
		[TTFE_DrawerDivider]_DIVIDER_05("DIVIDER_05", Float) = 0
		[TTFE_DrawerFeatureBorder]_WINDMASKSETTINGS("WIND MASK SETTINGS", Float) = 0
		[Header((Wind Mask))]_Radius("Radius", Float) = 1
		_Hardness("Hardness", Float) = 1
		[Toggle]_CenterofMass("Center of Mass", Float) = 0
		[TTFE_DrawerDivider]_DIVIDER_06("DIVIDER_06", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma shader_feature_local _WINDTYPE_GENTLEBREEZE _WINDTYPE_WINDOFF
		#define ASE_VERSION 19801
		#pragma surface surf Standard keepalpha noshadow vertex:vertexDataFunc 
		struct Input
		{
			float3 worldPos;
			float2 uv_texcoord;
			float3 worldNormal;
			INTERNAL_DATA
		};

		uniform float _GlobalWindStrength;
		uniform float _Radius;
		uniform float _Hardness;
		uniform float _BranchWindLarge;
		uniform float _CenterofMass;
		uniform float _BranchWindSmall;
		uniform float _PivotSway;
		uniform float _PivotSwayPower;
		uniform float _TEXTUREMAPS;
		uniform float _DIVIDER_05;
		uniform float _WINDMASKSETTINGS;
		uniform float _DIVIDER_06;
		uniform sampler2D _Normal;
		uniform sampler2D _Albedo;
		uniform sampler2D _Mask;
		uniform float _TTFELIGHTGLOBALCONTROLLERSHADER;
		uniform float _TEXTUREMAPS1;
		uniform float _DIVIDER_01;


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


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float3 ase_positionOS = v.vertex.xyz;
			float3 appendResult939_g1 = (float3(0.0 , 0.0 , saturate( ase_positionOS ).z));
			float3 break989_g1 = ase_positionOS;
			float3 appendResult938_g1 = (float3(break989_g1.x , ( break989_g1.y * 0.15 ) , 0.0));
			float mulTime975_g1 = _Time.y * 2.1;
			float3 temp_output_624_0_g1 = ( ( ase_positionOS - float3(0,-1,0) ) / _Radius );
			float dotResult625_g1 = dot( temp_output_624_0_g1 , temp_output_624_0_g1 );
			float temp_output_628_0_g1 = pow( saturate( dotResult625_g1 ) , _Hardness );
			float SphearicalMaskCM763_g1 = saturate( temp_output_628_0_g1 );
			float3 temp_cast_0 = (ase_positionOS.y).xxx;
			float2 appendResult928_g1 = (float2(ase_positionOS.x , ase_positionOS.z));
			float3 temp_output_996_0_g1 = ( cross( temp_cast_0 , float3( appendResult928_g1 ,  0.0 ) ) * 0.005 );
			float3 appendResult931_g1 = (float3(0.0 , ase_positionOS.y , 0.0));
			float3 break971_g1 = ase_positionOS;
			float3 appendResult967_g1 = (float3(break971_g1.x , 0.0 , ( break971_g1.z * 0.15 )));
			float mulTime976_g1 = _Time.y * 2.3;
			float dotResult849_g1 = dot( (ase_positionOS*0.02 + 0.0) , ase_positionOS );
			float CenterOfMassThicknessMask854_g1 = saturate( dotResult849_g1 );
			float3 appendResult981_g1 = (float3(ase_positionOS.x , 0.0 , 0.0));
			float3 break984_g1 = ase_positionOS;
			float3 appendResult966_g1 = (float3(0.0 , ( break984_g1.y * 0.2 ) , ( break984_g1.z * 0.4 )));
			float mulTime977_g1 = _Time.y * 2.0;
			float3 ase_positionWS = mul( unity_ObjectToWorld, v.vertex );
			float3 normalizeResult765_g1 = normalize( ase_positionWS );
			float mulTime772_g1 = _Time.y * 0.25;
			float simplePerlin2D769_g1 = snoise( ( normalizeResult765_g1 + mulTime772_g1 ).xy*0.43 );
			float WindMask_LargeB770_g1 = ( simplePerlin2D769_g1 * 1.5 );
			float3 normalizeResult1092_g1 = normalize( ase_positionOS );
			float CenterOfMassTrunkUP_C1098_g1 = saturate( distance( normalizeResult1092_g1 , float3(0,1,0) ) );
			float3 normalizeResult774_g1 = normalize( ase_positionWS );
			float mulTime780_g1 = _Time.y * 0.26;
			float simplePerlin2D778_g1 = snoise( ( normalizeResult774_g1 + mulTime780_g1 ).xy*0.7 );
			float WindMask_LargeC779_g1 = ( simplePerlin2D778_g1 * 1.5 );
			float mulTime906_g1 = _Time.y * 3.2;
			float3 worldToObj907_g1 = mul( unity_WorldToObject, float4( ase_positionOS, 1 ) ).xyz;
			float3 temp_output_872_0_g1 = ( mulTime906_g1 + ( 0.02 * worldToObj907_g1.x ) + ( worldToObj907_g1.y * 0.14 ) + ( worldToObj907_g1.z * 0.16 ) + float3(0.4,0.3,0.1) );
			float3 normalizeResult632_g1 = normalize( ase_positionOS );
			float CenterOfMassTrunkUP636_g1 = saturate( (distance( normalizeResult632_g1 , float3(0,1,0) )*1.0 + -0.05) );
			float3 ase_objectScale = float3( length( unity_ObjectToWorld[ 0 ].xyz ), length( unity_ObjectToWorld[ 1 ].xyz ), length( unity_ObjectToWorld[ 2 ].xyz ) );
			float mulTime905_g1 = _Time.y * 2.3;
			float3 worldToObj908_g1 = mul( unity_WorldToObject, float4( ase_positionOS, 1 ) ).xyz;
			float3 temp_output_866_0_g1 = ( mulTime905_g1 + ( 0.2 * worldToObj908_g1 ) + float3(0.4,0.3,0.1) );
			float mulTime904_g1 = _Time.y * 3.6;
			float3 temp_cast_4 = (ase_positionOS.x).xxx;
			float3 worldToObj910_g1 = mul( unity_WorldToObject, float4( temp_cast_4, 1 ) ).xyz;
			float temp_output_898_0_g1 = ( mulTime904_g1 + ( 0.2 * worldToObj910_g1.x ) );
			float3 normalizeResult697_g1 = normalize( ase_positionOS );
			float CenterOfMass701_g1 = saturate( (distance( normalizeResult697_g1 , float3(0,1,0) )*2.0 + 0.0) );
			float SphericalMaskProxySphere704_g1 = (( _CenterofMass )?( ( temp_output_628_0_g1 * CenterOfMass701_g1 ) ):( temp_output_628_0_g1 ));
			float3 worldToObj1131_g1 = mul( unity_WorldToObject, float4( ase_positionOS, 1 ) ).xyz;
			float mulTime1138_g1 = _Time.y * 4.0;
			float mulTime1129_g1 = _Time.y * 0.2;
			float2 appendResult1126_g1 = (float2(ase_positionWS.x , ase_positionWS.z));
			float2 normalizeResult1128_g1 = normalize( appendResult1126_g1 );
			float simpleNoise1139_g1 = SimpleNoise( ( mulTime1129_g1 + normalizeResult1128_g1 )*1.0 );
			float WindMask_SimpleSway1145_g1 = ( ( simpleNoise1139_g1 * 1.5 ) * _PivotSwayPower );
			float3 rotatedValue1151_g1 = RotateAroundAxis( float3( 0,0,0 ), ase_positionOS, normalize( float3(0.6,1,0.1) ), ( ( cos( ( ( worldToObj1131_g1 * 0.02 ) + mulTime1138_g1 + ( float3(0.6,1,0.8) * 0.3 * worldToObj1131_g1 ) ) ) * 0.1 ) * WindMask_SimpleSway1145_g1 * saturate( ase_objectScale ) ).x );
			float3 temp_cast_6 = (0.0).xxx;
			#if defined( _WINDTYPE_GENTLEBREEZE )
				float3 staticSwitch1044_g1 = ( ( ( ( ( ( ( ( ( appendResult939_g1 + ( appendResult938_g1 * cos( mulTime975_g1 ) ) + ( cross( float3(1.2,0.6,1) , ( appendResult938_g1 * float3(0.7,1,0.8) ) ) * sin( mulTime975_g1 ) ) ) * SphearicalMaskCM763_g1 * temp_output_996_0_g1 ) * 0.08 ) + ( ( ( appendResult931_g1 + ( appendResult967_g1 * cos( mulTime976_g1 ) ) + ( cross( float3(0.9,1,1.2) , ( appendResult967_g1 * float3(1,1,1) ) ) * sin( mulTime976_g1 ) ) ) * SphearicalMaskCM763_g1 * CenterOfMassThicknessMask854_g1 * temp_output_996_0_g1 ) * 0.1 ) + ( ( ( appendResult981_g1 + ( appendResult966_g1 * cos( mulTime977_g1 ) ) + ( cross( float3(1.1,1.3,0.8) , ( appendResult966_g1 * float3(1.4,0.8,1.1) ) ) * sin( mulTime977_g1 ) ) ) * SphearicalMaskCM763_g1 * temp_output_996_0_g1 ) * 0.05 ) ) * _BranchWindLarge ) * WindMask_LargeB770_g1 ) * CenterOfMassTrunkUP_C1098_g1 ) + ( ( ( WindMask_LargeC779_g1 * ( ( ( ( cos( temp_output_872_0_g1 ) * sin( temp_output_872_0_g1 ) * CenterOfMassTrunkUP636_g1 * SphearicalMaskCM763_g1 * CenterOfMassThicknessMask854_g1 * saturate( ase_objectScale ) ) * 0.2 ) + ( ( cos( temp_output_866_0_g1 ) * sin( temp_output_866_0_g1 ) * CenterOfMassTrunkUP636_g1 * CenterOfMassThicknessMask854_g1 * SphearicalMaskCM763_g1 * saturate( ase_objectScale ) ) * 0.2 ) + ( ( sin( temp_output_898_0_g1 ) * cos( temp_output_898_0_g1 ) * SphericalMaskProxySphere704_g1 * CenterOfMassThicknessMask854_g1 * CenterOfMassTrunkUP636_g1 ) * 0.2 ) ) * _BranchWindSmall ) ) * 0.3 ) * CenterOfMassTrunkUP_C1098_g1 ) + (( _PivotSway )?( ( ( rotatedValue1151_g1 - ase_positionOS ) * 0.4 ) ):( float3( 0,0,0 ) )) ) * saturate( ase_positionOS.y ) );
			#elif defined( _WINDTYPE_WINDOFF )
				float3 staticSwitch1044_g1 = temp_cast_6;
			#else
				float3 staticSwitch1044_g1 = ( ( ( ( ( ( ( ( ( appendResult939_g1 + ( appendResult938_g1 * cos( mulTime975_g1 ) ) + ( cross( float3(1.2,0.6,1) , ( appendResult938_g1 * float3(0.7,1,0.8) ) ) * sin( mulTime975_g1 ) ) ) * SphearicalMaskCM763_g1 * temp_output_996_0_g1 ) * 0.08 ) + ( ( ( appendResult931_g1 + ( appendResult967_g1 * cos( mulTime976_g1 ) ) + ( cross( float3(0.9,1,1.2) , ( appendResult967_g1 * float3(1,1,1) ) ) * sin( mulTime976_g1 ) ) ) * SphearicalMaskCM763_g1 * CenterOfMassThicknessMask854_g1 * temp_output_996_0_g1 ) * 0.1 ) + ( ( ( appendResult981_g1 + ( appendResult966_g1 * cos( mulTime977_g1 ) ) + ( cross( float3(1.1,1.3,0.8) , ( appendResult966_g1 * float3(1.4,0.8,1.1) ) ) * sin( mulTime977_g1 ) ) ) * SphearicalMaskCM763_g1 * temp_output_996_0_g1 ) * 0.05 ) ) * _BranchWindLarge ) * WindMask_LargeB770_g1 ) * CenterOfMassTrunkUP_C1098_g1 ) + ( ( ( WindMask_LargeC779_g1 * ( ( ( ( cos( temp_output_872_0_g1 ) * sin( temp_output_872_0_g1 ) * CenterOfMassTrunkUP636_g1 * SphearicalMaskCM763_g1 * CenterOfMassThicknessMask854_g1 * saturate( ase_objectScale ) ) * 0.2 ) + ( ( cos( temp_output_866_0_g1 ) * sin( temp_output_866_0_g1 ) * CenterOfMassTrunkUP636_g1 * CenterOfMassThicknessMask854_g1 * SphearicalMaskCM763_g1 * saturate( ase_objectScale ) ) * 0.2 ) + ( ( sin( temp_output_898_0_g1 ) * cos( temp_output_898_0_g1 ) * SphericalMaskProxySphere704_g1 * CenterOfMassThicknessMask854_g1 * CenterOfMassTrunkUP636_g1 ) * 0.2 ) ) * _BranchWindSmall ) ) * 0.3 ) * CenterOfMassTrunkUP_C1098_g1 ) + (( _PivotSway )?( ( ( rotatedValue1151_g1 - ase_positionOS ) * 0.4 ) ):( float3( 0,0,0 ) )) ) * saturate( ase_positionOS.y ) );
			#endif
			float3 FinalWind_Output1060_g1 = ( ( _GlobalWindStrength * staticSwitch1044_g1 ) + _TEXTUREMAPS + _DIVIDER_05 + _WINDMASKSETTINGS + _DIVIDER_06 );
			v.vertex.xyz += FinalWind_Output1060_g1;
			v.vertex.w = 1;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_Normal3 = i.uv_texcoord;
			o.Normal = UnpackNormal( tex2D( _Normal, uv_Normal3 ) );
			float2 uv_Albedo2 = i.uv_texcoord;
			float4 tex2DNode2 = tex2D( _Albedo, uv_Albedo2 );
			float2 uv_Mask4 = i.uv_texcoord;
			float4 tex2DNode4 = tex2D( _Mask, uv_Mask4 );
			o.Albedo = ( tex2DNode2 * saturate( tex2DNode4.g ) ).rgb;
			float4 color10 = IsGammaSpace() ? float4(0.2156863,0.5607843,0.2,1) : float4(0.03820438,0.2746773,0.03310476,1);
			float3 ase_positionWS = i.worldPos;
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_positionWS );
			float3 ase_viewDirWS = normalize( ase_viewVectorWS );
			float3 ase_normalWS = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float fresnelNdotV5 = dot( ase_normalWS, ase_viewDirWS );
			float fresnelNode5 = ( -0.1 + 5.0 * pow( 1.0 - fresnelNdotV5, 5.0 ) );
			o.Emission = ( saturate( ( color10 * fresnelNode5 ) ) + (tex2DNode2*0.4 + 0.0) ).rgb;
			o.Metallic = ( _TTFELIGHTGLOBALCONTROLLERSHADER + _TEXTUREMAPS1 + _DIVIDER_01 );
			o.Smoothness = tex2DNode4.a;
			o.Occlusion = tex2DNode4.g;
			o.Alpha = 1;
		}

		ENDCG
	}
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.FresnelNode;5;-769.7689,-336.0271;Inherit;True;Standard;WorldNormal;ViewDir;False;False;5;0;FLOAT3;0,0,1;False;4;FLOAT3;0,0,0;False;1;FLOAT;-0.1;False;2;FLOAT;5;False;3;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;10;-697.8334,-522.2871;Inherit;False;Constant;_Color2;Color 2;0;0;Create;True;0;0;0;False;0;False;0.2156863,0.5607843,0.2,1;0.2196077,0.5529411,0.1999998,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.CommentaryNode;21;-880,-928;Inherit;False;478;332;;4;25;23;22;29;DRAWERS;0,0,0,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;8;-434.1929,-403.9306;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;4;-748.3151,281.4371;Inherit;True;Property;_Mask;Mask;4;1;[NoScaleOffset];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;97cbfaa1a982c434d9829a9ab41c5b0d;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;2;-770.438,-110.2618;Inherit;True;Property;_Albedo;Albedo;2;1;[NoScaleOffset];Create;True;3;__________(TTFE) TREE GIZMO SHADER___________;_____________________________________________________;Texture Maps;0;0;False;2;TTFE_Drawer_SingleLineTexture;Space (5);False;-1;None;4465c0aae8371694d8400e4dc45b23e3;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;23;-736,-800;Inherit;False;Property;_TEXTUREMAPS1;TEXTURE MAPS;1;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (5);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;29;-736,-704;Inherit;False;Property;_DIVIDER_01;DIVIDER_01;5;0;Create;True;0;0;0;False;1;TTFE_DrawerDivider;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;22;-848,-880;Inherit;False;Property;_TTFELIGHTGLOBALCONTROLLERSHADER;(TTFE-LIGHT) GLOBAL CONTROLLER SHADER;0;0;Create;True;0;0;0;False;1;TTFE_DrawerTitle;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;7;-294.5463,-290.6969;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.WireNode;18;-466.0032,206.8213;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;15;-445.4461,284.9381;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;12;-429.237,-109.4421;Inherit;False;3;0;COLOR;0,0,0,0;False;1;FLOAT;0.4;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;25;-528,-816;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;6;-146.635,-179.5742;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;14;-267.7063,175.2692;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.WireNode;19;-65.42141,294.0568;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;20;-79.86507,274.3609;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;3;-765.7982,90.28165;Inherit;True;Property;_Normal;Normal;3;2;[NoScaleOffset];[Normal];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;3;None;4199ccd0e0911f74f9589bfd1dc792a4;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.FunctionNode;1;-319.9271,429.1129;Inherit;False;(TTFE) Tree Bark_Wind System;6;;1;58360699feb112c40b86ba9ba75062e6;0;0;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode;30;-48,-640;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;0,0;Float;False;True;-1;2;;0;0;Standard;Toby Fredson/The Toby Foliage Engine/Utility/(TTFE) Global Controller;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;Opaque;0.5;True;False;0;False;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;False;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;8;0;10;0
WireConnection;8;1;5;0
WireConnection;7;0;8;0
WireConnection;18;0;2;0
WireConnection;15;0;4;2
WireConnection;12;0;2;0
WireConnection;25;0;22;0
WireConnection;25;1;23;0
WireConnection;25;2;29;0
WireConnection;6;0;7;0
WireConnection;6;1;12;0
WireConnection;14;0;18;0
WireConnection;14;1;15;0
WireConnection;19;0;4;4
WireConnection;20;0;4;2
WireConnection;30;0;25;0
WireConnection;0;0;14;0
WireConnection;0;1;3;0
WireConnection;0;2;6;0
WireConnection;0;3;30;0
WireConnection;0;4;19;0
WireConnection;0;5;20;0
WireConnection;0;11;1;0
ASEEND*/
//CHKSM=18E891C2A39521466DA1982B0A43444A62C38FE9