using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;

public class SicknessSystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle input_deps)
    {
        float dt = Time.DeltaTime * SimulationManager.sim.speed_multiplier * 0.1f;

        float antibody_reproduction_rate_per_virus = SimulationManager.sim.antibody_reproduction_rate_per_virus;
        float virus_reproduction_rate = SimulationManager.sim.virus_reproduction_rate;
        float antibody_virus_kill_rate = SimulationManager.sim.antibody_virus_kill_rate;

        var has_started = SimulationManager.sim.has_started;

        float ambient = has_started ? SimulationManager.sim.ambient_chance_of_getting_sick_per_time : 0f;

        var deps = Entities.ForEach((ref PersonData p, ref HeadingData h) => {
            if (p.infected)
            {
                float eff_antibody = antibody_reproduction_rate_per_virus * p.virus;
                p.antibodies += eff_antibody * dt;
                p.virus *= Mathf.Pow(virus_reproduction_rate, dt);
                p.virus -= antibody_virus_kill_rate * p.antibodies * dt;
                if (p.virus < 0f)
                {
                    p.virus = 0f;
                    p.infected = false;
                    p.resistence = true;
                }
            } else if (h.rng.NextFloat() < ambient * dt && !p.resistence)
            {
                p.virus = 0.1f;
                p.infected = true;
            }
        }).Schedule(input_deps);

        return deps;
    }
}