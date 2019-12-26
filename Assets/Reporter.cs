using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

class ReporterReport<T>
{
    public float time;
    public T data;
}

public class Reporter : MonoBehaviour
{
    public Text text;
    List<ReporterReport<float>> history = new List<ReporterReport<float>>();
    // Start is called before the first frame update
    void Start()
    {

    }

    public void reportNew(float data, float t)
    {
        history.Add(new ReporterReport<float>()
        {
            data = data,
            time = t,
        });
    }

    // Update is called once per frame
    void Update()
    {
        if (history.Any())
        {
            var data = history.Last().data;
            if (data > 0)
            {
                text.text = data.ToString();
            }
            else
            {
                text.text = "No Pull Forces";
            }
        }

    }
}
