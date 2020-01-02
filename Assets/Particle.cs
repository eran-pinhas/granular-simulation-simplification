﻿using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Particle : MonoBehaviour
{
    static GameObject particlesContainer;
    static Particle()
    {
        particlesContainer = new GameObject("Particles");
        // particlesContainer.transform.parent = this.gameObject.transform;
    }

    public static Particle Generate(GameObject spawnee, Vector3 position, Quaternion rotation, string name, ICollisionListener collisionListener)
    {
        var go = Instantiate(spawnee, position, rotation);
        go.transform.parent = particlesContainer.transform;
        go.name = name;

        var cl = go.AddComponent<ColliderManager>();
        cl.collisionListener = collisionListener;
        var p = go.AddComponent<Particle>();
        p._id = (int) (1000000 * UnityEngine.Random.value);
        return p;
    }
    public enum PARTICLE_TYPE
    {
        PARTICLE,
        FEM_EDGE_PARTICLE,
        FEM_CENTER_PARTICLE,
    }
    private PARTICLE_TYPE _type = PARTICLE_TYPE.PARTICLE;
    private int _id;
    // Start is called before the first frame update

    public PARTICLE_TYPE Type
    {
        get { return _type; }
        set { _type = value; }
    }

    // public Particle(){
    //     _id  = (int) (1000000 * UnityEngine.Random.value);
    //     UnityEngine.Debug.Log(_id);
    // }

    public Tuple<float, float> Position
    {
        get
        {
            var vec3 = this.gameObject.transform.position;
            return Tuple.Create(vec3.x, vec3.y);
        }
        set
        {
            Vector3 vec3 = new Vector3(value.Item1, value.Item2, this.gameObject.transform.position.z);
            this.gameObject.transform.position = vec3;
        }
    }

    public int Id{
        get {
            return this._id;
        }
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

    }
}
