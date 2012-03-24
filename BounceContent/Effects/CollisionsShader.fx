float4x4 WorldViewProjection;

float3 MaterialColor;

struct VertexShaderInput
{
    float3 Position			: POSITION0;
    float3 Normal			: NORMAL;
	float3 Colour			: COLOR0;
};

struct VertexShaderOutput
{
    float4 Position			: POSITION0;
    float4 Color			: COLOR0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;

	input.Position.xyz += input.Normal * 0.1;	
    output.Position = mul(float4(input.Position,1.0), WorldViewProjection);
    
	output.Color = float4(MaterialColor, input.Colour.r);	
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    return input.Color;
}

technique VertexAlpha
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}

