using System;
using UnityEngine;
using UnityEngine.AI;

namespace GoldenHour
{
    /// <summary>
    /// Moves a character to a destination on the NavMesh (AI Navigation). Falls back to straight-line
    /// movement if no NavMesh is available, so the scenario never stalls.
    /// </summary>
    public sealed class Walker : MonoBehaviour
    {
        public const int CharacterLayer = 1;

        private NavMeshAgent agent;
        private HumanoidBody body;
        private Vector3 destination;
        private Action arrived;
        private bool moving;
        private float speed = 1.6f;

        public bool Moving => moving;
        public bool UsingNavMesh => agent != null && agent.enabled && agent.isOnNavMesh;

        public void Initialize(HumanoidBody humanoid, float walkSpeed)
        {
            body = humanoid;
            speed = walkSpeed;
            agent = gameObject.GetComponent<NavMeshAgent>();
            if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.7f;
            agent.speed = walkSpeed;
            agent.angularSpeed = 360f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.4f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.enabled = false;
        }

        public void GoTo(Vector3 target, Action onArrived)
        {
            destination = target;
            arrived = onArrived;
            moving = true;
            if (agent != null)
            {
                agent.enabled = true;
                if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }

                if (agent.isOnNavMesh)
                {
                    agent.speed = speed;
                    agent.isStopped = false;
                    if (!agent.SetDestination(target)) agent.enabled = false;
                }
                else
                {
                    agent.enabled = false;
                }
            }

            if (body != null) body.SetState(BodyState.Walking);
        }

        public void Stop(BodyState state = BodyState.Standing)
        {
            moving = false;
            if (agent != null && agent.enabled)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                agent.enabled = false;
            }

            if (body != null && body.State == BodyState.Walking) body.SetState(state);
        }

        private void Update()
        {
            if (!moving) return;
            if (UsingNavMesh)
            {
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f) Arrive();
                return;
            }

            Vector3 to = destination - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.3f)
            {
                Arrive();
                return;
            }

            Vector3 step = to.normalized * speed * Time.deltaTime;
            transform.position += step;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 6f);
        }

        private void Arrive()
        {
            Action callback = arrived;
            arrived = null;
            Stop();
            callback?.Invoke();
        }

        /// <summary>Puts a character root and all its children on the character layer (excluded from NavMesh baking).</summary>
        public static void SetCharacterLayer(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = CharacterLayer;
        }
    }
}
