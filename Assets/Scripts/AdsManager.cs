// Assets/Scripts/FlipOrbit/AdsManager.cs
using GoogleMobileAds;
using GoogleMobileAds.Api;
using System;
using System.Collections;
using UnityEngine;

#if UNITY_IOS
using Unity.Advertisement.IosSupport;
#endif

/// <summary>
/// AdMob の初期化・バナー・インタースティシャルを一括管理。
/// - 起動時にATT許可リクエスト後、バナー表示
/// - リトライ偶数回でインタースティシャル表示
/// </summary>
public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    [Header("◆ iOS 用 Ad Unit ID")]
    [SerializeField] private string bannerAdUnitIdIos = "ca-app-pub-XXXXXXXXXXXXXXXX/XXXXXXXXXX";
    [SerializeField] private string interstitialAdUnitIdIos = "ca-app-pub-XXXXXXXXXXXXXXXX/XXXXXXXXXX";

#if UNITY_IOS
    private string BannerAdUnitId => bannerAdUnitIdIos;
    private string InterstitialAdUnitId => interstitialAdUnitIdIos;
#else
    private string BannerAdUnitId => "unused";
    private string InterstitialAdUnitId => "unused";
#endif

    private BannerView bannerView;
    private InterstitialAd interstitialAd;

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
#if UNITY_IOS && !UNITY_EDITOR
        StartCoroutine(RequestATTAndInitialize());
#elif UNITY_ANDROID && !UNITY_EDITOR
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

    // ------------------ ATT (iOS 14.5+) ------------------

#if UNITY_IOS
    private IEnumerator RequestATTAndInitialize()
    {
        var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
        Debug.Log($"[Ads] Current ATT status: {status}");

        if (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
        {
            Debug.Log("[Ads] Requesting ATT permission...");
            ATTrackingStatusBinding.RequestAuthorizationTracking();

            // ユーザーが選択するまで待つ（最大10秒）
            float timeout = 10f;
            float elapsed = 0f;
            while (ATTrackingStatusBinding.GetAuthorizationTrackingStatus()
                   == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED
                   && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
        }

        var finalStatus = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
        Debug.Log($"[Ads] Final ATT status: {finalStatus}");

        // ATTの結果に関わらず広告は初期化する（許可なしでも非パーソナライズ広告は出る）
        InitializeAds();
    }
#endif

    // ------------------ 初期化 ------------------

    private void InitializeAds()
    {
        if (isInitialized) return;

        Debug.Log("[Ads] Starting MobileAds.Initialize...");
        Debug.Log($"[Ads] BannerAdUnitId = {BannerAdUnitId}");
        Debug.Log($"[Ads] InterstitialAdUnitId = {InterstitialAdUnitId}");

        MobileAds.Initialize(initStatus =>
        {
            isInitialized = true;
            Debug.Log("[Ads] MobileAds initialized.");

            // アダプター状態をログ出力（デバッグ用）
            foreach (var pair in initStatus.getAdapterStatusMap())
            {
                Debug.Log($"[Ads] Adapter: {pair.Key}, State: {pair.Value.InitializationState}, Desc: {pair.Value.Description}");
            }

            CreateBanner();
            RequestInterstitial();
        });
    }

    private AdRequest CreateAdRequest()
    {
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

        Debug.Log("[Ads] Creating banner...");

        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
        }

        bannerView = new BannerView(BannerAdUnitId, AdSize.Banner, AdPosition.Bottom);

        bannerView.OnBannerAdLoaded += () =>
        {
            Debug.Log("[Ads] Banner loaded successfully!");
        };

        bannerView.OnBannerAdLoadFailed += error =>
        {
            Debug.LogError($"[Ads] Banner FAILED to load: Code={error.GetCode()}, Message={error.GetMessage()}");
        };

        bannerView.LoadAd(CreateAdRequest());
        Debug.Log("[Ads] Banner LoadAd called.");
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

        Debug.Log("[Ads] Requesting interstitial...");
        var request = CreateAdRequest();

        InterstitialAd.Load(InterstitialAdUnitId, request,
            (InterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogError($"[Ads] Interstitial FAILED to load: {error?.GetMessage()}");
                    interstitialAd = null;
                    return;
                }

                Debug.Log("[Ads] Interstitial loaded successfully!");
                interstitialAd = ad;

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
            Debug.Log("[Ads] Showing interstitial...");
            interstitialAd.Show();
        }
        else
        {
            Debug.Log("[Ads] Interstitial not ready.");
        }
    }
}