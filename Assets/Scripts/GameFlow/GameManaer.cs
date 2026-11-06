using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public float scenarioDuration = 180f;
    private float remainingTime;
    public bool scenarioRunning = false;
    public bool scenarioCompleted = false;
    public bool scenarioPaused = false;

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
        ResetScenario();
    }

    private void Update()
    {
        if (!scenarioRunning || scenarioPaused)
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

    public void ResetScenario()
    {
        remainingTime = scenarioDuration;
        scenarioRunning = false;
        scenarioPaused = false;
        scenarioCompleted = false;
    }

    public void StartScenario()
    {
        if (scenarioCompleted)
        {
            ResetScenario();
        }

        remainingTime = scenarioDuration;
        scenarioPaused = false;
        scenarioRunning = true;
        Debug.Log("Golden Hour scenario started.");
    }

    public void PauseScenario()
    {
        if (scenarioRunning)
        {
            scenarioPaused = true;
        }
    }

    public void ResumeScenario()
    {
        if (scenarioRunning)
        {
            scenarioPaused = false;
        }
    }

    public void EndScenario()
    {
        if (scenarioCompleted)
        {
            return;
        }

        scenarioRunning = false;
        scenarioPaused = false;
        scenarioCompleted = true;
        Debug.Log("Golden Hour scenario ended.");
    }

    public float GetRemainingTime()
    {
        return remainingTime;
    }

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