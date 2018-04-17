const float MAX_DIST_M = 20.0; // same as depth map
const float NEAR_INFINITY = 0.99f;

Texture2D virtDepthTex;
Texture2D realColorTex;
Texture2D realDepthTex;
Texture2D colorMapper;
Texture2D depthMapper;

SamplerState virtDepthSamp { Texture = <virtDepthTex>; };
SamplerState realColorSamp { Texture = <realColorTex>; };
SamplerState colorMapperSamp { Texture = <colorMapper>; };
SamplerState depthMapperSamp { Texture = <depthMapper>; };
SamplerState realDepthSamp
{
	Texture = <realDepthTex>;
	Filter = None; // avoid floating depths on sharp depth edges
};

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

float ConvertBgra4444MmToUnitFloat(float4 inp) {
	// depth mm
	float depth = inp.a * 16 * 16 * 16 * 16
		+ inp.r * 16 * 16 * 16
		+ inp.g * 16 * 16
		+ inp.b * 16;
	return depth / 1000 / MAX_DIST_M; // depth m / max dist
}

float4 PixelShaderFunction(PixelInputType inp) : SV_TARGET
{
	float virtDepth = virtDepthTex.Sample(virtDepthSamp, inp.texCoord).x;
	float realDepth = ConvertBgra4444MmToUnitFloat(realDepthTex.Sample(realDepthSamp,
		depthMapper.Sample(depthMapperSamp, inp.texCoord).xy));

	if (realDepth < virtDepth && (realDepth != 0 || virtDepth > NEAR_INFINITY))
		return realColorTex.Sample(realColorSamp, colorMapper.Sample(colorMapperSamp, inp.texCoord).xy);
	else
		return float4(0, 0, 0, 0); // transparent -> see through virtual objects
}

technique Technique1
{
	pass Pass1
	{
		VertexShader = compile vs_5_0 VertexShaderFunction();
		PixelShader = compile ps_5_0 PixelShaderFunction();
	}
}