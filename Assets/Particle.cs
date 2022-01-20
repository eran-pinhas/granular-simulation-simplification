using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Particle : MonoBehaviour
{
    static GameObject particlesContainer;
    public static int current_id;
    static Particle()
    {
        particlesContainer = new GameObject("Particles");
        // particlesContainer.transform.parent = this.gameObject.transform;
    }

    public static Particle Generate(GameObject spawnee, Vector3 position, Quaternion rotation, ICollisionListener collisionListener)
    {
        var name = String.Format("Particle {0}", current_id);
        var go = Instantiate(spawnee, position, rotation);
        go.transform.parent = particlesContainer.transform;
        go.name = name;

        var cl = go.AddComponent<ColliderManager>();
        cl.collisionListener = collisionListener;
        var p = go.AddComponent<Particle>();
        p.Id = current_id;
        
        current_id++;
        return p;
    }
    public enum PARTICLE_TYPE
    {
        PARTICLE,
        FEM_EDGE_PARTICLE,
        FEM_HIDDEN_PARTICLE,
    }
    private PARTICLE_TYPE _type = PARTICLE_TYPE.PARTICLE;
    public int Id;
    public PARTICLE_TYPE Type
    {
        get { return _type; }
    }

    public void setSetAsEdge(int groupId){
        this._type = PARTICLE_TYPE.FEM_EDGE_PARTICLE;
        this.particleGroupId = groupId;
        this.gameObject.SetActive(true);
    }
    public void setAsFree(){
        this._type = PARTICLE_TYPE.PARTICLE;
        this.particleGroupId = -1;
        this.gameObject.SetActive(true);
    }
    public void setAsHidden(int groupId){
        this._type = PARTICLE_TYPE.FEM_HIDDEN_PARTICLE;
        this.particleGroupId = groupId;
        this.gameObject.SetActive(false);
    }

    public int particleGroupId = -1;

    public static Tuple<float, float> GetGOPos(GameObject go)
    {
        var vec3 = go.transform.position;
        return Tuple.Create(vec3.x, vec3.y);
    }
    public static void SetGOPos(GameObject go, Tuple<float, float> value)
    {
        Vector3 vec3 = new Vector3(value.Item1, value.Item2, go.transform.position.z);
        go.transform.position = vec3;
    }

    public Tuple<float, float> Position
    {
        get
        {
            return Particle.GetGOPos(this.gameObject);
        }
        set
        {
            Particle.SetGOPos(this.gameObject, value);
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
