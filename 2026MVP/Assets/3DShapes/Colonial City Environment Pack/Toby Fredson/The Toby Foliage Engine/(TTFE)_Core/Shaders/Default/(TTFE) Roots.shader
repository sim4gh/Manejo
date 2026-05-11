// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Toby Fredson/The Toby Foliage Engine/(TTFE) Roots"
{
	Properties
	{
		[TTFE_DrawerTitle]_TTFELIGHTROOTSSHADER("(TTFE-LIGHT) ROOTS SHADER", Float) = 0
		_Cutoff( "Mask Clip Value", Float ) = 0.2
		[TTFE_DrawerFeatureBorder][Space (5)]_TEXTUREMAPS("TEXTURE MAPS", Float) = 0
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture][Space (5)]_AlbedoMap("Albedo Map", 2D) = "white" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture]_NormalMap("Normal Map", 2D) = "bump" {}
		[NoScaleOffset][TTFE_Drawer_SingleLineTexture]_MaskMap("Mask Map", 2D) = "white" {}
		[TTFE_DrawerDivider]_DIVIDER_01("DIVIDER_01", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "TransparentCutout"  "Queue" = "AlphaTest+0" "IsEmissive" = "true"  }
		Cull Off
		CGPROGRAM
		#pragma target 4.5
		#define ASE_VERSION 19801
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows dithercrossfade 
		struct Input
		{
			float2 uv_texcoord;
			half ASEIsFrontFacing : VFACE;
		};

		uniform sampler2D _NormalMap;
		uniform sampler2D _AlbedoMap;
		uniform float _TTFELIGHTROOTSSHADER;
		uniform float _TEXTUREMAPS;
		uniform float _DIVIDER_01;
		uniform sampler2D _MaskMap;
		uniform float _Cutoff = 0.2;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_NormalMap2 = i.uv_texcoord;
			float3 break11 = UnpackNormal( tex2D( _NormalMap, uv_NormalMap2 ) );
			float3 appendResult13 = (float3(break11.x , break11.y , ( break11.z * i.ASEIsFrontFacing )));
			o.Normal = appendResult13;
			float2 uv_AlbedoMap1 = i.uv_texcoord;
			float4 tex2DNode1 = tex2D( _AlbedoMap, uv_AlbedoMap1 );
			o.Albedo = tex2DNode1.rgb;
			float3 temp_cast_1 = (( _TTFELIGHTROOTSSHADER + _TEXTUREMAPS + _DIVIDER_01 )).xxx;
			o.Emission = temp_cast_1;
			float2 uv_MaskMap3 = i.uv_texcoord;
			float4 tex2DNode3 = tex2D( _MaskMap, uv_MaskMap3 );
			o.Smoothness = tex2DNode3.a;
			o.Occlusion = tex2DNode3.g;
			o.Alpha = 1;
			clip( tex2DNode1.a - _Cutoff );
		}

		ENDCG
	}
	Fallback "Diffuse"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.CommentaryNode;8;-634.2241,136.1256;Inherit;False;460.4878;320.1864;;4;13;12;11;10;Fix Backface (Branch);1,1,1,1;0;0
Node;AmplifyShaderEditor.SamplerNode;2;-927.7933,193.955;Inherit;True;Property;_NormalMap;Normal Map;4;1;[NoScaleOffset];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;71e742f8438c6e54c91bb098edd2b3b8;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.CommentaryNode;24;-672,-496;Inherit;False;478;332;;4;23;25;22;21;DRAWERS;0,0,0,1;0;0
Node;AmplifyShaderEditor.BreakToComponentsNode;11;-609.7833,194.3297;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.FaceVariableNode;10;-605.2241,360.1259;Inherit;False;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;12;-474.2239,328.1259;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;3;-438.0489,494.6184;Inherit;True;Property;_MaskMap;Mask Map;5;1;[NoScaleOffset];Create;True;0;0;0;False;1;TTFE_Drawer_SingleLineTexture;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;21;-624,-448;Inherit;False;Property;_TTFELIGHTROOTSSHADER;(TTFE-LIGHT) ROOTS SHADER;0;0;Create;True;0;0;0;False;1;TTFE_DrawerTitle;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;25;-528,-368;Inherit;False;Property;_TEXTUREMAPS;TEXTURE MAPS;2;0;Create;True;0;0;0;False;2;TTFE_DrawerFeatureBorder;Space (5);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;23;-528,-288;Inherit;False;Property;_DIVIDER_01;DIVIDER_01;6;0;Create;True;0;0;0;False;1;TTFE_DrawerDivider;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;14;-105.4639,434.4944;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;15;-120.4639,234.4944;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;13;-323.2246,197.1258;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode;1;-432,-96;Inherit;True;Property;_AlbedoMap;Albedo Map;3;1;[NoScaleOffset];Create;True;0;0;0;False;2;TTFE_Drawer_SingleLineTexture;Space (5);False;-1;None;806e380996355d647a6242fe9813dd50;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleAddOpNode;22;-336,-384;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;0,0;Float;False;True;-1;5;;0;0;Standard;Toby Fredson/The Toby Foliage Engine/(TTFE) Roots;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;False;False;Off;0;False;;0;False;;False;0;False;;0;False;;False;0;Masked;0.2;True;True;0;False;TransparentCutout;;AlphaTest;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;11;0;2;0
WireConnection;12;0;11;2
WireConnection;12;1;10;0
WireConnection;14;0;3;4
WireConnection;15;0;3;2
WireConnection;13;0;11;0
WireConnection;13;1;11;1
WireConnection;13;2;12;0
WireConnection;22;0;21;0
WireConnection;22;1;25;0
WireConnection;22;2;23;0
WireConnection;0;0;1;0
WireConnection;0;1;13;0
WireConnection;0;2;22;0
WireConnection;0;4;14;0
WireConnection;0;5;15;0
WireConnection;0;10;1;4
ASEEND*/
//CHKSM=6300B6600DB06040564A6BEA0EA49E165DF573A2