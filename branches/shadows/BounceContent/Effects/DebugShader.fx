float4x4 WorldViewProjection;
float2 TextureScale = float2(0,1);

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
};

struct ShadowModelVSOutput
{
	float4 Position			: POSITION0;
	float Depth				: COLOR0;
};

VertexShaderOutput DebugVS(VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;

    output.Position = mul(input.Position, WorldViewProjection);
    output.Color = input.Color;
	output.TexCoord = input.TexCoord;

    return output;
}

ShadowModelVSOutput ShadowModelVS(float4 Position : POSITION0)
{
    ShadowModelVSOutput output = (ShadowModelVSOutput)0;

    output.Position = mul(Position, WorldViewProjection);
    output.Depth = output.Position.z / output.Position.w;

    return output;
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