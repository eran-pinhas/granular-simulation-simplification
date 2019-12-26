using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloorRidger : MonoBehaviour
{
    public GameObject parent;
    public GameObject spawnee;
    public int count;
    public Vector3 shift;
    void Start()
    {
        for (var i = -count; i <= count; i++)
        {
            var go = Instantiate(spawnee, parent.transform);
            go.transform.position += shift * i;
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
