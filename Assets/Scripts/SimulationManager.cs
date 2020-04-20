using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using UnityEngine.SceneManagement;

using Random = UnityEngine.Random;

public class SimulationManager : MonoBehaviour
{
    public static SimulationManager sim;

    public Rect bounds;
    public float interaction_radius;

    public int person_count;
    public float person_speed;
    public float person_speed_spread;

    public GameObject person_prefab;
    Entity person;

    public int2 tile_count;
    public float tile_size;

    public int2 block_count;
    public int tiles_per_block;

    public GameObject vertical_road_prefab;
    Entity vertical_road;

    public GameObject horizontal_road_prefab;
    Entity horizontal_road;

    public GameObject crosswalk_prefab;
    Entity crosswalk;

    public GameObject building_prefab;
    Entity building;

    public GameObject building_occupied_prefab;
    Entity building_occupied;

    public GameObject intro_text;
    public GameObject outro_text;
    public GameObject healthy_text;
    public GameObject recovered_text;
    public GameObject dead_text;

    public Color[] building_colors;
    public int people_per_room_apartment;
    public int rooms_per_apartment;

    public float virus_reproduction_rate;
    public float antibody_reproduction_rate_per_virus;
    public float infection_rate_per_virus;
    public float antibody_virus_kill_rate;

    public float current_time;
    public float time_per_second;
    public float last_update;
    public float update_statistics_every;
    public float speed_multiplier;
    public float day_time;

    public int home_bound;
    public int wearing_mask;

    public float ambient_chance_of_getting_sick_per_time;

    public List<float> healthy;
    public List<float> infected;
    public List<float> immune;

    public bool has_started;
    public bool has_ended;

    EntityManager manager;

    struct Spawn
    {
        public float2 pos;
        public float2 dir;
    };
        
    private Spawn GetSpawnPoint()
    {
        int2 block = new int2(Random.Range(0, block_count.x - 1),
                              Random.Range(0, block_count.y - 1));

        int horiz = Random.Range(0, 2);
        int dist = Random.Range(0, tiles_per_block);

        int2 tile = new int2(horiz == 0 ? dist : 0, horiz == 0 ? 0 : dist);
        tile = tile + block * tiles_per_block;

        float2 point = (float2)(tile) * tile_size;
        point += new float2(bounds.min.x, bounds.min.y);

        int sgn = Random.Range(0, 2);
        float2 dir = new float2(horiz == 0 ? 1f : 0f, horiz == 0 ? 0f : 1f);
        dir = sgn == 0 ? dir : -dir;

        if (dist > 0)
        {
            var side = Random.Range(0, 2);

            var d_II = Random.Range(-tile_size / 2f, tile_size / 2f);
            var d_I_ = tile_size / 2f + Random.Range(-6f, -1f);
            d_I_ = side == 0 ? d_I_ : -d_I_;

            point += new float2(
                horiz == 0 ? d_II : d_I_,
                horiz == 0 ? d_I_ : d_II);
        } else
        {
            var up_down = Random.Range(0, 2);
            var left_right = Random.Range(0, 2);
            var dx = tile_size / 2f + Random.Range(-6f, -1f);
            dx = left_right == 0 ? dx : -dx;
            var dy = tile_size / 2f + Random.Range(-6f, -1f);
            dy = up_down == 0 ? dy : -dy;
            point += new float2(dx, dy);
        }

        return new Spawn() { pos = point, dir = dir };
    }

    private void InstantiatePeople(ref Rooms apartment)
    {
        var people = manager.Instantiate(person, person_count, Allocator.Temp);
        var seed = new System.Random();

        for (int i = 0; i < person_count; i++)
        {
            Entity p = people[i];
            Spawn spawn = GetSpawnPoint();

            Vector3 pos = new Vector3(
                spawn.pos.x, 2.15f, spawn.pos.y);

            Translation R = new Translation();
            R.Value = pos;

            manager.AddComponentData(p, R);

            float virus = 0f; 
            bool infected = false; 

            int house_idx = -1;
            while (house_idx < 0)
            {
                house_idx = Random.Range(0, apartment.max_room_idx);
                if (apartment.space[house_idx] == 0)
                    house_idx = -1;
                else
                    apartment.space[house_idx] -= 1;
            }

            bool is_nightowl = Random.Range(0f, 1f) < 0.25f;

            PersonData person_data = new PersonData()
            {
                virus = virus,
                infected = infected,
                antibodies = 0f,
                resistence = false,
                home_coordinates = apartment.coordinates[house_idx],
                home_idx = house_idx,
                is_home = false,
                schedule_phase = (is_nightowl ? 0 : 1) + Random.Range(-0.25f, 0.25f)
            };
            manager.AddComponentData(p, person_data);

            HeadingData heading_data = new HeadingData()
            {
                speed = person_speed + Random.Range(-person_speed_spread, person_speed_spread),
                direction = spawn.dir,
                goal_coordinate = apartment.coordinates[house_idx],
                next_cross_margin = Random.Range(1f, 6f),
                rng = new Unity.Mathematics.Random((uint)seed.Next())
            };
            manager.AddComponentData(p, heading_data);

            URPMaterialPropertyBaseColor color = new URPMaterialPropertyBaseColor()
            {
                Value = new float4(1f, 1f, 1f, 1f)
            };
            manager.AddComponentData(p, color);
        }

        people.Dispose();
    }

    private void InstantiateRoads()
    {
        tile_count.x = (int)(bounds.width / tile_size) + tiles_per_block;
        tile_count.y = (int)(bounds.height / tile_size) + tiles_per_block;

        block_count = (int2)(tile_count / tiles_per_block);

        // Might be the other way round.
        int vertical_road_count = (int)((tile_count.y) * block_count.x);
        int horizontal_road_count = (int)((tile_count.x) * block_count.y);

        var vertical_roads = manager.Instantiate(vertical_road, vertical_road_count, Allocator.Temp);
        var horizontal_roads = manager.Instantiate(horizontal_road, horizontal_road_count, Allocator.Temp);
        var crosswalks = manager.Instantiate(crosswalk, block_count.x * block_count.y, Allocator.Temp);

        for (int x = 0; x < block_count.x; x++)
        {
            for (int y = 0; y < block_count.y; y++)
            {
                {
                    int idx = x * block_count.y + y;
                    Entity cross = crosswalks[idx];

                    Vector3 pos = new Vector3(
                        x * tiles_per_block * tile_size + bounds.x,
                        0,
                        y * tiles_per_block * tile_size + bounds.y);
                    Translation R = new Translation();
                    R.Value = pos;

                    manager.AddComponentData(cross, R);
                }

                for (int dy = 0; dy < tiles_per_block; dy++)
                {
                    int idx = x * tiles_per_block * block_count.y + y * tiles_per_block + dy;
                    if (dy == 0 || y == block_count.x - 1)
                    {
                        manager.DestroyEntity(vertical_roads[idx]);
                        continue;
                    }
                    Entity road = vertical_roads[idx];

                    Vector3 pos = new Vector3(
                        x * tiles_per_block * tile_size + bounds.x, 
                        0, 
                        (y * tiles_per_block + dy) * tile_size + bounds.y);
                    Translation R = new Translation();
                    R.Value = pos;

                    manager.AddComponentData(road, R);
                }

                for (int dx = 0; dx < tiles_per_block; dx++)
                {
                    int idx = y * tiles_per_block * block_count.x + x * tiles_per_block + dx;
                    if (dx == 0 || x == block_count.y - 1)
                    {
                        manager.DestroyEntity(horizontal_roads[idx]);
                        continue;
                    }
                    Entity road = horizontal_roads[idx];

                    Vector3 pos = new Vector3(
                        (x * tiles_per_block + dx) * tile_size + bounds.x,
                        0,
                        y * tiles_per_block * tile_size + bounds.y);
                    Translation R = new Translation();
                    R.Value = pos;

                    manager.AddComponentData(road, R);
                }

            }
        }

        horizontal_roads.Dispose();
        vertical_roads.Dispose();
        crosswalks.Dispose();
    }

    struct Rooms
    {
        public NativeArray<int> space;
        public NativeArray<int2> coordinates;
        public int max_room_idx;
    };

    private void InstantiateBuildings(ref Rooms apartment)
    {
        int buildings_per_block = tiles_per_block * tiles_per_block - 2 * tiles_per_block + 1;
        int building_count = buildings_per_block * block_count.x * block_count.y;
        var buildings = manager.Instantiate(building, building_count, Allocator.Temp);

        var buildings_occupied = manager.Instantiate(
            building_occupied, building_count * rooms_per_apartment, Allocator.Temp);

        float marker_spacing = 3f;
        int occupied_markers_per_row = (int)((tile_size - 4f) / marker_spacing);

        int idx = 0;
        int hash = 0;
        apartment.max_room_idx = 0;
        for (int x = 0; x < tile_count.x - tiles_per_block; x++)
        {
            for (int y = 0; y < tile_count.y - tiles_per_block; y++)
            {
                if (x % tiles_per_block > 0 && y % tiles_per_block > 0)
                {
                    var b = buildings[idx];

                    var scale = Random.Range(250, 750);

                    var T = new Translation();
                    T.Value = new Vector3(
                        x * tile_size + bounds.min.x, 
                        scale / 200f,
                        y * tile_size + bounds.min.y);
                    manager.AddComponentData(b, T);

                    var S = new NonUniformScale();
                    S.Value = new Vector3(2500f, scale, 2500f);
                    manager.AddComponentData(b, S);

                    var cidx = Random.Range(0, building_colors.Length);

                    var color = new URPMaterialPropertyBaseColor()
                    {
                        Value = new float4(
                            building_colors[cidx].r,
                            building_colors[cidx].g,
                            building_colors[cidx].b,
                            building_colors[cidx].a)
                    };
                    manager.AddComponentData(b, color);

                    var data = new BuildingData()
                    {
                        type = BuildingType.Apartment,
                        start_hash = hash,
                        end_hash = hash + rooms_per_apartment,
                        people_per_room = people_per_room_apartment
                    };
                    manager.AddComponentData(b, data);

                    for (int h = 0; h < rooms_per_apartment; h++)
                    {
                        var b_o = buildings_occupied[h + hash];

                        var T_o = new Translation()
                        {
                            Value = T.Value + 
                            new float3(tile_size / 2 - 3f, 0f, tile_size / 2 - 3f) - 
                            marker_spacing * new float3(
                                h % occupied_markers_per_row, 0f, (int)(h / occupied_markers_per_row))
                        };
                        manager.AddComponentData(b_o, T_o);

                        var color_o = new URPMaterialPropertyBaseColor()
                        {
                            Value = new float4(0f, 0f, 0f, 0f)
                        };
                        manager.AddComponentData(b_o, color_o);

                        var occ_data = new OccupiedData()
                        {
                            base_color = new float4(1f, 1f, 1f, 1f)
                        };
                        manager.AddComponentData(b_o, occ_data);

                        apartment.space[h + hash] = rooms_per_apartment;
                        apartment.coordinates[h + hash] = new int2(x, y);

                        apartment.max_room_idx++;

                    }

                    hash += rooms_per_apartment;

                    idx++;
                }
            }
        }

        buildings.Dispose();
        buildings_occupied.Dispose();
    }

    private void Awake()
    {
        if (sim != null && sim != this)
        {
            Destroy(gameObject);
            return;
        }

        sim = this;

        manager = World.DefaultGameObjectInjectionWorld.EntityManager;

        GameObjectConversionSettings settings = GameObjectConversionSettings.FromWorld(
            World.DefaultGameObjectInjectionWorld, null);

        person = GameObjectConversionUtility.ConvertGameObjectHierarchy(
            person_prefab, settings);

        vertical_road = GameObjectConversionUtility.ConvertGameObjectHierarchy(
            vertical_road_prefab, settings);

        horizontal_road = GameObjectConversionUtility.ConvertGameObjectHierarchy(
            horizontal_road_prefab, settings);

        crosswalk = GameObjectConversionUtility.ConvertGameObjectHierarchy(
            crosswalk_prefab, settings);

        building = GameObjectConversionUtility.ConvertGameObjectHierarchy(
            building_prefab, settings);

        building_occupied = GameObjectConversionUtility.ConvertGameObjectHierarchy(
            building_occupied_prefab, settings);

        var max_apartment_rooms = 13 * 13 * rooms_per_apartment * 4;

        var apartment = new Rooms()
        {
            space = new NativeArray<int>(max_apartment_rooms, Allocator.Temp),
            coordinates = new NativeArray<int2>(max_apartment_rooms, Allocator.Temp),
            max_room_idx = 0
        };

        current_time = 0f;

        InstantiateRoads();
        InstantiateBuildings(ref apartment);
        InstantiatePeople(ref apartment);
    }

    private void OnDestroy()
    {
        immune.Clear();
        healthy.Clear();
        infected.Clear();
    }

    // Start is called before the first frame update
    void Start()
    {
        has_started = false;
        outro_text.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButton("Cancel"))
            Application.Quit();

        if (current_time / (2 * day_time) > 15f)
        {
            has_started = false;
            has_ended = true;

            outro_text.SetActive(true);

            int im = (int)immune[immune.Count - 1];
            int dead = (int)(im * 0.075f);
            int recovered = (im - dead);

            healthy_text.GetComponent<TMPro.TextMeshProUGUI>().text = "Healthy: " + ((int)healthy[healthy.Count - 1]);
            recovered_text.GetComponent<TMPro.TextMeshProUGUI>().text = "Recovered: " + recovered;
            dead_text.GetComponent<TMPro.TextMeshProUGUI>().text = "Dead: " + dead;

            if (Input.GetMouseButtonDown(0) && has_ended)
                Application.Quit();
        }
    }
}
