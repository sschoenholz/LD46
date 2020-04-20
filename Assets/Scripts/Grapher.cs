using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Grapher : MonoBehaviour
{
    public enum GraphData
    {
        Healthy, Infected, Immune
    };

    public LineRenderer line_renderer;
    public int2 offset;
    public GraphData type;
    public Color color;
    public float y_scale;
    public float sub_offset;
    public GameObject label;

    // Start is called before the first frame update
    void Start()
    {
        line_renderer = GetComponent<LineRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        List<float> data = SimulationManager.sim.healthy;
        if (type == GraphData.Infected)
            data = SimulationManager.sim.infected;
        if (type == GraphData.Immune)
            data = SimulationManager.sim.immune;

        Vector2 min = SimulationManager.sim.bounds.min;
        float tile_size = SimulationManager.sim.tile_size;
        var anchor = new Vector3(
            min.x + tile_size * offset.x * 3,
            3f,
            min.y + tile_size * offset.y * 3 + sub_offset);

        var points = new Vector3[data.Count];
        for (int i = 0; i < data.Count; i++)
            points[i] = new Vector3(2f * i, data[i] / y_scale, 0f) + anchor;
        line_renderer.positionCount = data.Count;
        line_renderer.SetPositions(points);

        var T = label.transform;
        string str = "Healthy: ";
        if (type == GraphData.Infected)
            str = "Sick: ";
        if (type == GraphData.Immune)
            str = "Immune: ";

        T.position = anchor + new Vector3(64f, data[0] / y_scale + 12f, 0f);

        if (type == GraphData.Immune)
            T.position += new Vector3(48f, 0f, 0f);
        float count = data[data.Count - 1];
        if (count > 1000)
            label.GetComponent<TMPro.TextMeshPro>().text = str + (int)(count / 1000) + "k";
        else
            label.GetComponent<TMPro.TextMeshPro>().text = str + (int)count;

    }
}