using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Rendering;

[GenerateAuthoringComponent]
[MaterialProperty("_Color", MaterialPropertyFormat.Float4)]
public struct ColorData : IComponentData
{
    public float4 Value;
}