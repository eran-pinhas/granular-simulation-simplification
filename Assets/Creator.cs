using System;
using UnityEngine;

public class Creator : MonoBehaviour
{
    public GameObject spanee;
    public Transform pos;
    public int NumOfElements;
    public int WaitTime;
    public FEA fea;
    public Vector3 RandPart;

    public Vector3 scaleRandomPart;

    private GameObject particlesContainer;


    System.Random rand = new System.Random();

    DateTime lastTimeCreated;
    int createdCount = 0;

    // Start is called before the first frame update
    void Start()
    {
        particlesContainer = new GameObject("Particles");
        particlesContainer.transform.parent = this.gameObject.transform;
    }

    // Update is called once per frame
    void Update()
    {
        if (createdCount < NumOfElements && DateTime.Now.Subtract(lastTimeCreated).TotalMilliseconds > WaitTime)
        {
            lastTimeCreated = DateTime.Now;
            createdCount++;
            var obj = Instantiate(spanee, pos.position + new Vector3((float)(RandPart.x * rand.NextDouble()), (float)(RandPart.y * rand.NextDouble()), (float)(RandPart.z * rand.NextDouble())), pos.rotation);
            obj.name = obj.name + " " + createdCount;
            obj.tag = Tags.PARTICLE;
            var cl = obj.AddComponent<ColliderManager>();
            cl.collisionListener = this.fea;

            obj.transform.localScale += scaleRandomPart * (float)rand.NextDouble();

            obj.transform.parent = particlesContainer.transform;
            fea.informNewChild(obj);
        }


    }
}
