using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct HeadingData : IComponentData
{
    public float speed;
    public float2 direction;
    public int2 goal_coordinate;
    public float next_cross_margin;
    public Random rng;
}