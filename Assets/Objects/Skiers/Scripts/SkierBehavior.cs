﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkierBehavior : MonoBehaviour {

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag != "Collectable")
        {
            GameManager.instance.FailLevel();

            Destroy(this.gameObject); // Temp hack
        }
    }
}
