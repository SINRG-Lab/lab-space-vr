using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Setting_Parameter : MonoBehaviour
{
    [Header("Radius (nm)")]
    public TextMeshProUGUI radius;

    [Header("Temperature (C)")]
    public TextMeshProUGUI temperature;

    [Header("Required Height (nm)")]
    public TextMeshProUGUI requied_height;

    [Header("Substrate Dimension")]
    public TextMeshProUGUI substrate_dimension;

    public GrowthRate growth_rate;

    [Header("Substrate")]
    public float substrate_length = 0.09f;
    public float substrate_width = 0.09f;
    public GameObject Substrate;
    private Vector3 substrate_scale;

    [Header("Catalyst")]
    public Slider catalyst_count;
    [Range(1f, 10f)]
    public float catalyst_size_randomness = 5f;
    public Transform catalyst_holder;
    int last_count = -1;

    [Header("NanoWire")]
    public GameObject NanoWire;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        catalyst_count.wholeNumbers = true;
        catalyst_count.onValueChanged.AddListener(_ => SpawnCatalyst());
        SpawnCatalyst();
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log(float.Parse(substrate_dimension.text));
        Substrate.transform.localScale = new Vector3(float.Parse(substrate_dimension.text), 0.005f, float.Parse(substrate_dimension.text));
    }

    void SpawnCatalyst(){
        int count = (int)catalyst_count.value;
        if(count == last_count) return;
        last_count = count;

        for (int i = catalyst_holder.childCount -1; i >= 0; i--){
            Destroy(catalyst_holder.GetChild(i).gameObject);
        }

        for (int i = 0; i < count; i++){
            SpawnOnTopAnywhere(NanoWire, Substrate.transform, catalyst_holder);
        }
    }

    GameObject SpawnOnTopAnywhere(GameObject prefab, Transform substrate, Transform parent = null)
    {
        var subR = substrate.GetComponentInChildren<Renderer>();
        var subB = subR.bounds;

        var go  = Instantiate(prefab, parent);
        var goR = go.GetComponentInChildren<Renderer>();
        var ext = goR ? goR.bounds.extents : Vector3.zero; // half size of the object

        float x = Random.Range(subB.min.x + ext.x, subB.max.x - ext.x);
        float z = Random.Range(subB.min.z + ext.z, subB.max.z - ext.z);
        float y = subB.max.y;

        go.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        return go;
    }
}
