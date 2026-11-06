using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Scenario Settings")]
    public float scenarioDuration = 180f;

    private float remainingTime;

    public bool scenarioRunning = false;

    public bool scenarioCompleted = false;


    // ==========================================
    // INITIALIZATION
    // ==========================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }


    private void Start()
    {
        StartScenario();
    }


    // ==========================================
    // UPDATE TIMER
    // ==========================================

    private void Update()
    {
        if (!scenarioRunning)
        {
            return;
        }

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;

            EndScenario();
        }
    }


    // ==========================================
    // START SCENARIO
    // ==========================================

    public void StartScenario()
    {
        remainingTime = scenarioDuration;

        scenarioRunning = true;
        scenarioCompleted = false;

        Debug.Log(
            "=== GOLDEN HOUR SCENARIO STARTED ==="
        );

        Debug.Log(
            "Time available: " +
            scenarioDuration +
            " seconds"
        );
    }


    // ==========================================
    // END SCENARIO
    // ==========================================

    public void EndScenario()
    {
        if (!scenarioRunning)
        {
            return;
        }

        scenarioRunning = false;
        scenarioCompleted = true;

        Debug.Log(
            "=== GOLDEN HOUR SCENARIO ENDED ==="
        );
    }


    // ==========================================
    // GET REMAINING TIME
    // ==========================================

    public float GetRemainingTime()
    {
        return remainingTime;
    }


    // ==========================================
    // GET FORMATTED TIME
    // ==========================================

    public string GetFormattedTime()
    {
        int minutes =
            Mathf.FloorToInt(remainingTime / 60f);

        int seconds =
            Mathf.FloorToInt(remainingTime % 60f);

        return string.Format(
            "{0:00}:{1:00}",
            minutes,
            seconds
        );
    }
}