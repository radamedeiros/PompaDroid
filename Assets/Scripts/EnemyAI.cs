using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class EnemyAI : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,
        Waiting,
        Attacking,
        Chasing,
        Roaming
    }

    [System.Serializable]
    public class DecisionWeight
    {
        public int weight;
        public EnemyState state;
        public DecisionWeight(int weight, EnemyState state)
        {
            this.weight = weight;
            this.state = state;
        }
    }

    private Enemy enemy;
    private List<Transform> heroTransforms = new List<Transform>();

    public float attackReachMin;
    public float attackReachMax;
    public float personalSpace;

    public HeroDetector detector;

    private List<DecisionWeight> weights = new List<DecisionWeight>();

    private EnemyState currentState = EnemyState.Idle;

    private Coroutine actionCoroutine;

    private void Start()
    {
        enemy = GetComponent<Enemy>();

        // Encontrar todos os heróis na cena
        GameObject[] heroObjs = GameObject.FindGameObjectsWithTag("Hero");
        foreach (GameObject heroObj in heroObjs)
        {
            heroTransforms.Add(heroObj.transform);
        }

        if (heroTransforms.Count == 0)
        {
            Debug.LogError("Nenhum objeto Hero encontrado!");
        }
    }

    private void Update()
    {
        if (heroTransforms.Count == 0) return;

        // Selecionar o herói mais próximo
        Transform targetHero = GetClosestHero();

        if (targetHero == null) return;

        switch (currentState)
        {
            case EnemyState.Idle:
                DecideNextAction(targetHero);
                break;
            case EnemyState.Waiting:
            case EnemyState.Attacking:
            case EnemyState.Chasing:
            case EnemyState.Roaming:
                // Ação atual está sendo executada pela corrotina
                break;
        }
    }

    private Transform GetClosestHero()
    {
        Transform closestHero = null;
        float minDistanceSqr = Mathf.Infinity;
        foreach (Transform hero in heroTransforms)
        {
            if (hero == null) continue;
            float distanceSqr = (hero.position - transform.position).sqrMagnitude;
            if (distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                closestHero = hero;
            }
        }
        return closestHero;
    }

    private void DecideNextAction(Transform targetHero)
    {
        float sqrDistance = (targetHero.position - transform.position).sqrMagnitude;
        bool canReach = attackReachMin * attackReachMin < sqrDistance && sqrDistance < attackReachMax * attackReachMax;
        bool samePlane = Mathf.Abs(targetHero.position.z - transform.position.z) < 0.5f;

        weights.Clear();

        if (!detector.heroIsNearby)
        {
            weights.Add(new DecisionWeight(20, EnemyState.Waiting));
            weights.Add(new DecisionWeight(80, EnemyState.Chasing));
        }
        else
        {
            if (samePlane)
            {
                if (canReach)
                {
                    weights.Add(new DecisionWeight(70, EnemyState.Attacking));
                    weights.Add(new DecisionWeight(15, EnemyState.Waiting));
                    weights.Add(new DecisionWeight(15, EnemyState.Roaming));
                }
                else
                {
                    weights.Add(new DecisionWeight(80, EnemyState.Chasing));
                    weights.Add(new DecisionWeight(10, EnemyState.Waiting));
                    weights.Add(new DecisionWeight(10, EnemyState.Roaming));
                }
            }
            else
            {
                weights.Add(new DecisionWeight(60, EnemyState.Chasing));
                weights.Add(new DecisionWeight(20, EnemyState.Waiting));
                weights.Add(new DecisionWeight(20, EnemyState.Roaming));
            }
        }

        EnemyState nextState = GetWeightedRandomState(weights);
        SetState(nextState, targetHero);
    }

    private EnemyState GetWeightedRandomState(List<DecisionWeight> decisionWeights)
    {
        int totalWeight = 0;
        foreach (var weight in decisionWeights)
        {
            totalWeight += weight.weight;
        }

        int randomValue = Random.Range(0, totalWeight);
        foreach (var weight in decisionWeights)
        {
            if (randomValue < weight.weight)
            {
                return weight.state;
            }
            randomValue -= weight.weight;
        }
        return EnemyState.Idle;
    }

    private void SetState(EnemyState newState, Transform targetHero)
    {
        if (actionCoroutine != null)
        {
            StopCoroutine(actionCoroutine);
        }

        currentState = newState;

        switch (newState)
        {
            case EnemyState.Attacking:
                actionCoroutine = StartCoroutine(AttackRoutine(targetHero));
                break;
            case EnemyState.Chasing:
                actionCoroutine = StartCoroutine(ChaseRoutine(targetHero));
                break;
            case EnemyState.Roaming:
                actionCoroutine = StartCoroutine(RoamRoutine());
                break;
            case EnemyState.Waiting:
                actionCoroutine = StartCoroutine(WaitRoutine());
                break;
        }
    }

    private IEnumerator AttackRoutine(Transform targetHero)
    {
        enemy.FaceTarget(targetHero.position);
        enemy.Attack();

        float duration = Random.Range(1.0f, 1.5f);
        yield return new WaitForSeconds(duration);

        currentState = EnemyState.Idle;
    }

    private IEnumerator ChaseRoutine(Transform targetHero)
    {
        Vector3 direction = (targetHero.position - transform.position).normalized;
        direction.y = 0;
        Vector3 destination = targetHero.position - direction * personalSpace;
        destination.z += Random.Range(-0.4f, 0.4f);

        enemy.MoveTo(destination);

        float duration = Random.Range(0.2f, 0.4f);
        yield return new WaitForSeconds(duration);

        currentState = EnemyState.Idle;
    }

    private IEnumerator RoamRoutine()
    {
        float randomAngle = Random.Range(0, 360);
        Vector3 direction = new Vector3(Mathf.Sin(randomAngle), 0, Mathf.Cos(randomAngle));
        float distance = Random.Range(1, 3);
        Vector3 destination = transform.position + direction * distance;

        enemy.MoveTo(destination);

        float duration = Random.Range(0.3f, 0.6f);
        yield return new WaitForSeconds(duration);

        currentState = EnemyState.Idle;
    }

    private IEnumerator WaitRoutine()
    {
        enemy.Wait();

        float duration = Random.Range(0.2f, 0.5f);
        yield return new WaitForSeconds(duration);

        currentState = EnemyState.Idle;
    }
}
