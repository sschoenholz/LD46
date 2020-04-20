using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using UnityEngine;

public class ColorSystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle input_deps)
    {
        float dt = Time.DeltaTime;
        int person_count = SimulationManager.sim.person_count;
        int wearing_mask = SimulationManager.sim.wearing_mask;

        var deps = Entities.ForEach(
            (int entityInQueryIndex, ref URPMaterialPropertyBaseColor c, in PersonData p) =>
        {
            if (p.infected)
            {
                c = new URPMaterialPropertyBaseColor()
                {
                    Value = new float4(1f, 0f, 0f, 1f)
                };
            }
            else if (p.resistence)
            {
                c = new URPMaterialPropertyBaseColor()
                {
                    Value = new float4(0f, 0f, 1f, 1f)
                };
            }
            else if (entityInQueryIndex > person_count - wearing_mask)
            {
                c = new URPMaterialPropertyBaseColor()
                {
                    Value = new float4(1f, 0.3f, 0.66f, 1f)
                };
            }
            else
            {
                c = new URPMaterialPropertyBaseColor()
                {
                    Value = new float4(240f / 256, 184f / 256, 160f / 256, 1f)
                };
            }
        }).Schedule(input_deps);

        return deps;
    }
}