// Shader utilizzato per estrarre la depth-map virtuale

float4x4 WVP;

float4 VertexShaderFunction(float4 wPos : POSITION0, out float depth : TEXCOORD0) : POSITION0
{
    float4 tPos = mul(wPos, WVP);
    depth = tPos.w;
    return tPos;
}

float4 PixelShaderFunction(float depth : TEXCOORD0) : COLOR0
{
    depth = min(depth, 4);
    float a = floor(depth / 0.256f) / 256;
    float b = floor(fmod(depth, 0.256f) * 1000) / 256;
    return float4(b, a, 0, 0);                          //dispone i byte in modo da poter estrarre i valori come unsigned int
}

technique Technique1
{
    pass Pass1
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
