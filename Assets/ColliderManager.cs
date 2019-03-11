using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderManager : MonoBehaviour
{
    public ICollisionListener collisionListener;

    private List<GameObject> colliding = new List<GameObject>();

    private static bool IsCollide(GameObject go)
    {
        return (go.GetComponent<ColliderManager>() != null);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (IsCollide(collision.gameObject))
        {
            colliding.Add(collision.gameObject);
            collisionListener.informCollision(this.gameObject, collision.gameObject);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        RemoveCollision(collision.gameObject);
    }

    void RemoveCollision(GameObject go)
    {
        if (IsCollide(gameObject))
        {
            colliding.Remove(gameObject);
            if (collisionListener != null)
            {
                collisionListener.informCollisionRemoved(this.gameObject, go);
            }
        }
    }

    public List<GameObject> getColliding()
    {
        return colliding;
    }

    void OnDisable()
    {
        colliding.ForEach(x => collisionListener.informCollisionRemoved(gameObject, x));
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
