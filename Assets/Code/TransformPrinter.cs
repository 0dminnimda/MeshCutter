using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransformPrinter : MonoBehaviour
{
    [SerializeField]
    Transform t;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Debug.Log("forward: " + t.forward.ToString() + " right: " + t.right.ToString() + " up: " + t.up.ToString());
        Debug.Log(t.parent);
    }
}
