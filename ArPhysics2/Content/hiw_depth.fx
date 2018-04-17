const float GRADIENT_LOOP_MM = 500;

Texture2D depthTex;
Texture2D depthMapper;
SamplerState depthMapperSamp { Texture = <depthMapper>; };
SamplerState depthSamp
{
	Texture = <depthTex>;
	Filter = None;
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

float ConvertBgra4444ToFloat(float4 inp) {
	return inp.a * 16 * 16 * 16 * 16
		+ inp.r * 16 * 16 * 16
		+ inp.g * 16 * 16
		+ inp.b * 16;
}

float4 PixelShaderFunction(PixelInputType inp) : SV_TARGET
{
	float depth = ConvertBgra4444ToFloat(depthTex.Sample(depthSamp, depthMapper.Sample(depthMapperSamp, inp.texCoord).xy));
	float gray = (depth % GRADIENT_LOOP_MM) / GRADIENT_LOOP_MM;
	return float4(gray, gray, gray, 1);
}

technique Technique1
{
	pass Pass1
	{
		VertexShader = compile vs_5_0 VertexShaderFunction();
		PixelShader = compile ps_5_0 PixelShaderFunction();
	}
}