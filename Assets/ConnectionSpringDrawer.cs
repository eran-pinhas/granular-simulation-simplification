﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class ConnectionSpringDrawer : ConnectionDrawer
{
    public float spring;
    public float damp;

    Dictionary<(int, int), SpringJoint> springs = new Dictionary<(int, int), SpringJoint>();
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


    public void AddConnectionWithAnchor(GameObject a, GameObject b, Tuple<float, float> diff)
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

        var anchor = new Vector3(diff.Item1 / baseObjectTransform.lossyScale.x, 0, diff.Item2 / baseObjectTransform.lossyScale.z);
        anchor = Vector3.Scale(anchor, baseObjectTransform.lossyScale);

        sj.anchor = Vector3.zero;
        sj.connectedAnchor = anchor;
    }


    public SpringJoint getSpringJoint(GameObject a, GameObject b)
    {
        if (springs.ContainsKey((a.GetInstanceID(), b.GetInstanceID())))
            return springs[(a.GetInstanceID(), b.GetInstanceID())];
        else
            return springs[(b.GetInstanceID(), a.GetInstanceID())];
    }

    public override bool RemoveConnection(GameObject a, GameObject b)
    {
        if (springs.ContainsKey((a.GetInstanceID(), b.GetInstanceID())))
        {
            Destroy(springs[(a.GetInstanceID(), b.GetInstanceID())]);
            springs.Remove((a.GetInstanceID(), b.GetInstanceID()));
        }
        else
        {
            Destroy(springs[(b.GetInstanceID(), a.GetInstanceID())]);
            springs.Remove((b.GetInstanceID(), b.GetInstanceID()));
        }
        return base.RemoveConnection(a, b);
    }
}
