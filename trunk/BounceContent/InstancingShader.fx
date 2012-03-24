float4x4 World;
float4x4 View;
float4x4 Projection;

// ***** Light properties *****
float3 LightPosition;
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
    float2 TexCoord			: TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position			: POSITION0;
    float2 TexCoord			: TEXCOORD0;
    float4 Color			: COLOR0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);

	// calculate the light direction ( from the surface to the light ), which is not
    // normalized and is in world space
    float3 LightDirection = normalize( worldPosition - LightPosition );
    float3 Normal = mul( input.Normal, World );
    
    // similarly, calculate the view direction, from the eye to the surface.  not
    // normalized, in world space.
    float3 eyePosition = mul(-View._m30_m31_m32, transpose(View));    
    float3 ViewDirection = normalize( eyePosition - worldPosition );    
    
	// calculate halfway vector
	float3 halfwayVector = normalize( (normalize(-LightDirection) + ViewDirection) );
	
	// evaluate lighting equation
	float n_dot_l = dot( Normal, -LightDirection );
	float n_dot_h = dot( Normal, halfwayVector );
	float4 coeffs = lit( n_dot_l, n_dot_h, SpecularPower );
	
	coeffs.z *= Shininess;
	    
	output.Color = (0.8 * coeffs.y + AmbientLightColor) * MaterialColor + 0.33* coeffs.z;
	output.TexCoord = input.TexCoord;
	
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    return input.Color;
}

// On Windows shader 3.0 cards, we can use hardware instancing, reading
// the per-instance world transform directly from a secondary vertex stream.
VertexShaderOutput InstancedVertexShader( VertexShaderInput input,
                                                float4x4 instanceTransform : TEXCOORD2)
{
//	World = mul( World, transpose(instanceTransform) );
	World = mul( transpose(instanceTransform), World  );
	return VertexShaderFunction(input);
}


technique Light
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 InstancedVertexShader();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}

