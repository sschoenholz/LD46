using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class UIHandling : MonoBehaviour
{
    public Button pause;
    public Button normal;
    public Button fast;
    public Button blazing;
    public Button ludicrous;
    public Slider home_bound;
    public Slider wearing_mask;

    // Start is called before the first frame update
    void Start()
    {
        Button btn = pause.GetComponent<Button>();
        btn.onClick.AddListener(Pause);

        btn = normal.GetComponent<Button>();
        btn.onClick.AddListener(Normal);

        btn = fast.GetComponent<Button>();
        btn.onClick.AddListener(Fast);

        btn = blazing.GetComponent<Button>();
        btn.onClick.AddListener(Blazing);

        btn = ludicrous.GetComponent<Button>();
        btn.onClick.AddListener(Ludicrous);

        Slider sld = home_bound.GetComponent<Slider>();
        sld.onValueChanged.AddListener(HomeBound);

        sld = wearing_mask.GetComponent<Slider>();
        sld.onValueChanged.AddListener(WearingMask);
    }

    private void Pause()
    {
        SimulationManager.sim.speed_multiplier = 0f;
    }

    private void Normal()
    {
        SimulationManager.sim.speed_multiplier = 2f;
    }

    private void Fast()
    {
        SimulationManager.sim.speed_multiplier = 4f;
    }

    private void Blazing()
    {
        SimulationManager.sim.speed_multiplier = 8f;
    }

    private void Ludicrous()
    {
        SimulationManager.sim.speed_multiplier = 16f;
    }

    private void HomeBound(float value)
    {
        SimulationManager.sim.home_bound = (int)value;
    }

    private void WearingMask(float value)
    {
        SimulationManager.sim.wearing_mask = (int)value;
    }

    // Update is called once per frame
    void Update()
    {
    }
}
