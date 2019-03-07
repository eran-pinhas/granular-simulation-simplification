using UnityEngine;
using System.Collections;

public interface ICollisionListener
{
    void informCollision(GameObject a, GameObject b);

    void informCollisionRemoved(GameObject a, GameObject b);
}
