// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Toby Fredson/The Toby Foliage Engine/(TTFE) Mesh"
{
	Properties
	{
		[TTFE_DrawerTitle]_TTFELIGHTROOTSSHADER("(TTFE-LIGHT) ROOTS SHADER", Float) = 0
		[TTFE_DrawerFeatureBorder][Space (5)]_TEXTUREMAPS("TEXTURE MAPS", Float) = 0
		[TTFE_Drawer_SingleLineTexture][Space (5)]_AlbedoMap("Albedo Map", 2D) = "white" {}
		[Normal][TTFE_Drawer_SingleLineTexture]_NormalMap("Normal Map", 2D) = "bump" {}
		[TTFE_Drawer_SingleLineTexture]_MaskMap("Mask Map", 2D) = "white" {}
		[TTFE_DrawerDivider]_DIVIDER_2("DIVIDER_01", Float) = 0
		[TTFE_DrawerFeatureBorder]_TEXTURESETTINGS("TEXTURE SETTINGS", Float) = 0
		[Header((Albedo))]_AlbedoColor("Albedo Color", Color) = (1,1,1,0)
		[Header((Smoothness))]_SmoothnessAmount("Smoothness Amount", Range( 0 , 1)) = 1
		[TTFE_DrawerDivider]_DIVIDER_01("DIVIDER_01", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#pragma target 3.0
		#define ASE_VERSION 19801
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows dithercrossfade 
		struct Input
		{
			float2 uv_texcoord;
		};

		uniform sampler2D _NormalMap;
		uniform float4 _NormalMap_ST;
		uniform float4 _AlbedoColor;
		uniform sampler2D _AlbedoMap;
		uniform float4 _AlbedoMap_ST;
		uniform float _TTFELIGHTROOTSSHADER;
		uniform float _TEXTUREMAPS;
		uniform float _DIVIDER_01;
		uniform float _TEXTURESETTINGS;
		uniform float _DIVIDER_2;
		uniform float _SmoothnessAmount;
		uniform sampler2D _MaskMap;
		uniform float4 _MaskMap_ST;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_NormalMap = i.uv_texcoord * _NormalMap_ST.xy + _NormalMap_ST.zw;
			o.Normal = UnpackNormal( tex2D( _NormalMap, uv_NormalMap ) );
			float2 uv_AlbedoMap = i.uv_texcoord * _AlbedoMap_ST.xy + _AlbedoMap_ST.zw;
			o.Albedo = ( _AlbedoColor * tex2D( _AlbedoMap, uv_AlbedoMap ) ).rgb;
			float3 temp_cast_1 = (( _TTFELIGHTROOTSSHADER + _TEXTUREMAPS + _DIVIDER_01 + _TEXTURESETTINGS + _DIVIDER_2 )).xxx;
			o.Emission = temp_cast_1;
			float2 uv_MaskMap = i.uv_texcoord * _MaskMap_ST.xy + _MaskMap_ST.zw;
			float4 tex2DNode3 = tex2D( _MaskMap, uv_MaskMap );
			o.Smoothness = ( _SmoothnessAmount * tex2DNode3.a );
			o.Occlusion = tex2DNode3.g;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.CommentaryNode;127;-624,-816;Inherit;False;500;476;;4;131;130;129;128;DRAWERS;0,0,0,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;128;-576,-768;Inherit;False;Property;_TTFELIGHTROOTSSHADER;(TTFE-LIGHT) ROOTS SHADER;0;0;Create;True;0;0;0;False;1;TTFE_DrawerTitle;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;129;-480,-688;Inherit;False;Property;_TEXTUREMAPS;TEXTURE MAPS;1;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (5);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;130;-480,-608;Inherit;False;Property;_DIVIDER_01;DIVIDER_01;9;0;Create;True;0;0;0;False;1;TTFE_DrawerDivider;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;132;-514.9873,-525.4838;Inherit;False;Property;_TEXTURESETTINGS;TEXTURE SETTINGS;6;1;[Header];Create;True;0;0;0;False;1;TTFE_DrawerFeatureBorder;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;133;-482.9873,-445.4838;Inherit;False;Property;_DIVIDER_2;DIVIDER_01;5;0;Create;True;0;0;0;False;1;TTFE_DrawerDivider;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;4;-560,-304;Inherit;False;Property;_AlbedoColor;Albedo Color;7;1;[Header];Create;True;1;(Albedo);0;0;False;0;False;1,1,1,0;1,1,1,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;3;-576,320;Inherit;True;Property;_MaskMap;Mask Map;4;0;Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;1;-592,-80;Inherit;True;Property;_AlbedoMap;Albedo Map;2;0;Create;True;3;__________(TTFE) MESH SHADER___________;_____________________________________________________;Texture Maps;0;0;False;2;TTFE_Drawer_SingleLineTexture;Space (5);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;126;-576,528;Inherit;False;Property;_SmoothnessAmount;Smoothness Amount;8;1;[Header];Create;True;1;(Smoothness);0;0;False;0;False;1;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;5;-288,-272;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;2;-592,128;Inherit;True;Property;_NormalMap;Normal Map;3;1;[Normal];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;None;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleAddOpNode;131;-256,-656;Inherit;False;5;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;125;-272,464;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;0,0;Float;False;True;-1;2;;0;0;Standard;Toby Fredson/The Toby Foliage Engine/(TTFE) Mesh;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;False;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;Opaque;0;True;True;0;False;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;5;0;4;0
WireConnection;5;1;1;0
WireConnection;131;0;128;0
WireConnection;131;1;129;0
WireConnection;131;2;130;0
WireConnection;131;3;132;0
WireConnection;131;4;133;0
WireConnection;125;0;126;0
WireConnection;125;1;3;4
WireConnection;0;0;5;0
WireConnection;0;1;2;0
WireConnection;0;2;131;0
WireConnection;0;4;125;0
WireConnection;0;5;3;2
ASEEND*/
//CHKSM=A17ED8151C3B4F2EAD0BF4797E0FBE78FE0758E5