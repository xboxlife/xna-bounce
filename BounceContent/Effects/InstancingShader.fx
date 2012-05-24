#include "Shadow.h"

float4x4 World;
float4x4 View;
float4x4 Projection;

// ***** Light properties *****
float4 LightColor;
float4 AmbientLightColor;
float4 MaterialColor;

float3 LightDirection;
// ****************************

// ***** material properties *****

// output from phong specular will be scaled by this amount
float Shininess;

// specular exponent from phong lighting model.  controls the "tightness" of
// specular highlights.
float SpecularPower;
// *******************************


uniform texture2D Texture;
sampler TextureSampler = sampler_state
{
	texture = <Texture>;
	magfilter = LINEAR;
	minfilter = LINEAR;
	mipfilter = LINEAR;
	AddressU  = wrap;
	AddressV  = wrap;
};



struct VertexShaderInput
{
    float4 Position			: POSITION0;
    float3 Normal			: NORMAL;
};

struct VertexShaderOutput
{
    float4 Position			: POSITION0;
    float4 Color			: COLOR0;
	ShadowData Shadow		: TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input, float4x4 worldTransform)
{
    VertexShaderOutput output = (VertexShaderOutput)0;

    float4 worldPosition = mul(input.Position, worldTransform);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);
    float3 Normal = mul( input.Normal, worldTransform );
    
    // similarly, calculate the view direction, from the eye to the surface.  not
    // normalized, in world space.
    float3 eyePosition = mul(-View._m30_m31_m32, transpose(View));    
    float3 ViewDirection = normalize( eyePosition - worldPosition );    
    
	// calculate halfway vector
	float3 halfwayVector = normalize( (normalize(-LightDirection) + ViewDirection) );
	
	// evaluate lighting equation
	float n_dot_l = dot( Normal, LightDirection );
	float n_dot_h = dot( Normal, halfwayVector );
	float4 coeffs = lit( n_dot_l, n_dot_h, SpecularPower );
	
	coeffs.z *= Shininess;
	    
	output.Color = 0.8 * coeffs.y * MaterialColor + 0.33* coeffs.z +  AmbientLightColor * MaterialColor;
	output.Shadow = GetShadowData( worldPosition, output.Position );
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input, float2 vpos : VPOS) : COLOR0
{
	float shadow = GetShadowFactor( input.Shadow );
    return input.Color * saturate(shadow );
}

// On Windows shader 3.0 cards, we can use hardware instancing, reading
// the per-instance world transform directly from a secondary vertex stream
VertexShaderOutput InstancedVertexShader( VertexShaderInput input,
                                                float4x4 instanceTransform : TEXCOORD2)
{
	float4x4 worldTransform = mul( transpose(instanceTransform), World  );
	return VertexShaderFunction(input, worldTransform);
}

VertexShaderOutput NonInstancedVertexShader( VertexShaderInput input )
{
	return VertexShaderFunction(input, World);
}


technique Light
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 InstancedVertexShader();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}

