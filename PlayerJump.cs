using UnityEngine;
using EkumeEnumerations;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerHorizontalMovement))]
public class PlayerJump : MonoBehaviour
{
    public Transform groundChecker;
    public float radiusOfGroundChecker = 0.3f;

    public float jumpForce;

    public int inputControl;

    //Only one can be selected
    public bool activateDoubleJump;
    public bool noLimitOfJumps;

    //Jump when the player start the game with click/key
    public bool jumpWithTheFirstTime;

    public bool isGrounded;
    Rigidbody2D thisRigidBody;

    [HideInInspector] public bool isInDoubleJump;

    [HideInInspector] public bool originalActivateDoubleJump;
    [HideInInspector] public bool originalNoLimitOfJumps;

    bool startedToRun = false;

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (groundChecker != null)
        {
            Handles.color = Color.cyan;
            Handles.DrawWireDisc(groundChecker.position, groundChecker.forward, radiusOfGroundChecker);
        }
    }
#endif

    void Start()
    {
#if UNITY_EDITOR

        if (groundChecker == null)
        {
            Debug.LogError("Please add the ground checker to the component PlayerJump in the GameObject: " + this.gameObject.name);
        }

#endif
        thisRigidBody = GetComponent<Rigidbody2D>();

        originalActivateDoubleJump = activateDoubleJump;
        originalNoLimitOfJumps = noLimitOfJumps;

        if (jumpWithTheFirstTime)
            startedToRun = true;

        PlayerHorizontalMovement scriptMovement = GetComponent<PlayerHorizontalMovement>();

        if (!scriptMovement.startConstantVelocityWithInput)
        {
            startedToRun = true;
        }

        if (gameObject.tag == "Mount")
        {
            this.enabled = false;
        }
    }

    void FixedUpdate ()
    {
        isGrounded = Physics2D.OverlapCircle(groundChecker.position, radiusOfGroundChecker, 1 << LayerMask.NameToLayer("Ground"));
        if (isGrounded)
        {
            isInDoubleJump = false;
            PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerIsGrounded, true);
            PlayerStates.SetPlayerStateValue(PlayerStatesEnum.IsTheSecondJump, false);
        }
        else
        {
            PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerIsGrounded, false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (InputControls.GetControlDown(inputControl))
        {
            if (startedToRun)
            {
                ActivateJump();
            }
            else
            {
                startedToRun = true;
            }
        }
    }

    public void AddVelocityToUp (float forceToAdd)
    {
        if(!PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerInLadder))
            thisRigidBody.velocity = new Vector2(thisRigidBody.velocity.x, forceToAdd);
    }

    public void ActivateJump()
    {
        if (activateDoubleJump)
        {
            if (isGrounded || !isInDoubleJump)
            {
                AddVelocityToUp(jumpForce);

                if (!isInDoubleJump && !isGrounded)
                {
                    isInDoubleJump = true;
                    PlayerStates.SetPlayerStateValue(PlayerStatesEnum.IsTheSecondJump, true);
                }
            }
        }
        else
        {
            if (isGrounded)
            {
                AddVelocityToUp(jumpForce);
            }
        }

        if (noLimitOfJumps && !activateDoubleJump)
        {
            AddVelocityToUp(jumpForce);
        }
    }
}
