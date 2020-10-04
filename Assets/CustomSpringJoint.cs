using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomSpringJoint : MonoBehaviour
{

    public Vector3 realAnchor;
    
    public float parallelForce;
    public GameObject connectedBody;
    void Start()
    {
    }

    void Update()
    {
        var restSize = 3.5;
        var spring = 30;
        var damper = 1;
        
        var thisObjectTransform = this.gameObject.GetComponent<Transform>();
        var connObjectTransform = this.connectedBody.gameObject.GetComponent<Transform>();
        var thisRB = this.gameObject.GetComponent<Rigidbody>(); 
        var connRB = this.connectedBody.gameObject.GetComponent<Rigidbody>(); 


        
        var connRelativePosition = connObjectTransform.position- thisObjectTransform.position;
        var connRelativePosition_norm = connRelativePosition.normalized 
        var connRelativeVelocity = connRB.velocity- thisRB.velocity;

        var springRelativeShift = connRelativePosition.normalized *  (float)(connRelativePosition.magnitude - restSize);
        var springParallelDampen = Vector3.Dot(connRelativePosition_norm, connRelativeVelocity) * connRelativePosition_norm;

        var force = springRelativeShift * spring + connRelativeVelocity * damper;
        thisRB.AddForce(force);
        connRB.AddForce( - force);
        // Debug.Log(String.Format("{0}", new Vector3(1.0f, 2.0f, 3.0f) * (float)2.0d));
        Debug.Log(String.Format("{0}", connRelativeVelocity));
    }
}
