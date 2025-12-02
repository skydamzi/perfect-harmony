using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class LobbySceneController : MonoBehaviour
{
    void Start()
    {
        // This controller sets up the entire lobby scene from scratch.
        CreateEventSystem();
        CreateManagers();
        CreateLobbyUI();
    }

    void CreateEventSystem()
    {
        // An EventSystem is required for UI to work.
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
        }
    }

    void CreateLobbyUI()
    {
        // 1. Create Canvas
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // 2. Create LobbyPanel
        GameObject panelObj = new GameObject("LobbyPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(300, 400);
        panelObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.7f);

        // 3. Add and Configure VerticalLayoutGroup
        VerticalLayoutGroup layoutGroup = panelObj.AddComponent<VerticalLayoutGroup>();
        layoutGroup.padding = new RectOffset(20, 20, 20, 20);
        layoutGroup.spacing = 15f;
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        ContentSizeFitter sizeFitter = panelObj.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 4. Create UI Elements
        Text statusText = CreateText(panelObj.transform, "StatusText", "Welcome! Create or Join a Game.", 18);
        
        // Host IP Display (Hidden by default, shown when Create Game is clicked)
        Text inviteCodeDisplay = CreateText(panelObj.transform, "InviteCodeDisplay", "Host IP: ", 20);
        
        // Input Field for IP Address (Always visible now for easier access)
        InputField inviteCodeInput = CreateInputField(panelObj.transform, "InviteCodeInput", "Enter Host IP (e.g. 127.0.0.1)");
        
        Button createGameButton = CreateButton(panelObj.transform, "CreateGameButton", "Create Game (Host)");
        Button joinGameButton = CreateButton(panelObj.transform, "JoinGameButton", "Join Game (Client)");
        Button startGameButton = CreateButton(panelObj.transform, "StartGameButton", "Start Game");

        // 5. Create and link LobbyUI component
        LobbyUI lobbyUI = canvasObj.AddComponent<LobbyUI>();
        lobbyUI.statusText = statusText;
        lobbyUI.inviteCodeDisplay = inviteCodeDisplay;
        lobbyUI.inviteCodeInput = inviteCodeInput;
        lobbyUI.createGameButton = createGameButton;
        lobbyUI.joinGameButton = joinGameButton;
        lobbyUI.startGameButton = startGameButton; // Link the new button
        lobbyUI.lobbyPanel = panelObj;
        
        // Initially hide only the Host IP display and Start Game button
        // Input field remains visible so users know where to type
        inviteCodeDisplay.gameObject.SetActive(false);
        startGameButton.gameObject.SetActive(false);
    }

    Button CreateButton(Transform parent, string name, string buttonText)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        btnObj.AddComponent<Image>();
        Button button = btnObj.AddComponent<Button>();
        
        LayoutElement layout = btnObj.AddComponent<LayoutElement>();
        layout.minHeight = 40;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        Text text = textObj.AddComponent<Text>();
        text.text = buttonText;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;

        return button;
    }

    Text CreateText(Transform parent, string name, string content, int fontSize)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        Text text = textObj.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        
        LayoutElement layout = textObj.AddComponent<LayoutElement>();
        layout.minHeight = 30;

        return text;
    }

    InputField CreateInputField(Transform parent, string name, string placeholder)
    {
        GameObject inputObj = new GameObject(name);
        inputObj.transform.SetParent(parent, false);
        
        // Background Image (White, Opaque for visibility)
        Image bgImage = inputObj.AddComponent<Image>();
        bgImage.color = Color.white; 
        
        InputField inputField = inputObj.AddComponent<InputField>();

        // Layout Element (Ensure it takes up space)
        LayoutElement layout = inputObj.AddComponent<LayoutElement>();
        layout.minHeight = 50; // Made taller
        layout.preferredHeight = 50;
        layout.flexibleWidth = 1;

        // Text Area Setup
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(inputObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 5); // Padding
        textRect.offsetMax = new Vector2(-10, -5);
        
        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); // Try Arial first
        if(text.font == null) text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color = Color.black;
        text.alignment = TextAnchor.MiddleLeft;
        text.fontSize = 20;

        // Placeholder Setup
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(inputObj.transform, false);
        RectTransform placeholderRect = placeholderObj.AddComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(10, 5);
        placeholderRect.offsetMax = new Vector2(-10, -5);

        Text placeholderText = placeholderObj.AddComponent<Text>();
        placeholderText.text = placeholder;
        placeholderText.font = text.font;
        placeholderText.fontStyle = FontStyle.Italic;
        placeholderText.fontSize = 20;
        placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 1f); // Gray
        placeholderText.alignment = TextAnchor.MiddleLeft;

        inputField.textComponent = text;
        inputField.placeholder = placeholderText;
        inputField.image = bgImage; // Link background image

        return inputField;
    }

    void CreateManagers()
    {
        if (FindFirstObjectByType<MultiplayerManager>() == null)
        {
            GameObject mpManagerObj = new GameObject("MultiplayerManager");
            mpManagerObj.AddComponent<MultiplayerManager>();
        }
    }
}