using System;
using System.Collections;
using UnityAiNpc.Npc;
using UnityEngine;
using UnityEngine.UI;

namespace UnityAiNpc.UI
{
    /// <summary>
    /// The chat box: a scrolling transcript, a text field, a "thinking" indicator, and a
    /// typewriter effect for NPC lines. Works with whichever <see cref="NpcConversation"/> is open.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Text npcNameLabel;
        [SerializeField] private Text backendLabel;
        [SerializeField] private ScrollRect transcriptScroll;
        [SerializeField] private RectTransform transcriptContent;
        [SerializeField] private InputField input;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button closeButton;

        [Header("Prompt")]
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private Text promptLabel;

        [Header("Style")]
        [SerializeField] private Font font;
        [SerializeField] private int lineFontSize = 20;
        [SerializeField] private Color playerColor = new Color(0.55f, 0.8f, 1f);
        [SerializeField] private Color systemColor = new Color(0.65f, 0.65f, 0.65f);
        [SerializeField] private float charactersPerSecond = 55f;
        [SerializeField] private float closeDelayAfterGoodbye = 2.5f;

        private NpcConversation _npc;
        private Text _thinkingLine;
        private Coroutine _typing;
        private Text _typingLine;
        private string _typingFinalText;
        private bool _scrollToBottom;

        public event Action<DialogueUI> Opened;
        public event Action<DialogueUI> Closed;

        public bool IsOpen => panel != null && panel.activeSelf;
        public bool IsTyping => _typing != null;
        public NpcConversation CurrentNpc => _npc;
        public InputField Input => input;
        public RectTransform TranscriptContent => transcriptContent;

        /// <summary>True while waiting for or still revealing a reply.</summary>
        public bool IsBusy => IsTyping || (_npc != null && _npc.IsThinking);

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            ShowPrompt(null);
            if (sendButton != null) sendButton.onClick.AddListener(Submit);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (input != null)
            {
                input.characterLimit = NpcDialogue.MaxPlayerMessageLength;
                input.onEndEdit.AddListener(_ =>
                {
                    if (UnityEngine.Input.GetKey(KeyCode.Return) || UnityEngine.Input.GetKey(KeyCode.KeypadEnter)) Submit();
                });
            }
        }

        private void Update()
        {
            if (IsOpen && UnityEngine.Input.GetKeyDown(KeyCode.Escape)) Close();

            if (_thinkingLine != null)
                _thinkingLine.text = $"<i>{_npc.DisplayName} is thinking{new string('.', 1 + (int)(Time.time * 3f) % 3)}</i>";

            if (_npc != null && backendLabel != null)
                backendLabel.text = _npc.TotalTokens > 0 ? $"{_npc.BackendDescription} · {_npc.TotalTokens} tokens" : _npc.BackendDescription;
        }

        private void LateUpdate()
        {
            if (!_scrollToBottom || transcriptScroll == null) return;
            Canvas.ForceUpdateCanvases();
            transcriptScroll.verticalNormalizedPosition = 0f;
            _scrollToBottom = false;
        }

        public void ShowPrompt(string text)
        {
            if (promptRoot != null) promptRoot.SetActive(!string.IsNullOrEmpty(text));
            if (promptLabel != null && !string.IsNullOrEmpty(text)) promptLabel.text = text;
        }

        public void Open(NpcConversation npc)
        {
            if (npc == null) throw new ArgumentNullException(nameof(npc));
            if (_npc != npc)
            {
                Detach();
                ClearTranscript();
                _npc = npc;
                _npc.ThinkingChanged += OnThinkingChanged;
                _npc.Replied += OnReplied;
            }

            if (npcNameLabel != null)
            {
                npcNameLabel.text = npc.DisplayName;
                if (npc.Persona != null) npcNameLabel.color = npc.Persona.nameColor;
            }

            ShowPrompt(null);
            panel.SetActive(true);

            if (transcriptContent.childCount == 0 && npc.Persona != null && !string.IsNullOrEmpty(npc.Persona.greeting))
                AddNpcLine(npc.Persona.greeting, NpcMood.Neutral, animate: true);

            FocusInput();
            Opened?.Invoke(this);
        }

        public void Close()
        {
            if (!IsOpen) return;
            panel.SetActive(false);
            FinishTyping();
            Closed?.Invoke(this);
        }

        /// <summary>Sends whatever is in the text field.</summary>
        public void Submit() => SubmitText(input != null ? input.text : null);

        public void SubmitText(string text)
        {
            text = NpcDialogue.Sanitize(text);
            if (_npc == null || text.Length == 0 || _npc.IsThinking) return;

            AddLine($"<color=#{ColorUtility.ToHtmlStringRGB(playerColor)}><b>You:</b></color> {Escape(text)}");
            if (input != null) input.text = string.Empty;
            _npc.Talk(text);
            FocusInput();
        }

        private void OnThinkingChanged(bool thinking)
        {
            if (thinking && _thinkingLine == null)
            {
                _thinkingLine = AddLine(string.Empty, systemColor);
            }
            else if (!thinking && _thinkingLine != null)
            {
                Destroy(_thinkingLine.gameObject);
                _thinkingLine = null;
            }
            if (sendButton != null) sendButton.interactable = !thinking;
        }

        private void OnReplied(NpcReply reply)
        {
            if (reply.IsError)
                AddLine($"<i>(request failed: {Escape(reply.ErrorMessage)})</i>", systemColor);

            AddNpcLine(reply.Say, reply.Mood, animate: true);

            if (reply.EndConversation && IsOpen)
                StartCoroutine(CloseAfterTyping());
        }

        private void AddNpcLine(string say, NpcMood mood, bool animate)
        {
            var nameColor = _npc != null && _npc.Persona != null ? _npc.Persona.nameColor : Color.white;
            string prefix = $"<color=#{ColorUtility.ToHtmlStringRGB(nameColor)}><b>{Escape(_npc != null ? _npc.DisplayName : "NPC")}:</b></color> ";
            string suffix = mood == NpcMood.Neutral
                ? string.Empty
                : $"  <color=#{ColorUtility.ToHtmlStringRGB(NpcMoodVisuals.ColorFor(mood))}><i>({mood.ToString().ToLowerInvariant()})</i></color>";

            var line = AddLine(prefix);
            FinishTyping();
            string body = Escape(say);
            _typingLine = line;
            _typingFinalText = prefix + body + suffix;
            _typing = StartCoroutine(Typewrite(line, prefix, body, animate));
        }

        /// <summary>Stops the typewriter and shows the whole line, so nothing is left half-typed.</summary>
        private void FinishTyping()
        {
            if (_typing == null) return;
            StopCoroutine(_typing);
            if (_typingLine != null) _typingLine.text = _typingFinalText;
            _typing = null;
            _typingLine = null;
            _scrollToBottom = true;
        }

        private IEnumerator Typewrite(Text line, string prefix, string body, bool animate)
        {
            // Always yield once: a coroutine that finishes inside StartCoroutine would clear
            // _typing before the caller assigns it, leaving IsTyping stuck at true.
            yield return null;
            if (animate && charactersPerSecond > 0f)
            {
                float shown = 0f;
                while (shown < body.Length && line != null)
                {
                    shown += charactersPerSecond * Time.deltaTime;
                    line.text = prefix + body.Substring(0, Mathf.Min(body.Length, (int)shown));
                    _scrollToBottom = true;
                    yield return null;
                }
            }
            if (line != null) line.text = _typingFinalText;
            _scrollToBottom = true;
            _typing = null;
            _typingLine = null;
        }

        private IEnumerator CloseAfterTyping()
        {
            while (IsTyping) yield return null;
            yield return new WaitForSeconds(closeDelayAfterGoodbye);
            Close();
        }

        private Text AddLine(string richText, Color? color = null)
        {
            var go = new GameObject("Line", typeof(RectTransform));
            go.transform.SetParent(transcriptContent, false);
            var text = go.AddComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = lineFontSize;
            text.color = color ?? Color.white;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = richText;
            _scrollToBottom = true;
            return text;
        }

        private void ClearTranscript()
        {
            if (transcriptContent == null) return;
            // Unparent first: Destroy is deferred and Open() checks childCount right after this.
            for (int i = transcriptContent.childCount - 1; i >= 0; i--)
            {
                var child = transcriptContent.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
            _thinkingLine = null;
        }

        private void FocusInput()
        {
            if (input == null) return;
            input.Select();
            input.ActivateInputField();
        }

        private void Detach()
        {
            if (_npc == null) return;
            _npc.ThinkingChanged -= OnThinkingChanged;
            _npc.Replied -= OnReplied;
            _npc = null;
        }

        private void OnDestroy() => Detach();

        /// <summary>
        /// Stops model or player text from injecting rich-text tags (e.g. &lt;size=200&gt;) into the transcript:
        /// a '&lt;' that could open a tag is swapped for a look-alike angle quote.
        /// </summary>
        public static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool opensTag = c == '<' && i + 1 < text.Length && (char.IsLetter(text[i + 1]) || text[i + 1] == '/');
                sb.Append(opensTag ? '\u2039' : c);
            }
            return sb.ToString();
        }
    }
}
