using System.Collections;
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

        var sj = a.AddComponent<SpringJoint>();
        sj.connectedBody = b.GetComponent<Rigidbody>();
        sj.spring = this.spring;
        sj.damper = this.damp;

        if (!springs.ContainsKey((b.GetInstanceID(), a.GetInstanceID())) &&
            !springs.ContainsKey((a.GetInstanceID(), b.GetInstanceID())))
            springs[(a.GetInstanceID(), b.GetInstanceID())] = sj;
    }

    public SpringJoint getSpringJoint(GameObject a, GameObject b)
    {
        return springs[(a.GetInstanceID(), b.GetInstanceID())];
    }

    public override bool RemoveConnection(GameObject a, GameObject b)
    {
        if(springs.ContainsKey((a.GetInstanceID(), b.GetInstanceID()))){
            Destroy(springs[(a.GetInstanceID(), b.GetInstanceID())]);
            springs.Remove((a.GetInstanceID(), b.GetInstanceID()));
        }
        return base.RemoveConnection(a, b);
        
    }
}
