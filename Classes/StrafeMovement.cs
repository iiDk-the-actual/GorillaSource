using BepInEx;
using UnityEngine;

namespace GorillaSource
{
    public class StrafeMovement : MonoBehaviour
    {
        [SerializeField]
        public float accel = 200f;         // How fast the player accelerates on the ground
        [SerializeField]
        public float airAccel = 300f;      // How fast the player accelerates in the air
        [SerializeField]
        public float maxSpeed = 6.4f;      // Maximum player speed on the ground
        [SerializeField]
        public float maxAirSpeed = 0.6f;   // "Maximum" player speed in the air
        [SerializeField]
        public float friction = 8f;        // How fast the player decelerates on the ground
        [SerializeField]
        public float jumpForce = 3f;       // How high the player jumps

        [SerializeField]
        public GameObject camObj;

        private float lastJumpPress = -1f;
        private float jumpPressDuration = 0.1f;
        private bool onGround = false;

        private void Update()
        {
            if (UnityInput.Current.GetKey(KeyCode.Space) || ControllerInputPoller.instance.rightControllerPrimaryButton)
                lastJumpPress = Time.time;
        }

        private float fastStartTime;
        private void FixedUpdate()
        {
            Vector2 input = new Vector2((UnityInput.Current.GetKey(KeyCode.A) ? -1f : 0f) + (UnityInput.Current.GetKey(KeyCode.D) ? 1f : 0f), (UnityInput.Current.GetKey(KeyCode.S) ? -1f : 0f) + (UnityInput.Current.GetKey(KeyCode.W) ? 1f : 0f));
            Vector2 vrInput = Plugin.instance.GetLeftJoystickAxis();
            if (GetComponent<Rigidbody>().velocity.magnitude > maxSpeed * 3f)
                vrInput.y = 0f;
            if (vrInput.magnitude > 0.05f)
                input += vrInput.normalized;

            // Get player velocity
            Vector3 playerVelocity = GetComponent<Rigidbody>().velocity;
            // Slow down if on ground
            playerVelocity = CalculateFriction(playerVelocity);
            // Add player input
            playerVelocity += CalculateMovement(input, playerVelocity);
            // Assign new velocity to player object
            GetComponent<Rigidbody>().velocity = playerVelocity;

            if (playerVelocity.magnitude > maxSpeed * 2f)
            {
                if (fastStartTime == -1)
                    fastStartTime = Time.time;
            }
            else
                fastStartTime = -1;

            if (fastStartTime == -1)
                Plugin.StopSoundtrack();
        }

        /// <summary>
        /// Slows down the player if on ground
        /// </summary>
        /// <param name="currentVelocity">Velocity of the player</param>
        /// <returns>Modified velocity of the player</returns>
        private Vector3 CalculateFriction(Vector3 currentVelocity)
        {
            onGround = CheckGround();
            float speed = currentVelocity.magnitude;

            if (!onGround || UnityInput.Current.GetKey(KeyCode.Space) || ControllerInputPoller.instance.rightControllerPrimaryButton || speed == 0f)
                return currentVelocity;

            float drop = speed * friction * Time.deltaTime;
            return currentVelocity * (Mathf.Max(speed - drop, 0f) / speed);
        }

        /// <summary>
        /// Moves the player according to the input. (THIS IS WHERE THE STRAFING MECHANIC HAPPENS)
        /// </summary>
        /// <param name="input">Horizontal and vertical axis of the user input</param>
        /// <param name="velocity">Current velocity of the player</param>
        /// <returns>Additional velocity of the player</returns>
        private Vector3 CalculateMovement(Vector2 input, Vector3 velocity)
        {
            onGround = CheckGround();

            //Different acceleration values for ground and air
            float curAccel = accel;
            if (!onGround)
                curAccel = airAccel;

            //Ground speed
            float curMaxSpeed = maxSpeed;

            //Air speed
            if (!onGround)
                curMaxSpeed = maxAirSpeed;

            if (onGround && input.magnitude > 0)
                Plugin.PlayWalk();
            else
                Plugin.StopWalk();

            //Get rotation input and make it a vector
            Vector3 camRotation = new Vector3(0f, camObj.transform.rotation.eulerAngles.y, 0f);
            Vector3 inputVelocity = Quaternion.Euler(camRotation) *
                                    new Vector3(input.x * curAccel, 0f, input.y * curAccel);

            //Ignore vertical component of rotated input
            Vector3 alignedInputVelocity = new Vector3(inputVelocity.x, 0f, inputVelocity.z) * Time.deltaTime;

            //Get current velocity
            Vector3 currentVelocity = new Vector3(velocity.x, 0f, velocity.z);

            //How close the current speed to max velocity is (1 = not moving, 0 = at/over max speed)
            float max = Mathf.Max(0f, 1 - (currentVelocity.magnitude / curMaxSpeed));

            //How perpendicular the input to the current velocity is (0 = 90°)
            float velocityDot = Vector3.Dot(currentVelocity, alignedInputVelocity);

            //Scale the input to the max speed
            Vector3 modifiedVelocity = alignedInputVelocity * max;

            //The more perpendicular the input is, the more the input velocity will be applied
            Vector3 correctVelocity = Vector3.Lerp(alignedInputVelocity, modifiedVelocity, velocityDot);

            //Apply jump
            correctVelocity += GetJumpVelocity(velocity.y);

            //Return
            return correctVelocity;
        }

        /// <summary>
        /// Calculates the velocity with which the player is accelerated up when jumping
        /// </summary>
        /// <param name="yVelocity">Current "up" velocity of the player (velocity.y)</param>
        /// <returns>Additional jump velocity for the player</returns>
        private float lastJumpTime;
        private Vector3 GetJumpVelocity(float yVelocity)
        {
            Vector3 jumpVelocity = Vector3.zero;

            if (Time.time < lastJumpPress + jumpPressDuration && yVelocity < jumpForce && CheckGround())
            {
                if (Time.time > lastJumpTime)
                {
                    Plugin.Play2DAudio(Plugin.LoadSoundFromResource("GorillaSource.Resources.jump" + UnityEngine.Random.Range(1, 4) + ".wav"), 1f);
                    lastJumpTime = Time.time + jumpPressDuration;
                }

                if (Time.time > fastStartTime + 2.7f)
                    Plugin.PlaySoundtrack();

                lastJumpPress = -1f;
                jumpVelocity = new Vector3(0f, jumpForce - yVelocity, 0f);
            }

            return jumpVelocity;
        }

        /// <summary>
        /// Checks if the player is touching the ground. This is a quick hack to make it work, don't actually do it like this.
        /// </summary>
        /// <returns>True if the player touches the ground, false if not</returns>
        private bool CheckGround()
        {
            Ray ray = new Ray(GorillaLocomotion.GTPlayer.Instance.bodyCollider.transform.position, Vector3.down);
            bool result = Physics.Raycast(ray, GorillaLocomotion.GTPlayer.Instance.bodyCollider.bounds.extents.y + 0.1f, GorillaLocomotion.GTPlayer.Instance.locomotionEnabledLayers);
            return result;
        }
    }

}