﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PlayerPositionHistoryControl : MonoBehaviour
{
    private struct PositionHistory
    {
        public Vector3 shipPosition;
        public Quaternion shipRotation;
        public Vector3 shipVelocity;

        public Vector3 skierPosition;
        public Quaternion skierRotation;
        public Vector3 skierVelocity;

        public bool isConnected;

        //private const float MAX_SKIER_TO_SHIP_DISTANCE = 4.0f;

        public PositionHistory(Transform shipTransform, Transform skierTransform, bool newIsConnected)
        {
            shipPosition = shipTransform.position;
            shipRotation = shipTransform.rotation;
            shipVelocity = shipTransform.gameObject.GetComponent<Rigidbody>().velocity; // TO DO: Replace these calls by maintaining a reference to the rigidbody instead of finding it each time

            skierPosition = skierTransform.position;
            skierRotation = skierTransform.rotation;
            skierVelocity = skierTransform.gameObject.GetComponent<Rigidbody>().velocity; // TO DO: Replace these calls by maintaining a reference to the rigidbody instead of finding it each time

            isConnected = newIsConnected;

            //Debug.Log("Stored velocities: Ship = " + shipVelocity + " Skier = " + skierVelocity);

            //// Move the skier closer if it's too far away
            //Vector3 shipToSkier = shipPosition - skierPosition;
            //if (shipToSkier.magnitude > MAX_SKIER_TO_SHIP_DISTANCE)
            //{
            //    skierPosition = shipPosition - (shipToSkier.normalized * MAX_SKIER_TO_SHIP_DISTANCE);
            //}
        }
    }


    [Header("Object references:")]
    [Tooltip("The in-game skier object")]
    public GameObject theSkier;

    [Tooltip("The skier prefab. Used to re-instantiate the skier if it is lost/destroyed/disconnected")]
    public GameObject skierPrefab;

    [Tooltip("The in-game rope object")]
    public GameObject theRope;

    [Header("Rewind settings:")]

    [Tooltip("The frequency to save the ships position, in seconds")]
    public float saveInterval = 0.25f;

    [Tooltip("The amount of time to rewind when the player fails, in seconds")]
    public float rewindAmount = 4f;

    [Tooltip("The amount of time to wait before rewinding, in seconds")]
    public float rewindDelay = 1f;

    [Tooltip("The rewind speed factor. eg. 2.0 rewinds at twice the normal gameplay speed")]
    public float rewindSpeed = 1.0f;

    [Tooltip("Speeds up the rewind effect as it progresses. Factor starts at 1.0f and this term is added to it and then multiplied by delta time between each rewind point. ")]
    [Range(0f, 1f)]
    public float rewindSpeedIncreaseFactor = 0.2f;

    private float intervalTime;
    private List<PositionHistory> thePositionHistory;
    private int listSize;
    private ConfigurableJoint skiRopeJoint;
    private Rigidbody shipRigidbody;
    private Rigidbody skierRigidbody;
    private Vector3 skiRopeConnectedAnchor;
    private Vector3 skiRopeAnchor;

    public bool IsRewinding
    {
        get; private set;
    }
    private bool aboutToRewind;
    

    // Use this for initialization
    void Start () {
        intervalTime = 0f;

        listSize = (int)Mathf.Round(rewindAmount / saveInterval);
        thePositionHistory = new List<PositionHistory>(listSize + 1); // Add an extra element to allow the queue to expand without requiring a new allocation
        if ((rewindAmount / saveInterval) % 1 != 0)
        {
            Debug.LogWarning("Warning: rewindamount/saveInterval MUST equal an integer: " + rewindAmount + "/" + saveInterval + " = " + (rewindAmount/saveInterval + " has remainder " + ((rewindAmount / saveInterval) % 1) + "\n" 
                + "In an attempt to correct this, the total (padded) list size has been rounded to " + thePositionHistory.Capacity ));
        }

        skiRopeJoint = theSkier.GetComponent<ConfigurableJoint>();
        skiRopeConnectedAnchor = skiRopeJoint.connectedAnchor;
        skiRopeAnchor = skiRopeJoint.anchor;

        shipRigidbody = this.GetComponent<Rigidbody>();
        skierRigidbody = theSkier.GetComponent<Rigidbody>();

        IsRewinding = aboutToRewind = false;
    }


    private void FixedUpdate()
    {
        if (!IsRewinding)
        {
            // Store our position data:
            intervalTime += Time.fixedDeltaTime;
            if (intervalTime >= saveInterval)
            {
                intervalTime -= saveInterval;

                thePositionHistory.Add(new PositionHistory(this.transform, theSkier.transform, skiRopeJoint == null ? false : true));

                if (thePositionHistory.Count > listSize)
                {
                    thePositionHistory.RemoveAt(0); // Remove the first/oldest element
                }
            }

            // Check if the player needs to be reset:
            if (skiRopeJoint == null && SceneManager.Instance.IsPlaying && !aboutToRewind)
            {
                theRope.SetActive(false);

                aboutToRewind = true;

                StartCoroutine("DoRewind");
            }
        } // End of !IsRewinding condition
    }


    IEnumerator DoRewind()
    {
        yield return new WaitForSeconds(rewindDelay);
        yield return new WaitForFixedUpdate(); // Ensure we're starting on a physics beat
        
        IsRewinding = true;

        // Add the final state so we can begin to rewind:
        thePositionHistory.Add(new PositionHistory(this.transform, theSkier.transform, skiRopeJoint == null ? false : true));

        float rewindTime = 0f;
        float rewindInterval = saveInterval / rewindSpeed;
        float scaleFactor = 1.0f;
        bool hasReattachedRope = false;

        rewindTime = -Time.fixedDeltaTime; // Cancel out the first delta term so we start lerping at 0

        int currentPositionIndex = thePositionHistory.Count - 1;
        while(currentPositionIndex > 0)
        {
            rewindTime %= rewindInterval;

            while (rewindTime < rewindInterval)
            {
                rewindTime += (Time.fixedDeltaTime * scaleFactor);

                float lerpDelta = Mathf.Clamp01(rewindTime / rewindInterval);

                // Reset the ship:
                shipRigidbody.MovePosition(Vector3.Lerp(thePositionHistory[currentPositionIndex].shipPosition, thePositionHistory[currentPositionIndex - 1].shipPosition, lerpDelta));
                shipRigidbody.MoveRotation(Quaternion.Lerp(thePositionHistory[currentPositionIndex].shipRotation, thePositionHistory[currentPositionIndex - 1].shipRotation, lerpDelta));

                // Reset the skier:
                skierRigidbody.MovePosition(Vector3.Lerp(thePositionHistory[currentPositionIndex].skierPosition, thePositionHistory[currentPositionIndex - 1].skierPosition, lerpDelta));
                skierRigidbody.MoveRotation(Quaternion.Lerp(thePositionHistory[currentPositionIndex].skierRotation, thePositionHistory[currentPositionIndex - 1].skierRotation, lerpDelta));

                // Reset the rope:
                if (thePositionHistory[currentPositionIndex].isConnected)
                {
                    if (!hasReattachedRope)
                    {
                        hasReattachedRope = true;

                        theRope.SetActive(true);
                        RopeBehavior theRopeController = theRope.GetComponent<RopeBehavior>();
                        theRopeController.skierRopeAttachPointTransform = theSkier.transform;

                        Transform[] shipChildTransforms = this.GetComponentsInChildren<Transform>();
                        foreach (Transform current in shipChildTransforms)
                        {
                            if (current.name == "PlayerShipRopeAttachPoint") // TO DO: Replace this with a (much faster) tag comparison
                            {
                                theRopeController.playerShipRopeAttachPointTransform = current;
                                break;
                            }
                        }
                    }
                    else
                    {
                        theRope.GetComponent<RopeBehavior>().Update(); // Manually force an update, to ensure the rope is correctly aligned
                    }
                }

                yield return new WaitForFixedUpdate();
            } // End of inner "lerping" loop

            // Increase the rewind speed until half the points have been rewound, then decrease it again
            if (currentPositionIndex >= thePositionHistory.Count / 2)
            {
                scaleFactor += rewindSpeedIncreaseFactor;
            }
            else
            {
                scaleFactor -= rewindSpeedIncreaseFactor;
            }

            currentPositionIndex--;
        } // End of outer "positions" loop

        // Set the final ship position:
        shipRigidbody.MovePosition(thePositionHistory[0].shipPosition);
        shipRigidbody.MoveRotation(thePositionHistory[0].shipRotation);

        // Recreate the skier from its prefab for simpler joint setup:
        Destroy(theSkier.gameObject);
        theSkier = Instantiate<GameObject>(skierPrefab, thePositionHistory[0].skierPosition, thePositionHistory[0].skierRotation);
        theSkier.GetComponent<SkierBehavior>().ropeObject = theRope; // Is this needed anymore?
        theSkier.GetComponentInChildren<SkierAIController>().playerShipTransform = this.transform;

        skierRigidbody = theSkier.GetComponent<Rigidbody>();

        skiRopeJoint = theSkier.GetComponent<ConfigurableJoint>();
        skiRopeJoint.autoConfigureConnectedAnchor = false; // This is required for the following 2 properties to stick
        skiRopeJoint.connectedAnchor = skiRopeConnectedAnchor;
        skiRopeJoint.anchor = skiRopeAnchor;
        skiRopeJoint.connectedBody = shipRigidbody;

        theRope.GetComponent<RopeBehavior>().skierRopeAttachPointTransform = theSkier.transform;
        theRope.GetComponent<RopeBehavior>().Update(); // Manually force an update, to ensure the rope is correctly aligned

        shipRigidbody.velocity = Vector3.zero;
        skierRigidbody.velocity = Vector3.zero;

        thePositionHistory.Clear();
        thePositionHistory.Add(new PositionHistory(this.transform, theSkier.transform, skiRopeJoint == null ? false : true)); // Ensure the list is never empty, to avoid issues if the player immediately re-crashes

        IsRewinding = false;
        aboutToRewind = false;
    }
}