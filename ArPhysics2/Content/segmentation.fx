const float4 bodyColor = float4(0, 0, 1, 1); // blue
const float4 planeColor = float4(1, 0, 0, 1); // red
const float4 background = float4(1, 1, 1, 1); // white

float4 planeEq;

Texture2D depthTex;
Texture2D bodyTex;
Texture2D depthMapper;
SamplerState depthSamp { Texture = <realDepthTex>; };
SamplerState bodySamp { Texture = <realDepthTex>; };
SamplerState depthMapperSamp { Texture = <depthMapper>; };

struct PixelInputType
{
	float4 pos : SV_POSITION;
	float2 texCoord : TEXCOORD0;
};

PixelInputType VertexShaderFunction(float4 pos : POSITION, float2 texCoord : TEXCOORD0)
{
	PixelInputType outp;
	outp.pos = pos;
	outp.texCoord = texCoord;
	return outp;
}

float4 PixelShaderFunction(PixelInputType inp) : SV_TARGET
{
	float depth = depthTex.Sample(depthSamp, depthMapper.Sample(depthMapperSamp, inp.texCoord).xy).x;
	float body = bodyTex.Sample(depthSamp, depthMapper.Sample(depthMapperSamp, inp.texCoord).xy).a;
	if (body != 0)
		return bodyColor;
	//todo plane
	return background;
}

technique Technique1
{
	pass Pass1
	{
		VertexShader = compile vs_5_0 VertexShaderFunction();
		PixelShader = compile ps_5_0 PixelShaderFunction();
	}
}