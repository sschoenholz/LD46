using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[GenerateAuthoringComponent]
public struct PersonData : IComponentData
{
    public float virus;
    public bool infected;
    public float antibodies;
    public bool resistence;

    public int2 home_coordinates;
    public int home_idx;
    public bool is_home;

    public float schedule_phase;
    public bool heading_home;
}