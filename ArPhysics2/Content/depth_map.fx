// Shader utilizzato per estrarre la depth-map virtuale

const float MAX_DIST_M = 20.0;

float4x4 WVP;

float4 VertexShaderFunction(float4 pos : POSITION0) : SV_POSITION
{
	return mul(pos, WVP);
}

float4 PixelShaderFunction(float4 pos : SV_POSITION) : SV_TARGET
{
	float dist = pos.w / MAX_DIST_M; // pos.w is distance in meters
	return float4(dist, 1, 1, 1);
}

technique Technique1
{
	pass Pass1
	{
		VertexShader = compile vs_5_0 VertexShaderFunction();
		PixelShader = compile ps_5_0 PixelShaderFunction();
	}
}
