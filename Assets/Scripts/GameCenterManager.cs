// Assets/Scripts/FlipOrbit/GameCenterManager.cs
using UnityEngine;
using UnityEngine.SocialPlatforms;

/// <summary>
/// Game Center へのログイン／スコア送信／ランキング表示をまとめて管理。
/// シーンに 1 個だけ置いて、DontDestroyOnLoad で生き続けさせる想定。
/// </summary>
public class GameCenterManager : MonoBehaviour
{
    public static GameCenterManager Instance { get; private set; }

    [Header("Game Center Leaderboard ID")]
    [Tooltip("App Store Connect の Game Center → Leaderboards で設定した ID")]
    [SerializeField] private string leaderboardId = "flipo_highscore";

    private bool isAuthenticated;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Authenticate();
    }

    /// <summary>
    /// Game Center にログイン。iOS 実機でのみ実際に処理される。
    /// </summary>
    public void Authenticate()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (isAuthenticated) return;

        Social.localUser.Authenticate(success =>
        {
            isAuthenticated = success;
            Debug.Log(success
                ? "[GameCenter] Authentication succeeded."
                : "[GameCenter] Authentication failed.");
        });
#else
        // エディタでは常に成功扱い（テスト用）
        isAuthenticated = true;
#endif
    }

    /// <summary>
    /// スコアをリーダーボードに送信。
    /// </summary>
    public void ReportScore(long score)
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (!isAuthenticated)
        {
            Authenticate();
            if (!isAuthenticated)
            {
                Debug.LogWarning("[GameCenter] Not authenticated. Cannot report score.");
                return;
            }
        }

        Social.ReportScore(score, leaderboardId, success =>
        {
            Debug.Log(success
                ? $"[GameCenter] Score {score} reported."
                : "[GameCenter] Failed to report score.");
        });
#else
        Debug.Log($"[GameCenter] (Editor) Pretend to report score: {score}");
#endif
    }

    /// <summary>
    /// 標準のランキングUIを表示。
    /// </summary>
    public void ShowLeaderboard()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (!isAuthenticated)
        {
            Authenticate();
        }

        Social.ShowLeaderboardUI();
#else
        Debug.Log("[GameCenter] (Editor) ShowLeaderboard called.");
#endif
    }

    /// <summary>
    /// このプレイヤーの順位を取得してコールバックに返す。
    /// </summary>
    public void LoadLocalPlayerRank(System.Action<bool, long> onComplete)
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (!isAuthenticated)
        {
            Authenticate();
            if (!isAuthenticated)
            {
                onComplete?.Invoke(false, 0);
                return;
            }
        }

        ILeaderboard board = Social.CreateLeaderboard();
        board.id = leaderboardId;
        board.timeScope = TimeScope.AllTime;

        board.LoadScores(success =>
        {
            if (!success)
            {
                Debug.LogWarning("[GameCenter] Failed to load leaderboard scores.");
                onComplete?.Invoke(false, 0);
                return;
            }

            IScore local = board.localUserScore;
            if (local == null)
            {
                Debug.LogWarning("[GameCenter] Local user score not found.");
                onComplete?.Invoke(false, 0);
                return;
            }

            long rank = (long)local.rank;
            Debug.Log($"[GameCenter] Local player rank: {rank}");
            onComplete?.Invoke(true, rank);
        });
#else
        Debug.Log("[GameCenter] (Editor) LoadLocalPlayerRank dummy.");
        onComplete?.Invoke(false, 0);
#endif
    }
}
