using System;
using System.Collections.Generic;
using Hollowfen.Audio;
using Hollowfen.Data;
using Hollowfen.Foraging;
using Hollowfen.Items;
using Hollowfen.NPCs;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    /// <summary>
    /// Wren's readable coin ledger and the single explicit pouch-sale surface.
    /// Prices are quoted and committed by InventoryRuntime, so this view cannot drift
    /// from the same buyer policy used by authored Marra/Theo dialogue.
    /// </summary>
    public sealed class PurseScreen : UIScreen
    {
        public const string RuntimeScreenId = "purse";

        private const int VisibleLedgerRows = 6;
        private static readonly Color Ink = new Color(0.16f, 0.135f, 0.105f, 1f);
        private static readonly Color InkMuted = new Color(0.24f, 0.215f, 0.17f, 0.72f);
        private static readonly Color GreenInk = new Color(0.25f, 0.42f, 0.25f, 1f);
        private static readonly Color RedInk = new Color(0.55f, 0.25f, 0.20f, 1f);

        private static PurseScreen _instance;

        private CanvasGroup _group;
        private TMP_Text _silverValue;
        private TMP_Text _copperValue;
        private TMP_Text _totalValue;
        private TMP_Text _pouchCount;
        private TMP_Text _marraQuote;
        private TMP_Text _theoQuote;
        private TMP_Text _tradeStatus;
        private TMP_Text _sellLabel;
        private Button _sellButton;
        private Button _closeButton;

        private readonly List<LedgerRow> _ledgerRows = new List<LedgerRow>();
        private TMP_Text _emptyLedger;
        private bool _built;
        private bool _ownsGameplayPause;
        private float _previousTimeScale = 1f;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;
        private MushroomBuyer _nearbyBuyer;
        private string _nearbyBuyerName;
        private string _saleNotice;

        private sealed class LedgerRow
        {
            public GameObject Root;
            public TMP_Text Reason;
            public TMP_Text Amount;
            public TMP_Text Balance;
        }

        public bool IsOpen => UIManager.Instance != null && UIManager.Instance.TopScreen == this;

        public override GameObject DefaultSelected
        {
            get
            {
                if (_sellButton != null && _sellButton.gameObject.activeInHierarchy && _sellButton.interactable)
                    return _sellButton.gameObject;
                return _closeButton != null ? _closeButton.gameObject : base.DefaultSelected;
            }
        }

        /// <summary>Creates and registers the runtime screen the first time it is requested.</summary>
        public static PurseScreen Ensure()
        {
            if (_instance != null) return _instance;
            if (UIManager.Instance == null) return null;

            _instance = FindFirstObjectByType<PurseScreen>(FindObjectsInactive.Include);
            if (_instance != null)
            {
                // Domain reloads can preserve the inactive runtime object while rebuilding
                // UIManager's non-serialized registry. Heal that editor/runtime edge here.
                _instance.PrepareRegistration();
                UIManager.Instance.RegisterScreen(_instance);
                return _instance;
            }

            var host = new GameObject("_PurseScreen", typeof(RectTransform));
            host.SetActive(false);
            host.transform.SetParent(UIManager.Instance.transform, false);
            _instance = host.AddComponent<PurseScreen>();
            _instance.PrepareRegistration();
            UIManager.Instance.RegisterScreen(_instance);
            return _instance;
        }

        /// <summary>Gameplay shortcut/HUD entry. A second press closes the purse.</summary>
        public static void ToggleFromHud()
        {
            var manager = UIManager.Instance;
            if (manager == null) return;
            if (manager.TopScreen is PurseScreen)
            {
                manager.Back();
                return;
            }
            if (manager.HasOpenScreen || Time.timeScale <= 0f || PlayerInteractor.Suspended) return;
            if (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen) return;
            if (InspectScreen.Instance != null && InspectScreen.Instance.IsOpen) return;

            var screen = Ensure();
            if (screen == null) return;
            screen.CaptureNearbyBuyer();
            manager.OpenScreen(RuntimeScreenId);
        }

        /// <summary>Menu entry used by the pause journal. Returns to Pause when closed.</summary>
        public static void OpenFromMenu()
        {
            var manager = UIManager.Instance;
            if (manager == null || manager.TopScreen is PurseScreen) return;
            var screen = Ensure();
            if (screen == null) return;
            screen.ClearNearbyBuyer();
            manager.OpenScreen(RuntimeScreenId);
        }

        private void PrepareRegistration()
        {
            ConfigureRuntimeScreen(RuntimeScreenId, null, null, false);
        }

        protected override void Awake()
        {
            _instance = this;
            base.Awake();
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_built) return;
            try
            {
                BuildLayout();
                _built = true;
                ConfigureRuntimeScreen(RuntimeScreenId,
                    _closeButton != null ? _closeButton.gameObject : null, _group, false);
            }
            catch (Exception e)
            {
                Debug.LogError("[PurseScreen] Build failed: " + e);
            }
        }

        private void OnDestroy()
        {
            CoinPurse.OnChanged -= OnCoinsChanged;
            InventoryRuntime.OnChanged -= OnInventoryChanged;
            if (_instance == this) _instance = null;
        }

        public override void OnOpen()
        {
            base.OnOpen();
            _saleNotice = null;
            AcquireGameplayPauseIfNeeded();
            CoinPurse.OnChanged += OnCoinsChanged;
            InventoryRuntime.OnChanged += OnInventoryChanged;
            Refresh();
        }

        public override void OnClose()
        {
            CoinPurse.OnChanged -= OnCoinsChanged;
            InventoryRuntime.OnChanged -= OnInventoryChanged;
            ReleaseGameplayPause();
            ClearNearbyBuyer();
            base.OnClose();
        }

        private void AcquireGameplayPauseIfNeeded()
        {
            if (_ownsGameplayPause || Time.timeScale <= 0f) return;
            if (GameObject.FindGameObjectWithTag("Player") == null) return;

            _ownsGameplayPause = true;
            _previousTimeScale = Time.timeScale;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Time.timeScale = 0f;
            PlayerInteractor.Suspended = true;
            PlayerInteractor.SetPlayerInputEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ReleaseGameplayPause()
        {
            if (!_ownsGameplayPause) return;
            _ownsGameplayPause = false;
            Time.timeScale = _previousTimeScale;
            PlayerInteractor.Suspended = false;
            PlayerInteractor.SetPlayerInputEnabled(true);
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
        }

        private void CaptureNearbyBuyer()
        {
            ClearNearbyBuyer();
            var npc = PlayerInteractor.Current as NPCInteractable;
            string id = npc != null && npc.Data != null ? npc.Data.Id : null;
            if (string.Equals(id, "marra", StringComparison.OrdinalIgnoreCase))
            {
                _nearbyBuyer = MushroomBuyer.Marra;
                _nearbyBuyerName = Localization.Get("npc.marra.name");
            }
            else if (string.Equals(id, "theo", StringComparison.OrdinalIgnoreCase))
            {
                _nearbyBuyer = MushroomBuyer.Theo;
                _nearbyBuyerName = Localization.Get("npc.theo.name");
            }
        }

        private void ClearNearbyBuyer()
        {
            _nearbyBuyer = MushroomBuyer.None;
            _nearbyBuyerName = null;
        }

        private void OnCoinsChanged(int _) => Refresh();
        private void OnInventoryChanged(string _, int __) => Refresh();

        private void Refresh()
        {
            if (!_built) return;

            int total = CoinPurse.TotalCopper;
            _silverValue.text = CoinPurse.SilverPart.ToString();
            _copperValue.text = CoinPurse.CopperPart.ToString();
            _totalValue.text = string.Format(Localization.Get("purse.total"), total);

            var ledger = CoinPurse.RecentTransactions;
            _emptyLedger.gameObject.SetActive(ledger.Count == 0);
            for (int i = 0; i < _ledgerRows.Count; i++)
            {
                bool active = i < ledger.Count;
                var row = _ledgerRows[i];
                row.Root.SetActive(active);
                if (!active) continue;

                var entry = ledger[i];
                bool earned = entry.AmountCopper > 0;
                row.Reason.text = Localization.Get(entry.ReasonId);
                row.Amount.text = (earned ? "+" : "−") + CoinPurse.Format(Mathf.Abs(entry.AmountCopper));
                row.Amount.color = earned ? GreenInk : RedInk;
                row.Balance.text = string.Format(Localization.Get("purse.balance_after"),
                    CoinPurse.Format(entry.BalanceAfterCopper));
            }

            int carried = InventoryRuntime.TotalCount;
            _pouchCount.text = carried > 0
                ? string.Format(Localization.Get("purse.pouch.count"), carried)
                : Localization.Get("purse.pouch.empty");

            var marra = InventoryRuntime.QuoteFor(MushroomBuyer.Marra);
            var theo = InventoryRuntime.QuoteFor(MushroomBuyer.Theo);
            _marraQuote.text = FormatQuote(Localization.Get("npc.marra.name"), marra);
            _theoQuote.text = FormatQuote(Localization.Get("npc.theo.name"), theo);

            RefreshTradeAction();
        }

        private static string FormatQuote(string buyerName, InventoryRuntime.BasketSale quote)
        {
            return quote.SoldCount > 0
                ? string.Format(Localization.Get("purse.quote"), buyerName, quote.SoldCount,
                    CoinPurse.Format(quote.Copper))
                : string.Format(Localization.Get("purse.quote.none"), buyerName);
        }

        private void RefreshTradeAction()
        {
            bool nearBuyer = _nearbyBuyer != MushroomBuyer.None;
            var quote = InventoryRuntime.QuoteFor(_nearbyBuyer);
            bool canSell = nearBuyer && quote.SoldCount > 0 && quote.Copper > 0;

            _sellButton.gameObject.SetActive(canSell);
            _sellButton.interactable = canSell;
            if (canSell)
                _sellLabel.text = string.Format(Localization.Get("purse.sell.button"), quote.SoldCount,
                    _nearbyBuyerName, CoinPurse.Format(quote.Copper));

            if (!string.IsNullOrEmpty(_saleNotice))
                _tradeStatus.text = _saleNotice;
            else if (nearBuyer)
                _tradeStatus.text = canSell
                    ? string.Format(Localization.Get("purse.sell.nearby"), _nearbyBuyerName)
                    : Localization.Get("purse.sell.refused");
            else
                _tradeStatus.text = Localization.Get("purse.sell.away");
        }

        private void SellToNearbyBuyer()
        {
            if (_nearbyBuyer == MushroomBuyer.None) return;
            var sale = InventoryRuntime.SellTo(_nearbyBuyer);
            if (sale.SoldCount <= 0 || sale.Copper <= 0)
            {
                _saleNotice = Localization.Get("purse.sell.refused");
                Refresh();
                return;
            }

            string reason = _nearbyBuyer == MushroomBuyer.Marra
                ? "purse.transaction.marra_sale"
                : "purse.transaction.theo_sale";
            CoinPurse.Add(sale.Copper, reason);
            GameplaySfx.CoinsEarned();
            _saleNotice = string.Format(Localization.Get("purse.sell.complete"), sale.SoldCount,
                CoinPurse.Format(sale.Copper), sale.RefusedCount);
            Refresh();
            if (EventSystem.current != null && _closeButton != null)
                EventSystem.current.SetSelectedGameObject(_closeButton.gameObject);
        }

        private void BuildLayout()
        {
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.Init1080();
            if (gameObject.GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            var scrim = UICanvasUtil.NewImage("Scrim", transform, new Color(0.015f, 0.025f, 0.018f, 0.84f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            var panel = UICanvasUtil.NewRect("Ledger", transform);
            panel.sizeDelta = new Vector2(1460f, 870f);
            UICanvasUtil.AddShadow(panel, 30, 44, 0.58f, -14f);
            UICanvasUtil.MakeRoundedPanel(panel, HollowfenPalette.Parchment, 26, 0.42f);

            var inner = UICanvasUtil.NewImage("InnerRule", panel,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.18f), false);
            var innerImage = inner.GetComponent<Image>();
            innerImage.sprite = UICanvasUtil.RoundedOutline(20, 1.4f);
            innerImage.type = Image.Type.Sliced;
            var innerRt = (RectTransform)inner.transform;
            UICanvasUtil.Stretch(innerRt);
            innerRt.offsetMin = new Vector2(14f, 14f);
            innerRt.offsetMax = new Vector2(-14f, -14f);

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", panel, Localization.Get("purse.eyebrow"),
                14f, HollowfenPalette.Gold, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(680f, 20f), new Vector2(62f, -42f));

            var title = UICanvasUtil.NewHeading("Title", panel, Localization.Get("purse.title"), 60f,
                Ink, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(760f, 74f), new Vector2(60f, -66f));

            var rule = UICanvasUtil.NewImage("TitleRule", panel, HollowfenPalette.Gold, false);
            UICanvasUtil.SetRect((RectTransform)rule.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(110f, 2f), new Vector2(62f, -142f));

            _closeButton = BuildButton("Close", panel, Localization.Get("purse.close"),
                new Vector2(1260f, -58f), new Vector2(142f, 48f), false, OnBack, out _);

            BuildBalanceCard(panel);
            BuildLedgerCard(panel);
            BuildPouchCard(panel);

            var hint = UICanvasUtil.NewBody("Hint", panel, Localization.Get("purse.hint"), 15f,
                InkMuted, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(0f, 24f));
        }

        private void BuildBalanceCard(RectTransform panel)
        {
            var card = UICanvasUtil.NewRect("BalanceCard", panel);
            UICanvasUtil.SetRect(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(420f, 334f), new Vector2(62f, -178f));
            UICanvasUtil.MakeRoundedPanel(card, new Color(0.09f, 0.14f, 0.095f, 0.96f), 20, 0.22f);

            var label = UICanvasUtil.NewEyebrow("BalanceLabel", card,
                Localization.Get("purse.balance_heading"), 13f,
                HollowfenPalette.Gold, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-56f, 20f), new Vector2(0f, -34f));

            _silverValue = BuildDenomination(card, "SilverValue", "purse.silver", 112f);
            _copperValue = BuildDenomination(card, "CopperValue", "purse.copper", 308f);

            var divider = UICanvasUtil.NewImage("Divider", card, HollowfenPalette.DividerLine, false);
            UICanvasUtil.SetRect((RectTransform)divider.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(-56f, 1f), new Vector2(0f, 78f));

            _totalValue = UICanvasUtil.NewBody("Total", card, "", 18f, HollowfenPalette.Cream,
                FontStyles.Bold, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_totalValue.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(-48f, 26f), new Vector2(0f, 44f));

            var conversion = UICanvasUtil.NewBody("Conversion", card, Localization.Get("purse.conversion"), 14f,
                HollowfenPalette.Moss, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(conversion.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(-48f, 22f), new Vector2(0f, 18f));
        }

        private TMP_Text BuildDenomination(RectTransform card, string name, string labelId, float x)
        {
            var value = UICanvasUtil.NewHeading(name, card, "0", 74f, HollowfenPalette.Cream,
                FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(value.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0.5f, 1f), new Vector2(150f, 90f), new Vector2(x, -78f));
            var label = UICanvasUtil.NewEyebrow(name + "Label", card, Localization.Get(labelId), 13f,
                HollowfenPalette.Gold, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0.5f, 1f), new Vector2(150f, 20f), new Vector2(x, -180f));
            return value;
        }

        private void BuildLedgerCard(RectTransform panel)
        {
            var card = UICanvasUtil.NewRect("ActivityCard", panel);
            UICanvasUtil.SetRect(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(850f, 334f), new Vector2(518f, -178f));
            UICanvasUtil.MakeRoundedPanel(card, new Color(1f, 1f, 1f, 0.17f), 20, 0.20f);

            var heading = UICanvasUtil.NewEyebrow("ActivityHeading", card, Localization.Get("purse.activity"),
                13f, HollowfenPalette.Gold, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-52f, 20f), new Vector2(0f, -28f));

            _emptyLedger = UICanvasUtil.NewBody("EmptyLedger", card, Localization.Get("purse.activity.empty"),
                20f, InkMuted, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_emptyLedger.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(-80f, -90f), new Vector2(0f, -10f));

            for (int i = 0; i < VisibleLedgerRows; i++)
            {
                var row = UICanvasUtil.NewRect("LedgerRow_" + i, card);
                UICanvasUtil.SetRect(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-52f, 42f), new Vector2(0f, -62f - i * 43f));
                if (i > 0)
                {
                    var line = UICanvasUtil.NewImage("Rule", row, new Color(Ink.r, Ink.g, Ink.b, 0.09f), false);
                    UICanvasUtil.SetRect((RectTransform)line.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                        new Vector2(0.5f, 1f), new Vector2(0f, 1f), Vector2.zero);
                }

                var reason = UICanvasUtil.NewBody("Reason", row, "", 17f, Ink, FontStyles.Normal,
                    TextAlignmentOptions.Left);
                UICanvasUtil.SetRect(reason.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
                    new Vector2(-530f, 0f), new Vector2(10f, 0f));
                var balance = UICanvasUtil.NewBody("Balance", row, "", 13f, InkMuted, FontStyles.Italic,
                    TextAlignmentOptions.Right);
                UICanvasUtil.SetRect(balance.rectTransform, new Vector2(0.5f, 0f), Vector2.one,
                    new Vector2(1f, 0.5f), new Vector2(-230f, 0f), new Vector2(-118f, 0f));
                var amount = UICanvasUtil.NewBody("Amount", row, "", 18f, GreenInk, FontStyles.Bold,
                    TextAlignmentOptions.Right);
                UICanvasUtil.SetRect(amount.rectTransform, new Vector2(1f, 0f), Vector2.one,
                    new Vector2(1f, 0.5f), new Vector2(112f, 0f), new Vector2(-4f, 0f));

                _ledgerRows.Add(new LedgerRow { Root = row.gameObject, Reason = reason, Amount = amount, Balance = balance });
            }
        }

        private void BuildPouchCard(RectTransform panel)
        {
            var card = UICanvasUtil.NewRect("PouchCard", panel);
            UICanvasUtil.SetRect(card, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(1306f, 250f), new Vector2(62f, 54f));
            UICanvasUtil.MakeRoundedPanel(card, new Color(1f, 1f, 1f, 0.14f), 20, 0.20f);

            var heading = UICanvasUtil.NewEyebrow("PouchHeading", card, Localization.Get("purse.pouch"),
                13f, HollowfenPalette.Gold, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(300f, 20f), new Vector2(30f, -25f));
            _pouchCount = UICanvasUtil.NewHeading("PouchCount", card, "", 28f, Ink,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(_pouchCount.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(370f, 42f), new Vector2(30f, -55f));

            _marraQuote = UICanvasUtil.NewBody("MarraQuote", card, "", 16f, InkMuted,
                FontStyles.Normal, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(_marraQuote.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(500f, 28f), new Vector2(30f, -106f));
            _theoQuote = UICanvasUtil.NewBody("TheoQuote", card, "", 16f, InkMuted,
                FontStyles.Normal, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(_theoQuote.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(500f, 28f), new Vector2(30f, -142f));

            var divider = UICanvasUtil.NewImage("Divider", card, new Color(Ink.r, Ink.g, Ink.b, 0.12f), false);
            UICanvasUtil.SetRect((RectTransform)divider.transform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0.5f), new Vector2(1f, -52f), new Vector2(566f, 0f));

            _tradeStatus = UICanvasUtil.NewBody("TradeStatus", card, "", 17f, InkMuted,
                FontStyles.Italic, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(_tradeStatus.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(660f, 74f), new Vector2(602f, -34f));

            _sellButton = BuildButton("Sell", card, "Sell", new Vector2(602f, -132f),
                new Vector2(660f, 64f), true, SellToNearbyBuyer, out _sellLabel);
        }

        private static Button BuildButton(string name, RectTransform parent, string label, Vector2 anchored,
            Vector2 size, bool accent, UnityEngine.Events.UnityAction onClick, out TMP_Text labelText)
        {
            var rt = UICanvasUtil.NewRect(name + "Button", parent);
            UICanvasUtil.SetRect(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), size, anchored);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = UICanvasUtil.RoundedRect(14);
            image.type = Image.Type.Sliced;
            image.color = accent
                ? new Color(0.16f, 0.25f, 0.16f, 0.96f)
                : new Color(Ink.r, Ink.g, Ink.b, 0.08f);
            image.raycastTarget = true;

            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            labelText = UICanvasUtil.NewEyebrow("Label", rt, label, accent ? 15f : 13f,
                accent ? HollowfenPalette.Cream : Ink, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(labelText.rectTransform);

            var focus = rt.gameObject.AddComponent<FocusHighlight>();
            focus.Configure(image, rt,
                accent ? new Color(0.24f, 0.38f, 0.23f, 1f) : new Color(HollowfenPalette.Gold.r,
                    HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.24f), 1.015f);
            return button;
        }
    }
}
