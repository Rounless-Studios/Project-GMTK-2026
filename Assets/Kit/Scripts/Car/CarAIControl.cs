using System;
using UnityEngine;
using GMTK;
using Random = UnityEngine.Random;
/// <summary>
/// Unity Standard Assets CarAIControl, replace with your own
/// </summary>
namespace GMTK.Kit
{
    [RequireComponent(typeof (CarController))]
    public class CarAIControl : MonoBehaviour
    {
        public enum BrakeCondition
        {
            NeverBrake,                 // the car simply accelerates at full throttle all the time.
            TargetDirectionDifference,  // the car will brake according to the upcoming change in direction of the target. Useful for route-based AI, slowing for corners.
            TargetDistance,             // the car will brake as it approaches its target, regardless of the target's direction. Useful if you want the car to
                                        // head for a stationary target and come to rest when it arrives there.
        }

        // This script provides input to the car controller in the same way that the user control script does.
        // As such, it is really 'driving' the car, with no special physics or animation tricks to make the car behave properly.

        // "wandering" is used to give the cars a more human, less robotic feel. They can waver slightly
        // in speed and direction while driving towards their target.

        [SerializeField] [Range(0, 1)] private float m_CautiousSpeedFactor = 0.05f;               // percentage of max speed to use when being maximally cautious
        [SerializeField] [Range(0, 180)] private float m_CautiousMaxAngle = 50f;                  // angle of approaching corner to treat as warranting maximum caution
        [SerializeField] private float m_CautiousMaxDistance = 100f;                              // distance at which distance-based cautiousness begins
        [SerializeField] private float m_CautiousAngularVelocityFactor = 30f;                     // how cautious the AI should be when considering its own current angular velocity (i.e. easing off acceleration if spinning!)
        [SerializeField] private float m_SteerSensitivity = 0.05f;                                // how sensitively the AI uses steering input to turn to the desired direction
        [SerializeField] private float m_AccelSensitivity = 0.04f;                                // How sensitively the AI uses the accelerator to reach the current desired speed
        [SerializeField] private float m_BrakeSensitivity = 1f;                                   // How sensitively the AI uses the brake to reach the current desired speed
        [SerializeField] private float m_LateralWanderDistance = 3f;                              // how far the car will wander laterally towards its target
        [SerializeField] private float m_LateralWanderSpeed = 0.1f;                               // how fast the lateral wandering will fluctuate
        [SerializeField] [Range(0, 1)] private float m_AccelWanderAmount = 0.1f;                  // how much the cars acceleration will wander
        [SerializeField] private float m_AccelWanderSpeed = 0.1f;                                 // how fast the cars acceleration wandering will fluctuate
        [SerializeField] private BrakeCondition m_BrakeCondition = BrakeCondition.TargetDistance; // what should the AI consider when accelerating/braking?
        [SerializeField] private bool m_Driving;                                                  // whether the AI is currently actively driving or stopped.
        [SerializeField] private Transform m_Target;                                              // 'target' the target object to aim for.
        [SerializeField] private bool m_StopWhenTargetReached;                                    // should we stop driving when we reach the target?
        [SerializeField] private float m_ReachTargetThreshold = 2;                                // proximity to target to consider we 'reached' it, and stop driving.

        private float m_RandomPerlin;             // A random value for the car to base its wander on (so that AI cars don't all wander in the same pattern)
        private CarController m_CarController;    // Reference to actual car controller we are controlling
        private float m_AvoidOtherCarTime;        // time until which to avoid the car we recently collided with
        private float m_AvoidOtherCarSlowdown;    // how much to slow down due to colliding with another car, whilst avoiding
        private float m_AvoidPathOffset;          // direction (-1 or 1) in which to offset path to avoid other car, whilst avoiding
        private Rigidbody m_Rigidbody;

        [Header("GMTK Navigation Assist")]
        [SerializeField] private bool m_UseObstacleAvoidance = true;
        [SerializeField] private float m_AvoidanceRayLength = 9f;
        [SerializeField] private float m_AvoidanceStrength = 0.7f;
        [SerializeField] private float m_FeelerForwardOffset = 2.2f;   // how far ahead the feelers start
        [SerializeField] private float m_FeelerUpOffset = 0.4f;
        [SerializeField] private float m_FeelerSideAngle = 28f;        // spread of the left/right feelers
        [SerializeField] private float m_HeadOnSteerBoost = 1.5f;      // extra steer when something is dead ahead
        [SerializeField] private bool m_UseStuckRecovery = true;
        [SerializeField] private float m_StuckCheckInterval = 2f;      // how often to test for progress
        [SerializeField] private float m_StuckDistanceThreshold = 6f;  // min travel per interval to count as progress
        [SerializeField] private float m_RecoverDuration = 1.2f;
        [SerializeField] private float m_RecoverGracePeriod = 5f;      // don't recover during the launch off the grid
        [SerializeField] private float m_RespawnAfterSeconds = 6f;     // if reversing can't free us, respawn on track
        private Vector3 m_LastProgressPos;
        private float m_ProgressCheckTime;
        private float m_RecoverUntil;
        private float m_RecoverSteerDir = 1f;   // alternates each recovery so we don't re-wedge
        private float m_RaceStartTime = -1f;
        private float m_StuckSince = -1f;
        private IAIDriverModifier m_Modifier;   // optional personality hook (null if none)

        private void Awake()
        {
            // get the car controller reference
            m_CarController = GetComponent<CarController>();

            // give the random perlin a random value
            m_RandomPerlin = Random.value*100;

            m_Rigidbody = GetComponent<Rigidbody>();

            // optional personality module (see AIPersonality); inert when absent
            m_Modifier = GetComponent<IAIDriverModifier>();
        }

        public void SetTarget(Transform target)
        {
            m_Target = target;
            m_Driving = true;
        }

        private void FixedUpdate()
        {
            if (m_Target == null || !m_Driving)
            {
                // Car should not be moving,
                // use handbrake to stop
                m_CarController.Move(0, 0, -1f, 1f);
            }
            else
            {
                // GMTK: reverse out of walls / pile-ups when pinned
                if (m_UseStuckRecovery && HandleStuckRecovery()) return;

                Vector3 fwd = transform.forward;
                if (m_Rigidbody.linearVelocity.magnitude > m_CarController.MaxSpeed*0.1f)
                {
                    fwd = m_Rigidbody.linearVelocity;
                }

                float desiredSpeed = m_CarController.MaxSpeed;

                // now it's time to decide if we should be slowing down...
                switch (m_BrakeCondition)
                {
                    case BrakeCondition.TargetDirectionDifference:
                        {
                            // the car will brake according to the upcoming change in direction of the target. Useful for route-based AI, slowing for corners.

                            // check out the angle of our target compared to the current direction of the car
                            float approachingCornerAngle = Vector3.Angle(m_Target.forward, fwd);

                            // also consider the current amount we're turning, multiplied up and then compared in the same way as an upcoming corner angle
                            float spinningAngle = m_Rigidbody.angularVelocity.magnitude*m_CautiousAngularVelocityFactor;

                            // if it's different to our current angle, we need to be cautious (i.e. slow down) a certain amount
                            float cautiousnessRequired = Mathf.InverseLerp(0, m_CautiousMaxAngle,
                                                                           Mathf.Max(spinningAngle,
                                                                                     approachingCornerAngle));
                            desiredSpeed = Mathf.Lerp(m_CarController.MaxSpeed, m_CarController.MaxSpeed*m_CautiousSpeedFactor,
                                                      cautiousnessRequired);
                            break;
                        }

                    case BrakeCondition.TargetDistance:
                        {
                            // the car will brake as it approaches its target, regardless of the target's direction. Useful if you want the car to
                            // head for a stationary target and come to rest when it arrives there.

                            // check out the distance to target
                            Vector3 delta = m_Target.position - transform.position;
                            float distanceCautiousFactor = Mathf.InverseLerp(m_CautiousMaxDistance, 0, delta.magnitude);

                            // also consider the current amount we're turning, multiplied up and then compared in the same way as an upcoming corner angle
                            float spinningAngle = m_Rigidbody.angularVelocity.magnitude*m_CautiousAngularVelocityFactor;

                            // if it's different to our current angle, we need to be cautious (i.e. slow down) a certain amount
                            float cautiousnessRequired = Mathf.Max(
                                Mathf.InverseLerp(0, m_CautiousMaxAngle, spinningAngle), distanceCautiousFactor);
                            desiredSpeed = Mathf.Lerp(m_CarController.MaxSpeed, m_CarController.MaxSpeed*m_CautiousSpeedFactor,
                                                      cautiousnessRequired);
                            break;
                        }

                    case BrakeCondition.NeverBrake:
                        break;
                }

                // GMTK: personality speed scaling (e.g. reckless racers go faster)
                if (m_Modifier != null)
                    desiredSpeed *= m_Modifier.SpeedMultiplier;

                // Evasive action due to collision with other cars:

                // our target position starts off as the 'real' target position
                Vector3 offsetTargetPos = m_Target.position;

                // GMTK: personality target bias (e.g. rammers veer toward the player)
                if (m_Modifier != null)
                    offsetTargetPos += m_Modifier.GetTargetOffset(transform, m_Target.position);

                // if are we currently taking evasive action to prevent being stuck against another car:
                if (Time.time < m_AvoidOtherCarTime)
                {
                    // slow down if necessary (if we were behind the other car when collision occured)
                    desiredSpeed *= m_AvoidOtherCarSlowdown;

                    // and veer towards the side of our path-to-target that is away from the other car
                    offsetTargetPos += m_Target.right*m_AvoidPathOffset;
                }
                else
                {
                    // no need for evasive action, we can just wander across the path-to-target in a random way,
                    // which can help prevent AI from seeming too uniform and robotic in their driving
                    offsetTargetPos += m_Target.right*
                                       (Mathf.PerlinNoise(Time.time*m_LateralWanderSpeed, m_RandomPerlin)*2 - 1)*
                                       m_LateralWanderDistance;
                }

                // use different sensitivity depending on whether accelerating or braking:
                float accelBrakeSensitivity = (desiredSpeed < m_CarController.CurrentSpeed)
                                                  ? m_BrakeSensitivity
                                                  : m_AccelSensitivity;

                // decide the actual amount of accel/brake input to achieve desired speed.
                float accel = Mathf.Clamp((desiredSpeed - m_CarController.CurrentSpeed)*accelBrakeSensitivity, -1, 1);

                // add acceleration 'wander', which also prevents AI from seeming too uniform and robotic in their driving
                // i.e. increasing the accel wander amount can introduce jostling and bumps between AI cars in a race
                accel *= (1 - m_AccelWanderAmount) +
                         (Mathf.PerlinNoise(Time.time*m_AccelWanderSpeed, m_RandomPerlin)*m_AccelWanderAmount);

                // calculate the local-relative position of the target, to steer towards
                Vector3 localTarget = transform.InverseTransformPoint(offsetTargetPos);

                // work out the local angle towards the target
                float targetAngle = Mathf.Atan2(localTarget.x, localTarget.z)*Mathf.Rad2Deg;

                // get the amount of steering needed to aim the car towards the target
                float steer = Mathf.Clamp(targetAngle*m_SteerSensitivity, -1, 1)*Mathf.Sign(m_CarController.CurrentSpeed);

                // GMTK: blend in raycast obstacle avoidance so the AI stops kissing walls
                if (m_UseObstacleAvoidance)
                    steer = Mathf.Clamp(steer + ComputeAvoidanceSteer(), -1f, 1f);

                // feed input to the car controller.
                m_CarController.Move(steer, accel, accel, 0f);

                // if appropriate, stop driving when we're close enough to the target.
                if (m_StopWhenTargetReached && localTarget.magnitude < m_ReachTargetThreshold)
                {
                    m_Driving = false;
                }
            }
        }


        // GMTK: back up and re-orient when the car has been crawling for too long
        // (typically nose-first into a wall or wedged against other cars).
        private bool HandleStuckRecovery()
        {
            // Only recover while the race is actually running. Before the start the cars
            // sit frozen on the grid; without this guard the progress check reads "stuck"
            // and reverses everyone off the line.
            if (!Race.IsRaceInProgress)
            {
                m_LastProgressPos = transform.position;
                m_ProgressCheckTime = Time.time + m_StuckCheckInterval;
                m_RecoverUntil = 0f;
                m_RaceStartTime = -1f;
                m_StuckSince = -1f;
                return false;
            }

            // grace period after the lights go out: let cars launch off the grid before
            // the progress check can flag the normal start-line jostle as "stuck".
            if (m_RaceStartTime < 0f) m_RaceStartTime = Time.time;
            if (Time.time < m_RaceStartTime + m_RecoverGracePeriod)
            {
                m_LastProgressPos = transform.position;
                m_ProgressCheckTime = Time.time + m_StuckCheckInterval;
                return false;
            }

            if (Time.time >= m_RecoverUntil)
            {
                // progress-based stuck detection: catches full stops AND cars grinding
                // slowly along a wall (which a pure speed check misses).
                if (Time.time >= m_ProgressCheckTime)
                {
                    float moved = Vector3.Distance(transform.position, m_LastProgressPos);
                    if (moved < m_StuckDistanceThreshold)
                    {
                        if (m_StuckSince < 0f) m_StuckSince = Time.time;
                        m_RecoverUntil = Time.time + m_RecoverDuration;
                        // flip the steer each attempt so repeated recoveries back out at
                        // different angles instead of re-wedging into the same wall
                        m_RecoverSteerDir = -m_RecoverSteerDir;

                        // hard fallback: reversing couldn't free us (deep corner wedge) —
                        // respawn on the track at the current waypoint so the race continues
                        if (Time.time - m_StuckSince > m_RespawnAfterSeconds)
                        {
                            RespawnAtTarget();
                            m_StuckSince = -1f;
                            m_RecoverUntil = 0f;
                        }
                    }
                    else
                    {
                        m_StuckSince = -1f; // made progress
                    }
                    m_LastProgressPos = transform.position;
                    m_ProgressCheckTime = Time.time + m_StuckCheckInterval;
                }
            }

            if (Time.time < m_RecoverUntil)
            {
                // reverse (accel 0, footbrake -1 => reverse torque at low speed) while
                // turning so the nose swings off whatever we're wedged against
                m_CarController.Move(m_RecoverSteerDir, 0f, -1f, 0f);
                return true;
            }

            return false;
        }

        // GMTK: last-resort un-stick — drop the car back onto the track at the waypoint
        // it was heading for, facing the direction of travel, and zero its velocity.
        private void RespawnAtTarget()
        {
            if (m_Target == null) return;
            Vector3 fwd = m_Target.position - transform.position;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
            transform.position = m_Target.position + Vector3.up * 1f;
            transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            if (m_Rigidbody != null)
            {
                m_Rigidbody.linearVelocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }
        }

        // GMTK: three forward "feelers" that push steering away from nearby geometry.
        private float ComputeAvoidanceSteer()
        {
            Vector3 origin = transform.position + transform.forward * m_FeelerForwardOffset + transform.up * m_FeelerUpOffset;
            float left = Feeler(origin, Quaternion.AngleAxis(-m_FeelerSideAngle, transform.up) * transform.forward);
            float center = Feeler(origin, transform.forward);
            float right = Feeler(origin, Quaternion.AngleAxis(m_FeelerSideAngle, transform.up) * transform.forward);

            float steer = 0f;
            steer += left * m_AvoidanceStrength;    // obstacle on the left  -> steer right (+)
            steer -= right * m_AvoidanceStrength;   // obstacle on the right -> steer left  (-)

            if (center > 0.01f)
            {
                // head-on obstacle: commit to the clearer side
                float dir = (right <= left) ? 1f : -1f;
                steer += dir * center * m_AvoidanceStrength * m_HeadOnSteerBoost;
            }

            return Mathf.Clamp(steer, -1f, 1f);
        }

        // Returns 0 (clear) .. 1 (obstacle right at the feeler tip).
        // Only static geometry counts — car-vs-car is left to collision handling and
        // personalities, so avoidance doesn't cancel out ramming/blocking behaviour.
        private float Feeler(Vector3 origin, Vector3 dir)
        {
            if (Physics.Raycast(origin, dir, out var hit, m_AvoidanceRayLength))
            {
                if (hit.collider == null) return 0f;
                var root = hit.collider.transform.root;
                if (root == transform.root) return 0f;              // ignore our own car
                if (root.GetComponent<CarController>() != null) return 0f; // ignore other cars
                return 1f - (hit.distance / m_AvoidanceRayLength);
            }
            return 0f;
        }

        /// <summary>Re-read the optional personality module (call after adding one at runtime).</summary>
        public void RefreshModifier()
        {
            m_Modifier = GetComponent<IAIDriverModifier>();
        }

        private void OnCollisionStay(Collision col)
        {
            // detect collision against other cars, so that we can take evasive action
            if (col.rigidbody != null)
            {
                var otherAI = col.rigidbody.GetComponent<CarAIControl>();
                if (otherAI != null)
                {
                    // we'll take evasive action for 1 second
                    m_AvoidOtherCarTime = Time.time + 1;

                    // but who's in front?...
                    if (Vector3.Angle(transform.forward, otherAI.transform.position - transform.position) < 90)
                    {
                        // the other ai is in front, so it is only good manners that we ought to brake...
                        m_AvoidOtherCarSlowdown = 0.5f;
                    }
                    else
                    {
                        // we're in front! ain't slowing down for anybody...
                        m_AvoidOtherCarSlowdown = 1;
                    }

                    // both cars should take evasive action by driving along an offset from the path centre,
                    // away from the other car
                    var otherCarLocalDelta = transform.InverseTransformPoint(otherAI.transform.position);
                    float otherCarAngle = Mathf.Atan2(otherCarLocalDelta.x, otherCarLocalDelta.z);
                    m_AvoidPathOffset = m_LateralWanderDistance*-Mathf.Sign(otherCarAngle);
                }
            }
        }
    }
}
