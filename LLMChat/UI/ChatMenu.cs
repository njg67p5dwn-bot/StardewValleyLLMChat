using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using LLMChat.Data;
using LLMChat.I18n;
using LLMChat.Personalities;
using LLMChat.Services;

namespace LLMChat.UI;

public class ChatMenu : IClickableMenu
{
    private const int MenuWidth = 800;
    private const int MenuHeight = 600;
    private const int Padding = 16;
    private const int MessageAreaHeight = 400;
    private const int InputHeight = 48;
    private const int PortraitSize = 64;

    private readonly NPC _npc;
    private readonly LlmService _llmService;
    private readonly PersonalityManager _personalityManager;
    private readonly ConversationStore _conversationStore;
    private readonly List<DisplayMessage> _displayMessages = new();

    private ClickableTextureComponent _sendButton;
    private ClickableComponent _inputArea;
    private float _scrollY;
    private float _totalContentH;
    private string _streamingText = "";
    private bool _isWaitingForResponse;
    private CancellationTokenSource? _cts;

    // Custom text input state
    private string _inputText = "";
    private string _composingText = "";  // IME preedit text
    private bool _inputActive = true;
    private float _cursorBlink;

    private record DisplayMessage(string Speaker, string Text, bool IsPlayer);

    // SDL2 interop for IME composition tracking (cross-platform)
    private static class SDL2
    {
        private static readonly IntPtr _lib;

        private delegate void VoidDelegate();
        private static readonly VoidDelegate _startTextInput;
        private static readonly VoidDelegate _stopTextInput;

        static SDL2()
        {
            string libName;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                libName = "SDL2.dll";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                libName = "libSDL2.dylib";
            else
                libName = "libSDL2-2.0.so.0";

            _lib = NativeLibrary.Load(libName);
            _startTextInput = Marshal.GetDelegateForFunctionPointer<VoidDelegate>(
                NativeLibrary.GetExport(_lib, "SDL_StartTextInput"));
            _stopTextInput = Marshal.GetDelegateForFunctionPointer<VoidDelegate>(
                NativeLibrary.GetExport(_lib, "SDL_StopTextInput"));
        }

        public static void StartTextInput() => _startTextInput();
        public static void StopTextInput() => _stopTextInput();
    }

    public ChatMenu(
        NPC npc,
        LlmService llmService,
        PersonalityManager personalityManager,
        ConversationStore conversationStore)
        : base(
            (Game1.uiViewport.Width - MenuWidth) / 2,
            (Game1.uiViewport.Height - MenuHeight) / 2,
            MenuWidth,
            MenuHeight,
            showUpperRightCloseButton: true)
    {
        _npc = npc;
        _llmService = llmService;
        _personalityManager = personalityManager;
        _conversationStore = conversationStore;

        // Load existing conversation history into display
        var history = _conversationStore.GetHistory(npc.Name);
        foreach (var msg in history.Messages)
        {
            _displayMessages.Add(new DisplayMessage(
                msg.Role == "user" ? Game1.player.Name : npc.displayName,
                msg.Content,
                msg.Role == "user"
            ));
        }

        // Clickable input area
        _inputArea = new ClickableComponent(
            new Rectangle(
                xPositionOnScreen + Padding + 8,
                yPositionOnScreen + height - InputHeight - Padding - 8,
                width - Padding * 2 - 80,
                48
            ),
            "inputArea"
        );

        // Send button
        _sendButton = new ClickableTextureComponent(
            new Rectangle(
                xPositionOnScreen + width - Padding - 64,
                yPositionOnScreen + height - InputHeight - Padding - 8,
                64, 48
            ),
            Game1.mouseCursors,
            new Rectangle(365, 495, 12, 11),
            4f
        );

        // Subscribe to Window.TextInput for correct IME-ordered text
        Game1.game1.Window.TextInput += OnTextInput;

        // Enable SDL text input for IME
        try { SDL2.StartTextInput(); } catch { }

        ScrollToBottom();
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (!_inputActive || _isWaitingForResponse) return;

        char c = e.Character;

        if (c == '\r' || c == '\n')
        {
            // Enter - send message
            return;
        }
        else if (c == '\b')
        {
            // Backspace
            if (_inputText.Length > 0)
            {
                _inputText = _inputText[..^1];
            }
        }
        else if (c == 27) // Escape
        {
            return;
        }
        else if (!char.IsControl(c))
        {
            _inputText += c;
        }
    }

    private void ForceCommitIME()
    {
        try
        {
            SDL2.StopTextInput();
            SDL2.StartTextInput();
        }
        catch { }
    }

    private string GetGameDate()
    {
        return $"{Game1.currentSeason} {Game1.dayOfMonth}, Year {Game1.year}";
    }

    private async void SendMessage()
    {
        // Force commit any composing IME character first
        ForceCommitIME();

        // Wait a tick for the commit to propagate
        await System.Threading.Tasks.Task.Delay(50);

        var text = _inputText.Trim();
        if (string.IsNullOrEmpty(text) || _isWaitingForResponse)
            return;

        // Add player message
        _displayMessages.Add(new DisplayMessage(Game1.player.Name, text, true));
        _inputText = "";
        _isWaitingForResponse = true;
        _streamingText = "";
        ScrollToBottom();

        // Add placeholder for NPC response
        _displayMessages.Add(new DisplayMessage(_npc.displayName, "", false));

        var gameDate = GetGameDate();
        var history = _conversationStore.GetHistory(_npc.Name);
        var systemPrompt = _personalityManager.BuildSystemPrompt(_npc, history);

        _cts = new CancellationTokenSource();

        try
        {
            var response = await _llmService.ChatAsync(
                systemPrompt,
                history.Messages,
                text,
                onToken: token =>
                {
                    _streamingText += token;
                    if (_displayMessages.Count > 0)
                    {
                        _displayMessages[^1] = new DisplayMessage(_npc.displayName, _streamingText, false);
                    }
                    ScrollToBottom();
                },
                cancellationToken: _cts.Token
            );

            if (_displayMessages.Count > 0)
            {
                _displayMessages[^1] = new DisplayMessage(_npc.displayName, response, false);
            }
            ScrollToBottom();

            _conversationStore.AddMessage(_npc.Name, "user", text, gameDate);
            _conversationStore.AddMessage(_npc.Name, "assistant", response, gameDate);
        }
        catch (OperationCanceledException)
        {
            if (_displayMessages.Count > 0 && !_displayMessages[^1].IsPlayer)
            {
                _displayMessages[^1] = new DisplayMessage(_npc.displayName, Strings.Get("chat.cancelled"), false);
            }
        }
        finally
        {
            _isWaitingForResponse = false;
            _streamingText = "";
            _cts?.Dispose();
            _cts = null;
        }
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);

        if (_sendButton.containsPoint(x, y) && !_isWaitingForResponse)
        {
            SendMessage();
            Game1.playSound("smallSelect");
        }
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Enter && !_isWaitingForResponse)
        {
            SendMessage();
            return;
        }

        if (key == Keys.Escape)
        {
            exitThisMenu();
            return;
        }

        // Block all other keys from reaching the game
    }

    public override void receiveScrollWheelAction(int direction)
    {
        _scrollY -= direction > 0 ? 40 : -40;
        _scrollY = Math.Max(0, Math.Min(_scrollY, Math.Max(0, _totalContentH - MessageAreaHeight)));
    }

    private void ScrollToBottom()
    {
        _scrollY = Math.Max(0, _totalContentH - MessageAreaHeight);
    }

    public override void draw(SpriteBatch b)
    {
        // Prevent game's built-in ChatBox from activating while our menu is open
        if (Game1.chatBox?.chatBox?.Selected == true)
            Game1.chatBox.chatBox.Selected = false;

        _cursorBlink += 0.05f;

        // Dim background
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);

        // Draw menu background
        drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            xPositionOnScreen,
            yPositionOnScreen,
            width,
            height,
            Color.White
        );

        DrawPortraitArea(b);
        DrawMessages(b);
        DrawInputArea(b);

        _sendButton.draw(b);

        // Thinking indicator
        if (_isWaitingForResponse && string.IsNullOrEmpty(_streamingText))
        {
            var dots = (int)(Game1.currentGameTime.TotalGameTime.TotalSeconds * 2) % 4;
            var thinkingText = string.Format(Strings.Get("chat.thinking"), _npc.displayName) + new string('.', dots + 1);
            var pos = new Vector2(
                xPositionOnScreen + Padding + 12,
                yPositionOnScreen + height - InputHeight - Padding - 36
            );
            b.DrawString(Game1.smallFont, thinkingText, pos, Color.Gray);
        }

        drawMouse(b);
        base.draw(b);
    }

    private void DrawInputArea(SpriteBatch b)
    {
        // Input background
        drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            _inputArea.bounds.X - 4,
            _inputArea.bounds.Y - 4,
            _inputArea.bounds.Width + 8,
            _inputArea.bounds.Height + 8,
            Color.White
        );

        // Draw a slightly darker inner box
        b.Draw(Game1.fadeToBlackRect, new Rectangle(
            _inputArea.bounds.X, _inputArea.bounds.Y,
            _inputArea.bounds.Width, _inputArea.bounds.Height
        ), Color.Black * 0.05f);

        // Input text
        var displayText = _inputText;
        if (string.IsNullOrEmpty(displayText) && !_isWaitingForResponse)
        {
            b.DrawString(Game1.smallFont, Strings.Get("chat.type_here"),
                new Vector2(_inputArea.bounds.X + 8, _inputArea.bounds.Y + 12),
                Color.Gray);
        }
        else
        {
            b.DrawString(Game1.smallFont, displayText,
                new Vector2(_inputArea.bounds.X + 8, _inputArea.bounds.Y + 12),
                Game1.textColor);

            // Blinking cursor
            if (!_isWaitingForResponse && (int)(_cursorBlink % 2) == 0)
            {
                var textWidth = string.IsNullOrEmpty(displayText)
                    ? 0
                    : Game1.smallFont.MeasureString(displayText).X;
                b.DrawString(Game1.smallFont, "|",
                    new Vector2(_inputArea.bounds.X + 8 + textWidth, _inputArea.bounds.Y + 12),
                    Game1.textColor);
            }
        }
    }

    private void DrawPortraitArea(SpriteBatch b)
    {
        var portraitX = xPositionOnScreen + Padding + 8;
        var portraitY = yPositionOnScreen + Padding + 8;

        try
        {
            b.Draw(
                _npc.Portrait,
                new Rectangle(portraitX, portraitY, PortraitSize, PortraitSize),
                new Rectangle(0, 0, 64, 64),
                Color.White
            );
        }
        catch { }

        SpriteText.drawString(b, _npc.displayName, portraitX + PortraitSize + 12, portraitY + 16);

        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            xPositionOnScreen + Padding, portraitY + PortraitSize + 8, width - Padding * 2, 4, Color.White);
    }

    private void DrawMessages(SpriteBatch b)
    {
        var messageAreaX = xPositionOnScreen + Padding + 8;
        var messageAreaY = yPositionOnScreen + Padding + PortraitSize + 24;
        var messageAreaWidth = width - Padding * 2 - 16;

        float totalH = 0;
        var msgSnapshots = _displayMessages.ToList();
        foreach (var msg in msgSnapshots)
        {
            var fullText = $"[{msg.Speaker}] {msg.Text}";
            var wrappedText = Game1.parseText(fullText, Game1.smallFont, messageAreaWidth - 16);
            var textSize = Game1.smallFont.MeasureString(wrappedText);
            totalH += textSize.Y + 8;
        }
        _totalContentH = totalH;

        var scissorRect = new Rectangle(messageAreaX, messageAreaY, messageAreaWidth, MessageAreaHeight);
        var oldScissor = b.GraphicsDevice.ScissorRectangle;

        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null,
            new RasterizerState { ScissorTestEnable = true });
        b.GraphicsDevice.ScissorRectangle = scissorRect;

        float currentY = messageAreaY - _scrollY;

        foreach (var msg in msgSnapshots)
        {
            var color = msg.IsPlayer ? new Color(50, 80, 180) : new Color(100, 60, 20);
            var fullText = $"[{msg.Speaker}] {msg.Text}";
            var wrappedText = Game1.parseText(fullText, Game1.smallFont, messageAreaWidth - 16);
            var textSize = Game1.smallFont.MeasureString(wrappedText);

            if (currentY + textSize.Y >= messageAreaY && currentY < messageAreaY + MessageAreaHeight)
            {
                b.DrawString(Game1.smallFont, wrappedText, new Vector2(messageAreaX + 4, currentY), color);
            }

            currentY += textSize.Y + 8;
            if (currentY > messageAreaY + MessageAreaHeight) break;
        }

        b.End();
        b.GraphicsDevice.ScissorRectangle = oldScissor;
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
    }

    protected override void cleanupBeforeExit()
    {
        // Unsubscribe from TextInput
        Game1.game1.Window.TextInput -= OnTextInput;
        _cts?.Cancel();
        _conversationStore.SaveAll();
        base.cleanupBeforeExit();
    }
}
