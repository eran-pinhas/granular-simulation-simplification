using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class ConnectionSpringDrawer : ConnectionDrawer
{
    public float spring;
    public float damp;

    Dictionary<(int, int), SpringJoint> springs = new Dictionary<(int, int), SpringJoint>();
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

    public override void AddConnection(GameObject a, GameObject b)
    {
        base.AddConnection(a, b);

        if (!springs.ContainsKey((b.GetInstanceID(), a.GetInstanceID())) &&
            !springs.ContainsKey((a.GetInstanceID(), b.GetInstanceID())))
        {
            var sj = a.AddComponent<SpringJoint>();
            sj.connectedBody = b.GetComponent<Rigidbody>();
            sj.spring = this.spring;
            sj.damper = this.damp;
            springs[(a.GetInstanceID(), b.GetInstanceID())] = sj;
        }
    }


    public void AddConnectionWithAnchor(GameObject a, GameObject b, Tuple<float, float> aPos, Tuple<float, float> bPos)
    {
        this.AddConnection(a, b);

        SpringJoint sj;
        if (springs.ContainsKey((b.GetInstanceID(), a.GetInstanceID())))
        {
            sj = springs[(b.GetInstanceID(), a.GetInstanceID())];
        }
        else
        {
            sj = springs[(a.GetInstanceID(), b.GetInstanceID())];
        }

        var baseObjectTransform = a.gameObject.GetComponent<Transform>();

        var anchor = new Vector3((bPos.Item1 - aPos.Item1) / baseObjectTransform.lossyScale.x, 0, (bPos.Item2 - aPos.Item2) / baseObjectTransform.lossyScale.z);
        anchor = Vector3.Scale(anchor, baseObjectTransform.lossyScale);
        
        // sj.anchor = Vector3.zero;
        // sj.connectedAnchor = anchor;

        var csj = sj.gameObject.AddComponent<CustomSpringJoint>();
        csj.realAnchor = anchor;
    }


    public SpringJoint getSpringJoint(GameObject a, GameObject b)
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
        var sj = getSpringJoint(a, b);
        sj.damper = 0;
        sj.spring = 0;

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
