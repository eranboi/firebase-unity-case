using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;
using Firebase.Functions;
using TMPro;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private GameObject scoresPanel;
    
    [SerializeField] private Button playBtn;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private TextMeshProUGUI scoreText;

    private FirebaseFunctions functions;
    private bool isPlaying;
    private long startTime;
    private long finishTime;
    private int score;
    
    private FirebaseFirestore db;
    
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        functions = FirebaseFunctions.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
        UpdateUIState();
    }

    private void OnEnable()
    {
        playBtn.onClick.AddListener(PlayButtonClicked);
    }

    private void OnDisable()
    {
        playBtn.onClick.RemoveListener(PlayButtonClicked);
    }

    public void UpdateUIState()
    {
        var isLoggedIn = AuthManager.Instance.IsUserLoggedIn();
        Debug.Log($"UpdateUIState called. Is logged in: {isLoggedIn}");

        loginPanel.SetActive(!isLoggedIn);
        gamePanel.SetActive(isLoggedIn);

        if (isLoggedIn)
        {
            scoreText.text = $"Score: {score}";
            Debug.Log("Game panel activated");
        }
        else
        {
            Debug.Log("Login panel activated");
        }
    }

    private void PlayButtonClicked()
    {
        playBtn.interactable = false;
        StartCoroutine(!isPlaying ? GetStartTime() : GetFinishTime());
        playBtn.GetComponentInChildren<TextMeshProUGUI>().text = isPlaying ? "Finish" : "Start";
    }

    private IEnumerator GetStartTime()
    {
        yield return StartCoroutine(GetServerTimestampCoroutine());
        isPlaying = true;
        playBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Finish";
    }

    private IEnumerator GetFinishTime()
    {
        yield return StartCoroutine(GetServerTimestampCoroutine());
        isPlaying = false;
        playBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Start";
    }

    private IEnumerator GetServerTimestampCoroutine()
    {
        var function = functions.GetHttpsCallable("getServerTimestamp");
        Task<HttpsCallableResult> task = null;
        try
        {
            task = function.CallAsync();

        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }

        yield return new WaitUntil(() => task.IsCompleted);
        
        playBtn.interactable = true;

        if (task.Exception != null)
        {
            Debug.LogError($"Function call failed: {task.Exception}");
            SetFeedbackText($"Failed to get server timestamp: {task.Exception.Message}");
        }
        else
        {
            if (task.Result.Data is Dictionary<object, object> result && result.TryGetValue("timestamp", out var timestampObj))
            {
                if (timestampObj is long timestamp)
                {
                    ProcessTimestamp(timestamp);
                }
                else if (timestampObj is double timestampDouble)
                {
                    ProcessTimestamp((long)timestampDouble);
                }
                else
                {
                    SetFeedbackText($"Unexpected timestamp type: {timestampObj.GetType()}");
                }
            }
            else
            {
                SetFeedbackText("Timestamp not found in server response");
            }
        }
    }

    private void ProcessTimestamp(long timestamp)
    {
        if (!isPlaying)
        {
            startTime = timestamp;
            SetFeedbackText($"Game started at: {DateTimeOffset.FromUnixTimeMilliseconds(startTime).LocalDateTime}");
        }
        else
        { 
            finishTime = timestamp;
            CalculateScore();
        }
    }

    private void CalculateScore()
    {
        var delta = (finishTime - startTime) / 1000;
        var points = (int)(delta * 10);
        score = points;
        scoreText.text = $"Score: {score}";
        
        SetFeedbackText($"Game finished at: {DateTimeOffset.FromUnixTimeMilliseconds(finishTime).LocalDateTime}\n" +
                        $"Time elapsed: {delta} seconds\n" +
                        $"Score: {points}");
        
        SaveScore();

    }
    
    private void SaveScore()
    {
        if (!AuthManager.Instance.IsUserLoggedIn())
        {
            Debug.LogError("User not logged in. Cannot save score.");
            SetFeedbackText(feedbackText.text + "\nCannot save score: User not logged in.");
            return;
        }

        var userId = AuthManager.Instance.GetCurrentUserId();
        var roundId = Guid.NewGuid().ToString();

        var scoreData = new Dictionary<string, object>
        {
            { "RoundId", roundId },
            { "UserId", userId },
            { "StartTime", startTime },
            { "FinishTime", finishTime },
            { "Score", score }
        };

        db.Collection("scores").Document(roundId).SetAsync(scoreData).ContinueWith(task => {
            if (task.IsCompleted)
            {
                Debug.Log("Score saved successfully!");
                SetFeedbackText(feedbackText.text + "\nScore saved to database.");
            }
            else if (task.IsFaulted)
            {
                Debug.LogError($"Error saving score: {task.Exception}");
                SetFeedbackText(feedbackText.text + "\nError saving score to database.");
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void SetFeedbackText(string message)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
        }
    }
}

    
    
