#include "Shadow.h"

float4x4 World;
float4x4 ViewProjection;

float2 BlurStep;
float2 DepthBias;

struct VertexShaderInput
{
    float4 Position			: POSITION0;
};

struct BlurVertexInput
{
	float4 Position			: POSITION0;
	float2 TexCoord			: TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position			: POSITION0;
	float  Depth			: COLOR0;
};

struct BlurVertexOutput
{
	float4 Position			: POSITION0;
	float2 TexCoord			: TEXCOORD0;
};

VertexShaderOutput ShadowVS(VertexShaderInput input, float4x4 worldTransform)
{
    VertexShaderOutput output = (VertexShaderOutput)0;

    float4 worldPosition = mul(input.Position, worldTransform);
    output.Position = mul(worldPosition, ViewProjection);

    output.Depth = output.Position.z / output.Position.w;

    return output;
}

BlurVertexOutput BlurVS(BlurVertexInput input)
{
    BlurVertexOutput output = (BlurVertexOutput)0;

    output.Position = input.Position;
    output.TexCoord = input.TexCoord;

    return output;
}

VertexShaderOutput ShadowInstancedVS( VertexShaderInput input, float4x4 instanceTransform : TEXCOORD2)
{
	float4x4 worldTransform = mul( transpose(instanceTransform), World  );
	return ShadowVS(input, worldTransform); 
}

VertexShaderOutput ShadowNonInstancedVS( VertexShaderInput input )
{
	return ShadowVS(input, World);
}

float4 ShadowPS(VertexShaderOutput input) : COLOR0
{
	float depthSlopeBias = max(
		abs(ddx(input.Depth)), 
		abs(ddy(input.Depth))
	);
	return float4( input.Depth + depthSlopeBias * DepthBias.x + DepthBias.y, input.Depth*input.Depth, 0, 0 );
}

technique Shadow
{
    pass Pass1
    {
		VertexShader = compile vs_3_0 ShadowNonInstancedVS();
        PixelShader = compile ps_3_0 ShadowPS();
    }
}

technique ShadowInstanced
{
    pass Pass1
    {
		VertexShader = compile vs_3_0 ShadowInstancedVS();
        PixelShader = compile ps_3_0 ShadowPS();
    }
}

#ifdef VSM
float4 BlurPS( BlurVertexOutput input ) : COLOR0
{

//	float2 halfPixel = float2(0.5/1024, 0.5/1024);
	float2 halfPixel = float2(0.0, 0.0);

	float4 result = float4(0,0,0,0);

#define GAUSSIAN_BLUR
#ifdef GAUSSIAN_BLUR
	result += tex2D( ShadowMapSampler, input.TexCoord + halfPixel ) * 6.0 / 16.0;
	result += tex2D( ShadowMapSampler, input.TexCoord - BlurStep + halfPixel) * 4.0 / 16.0;
	result += tex2D( ShadowMapSampler, input.TexCoord + BlurStep + halfPixel ) * 4.0 / 16.0;
	result += tex2D( ShadowMapSampler, input.TexCoord - 2 * BlurStep + halfPixel ) * 1.0 / 16.0;
	result += tex2D( ShadowMapSampler, input.TexCoord + 2 * BlurStep + halfPixel ) * 1.0 / 16.0;
#else
	const float weight = 1.0 / 5.0;
	result += tex2D( ShadowMapSampler, input.TexCoord + halfPixel ) * weight;
	result += tex2D( ShadowMapSampler, input.TexCoord - BlurStep + halfPixel) * weight;
	result += tex2D( ShadowMapSampler, input.TexCoord + BlurStep + halfPixel ) * weight;
	result += tex2D( ShadowMapSampler, input.TexCoord - 2 * BlurStep + halfPixel ) *weight;
	result += tex2D( ShadowMapSampler, input.TexCoord + 2 * BlurStep + halfPixel ) * weight;
#endif

	return result;
}


technique Blur
{
	pass Pass1
	{
		VertexShader = compile vs_1_1 BlurVS();
        PixelShader = compile ps_2_0 BlurPS();
	}
}
#endif
