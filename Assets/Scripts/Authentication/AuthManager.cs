using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Auth;
using TMPro;
using UnityEngine.UI;
using Google;
using System.Threading.Tasks;

public class AuthManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Button loginBtn;
    [SerializeField] private Button signinBtn;
    [SerializeField] private Button logoutBtn;
    [SerializeField] private Button googleSignInBtn;

    private FirebaseAuth auth;
    private GoogleSignInConfiguration configuration;

    public static AuthManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize Firebase Auth
            auth = FirebaseAuth.DefaultInstance;
            auth.StateChanged += AuthStateChanged;

            // removed.
            var webClientId = "web client id here"; ;

            // Initialize Google Sign-In configuration
            configuration = new GoogleSignInConfiguration
            {
                WebClientId = webClientId,
                RequestIdToken = true
            };
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        loginBtn.onClick.AddListener(Login);
        signinBtn.onClick.AddListener(Signup);
        logoutBtn.onClick.AddListener(Logout);
        googleSignInBtn.onClick.AddListener(SignInWithGoogle);
    }

    private void OnDisable()
    {
        loginBtn.onClick.RemoveListener(Login);
        signinBtn.onClick.RemoveListener(Signup);
        logoutBtn.onClick.RemoveListener(Logout);
        googleSignInBtn.onClick.RemoveListener(SignInWithGoogle);
    }

    private void AuthStateChanged(object sender, System.EventArgs eventArgs)
    {
        var isLoggedIn = IsUserLoggedIn();
        Debug.Log($"Auth state changed. Is logged in: {isLoggedIn}");
        GameManager.Instance.UpdateUIState();
    }

    private void Login()
    {
        auth.SignInWithEmailAndPasswordAsync(emailInput.text, passwordInput.text).ContinueWith(task =>
        {
            if (task.IsFaulted)
                Debug.LogError($"Login failed: {task.Exception}");
            else
                Debug.Log("Login successful.");
        });
    }

    private void Signup()
    {
        auth.CreateUserWithEmailAndPasswordAsync(emailInput.text, passwordInput.text).ContinueWith(task =>
        {
            if (task.IsFaulted)
                Debug.LogError($"Signup failed: {task.Exception}");
            else
                Debug.Log("Signup successful.");
        });
    }

    private void SignInWithGoogle()
    {
        GoogleSignIn.Configuration = configuration;
        GoogleSignIn.DefaultInstance.SignIn().ContinueWith(OnGoogleAuthFinished);
    }

    private void OnGoogleAuthFinished(Task<GoogleSignInUser> task)
    {
        if (task.IsFaulted)
        {
            Debug.LogError("Google Sign-In task is faulted.");
            foreach (var e in task.Exception.Flatten().InnerExceptions)
            {
                var googleSignInError = e as GoogleSignIn.SignInException;
                if (googleSignInError == null) continue;
                
                Debug.LogError($"Google Sign-In error code: {googleSignInError.Status}");
                Debug.LogError($"Google Sign-In error message: {googleSignInError.Message}");
            }
        }
        else if (task.IsCanceled)
        {
            Debug.Log("Google Sign-In was canceled.");
        }
        else
        {
            Debug.Log("Google Sign-In successful.");
            Debug.Log($"User Email: {task.Result.Email}");

            // Sign in with Firebase using the Google credentials
            var credential = GoogleAuthProvider.GetCredential(task.Result.IdToken, null);
            auth.SignInWithCredentialAsync(credential).ContinueWith(OnFirebaseAuthFinished);
        }
    }


    private void OnFirebaseAuthFinished(Task<FirebaseUser> task)
    {
        if (task.IsFaulted)
        {
            Debug.LogError("Firebase Auth task is faulted.");
            foreach (var e in task.Exception.Flatten().InnerExceptions)
            {
                Debug.LogError($"Firebase Auth error: {e.Message}");
            }
        }
        else if (task.IsCanceled)
        {
            Debug.Log("Firebase Auth was canceled.");
        }
        else
        {
            FirebaseUser user = task.Result;
            Debug.Log($"Firebase Auth successful. User: {user.DisplayName}, Email: {user.Email}");
        }
    }


    private void Logout()
    {
        auth.SignOut();
        GoogleSignIn.DefaultInstance.SignOut();
    }
    
    public bool IsUserLoggedIn()
    {
        return auth.CurrentUser != null;
    }

    public string GetCurrentUserId()
    {
        return auth.CurrentUser?.UserId;
    }
}
