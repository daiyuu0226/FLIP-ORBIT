// Assets/Scripts/FlipOrbit/AdsManager.cs
using GoogleMobileAds;
using GoogleMobileAds.Api;
using System;
using System.Drawing;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// AdMob の初期化・バナー・インタースティシャルを一括管理。
/// - 起動時にバナー表示
/// - リトライ偶数回でインタースティシャル表示
/// </summary>
public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    [Header("◆ iOS 用 Ad Unit ID（※開発中はテストIDのままでOK）")]
    [SerializeField] private string bannerAdUnitIdIos = "ca-app-pub-3940256099942544/2934735716";    // Banner (iOS test)
    [SerializeField] private string interstitialAdUnitIdIos = "ca-app-pub-3940256099942544/4411468910"; // Interstitial (iOS test)

#if UNITY_IOS
    private string BannerAdUnitId => bannerAdUnitIdIos;
    private string InterstitialAdUnitId => interstitialAdUnitIdIos;
#else
    // iOS 以外では広告は実質無効
    private string BannerAdUnitId => "unused";
    private string InterstitialAdUnitId => "unused";
#endif

    private BannerView bannerView;
    private InterstitialAd interstitialAd;

    // リトライ回数カウント（偶数回でインタースティシャル表示）
    private int retryCount;
    private bool isInitialized;

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
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        InitializeAds();
#else
        Debug.Log("[Ads] Editor or unsupported platform. Ads disabled.");
#endif
    }

    private void OnDestroy()
    {
        DestroyBanner();
        DestroyInterstitial();
    }

    // ------------------ 初期化 ------------------

    private void InitializeAds()
    {
        if (isInitialized) return;

        MobileAds.Initialize(initStatus =>
        {
            isInitialized = true;
            Debug.Log("[Ads] MobileAds initialized.");

            CreateBanner();        // 起動時からバナー表示
            RequestInterstitial(); // インタースティシャルを1本プリロード
        });
    }

    private AdRequest CreateAdRequest()
    {
        // 必要ならここでキーワードなど追加
        return new AdRequest();
    }

    // ------------------ バナー ------------------

    private void CreateBanner()
    {
        if (BannerAdUnitId == "unused" || string.IsNullOrEmpty(BannerAdUnitId))
        {
            Debug.LogWarning("[Ads] BannerAdUnitId is not set.");
            return;
        }

        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
        }

        // 画面下部に標準サイズで表示
        bannerView = new BannerView(BannerAdUnitId, AdSize.Banner, AdPosition.Bottom);

        bannerView.OnBannerAdLoaded += () =>
        {
            Debug.Log("[Ads] Banner loaded.");
        };

        bannerView.OnBannerAdLoadFailed += error =>
        {
            Debug.LogError($"[Ads] Banner failed to load: {error}");
        };

        bannerView.LoadAd(CreateAdRequest());
    }

    private void DestroyBanner()
    {
        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
        }
    }

    // ------------------ インタースティシャル ------------------

    private void RequestInterstitial()
    {
        if (InterstitialAdUnitId == "unused" || string.IsNullOrEmpty(InterstitialAdUnitId))
        {
            Debug.LogWarning("[Ads] InterstitialAdUnitId is not set.");
            return;
        }

        Debug.Log("[Ads] Request interstitial...");
        var request = CreateAdRequest();

        InterstitialAd.Load(InterstitialAdUnitId, request,
            (InterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogError($"[Ads] Interstitial failed to load: {error}");
                    interstitialAd = null;
                    return;
                }

                Debug.Log("[Ads] Interstitial loaded.");
                interstitialAd = ad;

                // 閉じたら次をプリロード
                interstitialAd.OnAdFullScreenContentClosed += () =>
                {
                    Debug.Log("[Ads] Interstitial closed. Preloading next.");
                    DestroyInterstitial();
                    RequestInterstitial();
                };

                interstitialAd.OnAdFullScreenContentFailed += adError =>
                {
                    Debug.LogError($"[Ads] Interstitial failed to open: {adError}");
                    DestroyInterstitial();
                };
            });
    }

    private void DestroyInterstitial()
    {
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }
    }

    // ------------------ 公開API ------------------

    /// <summary>
    /// リトライ発生時に呼ぶ。内部でリトライカウントして偶数回だけ広告表示。
    /// </summary>
    public void RegisterRetryAndShowInterstitialIfNeeded()
    {
        retryCount++;
        Debug.Log($"[Ads] Retry count = {retryCount}");

        if (retryCount % 2 == 0)
        {
            ShowInterstitial();
        }
    }

    public void ShowInterstitial()
    {
        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            interstitialAd.Show();
        }
        else
        {
            Debug.Log("[Ads] Interstitial not ready.");
        }
    }
}
