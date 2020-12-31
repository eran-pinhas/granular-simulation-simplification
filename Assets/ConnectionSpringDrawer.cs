using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class ConnectionSpringDrawer : ConnectionDrawer
{
    public float spring;
    public float damp;

    Dictionary<(int, int), CustomSpringJoint> springs = new Dictionary<(int, int), CustomSpringJoint>();
    HashSet<(int, int)> eliminated = new HashSet<(int, int)>();
    // Start is called before the first frame update
    public override void Start()
    {
        base.Start();
    }

    // Update is called once per frame
    public override void Update()
    {
        base.Update();
    }


    public void AddConnectionWithAnchor(GameObject a, GameObject b, Tuple<float, float> aPos, Tuple<float, float> bPos)
    {
        var d = TopologyFunctions.Distance(aPos, bPos);
        this.DrawConnection(a, b);

        //CustomSpringJoint sj = this.getSpringJoint(a,b);

        //var baseObjectTransform = a.gameObject.GetComponent<Transform>();
        var csj = a.AddComponent<ByLengthCustomSpringJoint>();
        csj.connectedBody = b;
        csj.restSize = d;
        Debug.Log(d);

    }


    public CustomSpringJoint getSpringJoint(GameObject a, GameObject b)
    {
        if (springs.ContainsKey((a.GetInstanceID(), b.GetInstanceID())))
        {
            return springs[(a.GetInstanceID(), b.GetInstanceID())];
        }
        else
        {
            return springs[(b.GetInstanceID(), a.GetInstanceID())];
        }
    }

    public HashSet<(int, int)> getEliminated()
    {
        return eliminated;
    }

    public void eliminateSpringJoint(GameObject a, GameObject b)
    {
        var sj = this.getSpringJoint(a, b);
        //sj.damper = 0;
        //sj.spring = 0;

        var t = (a.GetInstanceID(), b.GetInstanceID());
        if (!springs.ContainsKey(t))
        {
            t = (b.GetInstanceID(), a.GetInstanceID());
        }
        eliminated.Add(t);

    }

    public override bool RemoveConnection(GameObject a, GameObject b)
    {
        if (springs.ContainsKey((a.GetInstanceID(), b.GetInstanceID())))
        {
            Destroy(springs[(a.GetInstanceID(), b.GetInstanceID())]);
            springs.Remove((a.GetInstanceID(), b.GetInstanceID()));
        }
        return base.RemoveConnection(a, b);

    }
}
