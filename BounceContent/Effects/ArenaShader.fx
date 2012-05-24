#include "Shadow.h"

float4x4 World;
float4x4 View;
float4x4 Projection;

// ***** Light properties *****
float3 LightDirection;
float4 AmbientLightColor;
float4 LightColor;

// ****************************

// ***** material properties *****
float4 EmissiveColor;
float Shininess;
float SpecularPower;
// *******************************

texture2D Texture;
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
    float3 ViewDirection    : TEXCOORD2;

	float3 Tangent			: TANGENT;
	float3 Binormal			: BINORMAL;
    float3 Normal			: NORMAL;

	ShadowData Shadow		: TEXCOORD3;
};



VertexShaderOutput ArenaVertexShader(VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);
        
    // calculate the view direction, from the eye to the surface.  not
    // normalized, in world space.
    float3 eyePosition = mul(-View._m30_m31_m32, transpose(View));    
    output.ViewDirection = worldPosition - eyePosition;    
    
    output.Tangent = mul(input.Tangent, World);
    output.Binormal = mul(input.Binormal, World);
    output.Normal = mul(input.Normal, World);
    
    output.TexCoord = input.TexCoord;
	output.Shadow = GetShadowData( worldPosition, output.Position);
 
    return output;
}

float3 evaluateLightingEquation( float3 LightDirection, float3 ViewDirection, float3 Normal	)
{
    ViewDirection = normalize( ViewDirection );
    Normal = normalize( Normal );
    
    // calculate phong diffuse light component
    float nDotL = saturate( dot( Normal, LightDirection ) );
    
	// TODO: use half-vector for specular 
    float3 reflectedLight = reflect( LightDirection, Normal );
    float rDotV = saturate (dot( reflectedLight, ViewDirection ) ); 
    float specular = Shininess * pow( rDotV, SpecularPower );
    
    return float3( 1, nDotL, specular );
}

float4 NormalMappingPS(VertexShaderOutput input) : COLOR0
{
    float3 normalFromMap = tex2D(NormalMapSampler, input.TexCoord);
    normalFromMap = normalFromMap.x * input.Tangent + normalFromMap.y * input.Binormal + normalFromMap.z * input.Normal;

	float4 texColor = tex2D(TextureSampler, input.TexCoord);
	float3 lightCoeffs = evaluateLightingEquation( LightDirection, input.ViewDirection, normalFromMap ); 
	
	float4 ambient = AmbientLightColor * lightCoeffs.x;
	float4 diffuse = LightColor * lightCoeffs.y;
	float4 specular = LightColor * lightCoeffs.z;

	float shadow =  GetShadowFactor( input.Shadow, lightCoeffs.y );
	return EmissiveColor + (ambient + diffuse * shadow) * texColor + specular*shadow;

}

float4 NormalMappingNoTexturePS(VertexShaderOutput input) : COLOR0
{
    float3 normalFromMap = tex2D(NormalMapSampler, input.TexCoord);
    normalFromMap = normalFromMap.x * input.Tangent + normalFromMap.y * input.Binormal + normalFromMap.z * input.Normal;

	float3 lightCoeffs = evaluateLightingEquation( LightDirection, input.ViewDirection, normalFromMap ); 
	
	float4 ambient = AmbientLightColor * lightCoeffs.x;
	float4 diffuse = LightColor * lightCoeffs.y;
	float4 specular = LightColor * lightCoeffs.z;
	
	float shadow =  GetShadowFactor( input.Shadow, lightCoeffs.y );
	return EmissiveColor + (ambient + diffuse * shadow) * float4( 1, 1, 1, 1 ) + specular * shadow;

}

float4 SplitIndexAsColorPS(VertexShaderOutput input) : COLOR0
{
	float3 lightCoeffs = evaluateLightingEquation( LightDirection, input.ViewDirection, input.Normal ); 
	return GetSplitIndexColor( input.Shadow ) * GetShadowFactor( input.Shadow, lightCoeffs.y );
}

float4 StandardPS(VertexShaderOutput input) : COLOR0
{
	float3 lightCoeffs = evaluateLightingEquation( LightDirection, input.ViewDirection, input.Normal ); 
	float4 texColor = tex2D(TextureSampler, input.TexCoord);
	
	float4 ambient = AmbientLightColor * lightCoeffs.x;
	float4 diffuse = LightColor * lightCoeffs.y;
	float4 specular = LightColor * lightCoeffs.z;
	
	float shadow =  GetShadowFactor( input.Shadow, lightCoeffs.y );
	return EmissiveColor + (ambient + diffuse * shadow) * texColor + specular * shadow;
}

float4 StandardNoTexturePS(VertexShaderOutput input) : COLOR0
{
	float3 lightCoeffs = evaluateLightingEquation( LightDirection, input.ViewDirection, input.Normal ); 

	float4 ambient = AmbientLightColor * lightCoeffs.x;
	float4 diffuse = LightColor * lightCoeffs.y;
	float4 specular = LightColor * lightCoeffs.z;
	
	float shadow =  GetShadowFactor( input.Shadow, lightCoeffs.y );
	return EmissiveColor + (ambient + diffuse* shadow) * float4( 1, 1, 1, 1 ) + specular*shadow;
}


technique LightTexturesNormalmaps
{
    pass Pass1
    {
		VertexShader = compile vs_3_0 ArenaVertexShader();
        PixelShader = compile ps_3_0 NormalMappingPS();
    }
}

technique LightTextures
{
    pass Pass1
    {
		VertexShader = compile vs_3_0 ArenaVertexShader();
        PixelShader = compile ps_3_0 StandardPS();
    }
}

technique LightNormalmaps
{
    pass Pass1
    {
		VertexShader = compile vs_3_0 ArenaVertexShader();
        PixelShader = compile ps_3_0 NormalMappingNoTexturePS();
    }
}

technique Light
{
    pass Pass1
    {
		VertexShader = compile vs_3_0 ArenaVertexShader();
        PixelShader = compile ps_3_0 StandardNoTexturePS();
    }
}

technique ShadowSplitIndex
{
    pass Pass1
    {
		VertexShader = compile vs_3_0 ArenaVertexShader();
        PixelShader = compile ps_3_0 SplitIndexAsColorPS();
    }
}

