using UnityEngine;
using EkumeEnumerations;

// The GameObject requires a Rigidbody component, if don't exist, then the component will be added
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHorizontalMovement : MonoBehaviour
{
    public enum HowToChangeDirection { WhenPressTheCorrespondingInputs, DependingOnHisVelocity, None }

   //In what direction is watching the player?
    public DirectionsXAxisEnum currentDirection;
    public HowToChangeDirection howToChangeDirection = HowToChangeDirection.WhenPressTheCorrespondingInputs;

    //Velocity of the player to move in X
    public float velocity;

    //Reduce velocity each millisecond when stop the movement.
    public bool constantReductionOfVelocity;
    public float speedToReduceVelocity;

    //Increase gradually the velocity to reach the value of velocity
    public bool gradualVelocity;
    public float speedToIncreaseVelocity;

    //Constant velocity
    public bool constantVelocityRight; //Ever is running to Right
    public bool constantVelocityLeft; //Ever is running to Left

    //Start constant velocity with input
    public bool startConstantVelocityWithInput;
    public int inputControlToStartConstantVelocity;

    
    //----------------------------------------------------------------------------------------------------------- /

    //Keys to move
    public bool canMoveToRight;
    public int inputControlToRight;
    
    public bool canMoveToLeft;
    public int inputControlToLeft;

    //Crouch down
    public bool playerCanCrouchDown;
    public int inputControlToCrouchDown;
    public bool canMoveIfCrouchDown;

  /*  public bool addImpulseIfCrouchMoving;
    public float impulseToAdd;
    public float velocityToReduceImpulse; */

    //If the player jump and is crouched down
    public bool canMoveIfJumpingCrouched;

    //----------------------------------------------------------------------------------------------------------- /

    // VARIABLES TO USE INTO THE SCRIPTS. DON'T MODIFICABLE VARIABLES.

    //Reduction of velocity
    [HideInInspector] public bool startToReduceVelocityInX;
    [HideInInspector] public float velocityTimeOfReduction;

    Rigidbody2D thisRigidBody;

    //Gradual increase of velocity
    float timerForGradualVelocity;

    bool playerIsCrouched;

   // [HideInInspector] public bool velocityAddedInCrouchDown;
   // [HideInInspector] public float timerForVelocityInCrouchDown;

    //----------------------------------------------------------------------------------------------------------- /

    //Save the original values of the variables (Made because some platforms change the values)

    [HideInInspector] public float originalVelocity;
    [HideInInspector] public float originalSpeedToReduceVelocity;
    [HideInInspector] public float originalSpeedToIncreaseVelocity;
    [HideInInspector] public bool originalConstantReductionOfVelocity;
    [HideInInspector] public bool originalGradualVelocity;
    //[HideInInspector] public bool originalImpuseIfCrouch;
    [HideInInspector] public float originalRigidbodyGravity;
    [HideInInspector] public float originalRigidbodyLinearDrag;
    [HideInInspector] public Quaternion originalRotation;
    //----------------------------------------------------------------------------------------------------------- /

    void Awake()
    {
        //Save the original values of the variables

        originalVelocity = velocity;
        originalSpeedToIncreaseVelocity = speedToIncreaseVelocity;
        originalSpeedToReduceVelocity = speedToReduceVelocity;
        originalConstantReductionOfVelocity = constantReductionOfVelocity;
        originalGradualVelocity = gradualVelocity;
    //    originalImpuseIfCrouch = addImpulseIfCrouchMoving;
        originalRigidbodyGravity = GetComponent<Rigidbody2D>().gravityScale;
        originalRigidbodyLinearDrag = GetComponent<Rigidbody2D>().drag;
        originalRotation = transform.rotation;

        //-----------------------------------------

        thisRigidBody = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (currentDirection == DirectionsXAxisEnum.Right)
            PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerDirectionIsRight, true);
        else if (currentDirection == DirectionsXAxisEnum.Left)
            PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerDirectionIsLeft, true);

        if (gameObject.tag == "Mount")
        {
            this.enabled = false;
        }
    }

    //----------------------------------------------------------------------------------------------------------- /

    //This function is called each frame (Each device can run in different rate of frames)
    void Update ()
    {
        CheckIfConstantVelocity();

        //If the variable constatVelocityLeft is false can call the function for use the controls.
        if (!constantVelocityLeft)
            ControlsMovementLeft();

        if (!constantVelocityRight)
            ControlsMovementRight();

        if (playerCanCrouchDown)
            CrouchDownPCControls();

     /*   if (velocityAddedInCrouchDown)
        {
            if (thisRigidBody.velocity.x > 0.1f)
            {
                thisRigidBody.velocity = new Vector2(thisRigidBody.velocity.x + timerForVelocityInCrouchDown, thisRigidBody.velocity.y);
            }
            else if (thisRigidBody.velocity.x < -0.1f)
            {
                thisRigidBody.velocity = new Vector2(thisRigidBody.velocity.x - timerForVelocityInCrouchDown, thisRigidBody.velocity.y);
            }

            timerForVelocityInCrouchDown = Mathf.Lerp(timerForVelocityInCrouchDown, 0, Time.deltaTime * velocityToReduceImpulse);
        }
        */
    }

    //----------------------------------------------------------------------------------------------------------- /

    void CheckIfConstantVelocity ()
    {
        if (!PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerStartedToMove))
        {
            if ((!startConstantVelocityWithInput) || (startConstantVelocityWithInput && InputControls.GetControlDown(inputControlToStartConstantVelocity)))
            {
                if (!canMoveToLeft || !canMoveToRight)
                    PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerStartedToMove, true);
            }
        }

        if (PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerStartedToMove))
        {
            //If this variable is true, then add a constant velocity to the rigidbody
            if (constantVelocityLeft)
                ChangeVelocityInX(-velocity);
            if (constantVelocityRight)
                ChangeVelocityInX(velocity);
        }
        
    }

    //This function is called each frame (Same rate of velocity for all devices)
    void FixedUpdate ()
    {
        if (startToReduceVelocityInX)
            ReductionVelocityInX();
    }

    //----------------------------------------------------------------------------------------------------------- /

    public void MovementRightStart()
    {
        if (howToChangeDirection == HowToChangeDirection.WhenPressTheCorrespondingInputs)
        {
            if (!PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerDirectionIsRight))
                transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);

            PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerDirectionIsRight, true);
            PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerDirectionIsLeft, false);

            currentDirection = DirectionsXAxisEnum.Right;
        }

        if (!PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerStartedToMove))
                PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerStartedToMove, true);
      

        startToReduceVelocityInX = false;
        //Call the function to move
        ChangeVelocityInX(velocity);
    }

    //----------------------------------------------------------------------------------------------------------- /

    public void MovementRightStop()
    {

        if (gradualVelocity)
            timerForGradualVelocity = 0;

        //If the variable is true
        if (constantReductionOfVelocity)
        {
            //Is asigned the value of velocity to the variable "newVeclocityInReducionInX" and later start to reduce the velocity.
            velocityTimeOfReduction = 0;
            startToReduceVelocityInX = true;
        }
        else
        {
            //If the variable "constantReductionOfVelocity" is false, the velocity of the object will be 0 if the key "Up" is raised
            thisRigidBody.velocity = (new Vector2(0, thisRigidBody.velocity.y));
        }

    }
    //----------------------------------------------------------------------------------------------------------- / 

    void ControlsMovementRight()
    {
        //If the variable is true, then can move to right
        if (canMoveToRight)
        {
            //If press the button
            if (InputControls.GetControl(inputControlToRight))
            {
                MovementRightStart();
            }

            // If the key "Right" is raised
            if (InputControls.GetControlUp(inputControlToRight))
            {
                MovementRightStop();
            }
        }


    }

    //----------------------------------------------------------------------------------------------------------- / 

    public void MovementLeftStart ()
    {
        if (howToChangeDirection == HowToChangeDirection.WhenPressTheCorrespondingInputs)
        {
            if (!PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerDirectionIsLeft))
                transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);

            PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerDirectionIsLeft, true);
            PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerDirectionIsRight, false);

            currentDirection = DirectionsXAxisEnum.Left;
        }

        if (!PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerStartedToMove))
            PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerStartedToMove, true);

        startToReduceVelocityInX = false;
        //Call the function to move
        ChangeVelocityInX(-velocity);
    }

    //----------------------------------------------------------------------------------------------------------- / 

    public void MovementLeftStop()
    {

        if (gradualVelocity)
            timerForGradualVelocity = 0;

        //If the variable is true
        if (constantReductionOfVelocity)
        {
            //Is asigned the value of velocity to the variable "newVeclocityInReducionInX" and later start to reduce the velocity.
            velocityTimeOfReduction = 0;
            startToReduceVelocityInX = true;
        }
        else
        {
            //If the variable "constantReductionOfVelocity" is false, the velocity of the object will be 0 if the key "Up" is raised
            thisRigidBody.velocity = (new Vector2(0, thisRigidBody.velocity.y));
        }
    }

    //----------------------------------------------------------------------------------------------------------- / 

    void ControlsMovementLeft()
    {
        //If is true this variable
        if (canMoveToLeft)
        {
            //If press the button
            if (InputControls.GetControl(inputControlToLeft))
            {
                MovementLeftStart();
            }

            // If the key "Left" is raised
            if (InputControls.GetControlUp(inputControlToLeft))
            {
                MovementLeftStop();
            }
        }

    }

    void CrouchDownPCControls()
    {
        if(InputControls.GetControl(inputControlToCrouchDown))
        {
            CrouchDownStart();
        }

        if (InputControls.GetControlUp (inputControlToCrouchDown))
        {
            CrouchDownStop();
        }
    }

    public void CrouchDownStart ()
    {
        PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerIsCrouchedDown, true);
        playerIsCrouched = true;

        if (!canMoveIfCrouchDown)
        {
            velocityTimeOfReduction = velocityTimeOfReduction/2;
            startToReduceVelocityInX = true;
        }

     /*   if (addImpulseIfCrouchMoving)
        {
            if (!velocityAddedInCrouchDown)
            {
                velocityAddedInCrouchDown = true;
                timerForVelocityInCrouchDown = impulseToAdd;
            }
        }
        */
    }

    public void CrouchDownStop()
    {
        PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerIsCrouchedDown, false);
        playerIsCrouched = false;
     //   velocityAddedInCrouchDown = false;
    }


    void ChangeVelocityInX (float newVelocityX)
    {
        if ((!playerCanCrouchDown || !playerIsCrouched || (playerIsCrouched && canMoveIfCrouchDown))
            || (playerCanCrouchDown && canMoveIfJumpingCrouched && playerIsCrouched && !PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerIsGrounded)))
        {
            if (!gradualVelocity)
            {
                thisRigidBody.velocity = new Vector2(newVelocityX, thisRigidBody.velocity.y);
            }
            else
            {
                timerForGradualVelocity += Time.deltaTime * speedToIncreaseVelocity;
                thisRigidBody.velocity = new Vector2(Mathf.Lerp(thisRigidBody.velocity.x, newVelocityX, timerForGradualVelocity), thisRigidBody.velocity.y);
            }
        }
    }

    // Constant reduction of velocity
    void ReductionVelocityInX()
    {
        velocityTimeOfReduction += Time.deltaTime * speedToReduceVelocity;
        float lastVelocityY = thisRigidBody.velocity.y;
        thisRigidBody.velocity = Vector2.Lerp(thisRigidBody.velocity, Vector2.zero, velocityTimeOfReduction);
        thisRigidBody.velocity = new Vector2(thisRigidBody.velocity.x, lastVelocityY);

        //If the velocity is 0 in X then stop the reduction of the velocity.
        if (thisRigidBody.velocity.x == 0)
        {
            startToReduceVelocityInX = false;
        }
    }
}
