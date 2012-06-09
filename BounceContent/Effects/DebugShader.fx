#include "Shadow.h"

float4x4 World;
float4x4 ViewProjection;
float4x4 InverseShadowTransform;

float2 TextureScale = float2(0,1);
float2 TextureOffset = float2(0,0);
float4 TintColor;
int SplitIndex = 0;

texture2D DebugTexture;
sampler DebugTextureSampler = sampler_state 
{	
	texture = <DebugTexture>; 
	magfilter = POINT;	
	minfilter = POINT;	
	mipfilter = POINT;	
	AddressU  = clamp;	
	AddressV  = clamp; 
};

struct VertexShaderInput
{
    float4 Position			: POSITION0;
	float4 Color			: COLOR0;
	float2 TexCoord			: TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position			: POSITION0;
	float4 Color			: COLOR0;
	float2 TexCoord			: TEXCOORD0;
	ShadowData Shadow		: TEXCOORD3;
};

struct ShadowModelVSOutput
{
	float4 Position			: POSITION0;
	float Depth				: COLOR0;
};

VertexShaderOutput DebugVS(VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;

	float4 worldPosition = mul(input.Position, World);
    output.Position = mul(worldPosition, ViewProjection);
    output.Color = input.Color;
	output.TexCoord = input.TexCoord;
	output.Shadow = GetShadowData( worldPosition, output.Position);

    return output;
}

ShadowModelVSOutput ShadowModelVS(float4 Position : POSITION0)
{
    ShadowModelVSOutput output = (ShadowModelVSOutput)0;

	float4 worldPosition = mul(Position, World);
    output.Position = mul(worldPosition, ViewProjection);
    output.Depth = output.Position.z / output.Position.w;

    return output;
}

VertexShaderOutput ReconstructSceneVS(float4 Position : POSITION0)
{
	VertexShaderOutput output = (VertexShaderOutput)0;

	float2 texCoords = Position.xy / 2.0f + TextureOffset;// + 0.75f / ShadowMapSize;
    float depth = tex2Dlod( ShadowMapSampler, float4(texCoords, 0, 1) ).r;

	float4 shadowClipPosition = float4(texCoords,  Position.z == 0 ? depth : 1.0f, 1); //float4(texCoords, depth, 1);
	float4 shadowWorldPosition = mul(shadowClipPosition, InverseShadowTransform);
	
	output.Position = mul(shadowWorldPosition, ViewProjection);
	output.Color = Position.w * TintColor;
//	output.Color = (1-depth) * TintColor;
	output.Shadow = GetShadowData( shadowWorldPosition / shadowWorldPosition.w, 0 );

    return output;
}

float4 ReconstructedScenePS(VertexShaderOutput input) : COLOR0
{
	ShadowSplitInfo splitInfo = GetSplitInfo(input.Shadow);
	if(splitInfo.SplitIndex != SplitIndex)
		discard;

	return float4(input.Color.rgb, 0.5f);

}

float4 ColorPS(VertexShaderOutput input) : COLOR0
{
	return input.Color;
}

float4 TexturePS(VertexShaderOutput input) : COLOR0
{
	float4 texColor = tex2D( DebugTextureSampler, input.TexCoord );
	return (texColor - TextureScale.x) / (TextureScale.y - TextureScale.x) * input.Color;
}

float4 ShadowTexturePS(VertexShaderOutput input) : COLOR0
{
	float texColor = tex2D( DebugTextureSampler, input.TexCoord ).x;
	float3 depthColor = (texColor - TextureScale.x) / (TextureScale.y - TextureScale.x) * input.Color.rgb;
	return float4(depthColor, input.Color.a);
}

float4 ShadowFactorPS(VertexShaderOutput input) : COLOR0
{
//return float4( input.Shadow.TexCoords_0_1.xy, 0, 1);
	return GetShadowFactor(input.Shadow) * GetSplitIndexColor(input.Shadow);
}

float4 ShadowModelPS(ShadowModelVSOutput input) : COLOR0
{
	return input.Depth;
}


technique Color
{
    pass Pass1
    {
		VertexShader = compile vs_1_1 DebugVS();
        PixelShader = compile ps_2_0 ColorPS();
    }
}

technique Texture
{
    pass Pass1
    {
		VertexShader = compile vs_1_1 DebugVS();
        PixelShader = compile ps_2_0 TexturePS();
    }
}

technique ShadowTexture
{
    pass Pass1
    {
		VertexShader = compile vs_1_1 DebugVS();
        PixelShader = compile ps_2_0 ShadowTexturePS();
    }
}

technique ShadowModel
{
	pass Pass1
	{
		VertexShader = compile vs_1_1 ShadowModelVS();
		PixelShader = compile ps_2_0 ShadowModelPS();
	}
}

technique Shadow
{
	pass Pass1
	{
		VertexShader = compile vs_3_0 DebugVS();
		PixelShader = compile ps_3_0 ShadowFactorPS();
	}
}

technique ReconstructScene
{
	pass Pass1
	{
		VertexShader = compile vs_3_0 ReconstructSceneVS();
		PixelShader = compile ps_3_0 ReconstructedScenePS();
	}
}