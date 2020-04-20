using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;

public class UpdateStatisticsSystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle input_deps)
    {
        float current_time = SimulationManager.sim.current_time;
        float last_update = SimulationManager.sim.last_update;
        float update_statistics_every = SimulationManager.sim.update_statistics_every;

        var deps = input_deps;
        if (current_time - last_update > update_statistics_every)
        {
            float total_infected = 0f;
            float total_healthy = 0f;
            float total_immune = 0f;

            input_deps.Complete();
            Entities.ForEach(
                (in PersonData P) =>
                {
                    if (P.infected)
                        total_infected += 1f;
                    else if (P.resistence)
                        total_immune += 1f;
                    else
                        total_healthy += 1f;
                }).Run();

            SimulationManager.sim.healthy.Add(total_healthy);
            SimulationManager.sim.infected.Add(total_infected);
            SimulationManager.sim.immune.Add(total_immune);

            SimulationManager.sim.last_update = current_time;
        }

        return deps;
    }
}
