using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DayTime : MonoBehaviour
{
    public GameObject day_counter;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        var light = gameObject.GetComponent<Light>();

        var current_time = SimulationManager.sim.current_time;
        var day_time = SimulationManager.sim.day_time;

        light.colorTemperature = 6639 + 2000f * Mathf.Sin(current_time / day_time * Mathf.PI);

        var text = day_counter.GetComponent<TMPro.TextMeshProUGUI>();
        text.text = "Day " + ((int)(current_time / (2 * day_time)) + 1) + " of 16";
    }
}
