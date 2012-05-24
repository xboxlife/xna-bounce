float4x4 World;
float4x4 View;
float4x4 Projection;

// ***** Light properties *****
float3 LightPosition;
float4 AmbientLightColor;
float4 LightColor;

// ****************************

// ***** material properties *****
float4 EmissiveColor;
float Shininess;
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


uniform texture2D NormalMap;
sampler NormalMapSampler = sampler_state
{
	texture = <NormalMap>;
	magfilter = LINEAR;
	minfilter = LINEAR;
	mipfilter = LINEAR;
	AddressU  = wrap;
	AddressV  = wrap;
};


// TODO: add effect parameters here.

struct VertexShaderInput
{
    float4 Position			: POSITION0;
    float3 Normal			: NORMAL;
    float2 TexCoord			: TEXCOORD0;
    float3 Binormal			: BINORMAL0;
    float3 Tangent			: TANGENT0;
};

struct VertexShaderOutput
{
    float4 Position			: POSITION0;
    float2 TexCoord			: TEXCOORD0;
    float3 LightDirection   : TEXCOORD1;
    float3 ViewDirection    : TEXCOORD2;
    float3x3 TangentToWorld : TEXCOORD3;
    float3 Normal			: COLOR0;
};

VertexShaderOutput ArenaVertexShader(VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);
    
    output.Normal = mul( input.Normal, World );
   
	// calculate the light direction ( from the surface to the light ), which is not
    // normalized and is in world space
    output.LightDirection = LightPosition - worldPosition;
        
    // similarly, calculate the view direction, from the eye to the surface.  not
    // normalized, in world space.
    float3 eyePosition = mul(-View._m30_m31_m32, transpose(View));    
    output.ViewDirection = worldPosition - eyePosition;    
    
    // calculate tangent space to world space matrix using the world space tangent,
    // binormal, and normal as basis vectors.  the pixel shader will normalize these
    // in case the world matrix has scaling.
    output.TangentToWorld[0] = mul(input.Tangent, World);
    output.TangentToWorld[1] = mul(input.Binormal, World);
    output.TangentToWorld[2] = mul(input.Normal, World);
    
    // pass the texture coordinate through without additional processing
    output.TexCoord = input.TexCoord;
    
    return output;
}

float3 evaluateLightingEquation( float3 LightDirection, float3 ViewDirection, float3 Normal	)
{
    ViewDirection = normalize( ViewDirection );
    LightDirection = normalize( LightDirection );    
    Normal = normalize( Normal );
    
    // calculate phong diffuse light component
    float nDotL = max( dot( Normal, LightDirection ), 0 );
    
    // use phong to calculate specular highlights: reflect the incoming light
    // vector off the normal, and use a dot product to see how "similar"
    // the reflected vector is to the view vector.    
    float3 reflectedLight = reflect( LightDirection, Normal );
    float rDotV = max( dot( reflectedLight, ViewDirection ), 0.00001 ); // use small epsilon instead of 0 to work around compiler error: "Nan and infinity literals not allowed by shader model"
    float specular = Shininess * pow( rDotV, SpecularPower );
    
    return float3( 1, nDotL, specular );
}

float4 NormalMappingPS(VertexShaderOutput input) : COLOR0
{
	// look up the normal from the normal map, and transform from tangent space
    // into world space using the matrix created above.  normalize the result
    // in case the matrix contains scaling.
    float3 normalFromMap = tex2D(NormalMapSampler, input.TexCoord);
    normalFromMap = mul(normalFromMap, input.TangentToWorld);

	float4 texColor = tex2D(TextureSampler, input.TexCoord);

	float3 lightCoeffs = evaluateLightingEquation( input.LightDirection, input.ViewDirection, normalFromMap ); 
	
	float4 ambient = AmbientLightColor * lightCoeffs.x;
	float4 diffuse = LightColor * lightCoeffs.y;
	float4 specular = LightColor * lightCoeffs.z;
	
	return EmissiveColor + (ambient + diffuse) * texColor + specular;
}

float4 NormalMappingNoTexturePS(VertexShaderOutput input) : COLOR0
{
	// look up the normal from the normal map, and transform from tangent space
    // into world space using the matrix created above.  normalize the result
    // in case the matrix contains scaling.
    float3 normalFromMap = tex2D(NormalMapSampler, input.TexCoord);
    normalFromMap = mul(normalFromMap, input.TangentToWorld);

	float3 lightCoeffs = evaluateLightingEquation( input.LightDirection, input.ViewDirection, normalFromMap ); 
	
	float4 ambient = AmbientLightColor * lightCoeffs.x;
	float4 diffuse = LightColor * lightCoeffs.y;
	float4 specular = LightColor * lightCoeffs.z;
	
	return EmissiveColor + (ambient + diffuse) * float4( 1, 1, 1, 1 ) + specular;

}

float4 StandardPS(VertexShaderOutput input) : COLOR0
{
	float3 lightCoeffs = evaluateLightingEquation( input.LightDirection, input.ViewDirection, input.Normal ); 
	float4 texColor = tex2D(TextureSampler, input.TexCoord);
	
	float4 ambient = AmbientLightColor * lightCoeffs.x;
	float4 diffuse = LightColor * lightCoeffs.y;
	float4 specular = LightColor * lightCoeffs.z;
	
	
	return EmissiveColor + (ambient + diffuse) * texColor + specular;
}

float4 StandardNoTexturePS(VertexShaderOutput input) : COLOR0
{
	float3 lightCoeffs = evaluateLightingEquation( input.LightDirection, input.ViewDirection, input.Normal ); 
		
	float4 ambient = AmbientLightColor * lightCoeffs.x;
	float4 diffuse = LightColor * lightCoeffs.y;
	float4 specular = LightColor * lightCoeffs.z;
	
	return EmissiveColor + (ambient + diffuse) * float4( 1, 1, 1, 1 ) + specular;
}


technique LightTexturesNormalmaps
{
    pass Pass1
    {
		VertexShader = compile vs_1_1 ArenaVertexShader();
        PixelShader = compile ps_2_0 NormalMappingPS();
    }
}

technique LightTextures
{
    pass Pass1
    {
		VertexShader = compile vs_1_1 ArenaVertexShader();
        PixelShader = compile ps_2_0 StandardPS();
    }
}

technique LightNormalmaps
{
    pass Pass1
    {
		VertexShader = compile vs_1_1 ArenaVertexShader();
        PixelShader = compile ps_2_0 NormalMappingNoTexturePS();
    }
}

technique Light
{
    pass Pass1
    {
		VertexShader = compile vs_1_1 ArenaVertexShader();
        PixelShader = compile ps_2_0 StandardNoTexturePS();
    }
}


