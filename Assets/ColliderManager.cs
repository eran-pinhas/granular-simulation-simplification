using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderManager : MonoBehaviour
{
    public ICollisionListener collisionListener;

    private List<GameObject> colliding = new List<GameObject>();

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == Tags.PARTICLE)
        {
            colliding.Add(collision.gameObject);
            collisionListener.informCollision(this.gameObject, collision.gameObject);
        }
    }
    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.tag == Tags.PARTICLE)
        {
            colliding.Remove(collision.gameObject);
            collisionListener.informCollisionRemoved(this.gameObject, collision.gameObject);
        }
    }
    public List<GameObject> getColliding()
    {
        return colliding;
    }

    void OnDisable()
    {
        colliding.ForEach(x => collisionListener.informCollisionRemoved(gameObject, x));
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
