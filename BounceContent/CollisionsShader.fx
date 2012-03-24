float4x4 World;
float4x4 View;
float4x4 Projection;

// ***** Light properties *****
float3 LightPosition;
float4 MaterialColor;
float4 AmbientLightColor;
// ****************************

// ***** material properties *****

// output from phong specular will be scaled by this amount
float Shininess;

// specular exponent from phong lighting model.  controls the "tightness" of
// specular highlights.
float SpecularPower;
// *******************************

struct VertexShaderInput
{
    float4 Position			: POSITION0;
    float3 Normal			: NORMAL;
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
	float3 Normal = mul( input.Normal, World );
	
    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);

	// calculate the light direction ( from the surface to the light ), which is not
    // normalized and is in world space
    float3 LightDirection = normalize( worldPosition - LightPosition );
    
    
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
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{

    return input.Color;
}

technique Light
{
    pass Pass1
    {
        VertexShader = compile vs_1_0 VertexShaderFunction();
        PixelShader = compile ps_1_0 PixelShaderFunction();
    }
}

