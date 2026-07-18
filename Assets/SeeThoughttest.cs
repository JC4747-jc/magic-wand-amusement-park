using UnityEngine;
using Unity.XR.PXR;

public class SeeThoughttest : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PXR_Manager.EnableVideoSeeThrough = true;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
