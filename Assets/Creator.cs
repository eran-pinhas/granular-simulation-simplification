using System;
using UnityEngine;

public class Creator : MonoBehaviour
{
    public GameObject spanee;
    public Transform pos;
    public int NumOfElements;
    public int WaitTime;
    public MeshGenerator fea;
    public Vector3 RandPart;

    public Vector3 scaleRandomPart;

    static System.Random rand = new System.Random();

    DateTime lastTimeCreated;
    int createdCount = 0;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        for (var i = 0;i < 2; i++)
        {
            if (createdCount < NumOfElements && DateTime.Now.Subtract(lastTimeCreated).TotalMilliseconds > WaitTime)
            {
                lastTimeCreated = DateTime.Now;
                createdCount++;
                var name = String.Format("Particle {0}", createdCount);

                var particle = Particle.Generate(
                    spanee,
                    pos.position + new Vector3((float)(RandPart.x * rand.NextDouble()), (float)(RandPart.y * rand.NextDouble()), (float)(RandPart.z * rand.NextDouble())),
                    pos.rotation,
                    name,
                    this.fea);

                var go = particle.gameObject;

                particle.setAsFree();

                go.transform.localScale += scaleRandomPart * (float)rand.NextDouble();

                fea.informNewChild(particle);
            }
        }
    }
}
