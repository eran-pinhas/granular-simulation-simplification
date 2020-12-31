using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class ByLengthCustomSpringJoint : CustomSpringJoint
{

    public float spring = 20f;
    public float damper = 1f;
    public float restSize = 0f;

    public override float getForce()
    {

        // var thisObjectTransform = this.gameObject.GetComponent<Transform>();
        //var connObjectTransform = this.connectedBody.gameObject.GetComponent<Transform>();


        var thisRB = this.gameObject.GetComponent<Rigidbody>();
        var connRB = this.connectedBody.gameObject.GetComponent<Rigidbody>();

        var relativePosition = connRB.position - thisRB.position;
        var relativeVelocity = connRB.velocity - thisRB.velocity;

        float springRelativeShift = relativePosition.magnitude - restSize;
        var springParallelDampen = Vector3.Dot(relativePosition.normalized, relativeVelocity);
        Debug.Log(String.Format("{0} {1}", springRelativeShift, springParallelDampen));

        var force = -springRelativeShift * spring -springParallelDampen * damper;
        return force;
    }
}
