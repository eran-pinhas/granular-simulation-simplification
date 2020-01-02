using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderManager : MonoBehaviour
{
    public ICollisionListener collisionListener;

    private List<Particle> colliding = new List<Particle>();

    void OnCollisionEnter(Collision collision)
    {
        var p1 = this.GetComponent<Particle>();
        var p2 = collision.gameObject.GetComponent<Particle>();
        if (p1 != null && p2 != null)
        {
            colliding.Add(p2);
            collisionListener.informCollision(p1, p2);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        RemoveCollision(collision.gameObject);
    }

    void RemoveCollision(GameObject go)
    {

        var p1 = this.GetComponent<Particle>();
        var p2 = go.GetComponent<Particle>();
        if (p1 != null && p2 != null)
        {
            colliding.Remove(p2);
            if (collisionListener != null)
            {
                collisionListener.informCollisionRemoved(p1, p2);
            }
        }
    }

    void OnDisable()
    {
        var p = this.GetComponent<Particle>();
        colliding.ForEach(x => collisionListener.informCollisionRemoved(p, x));
        colliding.ForEach(x =>
        {
            var cm = x.GetComponent<ColliderManager>();
            if (cm != null) cm.RemoveCollision(this.gameObject);
        });
        colliding.Clear();
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
