using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomSpringJoint : MonoBehaviour
{
    public GameObject connectedBody;

    void Start()
    {
    }

    virtual public float getForce()
    {
        throw new NotImplementedException();
    }

    void Update()
    {
        var thisObjectTransform = this.gameObject.GetComponent<Transform>();
        var connObjectTransform = this.connectedBody.gameObject.GetComponent<Transform>();

        var connRelative_direction = (connObjectTransform.position - thisObjectTransform.position).normalized;
        var force = connRelative_direction * this.getForce();

        var thisRB = this.gameObject.GetComponent<Rigidbody>();
        var connRB = this.connectedBody.gameObject.GetComponent<Rigidbody>();

        thisRB.AddForce(-force);
        connRB.AddForce(force);
        // Debug.Log(String.Format("{0}", new Vector3(1.0f, 2.0f, 3.0f) * (float)2.0d));
    }
}
