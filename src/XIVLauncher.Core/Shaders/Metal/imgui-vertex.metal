#include <metal_stdlib>
using namespace metal;

struct VS_INPUT
{
    float2 pos [[ attribute(0) ]];
    float2 uv [[ attribute(1) ]];
    float4 col [[ attribute(2) ]];
};

struct PS_INPUT
{
    float4 pos [[ position ]];
    float4 col;
    float2 uv;
};

vertex PS_INPUT VS(
    VS_INPUT input [[ stage_in ]],
    constant float4x4 &ProjectionMatrix [[ buffer(1) ]])
{
    PS_INPUT output;
    output.pos = ProjectionMatrix * float4(input.pos.xy, 0.f, 1.f);
    output.col = input.col;
    output.uv = input.uv;
    return output;
}