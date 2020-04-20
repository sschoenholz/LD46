using Unity.Entities;
using Unity.Mathematics;

public enum BuildingType
{
    Apartment,
    Office,
    Recreation
};

[GenerateAuthoringComponent]
public struct BuildingData : IComponentData
{
    public BuildingType type;
    public int start_hash;
    public int end_hash;
    public int people_per_room;
}