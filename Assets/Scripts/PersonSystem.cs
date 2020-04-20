using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using UnityEngine;

using Random = UnityEngine.Random;

public class PersonSystem : JobComponentSystem
{
    struct ProcessCells : IJobNativeMultiHashMapMergedSharedKeyIndices
    {
        public NativeArray<int> cell_index;
        public NativeArray<float> cell_virus;
        public NativeArray<int> cell_count;
        public NativeArray<float2> cell_position;

        [ReadOnly]
        public int person_count;
        [ReadOnly]
        public int wearing_mask;

        public void ExecuteFirst(int index)
        {
            cell_index[index] = index;
            if (index > person_count - wearing_mask)
                cell_virus[index] *= 0.35f;
        }

        public void ExecuteNext(int firstIndex, int index)
        {
            cell_count[firstIndex] += 1;
            cell_position[firstIndex] += cell_position[index];
            float virus = cell_virus[index];
            if (index > person_count - wearing_mask)
                virus *= 0.4f;
            cell_virus[firstIndex] += virus;
            cell_index[index] = firstIndex;
        }
    }

    protected override JobHandle OnUpdate(JobHandle input_deps)
    {
        float dt = Time.DeltaTime * SimulationManager.sim.speed_multiplier;
        float current_time = SimulationManager.sim.current_time;
        float day_time = SimulationManager.sim.day_time;

        var home_bound = SimulationManager.sim.home_bound;
        var wearing_mask = SimulationManager.sim.wearing_mask;
        var has_started = SimulationManager.sim.has_started;

        int person_count = SimulationManager.sim.person_count;
        Rect bounds = SimulationManager.sim.bounds;
        float interaction_radius = SimulationManager.sim.interaction_radius;
        float infection_rate_per_virus = SimulationManager.sim.infection_rate_per_virus;
        var max_hash = (int)(bounds.width * bounds.height / interaction_radius / interaction_radius);

        var hash_map = new NativeMultiHashMap<int, int>(person_count, Allocator.TempJob);

        var cell_index = new NativeArray<int>(person_count, Allocator.TempJob);
        var cell_virus = new NativeArray<float>(person_count, Allocator.TempJob);
        var cell_count = new NativeArray<int>(person_count, Allocator.TempJob);
        var cell_position = new NativeArray<float2>(person_count, Allocator.TempJob);

        var room_count = 13 * 13 * SimulationManager.sim.rooms_per_apartment * 4;
        var occupied_count = new NativeArray<int>(room_count, Allocator.TempJob);
        var occupied_virus = new NativeArray<float>(room_count, Allocator.TempJob);
        var occupied_antibodies = new NativeArray<float>(room_count, Allocator.TempJob);

        // Read data into NativeArrays.
        var read_pos_deps = Entities.WithAll<PersonData>().ForEach(
            (int entityInQueryIndex, in Translation R) =>
            {
                cell_position[entityInQueryIndex] = new float2(R.Value.x, R.Value.z);
            }).Schedule(input_deps);

        var read_virus_deps = Entities.ForEach(
        (int entityInQueryIndex, in PersonData p) =>
        {
            cell_virus[entityInQueryIndex] = p.virus;
        }).Schedule(input_deps);

        var initialize_cell_count_deps = new MemsetNativeArray<int>
        {
            Source = cell_count,
            Value =  1
        }.Schedule(person_count, 64, input_deps);

        var initialize_occupied_count_deps = new MemsetNativeArray<int>
        {
            Source = occupied_count,
            Value = 1
        }.Schedule(room_count, 64, input_deps);

        // Place people into the hash map.
        var parallel_hash_map = hash_map.AsParallelWriter();
        var read_hash_deps = Entities.ForEach(
            (int entityInQueryIndex, in Translation R, in PersonData P) =>
            {
                var pos = new float2(R.Value.x, R.Value.z) + new float2(bounds.min.x, bounds.min.y);
                int hash;
                if (P.is_home)
                    hash = max_hash + P.home_idx;
                else
                    hash = (int)math.hash(new int2(math.floor(pos / interaction_radius)));
                parallel_hash_map.Add(hash, entityInQueryIndex);
            }).Schedule(input_deps);

        var read_deps = JobHandle.CombineDependencies(read_pos_deps, read_hash_deps);
        read_deps = JobHandle.CombineDependencies(read_deps, read_virus_deps);
        read_deps = JobHandle.CombineDependencies(read_deps, initialize_cell_count_deps);
        read_deps = JobHandle.CombineDependencies(read_deps, initialize_occupied_count_deps);

        var process_cells_deps = new ProcessCells
        {
            cell_index = cell_index,
            cell_virus = cell_virus,
            cell_count = cell_count,
            cell_position = cell_position,
            wearing_mask = wearing_mask
        }.Schedule(hash_map, 64, read_deps);

        // Virus transmission.
        var random_array = World.GetExistingSystem<RandomSystem>().RandomArray;

        var transmission_deps = Entities
            .WithNativeDisableParallelForRestriction(random_array)
            .ForEach(
            (int entityInQueryIndex, ref PersonData P, in int nativeThreadIndex) =>
            {
                var idx = cell_index[entityInQueryIndex];
                
                if (!P.resistence && !P.infected)
                {
                    var random = random_array[nativeThreadIndex];
                    float p_infect = infection_rate_per_virus * cell_virus[idx] * dt;
                    if (P.is_home)
                        p_infect *= 0.25f;
                    if (entityInQueryIndex > person_count - wearing_mask)
                        p_infect *= 0.4f;
                    if (random.NextFloat() < p_infect)
                    {
                        P.infected = true;
                        P.virus = 0.1f;
                    }
                    random_array[nativeThreadIndex] = random;
                }
            }).Schedule(process_cells_deps);

        // Moving. 
        float tile_size = SimulationManager.sim.tile_size;
        int tiles_per_block = SimulationManager.sim.tiles_per_block;
        
        var move_deps = Entities.ForEach(
            (int entityInQueryIndex, ref Translation T, ref HeadingData heading, in PersonData P) =>
            {
                if (!P.is_home)
                {
                    // Navigate streets.
                    float2 R = new float2(T.Value.x, T.Value.z);
                    float2 dR = (R - new float2(bounds.min.x, bounds.min.y) + 0.5f * tile_size);

                    int2 goal = heading.goal_coordinate;
                    int2 cur = (int2)(dR / tile_size);

                    float2 V = heading.direction;

                    int goal_c = Mathf.Abs(V.x) > 0.5f ? goal.x : goal.y;
                    int cur_c = Mathf.Abs(V.x) > 0.5f ? cur.x : cur.y;
                    float sgn = Mathf.Abs(V.x) > 0.5f ? Mathf.Sign(V.x) : Mathf.Sign(V.y);
                    float dR_c = Mathf.Abs(V.x) > 0.5f ? dR.x : dR.y;
                    dR_c = dR_c - cur_c * tile_size;

                    int d = goal_c - cur_c;

                    if (Mathf.Abs(d) <= 1 &&
                        cur_c % tiles_per_block == 0)
                    {
                        if (((Mathf.Abs(dR_c) > heading.next_cross_margin && Mathf.Abs(dR_c) < tile_size / 2f) ||
                            (Mathf.Abs(dR_c) < tile_size - heading.next_cross_margin && Mathf.Abs(dR_c) > tile_size / 2f)))
                        {
                            if (Mathf.Abs(V.x) > 0.5f)
                            {
                                V.x = 0f;
                                V.y = Mathf.Sign(goal_c - cur_c);
                            }
                            else
                            {
                                V.y = 0f;
                                V.x = Mathf.Sign(goal_c - cur_c);
                            }
                            heading.next_cross_margin = heading.rng.NextFloat(1f, 6f);
                        }
                    }
                    else if (d * sgn < 0)
                        V = -V;

                    //  Move.

                    R = R + V * dt * heading.speed;

                    if (R.x < bounds.min.x)
                        V.x = -V.x;
                    if (R.x > bounds.max.x)
                        V.x = -V.x;

                    if (R.y < bounds.min.y)
                        V.y = -V.y;
                    if (R.y > bounds.max.y)
                        V.y = -V.y;

                    T.Value.x = R.x;
                    T.Value.z = R.y;
                    heading.direction = V;
                } else
                {
                    T.Value = new float3(10000f, 10000f, 10000f);
                }
            }).Schedule(transmission_deps);

        var sim_deps = move_deps;

        int2 tile_count = SimulationManager.sim.tile_count;

        var update_target_deps = Entities.ForEach(
            (int entityInQueryIndex, ref HeadingData heading, ref PersonData P, ref Translation T) =>
            {
                float cycle = Mathf.Sin(current_time / day_time * Mathf.PI + P.schedule_phase * Mathf.PI);
                if (!P.is_home)
                {
                    if ((cycle < 0.25f && !P.heading_home) || entityInQueryIndex < home_bound)
                    {
                        P.heading_home = true;
                        heading.goal_coordinate = P.home_coordinates;
                    }

                    float2 R = new float2(T.Value.x, T.Value.z);
                    float2 dR = (R - new float2(bounds.min.x, bounds.min.y) + 0.5f * tile_size);

                    int2 goal = heading.goal_coordinate;
                    int2 cur = (int2)(dR / tile_size);

                    if (Mathf.Abs(goal.x - cur.x) + Mathf.Abs(goal.y - cur.y) <= 2)
                    {
                        if (P.heading_home)
                            P.is_home = true;
                        else
                        {
                            int2 dcoord = heading.rng.NextInt2(-3, 4);

                            if (heading.goal_coordinate.x + dcoord.x >= tile_count.x - 2 ||
                                heading.goal_coordinate.x + dcoord.x <= 0)
                                dcoord.x = -dcoord.x;
                            if (heading.goal_coordinate.y + dcoord.y >= tile_count.y - 2 ||
                                heading.goal_coordinate.y + dcoord.y <= 0)
                                dcoord.y = -dcoord.y;
                        
                            heading.goal_coordinate += dcoord;
                        }
                    }
                }  else if (cycle > 0.25f && entityInQueryIndex > home_bound)
                {
                    P.heading_home = false;
                    P.is_home = false;

                    int2 coord = P.home_coordinates;
                    int2 in_block = coord % tiles_per_block;
                    int horiz = 0;

                    if (heading.rng.NextFloat() < 0.5f)
                    {
                        coord.x += in_block.x == 1 ? -1 : 1;
                        horiz = 1;
                    }
                    else
                    {
                        coord.y += in_block.y == 1 ? -1 : 1;
                        horiz = 0;
                    }

                    float2 pos2 = ((float2)coord) * tile_size + new float2(bounds.min.x, bounds.min.y);

                    var side = heading.rng.NextFloat() > 0.5f ? 0 : 1;

                    var d_II = heading.rng.NextFloat(-tile_size / 2f, tile_size / 2f);
                    var d_I_ = tile_size / 2f + heading.rng.NextFloat(-6f, -1f);
                    d_I_ = side == 0 ? d_I_ : -d_I_;

                    pos2 += new float2(
                        horiz == 0 ? d_II : d_I_,
                        horiz == 0 ? d_I_ : d_II);

                    T.Value = new float3(pos2.x, 2.15f, pos2.y);
                    
                    float2 dir = new float2(horiz == 0 ? 1f : 0f, horiz == 0 ? 0f : 1f);
                    dir = heading.rng.NextFloat() > 0.5f ? dir : -dir;
                    heading.direction = dir;

                    int2 dcoord = heading.rng.NextInt2(-3, 4);

                    if (heading.goal_coordinate.x + dcoord.x >= tile_count.x - 2 ||
                        heading.goal_coordinate.x + dcoord.x <= 0)
                        dcoord.x = -dcoord.x;
                    if (heading.goal_coordinate.y + dcoord.y >= tile_count.y - 2 ||
                        heading.goal_coordinate.y + dcoord.y <= 0)
                        dcoord.y = -dcoord.y;

                    heading.goal_coordinate += dcoord;
                }
            }).Schedule(sim_deps);

        // Count occupied apartments.

        update_target_deps.Complete();
        Entities.ForEach(
            (in PersonData P) =>
            {
                if (P.is_home)
                {
                    occupied_count[P.home_idx] += 1;
                    occupied_virus[P.home_idx] += P.virus;
                    occupied_antibodies[P.home_idx] += P.antibodies;
                }

            }).Run();

        var count_occupied_deps = Entities.ForEach(
            (int entityInQueryIndex, ref URPMaterialPropertyBaseColor color, in OccupiedData data) =>
            {
                var c = new float4(1f, 1f, 1f, 1f);

                if (occupied_virus[entityInQueryIndex] > 5f)
                    c = new float4(1f, 0f, 0f, 1f);
                else if (occupied_antibodies[entityInQueryIndex] > 5f)
                    c = new float4(0f, 0f, 1f, 1f);

                color = new URPMaterialPropertyBaseColor()
                {
                    Value = (occupied_count[entityInQueryIndex] - 1) / 4f * c
                };
            }).Schedule(input_deps);

        // Disposal
        var dispose_deps = hash_map.Dispose(update_target_deps);
        dispose_deps = JobHandle.CombineDependencies(dispose_deps,
                                                     cell_index.Dispose(sim_deps));
        dispose_deps = JobHandle.CombineDependencies(dispose_deps,
                                                     cell_virus.Dispose(sim_deps));
        dispose_deps = JobHandle.CombineDependencies(dispose_deps,
                                                     cell_count.Dispose(sim_deps));
        dispose_deps = JobHandle.CombineDependencies(dispose_deps,
                                                     cell_position.Dispose(sim_deps));
        dispose_deps = JobHandle.CombineDependencies(dispose_deps,
                                                     occupied_count.Dispose(count_occupied_deps));
        dispose_deps = JobHandle.CombineDependencies(dispose_deps,
                                                     occupied_virus.Dispose(count_occupied_deps));
        dispose_deps = JobHandle.CombineDependencies(dispose_deps,
                                                     occupied_antibodies.Dispose(count_occupied_deps));

        if (has_started)
            SimulationManager.sim.current_time += dt * SimulationManager.sim.time_per_second;

        return dispose_deps;
    }
}