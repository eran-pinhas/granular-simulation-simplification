using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Crane : MonoBehaviour
{
    GameObject beam;
    GameObject hook;
    GameObject beamTransform;
    public float beamLengthSensitivity = 10f;
    public float beamAngleSensitivity = 0.1f;
    public float carneXSensitivity = 10f;
    public float hookSensitivity = 3f;
    private float hookAngle = 0f;
    private float beamAngle = 10f;
    private float beamLength = 20f;
    private float craneX = 0f;
    // Start is called before the first frame update
    void Start()
    {
        beamTransform = this.gameObject.transform.Find("BeamTransform").gameObject;
        hook = beamTransform.transform.Find("Hook").gameObject;
        beam = beamTransform.transform.Find("Beam").gameObject;
    }

    void setPositions()
    {
        beamTransform.transform.localRotation = Quaternion.Euler(0, 0, beamAngle);
        hook.transform.localRotation = Quaternion.Euler(0, 0, hookAngle);
        this.gameObject.transform.localPosition = new Vector3(craneX, this.gameObject.transform.position.y, this.gameObject.transform.position.z);
        
        beam.transform.localScale = new Vector3(beamLength, 1, 10);
        beam.transform.localPosition = new Vector3(-beamLength/2, 0, 0);
        hook.transform.localPosition = new Vector3(-beamLength, 0, 0);
    }
    // Update is called once per frame
    void Update()
    {
        var beamAngleDelta = Time.deltaTime * beamAngleSensitivity;
        var craneXDelta = Time.deltaTime * carneXSensitivity;
        var hookDelta = Time.deltaTime * hookSensitivity;
        var beamLengthDelta = Time.deltaTime * beamLengthSensitivity;
        if (Input.GetKey(KeyCode.S))
        {
            beamAngle += beamAngleDelta;
        }
        if (Input.GetKey(KeyCode.W))
        {
            beamAngle -= beamAngleDelta;
        }
        if (Input.GetKey(KeyCode.A))
        {
            craneX -= craneXDelta;
        }
        if (Input.GetKey(KeyCode.D))
        {
            craneX += craneXDelta;
        }
        if (Input.GetKey(KeyCode.Q))
        {
            hookAngle -= hookDelta;
        }
        if (Input.GetKey(KeyCode.E))
        {
            hookAngle += hookDelta;
        }
        if (Input.GetKey(KeyCode.N))
        {
            beamLength -= beamLengthDelta;
        }
        if (Input.GetKey(KeyCode.M))
        {
            beamLength += beamLengthDelta;
        }
        setPositions();
    }
}
