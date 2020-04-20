using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

class CameraSystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle input_deps) {
        var dt = Time.DeltaTime;

        if (Input.GetMouseButtonDown(0) && !SimulationManager.sim.has_started  && !SimulationManager.sim.has_ended)
        {
            SimulationManager.sim.has_started = true;
            SimulationManager.sim.speed_multiplier = 2f;
            SimulationManager.sim.intro_text.SetActive(false);
        }

        if (!SimulationManager.sim.has_started)
            return input_deps;

        var count = SimulationManager.sim.person_count;
        var people_pos = new NativeArray<float3>(count, Allocator.TempJob);
        var deps = Entities.WithAll<PersonData>()
            .ForEach(
            (int entityInQueryIndex, in Translation Tr) =>
            {
                people_pos[entityInQueryIndex] = Tr.Value;
            }).Schedule(input_deps);

        deps.Complete();

        Camera c = Camera.main;
        CameraData cam = c.GetComponent<CameraData>();
        Transform T = c.GetComponent<Transform>();
        float size = c.orthographicSize;
        float rescale = size / 90f;

        if (Input.mousePosition.x > Screen.width - cam.input_margin)
        {
            c.transform.position += new Vector3(cam.horizontal_speed, 0f, -cam.horizontal_speed) * dt * rescale;
        }
        if (Input.mousePosition.x < cam.input_margin)
        {
            c.transform.position += new Vector3(-cam.horizontal_speed, 0f, cam.horizontal_speed) * dt * rescale;
        }
        if (Input.mousePosition.y > Screen.height - cam.input_margin)
        {
            c.transform.position += new Vector3(cam.vertical_speed, 0f, cam.vertical_speed) * dt * rescale;
        }
        if (Input.mousePosition.y < cam.input_margin)
        {
            c.transform.position += new Vector3(-cam.vertical_speed, 0f, -cam.vertical_speed) * dt * rescale;
        }
        if (Input.GetAxis("Mouse ScrollWheel") > 0f)
        {
            c.orthographicSize -= cam.scroll_speed * dt;
        }
        if (Input.GetAxis("Mouse ScrollWheel") < 0f)
        {
            c.orthographicSize += cam.scroll_speed * dt;
        }
        
        return people_pos.Dispose(deps);
    }
}