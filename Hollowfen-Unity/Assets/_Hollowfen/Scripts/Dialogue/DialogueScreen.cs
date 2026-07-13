using System.Collections;
using System.Collections.Generic;
using Hollowfen.Foraging;
using Hollowfen.Quests;
using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hollowfen.Dialogue
{
    // Lazy-built parchment panel for NPC conversations. Pattern matches InspectScreen / MapScreen:
    // not a UIScreen subclass (UIManager is DDOL from MainMenu), screen-local Canvas, builds UI
    // programmatically on first Open, sets Time.timeScale=0 + suspends PlayerInteractor while open.
    //
    // Cinematic camera: DialogueCinematics (batch-45) directs Camera.main while a dialogue plays —
    // establishing two-shot, over-shoulder glides between speakers, per-line push-ins, isCloseup
    // singles — driven from Open(dialog, anchor) / ShowCurrentLine / BeginChoices / Close below.
    // This screen shows the line text with typewriter + speaker accent color and fires the
    // dialog's outcome (unlock card / complete quest / chain to next dialog) on end.
    public class DialogueScreen : MonoBehaviour
    {
        public static DialogueScreen Instance { get; private set; }

        [SerializeField] private Sprite _parchmentSprite;
        [SerializeField, Tooltip("Characters per second for typewriter. 32 matches the web prototype.")]
        private float _typewriterCps = 32f;
        [SerializeField] private float _panelHeight = 320f;
        [SerializeField] private float _panelMargin = 60f;
        [SerializeField, Tooltip("Mixer group for line voice-over (SFX for the batch-29 test; a dedicated Voice group is a follow-up). Null = unrouted.")]
        private UnityEngine.Audio.AudioMixerGroup _voiceOutput;

        private AudioSource _voiceSource;   // lazily created; AudioSource ignores timeScale, so VO plays through the dialogue freeze

        private static readonly Dictionary<string, Color> SpeakerColors = new Dictionary<string, Color>
        {
            { "Bram",  ParseColor("#7a4a1a") },
            { "Wren",  ParseColor("#5b3a6a") },
            { "Marra", ParseColor("#8a3a2a") },
            { "Almy",  ParseColor("#4a6b3a") },
            { "Joren", ParseColor("#4d4338") },
            { "Voss",  ParseColor("#3e4e63") },
            { "Theo",  ParseColor("#1f6f62") },
            { "Edda",  ParseColor("#77704f") },
            { "Hollin", ParseColor("#5a4a72") },
            { "Pell",   ParseColor("#615a48") },
            { "Calden", ParseColor("#3d3550") },
            { "Aldric", ParseColor("#6b3a3a") },
            { "Aldric's letter", ParseColor("#6b3a3a") },
        };
        private static readonly Color DefaultSpeakerColor = ParseColor("#3a2810");

        private bool _isOpen;
        private bool _built;
        private float _previousTimeScale = 1f;
        private DialogueData _currentDialog;
        private int _currentLineIndex;
        private Coroutine _typewriterCo;
        private bool _lineFullyShown;

        // Choice state — active after the last line of a dialog that has _choices.
        private bool _choosing;
        private int _choiceIndex;
        private DialogueChoice[] _activeChoices;
        private bool _stickLatched;

        private GameObject _root;
        private TMP_Text _speakerLabel;
        private TMP_Text _bodyText;
        private TMP_Text _hintText;

        public bool IsOpen => _isOpen;
        public bool IsChoosing => _choosing;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _root = gameObject;
            SetActiveSilent(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open(DialogueData dialog) => Open(dialog, null);

        // Anchored variant (batch-45): `anchor` is the NPC/prop being spoken with — it gives the
        // DialogueCinematics director its second framing subject. Null = no camera direction.
        public void Open(DialogueData dialog, Transform anchor)
        {
            if (dialog == null || dialog.Lines == null || dialog.Lines.Length == 0) return;
            BuildIfNeeded();
            _currentDialog = dialog;
            _currentLineIndex = 0;
            _isOpen = true;
            SetActiveSilent(true);
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            PlayerInteractor.Suspended = true;
            // Kill the player controller's input too — Space advances dialogue AND jumps, and with
            // the batch-45 unscaled animators the jump visibly fires mid-conversation (batch-46 fix).
            PlayerInteractor.SetPlayerInputEnabled(false);
            SetHudVisible(false);
            CursorVisible(true);
            if (anchor != null) DialogueCinematics.Ensure().Begin(anchor);
            ShowCurrentLine();
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            if (_typewriterCo != null) { StopCoroutine(_typewriterCo); _typewriterCo = null; }
            if (_voiceSource != null) _voiceSource.Stop();
            if (DialogueCinematics.Instance != null) DialogueCinematics.Instance.End();
            EndChoices();
            SetActiveSilent(false);
            Time.timeScale = _previousTimeScale;
            PlayerInteractor.Suspended = false;
            PlayerInteractor.SetPlayerInputEnabled(true);
            SetHudVisible(true);
            CursorVisible(false);
            _currentDialog = null;
        }

        private void Update()
        {
            if (!_isOpen) return;
            if (_choosing) { ReadChoiceInput(); return; }
            if (ReadAdvancePressed())
            {
                if (!_lineFullyShown) SkipTypewriter();
                else AdvanceLine();
            }
        }

        private bool ReadAdvancePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
                return true;
            var pad = Gamepad.current;
            if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame))
                return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            return false;
        }

        private void ShowCurrentLine()
        {
            if (_currentDialog == null || _currentLineIndex >= _currentDialog.Lines.Length) { FinishDialog(); return; }
            var line = _currentDialog.Lines[_currentLineIndex];
            _speakerLabel.text = (line.speaker ?? "").ToUpperInvariant();
            // Plate takes the speaker's ink as a tint; label stays warm cream for contrast.
            var speakerInk = SpeakerColors.TryGetValue(line.speaker ?? "", out var c) ? c : DefaultSpeakerColor;
            if (_namePlateFill != null)
                _namePlateFill.color = Color.Lerp(new Color(0.13f, 0.10f, 0.06f, 0.97f), speakerInk, 0.45f);
            if (_namePlateRT != null)
            {
                float w = Mathf.Max(120f, _speakerLabel.GetPreferredValues(_speakerLabel.text).x + 56f);
                _namePlateRT.sizeDelta = new Vector2(w, 40f);
            }
            _bodyText.text = "";
            _lineFullyShown = false;
            if (_typewriterCo != null) StopCoroutine(_typewriterCo);
            _typewriterCo = StartCoroutine(Typewriter(line.text ?? ""));
            PlayVoice(line.voiceClip);

            // Direct the camera at this line's speaker (batch-45). Push-in paced by the VO
            // read when voiced, else by a typewriter-speed estimate of the text.
            if (DialogueCinematics.Instance != null)
            {
                float est = line.voiceClip != null
                    ? line.voiceClip.length + 0.8f
                    : (line.text != null ? line.text.Length : 0) / Mathf.Max(1f, _typewriterCps) + 1.6f;
                DialogueCinematics.Instance.OnLine(line.speaker, line.isCloseup, est);
            }
        }

        // Voice-over for the current line. Always stops the previous line's clip first, so
        // advancing early cuts the read cleanly; a null clip just silences (pre-VO dialogues).
        private void PlayVoice(AudioClip clip)
        {
            if (_voiceSource == null)
            {
                if (clip == null) return;
                _voiceSource = gameObject.AddComponent<AudioSource>();
                _voiceSource.playOnAwake = false;
                _voiceSource.spatialBlend = 0f;   // UI-space, not positional
                _voiceSource.priority = 0;        // speech: never virtualized (batch-30 catch — ~50 ambient sources contend)
                _voiceSource.outputAudioMixerGroup = _voiceOutput;
            }
            _voiceSource.Stop();
            if (clip == null) return;
            _voiceSource.clip = clip;
            _voiceSource.Play();
        }

        private IEnumerator Typewriter(string text)
        {
            if (string.IsNullOrEmpty(text)) { _bodyText.text = ""; _lineFullyShown = true; yield break; }
            float secondsPerChar = _typewriterCps > 0 ? 1f / _typewriterCps : 0f;
            for (int i = 1; i <= text.Length; i++)
            {
                _bodyText.text = text.Substring(0, i);
                yield return new WaitForSecondsRealtime(secondsPerChar);
            }
            _lineFullyShown = true;
        }

        private void SkipTypewriter()
        {
            if (_typewriterCo != null) { StopCoroutine(_typewriterCo); _typewriterCo = null; }
            if (_currentLineIndex < _currentDialog.Lines.Length)
                _bodyText.text = _currentDialog.Lines[_currentLineIndex].text ?? "";
            _lineFullyShown = true;
        }

        private void AdvanceLine()
        {
            _currentLineIndex++;
            if (_currentLineIndex >= _currentDialog.Lines.Length) FinishDialog();
            else ShowCurrentLine();
        }

        private void FinishDialog()
        {
            var done = _currentDialog;
            if (done == null) { Close(); return; }

            // Fire outcomes. Order: items first, then card unlock, then quest complete, then chain.
            if (!string.IsNullOrEmpty(done.GiveItemId))
                Items.KeyItems.Grant(done.GiveItemId);

            if (done.GrantForage != null && done.GrantForageCount > 0)
                InventoryRuntime.Add(done.GrantForage, done.GrantForageCount);

            // Consume BEFORE any basket sale so the tonic ingredient isn't also sold.
            if (done.ConsumeForage != null && done.ConsumeForageCount > 0)
                InventoryRuntime.Remove(done.ConsumeForage, done.ConsumeForageCount);

            // Basket sale pays per item (repeatable Marra loop) on top of any fixed grant,
            // and the count must be read BEFORE the basket empties.
            int coinsIn = done.GrantCoinsCopper;
            if (done.SellsForageBasket)
            {
                coinsIn += InventoryRuntime.TotalCount * done.BasketCopperPerItem;
                InventoryRuntime.RemoveAll();
            }

            if (done.SpendsCoinsCopper > 0)
                Items.CoinPurse.TrySpend(done.SpendsCoinsCopper);

            if (coinsIn > 0)
                Items.CoinPurse.Add(coinsIn);

            if (done.SetFlagIds != null)
                foreach (var flag in done.SetFlagIds)
                    GameScores.SetFlag(flag);

            // Score deltas (story.md relationship tables)
            if (done.VillageHopeDelta != 0) GameScores.AddVillageHope(done.VillageHopeDelta);
            if (done.KnowledgeDelta != 0) GameScores.AddKnowledge(done.KnowledgeDelta);
            if (done.RelationshipNpcIds != null && done.RelationshipDeltas != null)
            {
                int n = Mathf.Min(done.RelationshipNpcIds.Length, done.RelationshipDeltas.Length);
                for (int i = 0; i < n; i++)
                    GameScores.AddRelationship(done.RelationshipNpcIds[i], done.RelationshipDeltas[i]);
            }

            if (done.UnlockStoryCard != null)
                QuestManager.UnlockStoryCard(done.UnlockStoryCard.Id);

            if (done.CompleteQuest != null)
                QuestManager.CompleteQuest(done.CompleteQuest.Id);

            // Choices take precedence over the linear chain: outcomes above have already
            // fired ("this conversation happened"); each branch then owns its own outcomes.
            if (done.Choices != null && done.Choices.Length > 0)
            {
                BeginChoices(done.Choices);
                return;
            }

            if (done.NextDialog != null)
            {
                // Chain: hold the screen open, swap dialog seamlessly.
                _currentDialog = done.NextDialog;
                _currentLineIndex = 0;
                ShowCurrentLine();
            }
            else
            {
                Close();
            }
        }

        // ----------------- CHOICES -----------------

        private void BeginChoices(DialogueChoice[] choices)
        {
            _choosing = true;
            _activeChoices = choices;
            _choiceIndex = 0;
            // Camera settles to the two-shot while the player weighs the choice (batch-45).
            if (DialogueCinematics.Instance != null) DialogueCinematics.Instance.OnChoices();
            _stickLatched = true; // require the stick to re-center before it moves the cursor
            BuildChoicePillsIfNeeded();
            int shown = Mathf.Min(choices.Length, _choicePills.Count);
            for (int i = 0; i < _choicePills.Count; i++)
            {
                bool active = i < shown;
                _choicePills[i].Root.SetActive(active);
                if (active)
                {
                    _choicePills[i].Label.text = (i + 1) + "   " + (choices[i].text ?? "");
                    // Choice 1 reads on TOP: stack downward from the highest slot.
                    ((RectTransform)_choicePills[i].Root.transform).anchoredPosition =
                        new Vector2(0f, (shown - 1 - i) * 54f);
                }
            }
            _choiceRoot.SetActive(true);
            if (_hintText != null) _hintText.text = "1–4 / stick  ·  choose      Space  ·  confirm";
            RefreshChoiceHighlight();
        }

        private void EndChoices()
        {
            _choosing = false;
            _activeChoices = null;
            if (_choiceRoot != null) _choiceRoot.SetActive(false);
            if (_hintText != null) _hintText.text = "Space  ·  continue";
        }

        // Public on purpose: mouse Buttons, future controllers, and the verification
        // harness all drive selection through the same door.
        public void SelectChoice(int index)
        {
            if (!_choosing || _activeChoices == null || index < 0 || index >= _activeChoices.Length) return;
            var choice = _activeChoices[index];
            EndChoices();

            if (!string.IsNullOrEmpty(choice.setsFlagId))
                GameScores.SetFlag(choice.setsFlagId);

            if (choice.next != null)
            {
                _currentDialog = choice.next;
                _currentLineIndex = 0;
                ShowCurrentLine();
            }
            else
            {
                Close();
            }
        }

        private void ReadChoiceInput()
        {
            int count = _activeChoices.Length;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame && count > 0) { SelectChoice(0); return; }
                if (kb.digit2Key.wasPressedThisFrame && count > 1) { SelectChoice(1); return; }
                if (kb.digit3Key.wasPressedThisFrame && count > 2) { SelectChoice(2); return; }
                if (kb.digit4Key.wasPressedThisFrame && count > 3) { SelectChoice(3); return; }
            }

            int move = 0;
            if (kb != null)
            {
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) move = 1;
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) move = -1;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.down.wasPressedThisFrame) move = 1;
                if (pad.dpad.up.wasPressedThisFrame) move = -1;
                float y = pad.leftStick.ReadValue().y;
                if (Mathf.Abs(y) < 0.3f) _stickLatched = false;
                else if (!_stickLatched && Mathf.Abs(y) > 0.6f) { move = y < 0 ? 1 : -1; _stickLatched = true; }
            }
            if (move != 0)
            {
                _choiceIndex = (_choiceIndex + move + count) % count;
                RefreshChoiceHighlight();
                return;
            }

            bool confirm = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (confirm) SelectChoice(_choiceIndex);
        }

        private void RefreshChoiceHighlight()
        {
            for (int i = 0; i < _choicePills.Count; i++)
            {
                if (!_choicePills[i].Root.activeSelf) continue;
                bool selected = i == _choiceIndex;
                _choicePills[i].Fill.color = selected
                    ? Color.Lerp(new Color(0.13f, 0.10f, 0.06f, 0.97f), HollowfenPalette.Gold, 0.42f)
                    : new Color(0.13f, 0.10f, 0.06f, 0.94f);
                _choicePills[i].Stroke.color = selected
                    ? HollowfenPalette.Gold
                    : new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.35f);
                _choicePills[i].Label.color = selected ? HollowfenPalette.GoldGlow : HollowfenPalette.Cream;
            }
        }

        // Cinematic frame: the gameplay HUD steps aside while a conversation plays.
        private static void SetHudVisible(bool visible)
        {
            string[] hudRoots = { "_HUDCanvas", "_MiniMapCanvas" };
            foreach (var name in hudRoots)
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                var cg = go.GetComponent<CanvasGroup>();
                if (cg == null) cg = go.AddComponent<CanvasGroup>();
                cg.alpha = visible ? 1f : 0f;
            }
        }

        private static void CursorVisible(bool visible)
        {
            Cursor.visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        }

        private void SetActiveSilent(bool active)
        {
            if (_root != null && _root.activeSelf != active) _root.SetActive(active);
        }

        private static Color ParseColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        // ----------------- UI BUILDER -----------------

        private Image _namePlateFill;
        private RectTransform _namePlateRT;

        private struct ChoicePill
        {
            public GameObject Root;
            public Image Fill;
            public Image Stroke;
            public TMP_Text Label;
        }

        private GameObject _choiceRoot;
        private readonly List<ChoicePill> _choicePills = new List<ChoicePill>();

        // Four reusable choice pills stacked above the panel's right edge, in the
        // journal register: ink pill, gold hairline, cream text; selection glows gold.
        private void BuildChoicePillsIfNeeded()
        {
            if (_choiceRoot != null) return;
            var canvasRT = (RectTransform)transform;

            _choiceRoot = new GameObject("Choices", typeof(RectTransform));
            _choiceRoot.transform.SetParent(canvasRT, false);
            var cRT = (RectTransform)_choiceRoot.transform;
            cRT.anchorMin = new Vector2(0.5f, 0f);
            cRT.anchorMax = new Vector2(0.5f, 0f);
            cRT.pivot = new Vector2(1f, 0f);
            // Panel: 1240 wide, centred, bottom y=134, height 230 → stack starts just above it.
            cRT.anchoredPosition = new Vector2(620f, 134f + 230f + 14f);
            cRT.sizeDelta = new Vector2(620f, 4 * 54f);

            for (int i = 0; i < 4; i++)
            {
                var pillGo = new GameObject("Choice" + (i + 1), typeof(RectTransform));
                pillGo.transform.SetParent(cRT, false);
                var pRT = (RectTransform)pillGo.transform;
                pRT.anchorMin = new Vector2(1f, 0f);
                pRT.anchorMax = new Vector2(1f, 0f);
                pRT.pivot = new Vector2(1f, 0f);
                pRT.sizeDelta = new Vector2(600f, 46f);
                pRT.anchoredPosition = new Vector2(0f, i * 54f);

                var fill = pillGo.AddComponent<Image>();
                fill.sprite = UICanvasUtil.RoundedRect(12);
                fill.type = Image.Type.Sliced;
                fill.color = new Color(0.13f, 0.10f, 0.06f, 0.94f);

                var strokeGo = UICanvasUtil.NewImage("Hairline", pRT, HollowfenPalette.Gold, false);
                var stroke = strokeGo.GetComponent<Image>();
                stroke.sprite = UICanvasUtil.RoundedOutline(12, 1.6f);
                stroke.type = Image.Type.Sliced;
                UICanvasUtil.Stretch((RectTransform)strokeGo.transform);

                var label = UICanvasUtil.NewBody("Label", pRT, "", 20f, HollowfenPalette.Cream,
                    FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
                var lRT = label.rectTransform;
                lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
                lRT.offsetMin = new Vector2(24f, 0f); lRT.offsetMax = new Vector2(-18f, 0f);

                int captured = i;
                var btn = pillGo.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => SelectChoice(captured));

                _choicePills.Add(new ChoicePill { Root = pillGo, Fill = fill, Stroke = stroke, Label = label });
                pillGo.SetActive(false);
            }
            _choiceRoot.SetActive(false);
        }

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            var canvasRT = (RectTransform)transform;

            // Tear down any leftover children
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);

            // Cinematic letterbox bars — solid enough to read as a deliberate frame.
            var top = UICanvasUtil.NewImage("Letterbox.Top", canvasRT, new Color(0.02f, 0.02f, 0.015f, 0.85f), true);
            var tRT = (RectTransform)top.transform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.pivot = new Vector2(0.5f, 1f);
            tRT.sizeDelta = new Vector2(0f, 110f);
            tRT.anchoredPosition = Vector2.zero;

            var bot = UICanvasUtil.NewImage("Letterbox.Bot", canvasRT, new Color(0.02f, 0.02f, 0.015f, 0.85f), false);
            var bRT = (RectTransform)bot.transform;
            bRT.anchorMin = new Vector2(0f, 0f); bRT.anchorMax = new Vector2(1f, 0f);
            bRT.pivot = new Vector2(0.5f, 0f);
            bRT.sizeDelta = new Vector2(0f, 110f);
            bRT.anchoredPosition = Vector2.zero;

            // Rounded parchment panel above the bottom bar, soft shadow, hairline stroke.
            var panel = new GameObject("DialoguePanel", typeof(RectTransform));
            panel.transform.SetParent(canvasRT, false);
            var pRT = (RectTransform)panel.transform;
            pRT.anchorMin = new Vector2(0.5f, 0f); pRT.anchorMax = new Vector2(0.5f, 0f);
            pRT.pivot = new Vector2(0.5f, 0f);
            pRT.sizeDelta = new Vector2(1240f, 230f);
            pRT.anchoredPosition = new Vector2(0f, 134f);

            UICanvasUtil.AddShadow(pRT, 20, 30, 0.4f, -8f);
            var panelImg = UICanvasUtil.MakeRoundedPanel(pRT, HollowfenPalette.Parchment, 18, 0.36f);
            if (_parchmentSprite != null)
            {
                // Parchment texture wash inside the rounded shape.
                var wash = UICanvasUtil.NewImage("ParchmentWash", pRT, new Color(1f, 1f, 1f, 0.5f), false);
                var washImg = wash.GetComponent<Image>();
                washImg.sprite = _parchmentSprite;
                washImg.type = Image.Type.Simple;
                UICanvasUtil.Stretch((RectTransform)wash.transform);
                wash.transform.SetSiblingIndex(0);
            }

            // Speaker name plate — small ink tab riding the panel's top-left edge.
            _namePlateRT = UICanvasUtil.NewRect("NamePlate", pRT);
            _namePlateRT.anchorMin = new Vector2(0f, 1f);
            _namePlateRT.anchorMax = new Vector2(0f, 1f);
            _namePlateRT.pivot = new Vector2(0f, 0.5f);
            _namePlateRT.sizeDelta = new Vector2(170f, 40f);
            _namePlateRT.anchoredPosition = new Vector2(42f, 0f);
            _namePlateFill = _namePlateRT.gameObject.AddComponent<Image>();
            _namePlateFill.sprite = UICanvasUtil.RoundedRect(11);
            _namePlateFill.type = Image.Type.Sliced;
            _namePlateFill.color = new Color(0.16f, 0.12f, 0.07f, 0.97f);
            _namePlateFill.raycastTarget = false;
            var plateStroke = UICanvasUtil.NewImage("Hairline", _namePlateRT, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.5f), false);
            var psImg = plateStroke.GetComponent<Image>();
            psImg.sprite = UICanvasUtil.RoundedOutline(11, 1.6f);
            psImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)plateStroke.transform);

            _speakerLabel = UICanvasUtil.NewEyebrow("Speaker", _namePlateRT, "", 14f, HollowfenPalette.GoldGlow, TextAlignmentOptions.Center);
            _speakerLabel.fontStyle = FontStyles.Bold;
            UICanvasUtil.Stretch(_speakerLabel.rectTransform);

            // Body — roomy serif italic, vertically centred in the panel.
            _bodyText = UICanvasUtil.NewBody("Body", pRT, "", 27f, HollowfenPalette.InkDeep, FontStyles.Italic, TextAlignmentOptions.TopLeft);
            _bodyText.textWrappingMode = TextWrappingModes.Normal;
            _bodyText.lineSpacing = 8f;
            var btRT = _bodyText.rectTransform;
            btRT.anchorMin = new Vector2(0f, 0f); btRT.anchorMax = new Vector2(1f, 1f);
            btRT.pivot = new Vector2(0.5f, 0.5f);
            btRT.offsetMin = new Vector2(56f, 44f);
            btRT.offsetMax = new Vector2(-56f, -48f);

            // Hint (bottom-right, quiet)
            _hintText = UICanvasUtil.NewBody("Hint", pRT, "Space  ·  continue", 13f,
                new Color(HollowfenPalette.Moss.r, HollowfenPalette.Moss.g, HollowfenPalette.Moss.b, 0.75f),
                FontStyles.Italic, TextAlignmentOptions.BottomRight);
            var hRT = _hintText.rectTransform;
            hRT.anchorMin = new Vector2(0f, 0f); hRT.anchorMax = new Vector2(1f, 0f);
            hRT.pivot = new Vector2(0.5f, 0f);
            hRT.sizeDelta = new Vector2(-56f, 22f);
            hRT.anchoredPosition = new Vector2(0f, 14f);
        }

        private static void BuildFrame(RectTransform panelRT, float inset, Color color, float thickness)
        {
            var t = UICanvasUtil.NewImage("Frame.Top", panelRT, color, false);
            var tr = (RectTransform)t.transform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-inset * 2f, thickness); tr.anchoredPosition = new Vector2(0f, -inset);
            var b = UICanvasUtil.NewImage("Frame.Bot", panelRT, color, false);
            var br = (RectTransform)b.transform;
            br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(1f, 0f); br.pivot = new Vector2(0.5f, 0f);
            br.sizeDelta = new Vector2(-inset * 2f, thickness); br.anchoredPosition = new Vector2(0f, inset);
            var l = UICanvasUtil.NewImage("Frame.Left", panelRT, color, false);
            var lr = (RectTransform)l.transform;
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(0f, 1f); lr.pivot = new Vector2(0f, 0.5f);
            lr.sizeDelta = new Vector2(thickness, -inset * 2f); lr.anchoredPosition = new Vector2(inset, 0f);
            var r = UICanvasUtil.NewImage("Frame.Right", panelRT, color, false);
            var rr = (RectTransform)r.transform;
            rr.anchorMin = new Vector2(1f, 0f); rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(1f, 0.5f);
            rr.sizeDelta = new Vector2(thickness, -inset * 2f); rr.anchoredPosition = new Vector2(-inset, 0f);
        }
    }
}
