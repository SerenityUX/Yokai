using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class CharacterData
{
    public string filePath;
    public string name;
    public string creatureDescription;
    public string Type;
    public int powerScore;
    public string[] characterAudios;
    
    // Runtime-loaded sprite (not in JSON)
    [System.NonSerialized]
    public Sprite sprite;
    
    // Runtime-loaded audio clips (not in JSON)
    [System.NonSerialized]
    public AudioClip[] audioClips;
}

[System.Serializable]
public class CharacterDataList
{
    public CharacterData[] characters;
}

// Legacy class for backwards compatibility - will be replaced
[System.Serializable]
public class InventoryItem
{
    public string itemName;
    public Sprite itemImage;
    public int ScoreAmount;
}

public class InventoryManagement : MonoBehaviour
{
    [Header("Player 1 Inventory Slots (1-5)")]
    [SerializeField] private SpriteRenderer player1Slot1;
    [SerializeField] private SpriteRenderer player1Slot2;
    [SerializeField] private SpriteRenderer player1Slot3;
    [SerializeField] private SpriteRenderer player1Slot4;
    [SerializeField] private SpriteRenderer player1Slot5;

    [Header("Player 2 Inventory Slots (6-9-0)")]
    [SerializeField] private SpriteRenderer player2Slot1;
    [SerializeField] private SpriteRenderer player2Slot2;
    [SerializeField] private SpriteRenderer player2Slot3;
    [SerializeField] private SpriteRenderer player2Slot4;
    [SerializeField] private SpriteRenderer player2Slot5;

    [Header("Character Data")]
    [SerializeField] private string characterDataJsonPath = "My Assets/Creatures/characterData";
    [Tooltip("If JSON is not in Resources folder, it will try to load from Assets folder in editor")]
    [SerializeField] private string cardBackPath = "My Assets/CardBack";
    [Tooltip("Path to the card back sprite (without extension)")]
    
    [Header("Audio")]
    [SerializeField] private AudioClip chipPlaceSound;
    [SerializeField] private AudioClip gongSound;
    [SerializeField] private AudioClip clickSound;
    
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI creatureDescriptionText;
    [Tooltip("Text object to display the creature description of the most recently placed chip")]
    
    // Loaded character data from JSON
    private List<CharacterData> availableCharacters = new List<CharacterData>();
    
    // Track assigned characters in current round to prevent duplicates
    private List<CharacterData> assignedCharactersThisRound = new List<CharacterData>();

    [Header("Current Inventory Items")]
    public CharacterData player1Item1;
    public CharacterData player1Item2;
    public CharacterData player1Item3;
    public CharacterData player1Item4;
    public CharacterData player1Item5;

    public CharacterData player2Item1;
    public CharacterData player2Item2;
    public CharacterData player2Item3;
    public CharacterData player2Item4;
    public CharacterData player2Item5;

    private SpriteRenderer[] player1Slots;
    private SpriteRenderer[] player2Slots;
    private CharacterData[] player1Inventory;
    private CharacterData[] player2Inventory;
    private Vector3[] player1OriginalScales;
    private Vector3[] player2OriginalScales;
    private Sprite[] player1OriginalSprites;
    private Sprite[] player2OriginalSprites;
    
    // Score display components
    private SpriteRenderer[] player1ScoreSquares;
    private TextMeshProUGUI[] player1ScoreTexts;
    private SpriteRenderer[] player2ScoreSquares;
    private TextMeshProUGUI[] player2ScoreTexts;
    
    // Game state
    private int player1Score = 0;
    private int player2Score = 0;
    private bool gameOver = false;
    private bool prefirstMove = true;
    private GameObject winScreen;
    private TextMeshProUGUI winText;
    private TextMeshProUGUI restartText;
    private GameObject preFirstMoveScreen;
    private TextMeshProUGUI preFirstMoveText;
    private List<GameObject> preFirstMoveTileImages = new List<GameObject>(); // Track tile images for cleanup
    private Dictionary<GameObject, TileFlipData> tileFlipDataMap = new Dictionary<GameObject, TileFlipData>(); // Track flip state
    private Sprite cardBackSprite;
    private bool[] player1SlotWins; // Track which slots player 1 won
    private bool[] player2SlotWins; // Track which slots player 2 won
    private Coroutine tileFlippingCoroutine; // Track the tile flipping coroutine so we can stop it
    
    // Helper class to track tile flip data
    private class TileFlipData
    {
        public bool isFlipped;
        public Sprite characterSprite;
        public Image imageComponent;
        public Coroutine flipCoroutine;
    }
    
    // Fade coroutines tracking
    private Dictionary<int, Coroutine> player1FadeCoroutines = new Dictionary<int, Coroutine>();
    private Dictionary<int, Coroutine> player2FadeCoroutines = new Dictionary<int, Coroutine>();
    private const float fadeDuration = 0.3f; // Duration of fade transition in seconds
    
    // Audio
    private AudioSource audioSource;

    private void Awake()
    {
        // Load character data from JSON
        LoadCharacterData();
        
        // Load card back sprite
        LoadCardBackSprite();
        
        // Initialize arrays
        player1Slots = new SpriteRenderer[] { player1Slot1, player1Slot2, player1Slot3, player1Slot4, player1Slot5 };
        player2Slots = new SpriteRenderer[] { player2Slot1, player2Slot2, player2Slot3, player2Slot4, player2Slot5 };
        player1Inventory = new CharacterData[] { player1Item1, player1Item2, player1Item3, player1Item4, player1Item5 };
        player2Inventory = new CharacterData[] { player2Item1, player2Item2, player2Item3, player2Item4, player2Item5 };
        
        // Store original scales and sprites
        player1OriginalScales = new Vector3[5];
        player2OriginalScales = new Vector3[5];
        player1OriginalSprites = new Sprite[5];
        player2OriginalSprites = new Sprite[5];
        
        for (int i = 0; i < 5; i++)
        {
            if (player1Slots[i] != null)
            {
                player1OriginalScales[i] = player1Slots[i].transform.localScale;
                player1OriginalSprites[i] = player1Slots[i].sprite;
            }
            if (player2Slots[i] != null)
            {
                player2OriginalScales[i] = player2Slots[i].transform.localScale;
                player2OriginalSprites[i] = player2Slots[i].sprite;
            }
        }
        
        // Initialize score display arrays
        player1ScoreSquares = new SpriteRenderer[5];
        player1ScoreTexts = new TextMeshProUGUI[5];
        player2ScoreSquares = new SpriteRenderer[5];
        player2ScoreTexts = new TextMeshProUGUI[5];
        
        // Don't create score displays upfront - they'll be created when chips are placed
        // This prevents orange squares from appearing incorrectly
        
        // Initialize win screen (will be created when needed)
        winScreen = null;
        winText = null;
        restartText = null;
        preFirstMoveScreen = null;
        preFirstMoveText = null;
        player1SlotWins = new bool[5];
        player2SlotWins = new bool[5];
        
        // Initialize AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        
        // Initialize assigned characters list for new round
        assignedCharactersThisRound = new List<CharacterData>();
    }
    
    private void Start()
    {
        // Show pre-first move screen if needed
        if (prefirstMove)
        {
            ShowPreFirstMoveScreen();
            // Start flipping tiles after a short delay
            StartCoroutine(DelayedStartFlipping());
        }
    }
    
    private IEnumerator DelayedStartFlipping()
    {
        yield return new WaitForSeconds(0.5f); // Wait a bit before starting flips
        tileFlippingCoroutine = StartCoroutine(StartTileFlipping());
    }
    
    private void LoadCharacterData()
    {
        availableCharacters.Clear();
        
        // Try to load JSON from Resources first
        TextAsset jsonFile = Resources.Load<TextAsset>(characterDataJsonPath);
        
        if (jsonFile == null)
        {
            // Fallback: try loading from file system (for editor)
            string fullPath = Path.Combine(Application.dataPath, characterDataJsonPath + ".json");
            if (File.Exists(fullPath))
            {
                string jsonContent = File.ReadAllText(fullPath);
                LoadCharactersFromJson(jsonContent);
            }
            else
            {
                Debug.LogError($"Could not load character data from Resources path: {characterDataJsonPath} or file path: {fullPath}");
            }
        }
        else
        {
            LoadCharactersFromJson(jsonFile.text);
        }
    }
    
    private void LoadCharactersFromJson(string jsonContent)
    {
        try
        {
            // JsonUtility doesn't support arrays directly, so we wrap it
            string wrappedJson = "{\"characters\":" + jsonContent + "}";
            CharacterDataList wrapper = JsonUtility.FromJson<CharacterDataList>(wrappedJson);
            
            if (wrapper != null && wrapper.characters != null && wrapper.characters.Length > 0)
            {
                // Load sprites and audio clips for each character
                foreach (CharacterData character in wrapper.characters)
                {
                    LoadCharacterSprite(character);
                    LoadCharacterAudios(character);
                    availableCharacters.Add(character);
                }
                
                Debug.Log($"Loaded {availableCharacters.Count} characters from JSON");
            }
            else
            {
                Debug.LogError("Failed to parse character data from JSON - wrapper or characters array is null");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading character data: {e.Message}\nStack trace: {e.StackTrace}");
        }
    }
    
    private void LoadCharacterSprite(CharacterData character)
    {
        if (string.IsNullOrEmpty(character.filePath))
        {
            Debug.LogWarning($"Character {character.name} has no filePath");
            return;
        }
        
        Sprite sprite = null;
        
#if UNITY_EDITOR
        // In editor, use AssetDatabase to load from any path in Assets folder
        string assetPath = "Assets/My Assets/Creatures" + character.filePath;
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        
        if (sprite == null)
        {
            // Try without leading slash
            assetPath = "Assets/My Assets/Creatures" + character.filePath.TrimStart('/');
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
        
        if (sprite == null)
        {
            // Try alternative path format
            assetPath = "Assets" + character.filePath;
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
        
        if (sprite != null)
        {
            character.sprite = sprite;
            Debug.Log($"Loaded sprite for {character.name} from {assetPath}");
            return;
        }
#endif
        
        // Fallback: Try Resources.Load (for runtime builds - requires Resources folder)
        if (sprite == null)
        {
            // Remove leading slash and file extension for Resources.Load
            string resourcePath = character.filePath.TrimStart('/');
            resourcePath = resourcePath.Replace(".png", "").Replace(".PNG", "");
            
            // Try loading from Resources with various path formats
            sprite = Resources.Load<Sprite>(resourcePath);
            
            // Try alternative paths if first attempt fails
            if (sprite == null)
            {
                // Try with "My Assets/Creatures" prefix
                string pathWithPrefix = "My Assets/Creatures/" + resourcePath;
                sprite = Resources.Load<Sprite>(pathWithPrefix);
            }
            
            if (sprite == null)
            {
                // Try just the characters folder (e.g., "characters/27")
                if (resourcePath.Contains("characters/"))
                {
                    int charsIndex = resourcePath.IndexOf("characters/");
                    string charsPath = resourcePath.Substring(charsIndex);
                    sprite = Resources.Load<Sprite>(charsPath);
                }
            }
            
            if (sprite == null)
            {
                // Try with just the filename (e.g., "27")
                string filename = Path.GetFileNameWithoutExtension(character.filePath);
                sprite = Resources.Load<Sprite>(filename);
            }
            
            // Last resort: try loading all sprites from Resources and find by name
            if (sprite == null)
            {
                Sprite[] allSprites = Resources.LoadAll<Sprite>("");
                string targetName = Path.GetFileNameWithoutExtension(character.filePath);
                foreach (Sprite s in allSprites)
                {
                    if (s.name == targetName)
                    {
                        sprite = s;
                        break;
                    }
                }
            }
        }
        
        if (sprite != null)
        {
            character.sprite = sprite;
            Debug.Log($"Loaded sprite for {character.name} from {character.filePath}");
        }
        else
        {
            Debug.LogWarning($"Could not load sprite for {character.name} from path: {character.filePath}. Tried AssetDatabase and Resources.Load methods.");
        }
    }
    
    private void LoadCharacterAudios(CharacterData character)
    {
        if (character.characterAudios == null || character.characterAudios.Length == 0)
        {
            character.audioClips = new AudioClip[0];
            return;
        }
        
        List<AudioClip> loadedClips = new List<AudioClip>();
        
        foreach (string audioPath in character.characterAudios)
        {
            if (string.IsNullOrEmpty(audioPath))
                continue;
            
            AudioClip clip = null;
            
#if UNITY_EDITOR
            // In editor, use AssetDatabase to load from any path in Assets folder
            string assetPath = "Assets/My Assets/Creatures" + audioPath;
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            
            if (clip == null)
            {
                // Try without leading slash
                assetPath = "Assets/My Assets/Creatures" + audioPath.TrimStart('/');
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            }
            
            if (clip == null)
            {
                // Try alternative path format
                assetPath = "Assets" + audioPath;
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            }
#endif
            
            // Fallback: Try Resources.Load (for runtime builds)
            if (clip == null)
            {
                // Remove leading slash and file extension for Resources.Load
                string resourcePath = audioPath.TrimStart('/');
                resourcePath = resourcePath.Replace(".mp3", "").Replace(".MP3", "");
                
                clip = Resources.Load<AudioClip>(resourcePath);
                
                if (clip == null)
                {
                    // Try with "My Assets/Creatures" prefix
                    string pathWithPrefix = "My Assets/Creatures/" + resourcePath;
                    clip = Resources.Load<AudioClip>(pathWithPrefix);
                }
                
                if (clip == null && resourcePath.Contains("audio/"))
                {
                    // Try just the audio folder
                    int audioIndex = resourcePath.IndexOf("audio/");
                    string audioPathOnly = resourcePath.Substring(audioIndex);
                    clip = Resources.Load<AudioClip>(audioPathOnly);
                }
            }
            
            if (clip != null)
            {
                loadedClips.Add(clip);
            }
            else
            {
                Debug.LogWarning($"Could not load audio clip for {character.name} from path: {audioPath}");
            }
        }
        
        character.audioClips = loadedClips.ToArray();
        
        if (character.audioClips.Length > 0)
        {
            Debug.Log($"Loaded {character.audioClips.Length} audio clip(s) for {character.name}");
        }
    }
    
    private void LoadCardBackSprite()
    {
        Sprite sprite = null;
        
#if UNITY_EDITOR
        // In editor, use AssetDatabase
        string assetPath = "Assets/" + cardBackPath + ".png";
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        
        if (sprite == null)
        {
            // Try without leading Assets/
            assetPath = cardBackPath + ".png";
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/" + assetPath);
        }
#endif
        
        // Fallback: Try Resources.Load
        if (sprite == null)
        {
            string resourcePath = cardBackPath.Replace("My Assets/", "").Replace(".png", "");
            sprite = Resources.Load<Sprite>(resourcePath);
            
            if (sprite == null)
            {
                // Try just the filename
                sprite = Resources.Load<Sprite>("CardBack");
            }
        }
        
        if (sprite != null)
        {
            cardBackSprite = sprite;
            Debug.Log($"Loaded card back sprite from {cardBackPath}");
        }
        else
        {
            Debug.LogWarning($"Could not load card back sprite from: {cardBackPath}");
        }
    }
    
    private void CreateScoreDisplays()
    {
        // Color for square: #FF7C00 (orange) - will be reset to white when item is assigned
        Color squareColor = new Color(0xFF / 255f, 0x7C / 255f, 0x00 / 255f, 1f);
        // Color for text: #FF7C00
        Color textColor = new Color(0xFF / 255f, 0x7C / 255f, 0x00 / 255f, 1f);
        
        // Create score displays for Player 1 slots
        for (int i = 0; i < 5; i++)
        {
            if (player1Slots[i] != null)
            {
                CreateScoreDisplayForSlot(player1Slots[i], i, true, squareColor, textColor);
            }
        }
        
        // Create score displays for Player 2 slots
        for (int i = 0; i < 5; i++)
        {
            if (player2Slots[i] != null)
            {
                CreateScoreDisplayForSlot(player2Slots[i], i, false, squareColor, textColor);
            }
        }
    }
    
    private void CreateScoreDisplayForSlot(SpriteRenderer slotRenderer, int slotIndex, bool isPlayer1, Color squareColor, Color textColor)
    {
        if (slotRenderer == null) return;
        
        Transform slotTransform = slotRenderer.transform;
        string parentName = $"Inventory {slotIndex + 1}";
        string textName = "Text (TMP)";
        
        // Look for existing parent (empty GameObject with RectTransform)
        Transform existingParent = slotTransform.Find(parentName);
        RectTransform parentRect;
        
        if (existingParent != null)
        {
            parentRect = existingParent.GetComponent<RectTransform>();
            if (parentRect == null)
            {
                parentRect = existingParent.gameObject.AddComponent<RectTransform>();
            }
        }
        else
        {
            // Create empty parent GameObject with RectTransform
            GameObject parentObject = new GameObject(parentName);
            parentObject.transform.SetParent(slotTransform);
            parentRect = parentObject.AddComponent<RectTransform>();
            
            // Set parent position (z: -1 is important for z-index)
            parentRect.localPosition = new Vector3(0, 0, -1f);
            parentRect.localScale = Vector3.one;
            parentRect.localRotation = Quaternion.identity;
            
            // Configure RectTransform anchors (centered) - 1/4 size
            parentRect.anchorMin = new Vector2(0.5f, 0.5f);
            parentRect.anchorMax = new Vector2(0.5f, 0.5f);
            parentRect.pivot = new Vector2(0.5f, 0.5f);
            parentRect.sizeDelta = new Vector2(15.814392f / 4f, 17.089996f / 4f); // 1/4 size
            
            // Position parent centered on the slot
            parentRect.transform.localPosition = new Vector3(0, 0, -1f);
        }
        
        // Look for existing text (TextMeshProUGUI, not TextMeshPro!)
        Transform existingText = parentRect.transform.Find(textName);
        TextMeshProUGUI scoreText;
        
        if (existingText != null)
        {
            scoreText = existingText.GetComponent<TextMeshProUGUI>();
            if (scoreText == null)
            {
                scoreText = existingText.gameObject.AddComponent<TextMeshProUGUI>();
            }
        }
        else
        {
            // Create Text GameObject as child of parent
            GameObject textObject = new GameObject(textName);
            textObject.transform.SetParent(parentRect.transform);
            scoreText = textObject.AddComponent<TextMeshProUGUI>();
            
            // Configure RectTransform for text
            RectTransform textRect = scoreText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(-0.000015258789f / 4f, 0f); // 1/4 size
            textRect.localPosition = new Vector3(0, 0, 0.001f); // Higher z-index to be on top
            textRect.localScale = Vector3.one;
            textRect.sizeDelta = new Vector2(15.8144f / 4f, 17.09f / 4f); // 1/4 size
        }
        
        // Configure text - 1/4 size
        scoreText.text = "0";
        scoreText.color = textColor;
        scoreText.fontSize = 4.5f; // 1/4 of 18 (scaled proportionally)
        scoreText.alignment = TextAlignmentOptions.Center;
        scoreText.fontStyle = FontStyles.Normal;
        
        // Create outline effect using duplicate text objects (workaround for outline not working)
        Color32 outlineColor = new Color32(0x56, 0x15, 0x01, 255);
        float outlineOffset = 0.15f; // Thick outline offset
        
        // Create 8 outline text objects (one for each direction) positioned behind main text
        string[] outlineNames = { "Outline1", "Outline2", "Outline3", "Outline4", "Outline5", "Outline6", "Outline7", "Outline8" };
        Vector2[] outlineOffsets = new Vector2[]
        {
            new Vector2(-outlineOffset, 0),      // Left
            new Vector2(outlineOffset, 0),       // Right
            new Vector2(0, -outlineOffset),     // Down
            new Vector2(0, outlineOffset),       // Up
            new Vector2(-outlineOffset, -outlineOffset), // Bottom-left
            new Vector2(outlineOffset, -outlineOffset),   // Bottom-right
            new Vector2(-outlineOffset, outlineOffset), // Top-left
            new Vector2(outlineOffset, outlineOffset)     // Top-right
        };
        
        // Check if outline objects already exist
        bool outlinesExist = parentRect.transform.Find(outlineNames[0]) != null;
        
        if (!outlinesExist)
        {
            // Create outline objects first (so they render behind)
            for (int i = 0; i < outlineNames.Length; i++)
            {
                GameObject outlineObj = new GameObject(outlineNames[i]);
                outlineObj.transform.SetParent(parentRect.transform);
                // Set sibling index to be before main text (renders first/behind)
                outlineObj.transform.SetSiblingIndex(scoreText.transform.GetSiblingIndex());
                TextMeshProUGUI outlineText = outlineObj.AddComponent<TextMeshProUGUI>();
                
                // Copy all settings from main text
                outlineText.text = scoreText.text;
                outlineText.font = scoreText.font; // Use same font asset
                outlineText.fontSize = scoreText.fontSize;
                outlineText.alignment = scoreText.alignment;
                outlineText.fontStyle = scoreText.fontStyle;
                outlineText.color = outlineColor;
                
                // Configure RectTransform
                RectTransform outlineRect = outlineText.rectTransform;
                outlineRect.anchorMin = scoreText.rectTransform.anchorMin;
                outlineRect.anchorMax = scoreText.rectTransform.anchorMax;
                outlineRect.pivot = scoreText.rectTransform.pivot;
                outlineRect.sizeDelta = scoreText.rectTransform.sizeDelta;
                outlineRect.anchoredPosition = scoreText.rectTransform.anchoredPosition + outlineOffsets[i];
                outlineRect.localPosition = new Vector3(
                    scoreText.rectTransform.localPosition.x + outlineOffsets[i].x,
                    scoreText.rectTransform.localPosition.y + outlineOffsets[i].y,
                    0f // Lower z-index, behind main text
                );
                outlineRect.localScale = scoreText.rectTransform.localScale;
            }
            
            // Move main text to end (renders last/on top)
            scoreText.transform.SetAsLastSibling();
        }
        
        // Force update
        scoreText.ForceMeshUpdate();
        
        // Store references
        if (isPlayer1)
        {
            player1ScoreSquares[slotIndex] = null; // No square anymore
            player1ScoreTexts[slotIndex] = scoreText;
        }
        else
        {
            player2ScoreSquares[slotIndex] = null; // No square anymore
            player2ScoreTexts[slotIndex] = scoreText;
        }
        
        // Initially hide the score display and ensure it's properly positioned
        parentRect.gameObject.SetActive(false);
        
        // Ensure the parent is properly parented to avoid appearing in wrong location
        if (parentRect.transform.parent != slotTransform)
        {
            parentRect.transform.SetParent(slotTransform, false);
        }
    }

    private void Update()
    {
        if (gameOver)
        {
            // Handle restart
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                RestartGame();
            }
            return;
        }
        
        // Check for key presses even during pre-first move screen
        // Player 1 controls (keys 1-5) using new Input System
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (prefirstMove)
            {
                prefirstMove = false;
                HidePreFirstMoveScreen();
                ShowAllGameElements();
            }
            AssignRandomItem(0, true);
        }
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (prefirstMove)
            {
                prefirstMove = false;
                HidePreFirstMoveScreen();
                ShowAllGameElements();
            }
            AssignRandomItem(1, true);
        }
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            if (prefirstMove)
            {
                prefirstMove = false;
                HidePreFirstMoveScreen();
                ShowAllGameElements();
            }
            AssignRandomItem(2, true);
        }
        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            if (prefirstMove)
            {
                prefirstMove = false;
                HidePreFirstMoveScreen();
                ShowAllGameElements();
            }
            AssignRandomItem(3, true);
        }
        if (Keyboard.current.digit5Key.wasPressedThisFrame)
        {
            if (prefirstMove)
            {
                prefirstMove = false;
                HidePreFirstMoveScreen();
                ShowAllGameElements();
            }
            AssignRandomItem(4, true);
        }

        // Player 2 controls (keys 6-9-0) using new Input System
        if (Keyboard.current.digit6Key.wasPressedThisFrame)
        {
            if (prefirstMove)
            {
                prefirstMove = false;
                HidePreFirstMoveScreen();
                ShowAllGameElements();
            }
            AssignRandomItem(0, false);
        }
        if (Keyboard.current.digit7Key.wasPressedThisFrame)
        {
            if (prefirstMove)
            {
                prefirstMove = false;
                HidePreFirstMoveScreen();
                ShowAllGameElements();
            }
            AssignRandomItem(1, false);
        }
        if (Keyboard.current.digit8Key.wasPressedThisFrame)
        {
            if (prefirstMove)
            {
                prefirstMove = false;
                HidePreFirstMoveScreen();
                ShowAllGameElements();
            }
            AssignRandomItem(2, false);
        }
        if (Keyboard.current.digit9Key.wasPressedThisFrame)
        {
            if (prefirstMove)
            {
                prefirstMove = false;
                HidePreFirstMoveScreen();
                ShowAllGameElements();
            }
            AssignRandomItem(3, false);
        }
        if (Keyboard.current.digit0Key.wasPressedThisFrame)
        {
            if (prefirstMove)
            {
                prefirstMove = false;
                HidePreFirstMoveScreen();
                ShowAllGameElements();
            }
            AssignRandomItem(4, false);
        }
    }

    private void AssignRandomItem(int slotIndex, bool isPlayer1)
    {
        if (availableCharacters == null || availableCharacters.Count == 0)
        {
            Debug.LogWarning("No available characters in the list! Make sure characterData.json is loaded.");
            return;
        }

        // Check if slot already has an item - prevent reassignment
        // Check by comparing current sprite with original sprite
        if (isPlayer1)
        {
            if (player1Slots[slotIndex] != null)
            {
                Sprite currentSprite = player1Slots[slotIndex].sprite;
                Sprite originalSprite = player1OriginalSprites[slotIndex];
                
                // If current sprite is different from original, slot is occupied
                if (currentSprite != originalSprite)
                {
                    Debug.Log($"Player 1 Slot {slotIndex + 1} already has an item. Cannot replace it.");
                    return;
                }
            }
        }
        else
        {
            if (player2Slots[slotIndex] != null)
            {
                Sprite currentSprite = player2Slots[slotIndex].sprite;
                Sprite originalSprite = player2OriginalSprites[slotIndex];
                
                // If current sprite is different from original, slot is occupied
                if (currentSprite != originalSprite)
                {
                    Debug.Log($"Player 2 Slot {slotIndex + 1} already has an item. Cannot replace it.");
                    return;
                }
            }
        }

        // Get available characters that haven't been assigned yet this round
        List<CharacterData> availableForSelection = new List<CharacterData>();
        foreach (CharacterData character in availableCharacters)
        {
            if (!assignedCharactersThisRound.Contains(character))
            {
                availableForSelection.Add(character);
            }
        }
        
        // If all characters have been assigned, reset the list (shouldn't happen with 10 slots and many characters, but safety check)
        if (availableForSelection.Count == 0)
        {
            Debug.LogWarning("All characters have been assigned! Resetting assigned list.");
            assignedCharactersThisRound.Clear();
            availableForSelection = new List<CharacterData>(availableCharacters);
        }
        
        // Get random character from available (unassigned) characters
        int randomIndex = UnityEngine.Random.Range(0, availableForSelection.Count);
        CharacterData randomCharacter = availableForSelection[randomIndex];
        
        // Add to assigned list to prevent duplicates
        assignedCharactersThisRound.Add(randomCharacter);

        // Update the inventory slot
        if (isPlayer1)
        {
            if (player1Slots[slotIndex] != null && randomCharacter.sprite != null)
            {
                SpriteRenderer slotRenderer = player1Slots[slotIndex];
                Sprite originalSprite = player1OriginalSprites[slotIndex];
                Vector3 originalScale = player1OriginalScales[slotIndex];
                
                // Store the original sprite's size
                Vector2 originalSpriteSize = originalSprite != null ? originalSprite.bounds.size : Vector2.one;
                
                // Assign the new sprite
                slotRenderer.sprite = randomCharacter.sprite;
                
                // Reset color to white so the image doesn't have a color overlay
                slotRenderer.color = Color.white;
                
                // Calculate scale adjustment to maintain the same visual size
                Vector3 finalScale = originalScale;
                if (randomCharacter.sprite != null)
                {
                    Vector2 newSpriteSize = randomCharacter.sprite.bounds.size;
                    if (newSpriteSize.x > 0 && newSpriteSize.y > 0 && originalSpriteSize.x > 0 && originalSpriteSize.y > 0)
                    {
                        Vector3 scaleAdjustment = new Vector3(
                            originalSpriteSize.x / newSpriteSize.x,
                            originalSpriteSize.y / newSpriteSize.y,
                            1f
                        );
                        finalScale = Vector3.Scale(originalScale, scaleAdjustment);
                    }
                }
                
                player1Inventory[slotIndex] = randomCharacter;
                
                // Update public variables
                switch (slotIndex)
                {
                    case 0: player1Item1 = randomCharacter; break;
                    case 1: player1Item2 = randomCharacter; break;
                    case 2: player1Item3 = randomCharacter; break;
                    case 3: player1Item4 = randomCharacter; break;
                    case 4: player1Item5 = randomCharacter; break;
                }
                
                // Start bounce animation
                StartCoroutine(AnimateChipBounce(slotRenderer, finalScale));
                
                // Update score display
                UpdateScoreDisplay(slotIndex, true, randomCharacter.powerScore);
                
                // Play chip placement sound
                PlayChipSound(randomCharacter);
                
                // Update creature description text
                UpdateCreatureDescription(randomCharacter);
                
                Debug.Log($"Player 1 Slot {slotIndex + 1}: Assigned {randomCharacter.name}");
                
                // Update opacity continuously for this slot
                UpdateSlotOpacity(slotIndex);
                
                // Check if all slots are filled
                CheckGameCompletion();
            }
        }
        else
        {
            if (player2Slots[slotIndex] != null && randomCharacter.sprite != null)
            {
                SpriteRenderer slotRenderer = player2Slots[slotIndex];
                Sprite originalSprite = player2OriginalSprites[slotIndex];
                Vector3 originalScale = player2OriginalScales[slotIndex];
                
                // Store the original sprite's size
                Vector2 originalSpriteSize = originalSprite != null ? originalSprite.bounds.size : Vector2.one;
                
                // Assign the new sprite
                slotRenderer.sprite = randomCharacter.sprite;
                
                // Reset color to white so the image doesn't have a color overlay
                slotRenderer.color = Color.white;
                
                // Calculate scale adjustment to maintain the same visual size
                Vector3 finalScale = originalScale;
                if (randomCharacter.sprite != null)
                {
                    Vector2 newSpriteSize = randomCharacter.sprite.bounds.size;
                    if (newSpriteSize.x > 0 && newSpriteSize.y > 0 && originalSpriteSize.x > 0 && originalSpriteSize.y > 0)
                    {
                        Vector3 scaleAdjustment = new Vector3(
                            originalSpriteSize.x / newSpriteSize.x,
                            originalSpriteSize.y / newSpriteSize.y,
                            1f
                        );
                        finalScale = Vector3.Scale(originalScale, scaleAdjustment);
                    }
                }
                
                player2Inventory[slotIndex] = randomCharacter;
                
                // Update public variables
                switch (slotIndex)
                {
                    case 0: player2Item1 = randomCharacter; break;
                    case 1: player2Item2 = randomCharacter; break;
                    case 2: player2Item3 = randomCharacter; break;
                    case 3: player2Item4 = randomCharacter; break;
                    case 4: player2Item5 = randomCharacter; break;
                }
                
                // Start bounce animation
                StartCoroutine(AnimateChipBounce(slotRenderer, finalScale));
                
                // Update score display
                UpdateScoreDisplay(slotIndex, false, randomCharacter.powerScore);
                
                // Play chip placement sound
                PlayChipSound(randomCharacter);
                
                // Update creature description text
                UpdateCreatureDescription(randomCharacter);
                
                Debug.Log($"Player 2 Slot {slotIndex + 1}: Assigned {randomCharacter.name}");
                
                // Update opacity continuously for this slot
                UpdateSlotOpacity(slotIndex);
                
                // Check if all slots are filled
                CheckGameCompletion();
            }
        }
    }
    
    private void CheckGameCompletion()
    {
        // Check if all slots are filled
        bool allPlayer1Filled = true;
        bool allPlayer2Filled = true;
        
        for (int i = 0; i < 5; i++)
        {
            if (player1Inventory[i] == null || player1Slots[i].sprite == player1OriginalSprites[i])
            {
                allPlayer1Filled = false;
            }
            if (player2Inventory[i] == null || player2Slots[i].sprite == player2OriginalSprites[i])
            {
                allPlayer2Filled = false;
            }
        }
        
        if (allPlayer1Filled && allPlayer2Filled)
        {
            // All slots filled, compare scores
            CompareScoresAndEndGame();
        }
    }
    
    private void CompareScoresAndEndGame()
    {
        // Compare each slot: Player 1 Inv {powerScore} vs Player 2 Inv {powerScore}
        player1Score = 0;
        player2Score = 0;
        
        // Reset slot wins
        for (int i = 0; i < 5; i++)
        {
            player1SlotWins[i] = false;
            player2SlotWins[i] = false;
        }
        
        for (int i = 0; i < 5; i++)
        {
            int p1Score = player1Inventory[i] != null ? player1Inventory[i].powerScore : 0;
            int p2Score = player2Inventory[i] != null ? player2Inventory[i].powerScore : 0;
            
            Debug.Log($"Slot {i + 1}: Player 1 Inv {p1Score} vs Player 2 Inv {p2Score}");
            
            if (p1Score > p2Score)
            {
                player1Score++;
                player1SlotWins[i] = true;
                Debug.Log($"Slot {i + 1}: Player 1 wins!");
            }
            else if (p2Score > p1Score)
            {
                player2Score++;
                player2SlotWins[i] = true;
                Debug.Log($"Slot {i + 1}: Player 2 wins!");
            }
            else
            {
                // Tie - both get a point
                player1Score++;
                player2Score++;
                player1SlotWins[i] = true;
                player2SlotWins[i] = true;
                Debug.Log($"Slot {i + 1}: Tie! Both get a point.");
            }
        }
        
        // Wait 3 seconds before showing win screen
        StartCoroutine(DelayedWinScreen());
    }
    
    private void UpdateSlotOpacity(int slotIndex)
    {
        // Only update opacity if BOTH players have chips in this slot
        bool p1HasChip = player1Inventory[slotIndex] != null && 
                        player1Slots[slotIndex] != null && 
                        player1Slots[slotIndex].sprite != player1OriginalSprites[slotIndex];
        
        bool p2HasChip = player2Inventory[slotIndex] != null && 
                        player2Slots[slotIndex] != null && 
                        player2Slots[slotIndex].sprite != player2OriginalSprites[slotIndex];
        
        // Only apply opacity if BOTH have chips
        if (!p1HasChip || !p2HasChip)
        {
            // Stop any active fade coroutines and reset to full opacity if one is missing
            if (player1FadeCoroutines.ContainsKey(slotIndex) && player1FadeCoroutines[slotIndex] != null)
            {
                StopCoroutine(player1FadeCoroutines[slotIndex]);
                player1FadeCoroutines.Remove(slotIndex);
            }
            if (player2FadeCoroutines.ContainsKey(slotIndex) && player2FadeCoroutines[slotIndex] != null)
            {
                StopCoroutine(player2FadeCoroutines[slotIndex]);
                player2FadeCoroutines.Remove(slotIndex);
            }
            
            if (player1Slots[slotIndex] != null)
            {
                Color color = player1Slots[slotIndex].color;
                color.a = 1.0f;
                player1Slots[slotIndex].color = color;
            }
            if (player2Slots[slotIndex] != null)
            {
                Color color = player2Slots[slotIndex].color;
                color.a = 1.0f;
                player2Slots[slotIndex].color = color;
            }
            return;
        }
        
        int p1Score = player1Inventory[slotIndex].powerScore;
        int p2Score = player2Inventory[slotIndex].powerScore;
        
        // Update Player 1 chip opacity with fade
        if (player1Slots[slotIndex] != null)
        {
            float targetAlpha = (p1Score < p2Score) ? 0.5f : 1.0f;
            
            // Stop existing fade coroutine if one is running
            if (player1FadeCoroutines.ContainsKey(slotIndex) && player1FadeCoroutines[slotIndex] != null)
            {
                StopCoroutine(player1FadeCoroutines[slotIndex]);
            }
            
            // Start new fade coroutine
            player1FadeCoroutines[slotIndex] = StartCoroutine(FadeOpacity(player1Slots[slotIndex], targetAlpha, slotIndex, true));
        }
        
        // Update Player 2 chip opacity with fade
        if (player2Slots[slotIndex] != null)
        {
            float targetAlpha = (p2Score < p1Score) ? 0.5f : 1.0f;
            
            // Stop existing fade coroutine if one is running
            if (player2FadeCoroutines.ContainsKey(slotIndex) && player2FadeCoroutines[slotIndex] != null)
            {
                StopCoroutine(player2FadeCoroutines[slotIndex]);
            }
            
            // Start new fade coroutine
            player2FadeCoroutines[slotIndex] = StartCoroutine(FadeOpacity(player2Slots[slotIndex], targetAlpha, slotIndex, false));
        }
    }
    
    private IEnumerator FadeOpacity(SpriteRenderer spriteRenderer, float targetAlpha, int slotIndex, bool isPlayer1)
    {
        if (spriteRenderer == null) yield break;
        
        Color startColor = spriteRenderer.color;
        float startAlpha = startColor.a;
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / fadeDuration);
            
            Color currentColor = spriteRenderer.color;
            currentColor.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            spriteRenderer.color = currentColor;
            
            yield return null;
        }
        
        // Ensure we end at exactly the target alpha
        Color finalColor = spriteRenderer.color;
        finalColor.a = targetAlpha;
        spriteRenderer.color = finalColor;
        
        // Remove coroutine from dictionary
        if (isPlayer1 && player1FadeCoroutines.ContainsKey(slotIndex))
        {
            player1FadeCoroutines.Remove(slotIndex);
        }
        else if (!isPlayer1 && player2FadeCoroutines.ContainsKey(slotIndex))
        {
            player2FadeCoroutines.Remove(slotIndex);
        }
    }
    
    private IEnumerator DelayedWinScreen()
    {
        yield return new WaitForSeconds(3f);
        
        // Play gong sound when round ends
        PlayGongSound();
        
        // Determine winner
        gameOver = true;
        ShowWinScreen();
    }
    
    private void ShowWinScreen()
    {
        // Hide all inventory items
        HideInventoryItems();
        
        // Create win screen
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("WinScreenCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        
        // Create win screen container
        winScreen = new GameObject("WinScreen");
        winScreen.transform.SetParent(canvas.transform, false);
        RectTransform winRect = winScreen.AddComponent<RectTransform>();
        winRect.anchorMin = Vector2.zero;
        winRect.anchorMax = Vector2.one;
        winRect.sizeDelta = Vector2.zero;
        winRect.anchoredPosition = Vector2.zero;
        
        // Create win text
        GameObject winTextObj = new GameObject("WinText");
        winTextObj.transform.SetParent(winScreen.transform, false);
        winText = winTextObj.AddComponent<TextMeshProUGUI>();
        RectTransform winTextRect = winText.rectTransform;
        winTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        winTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        winTextRect.pivot = new Vector2(0.5f, 0.5f);
        winTextRect.anchoredPosition = new Vector2(0, 50); // Moved down a little
        winTextRect.sizeDelta = new Vector2(800, 200); // Proper width and height
        winText.fontSize = 72;
        winText.alignment = TextAlignmentOptions.Center;
        winText.verticalAlignment = VerticalAlignmentOptions.Middle; // Center vertically within text box
        winText.fontStyle = FontStyles.Bold;
        winText.textWrappingMode = TextWrappingModes.NoWrap; // Prevent wrapping
        winText.overflowMode = TextOverflowModes.Overflow; // Allow overflow if needed
        
        // Determine winner text - color #FF7C00
        Color orangeColor = new Color(0xFF / 255f, 0x7C / 255f, 0x00 / 255f, 1f);
        if (player1Score > player2Score)
        {
            winText.text = "Player 1 Won";
            winText.color = orangeColor;
        }
        else if (player2Score > player1Score)
        {
            winText.text = "Player 2 Won";
            winText.color = orangeColor;
        }
        else
        {
            winText.text = "Tie!";
            winText.color = orangeColor;
        }
        
        // Create restart text - color #FF7C00
        GameObject restartTextObj = new GameObject("RestartText");
        restartTextObj.transform.SetParent(winScreen.transform, false);
        restartText = restartTextObj.AddComponent<TextMeshProUGUI>();
        RectTransform restartTextRect = restartText.rectTransform;
        restartTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        restartTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        restartTextRect.pivot = new Vector2(0.5f, 0.5f);
        restartTextRect.anchoredPosition = new Vector2(0, -100); // More spacing from win text
        restartTextRect.sizeDelta = new Vector2(600, 100); // Proper width and height
        restartText.fontSize = 36;
        restartText.alignment = TextAlignmentOptions.Center;
        restartText.text = "Press Space to restart";
        restartText.color = orangeColor;
        restartText.textWrappingMode = TextWrappingModes.NoWrap;
        restartText.overflowMode = TextOverflowModes.Overflow;
    }
    
    private void HideInventoryItems()
    {
        // Hide all player slots
        foreach (SpriteRenderer slot in player1Slots)
        {
            if (slot != null) slot.gameObject.SetActive(false);
        }
        foreach (SpriteRenderer slot in player2Slots)
        {
            if (slot != null) slot.gameObject.SetActive(false);
        }
        
        // Hide all score displays (using scoreTexts since squares are removed)
        for (int i = 0; i < 5; i++)
        {
            if (player1ScoreTexts[i] != null && player1ScoreTexts[i].transform.parent != null)
            {
                player1ScoreTexts[i].transform.parent.gameObject.SetActive(false);
            }
            if (player2ScoreTexts[i] != null && player2ScoreTexts[i].transform.parent != null)
            {
                player2ScoreTexts[i].transform.parent.gameObject.SetActive(false);
            }
        }
        
        // Hide all UI text elements except win screen texts and background
        TextMeshProUGUI[] allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
        foreach (TextMeshProUGUI text in allTexts)
        {
            // Don't hide our win screen texts
            if (text != winText && text != restartText && text.gameObject.name != "WinText" && text.gameObject.name != "RestartText")
            {
                // Hide all other text elements (including title, player labels, etc.)
                text.gameObject.SetActive(false);
            }
        }
        
        // Hide all SpriteRenderers except background (check by name or tag)
        SpriteRenderer[] allSprites = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (SpriteRenderer sprite in allSprites)
        {
            // Skip if it's part of win screen or if it's a background
            if (sprite.gameObject.name.Contains("WinScreen") || 
                sprite.gameObject.name.Contains("Background") || 
                sprite.gameObject.name.Contains("BG") ||
                sprite.gameObject.name.Contains("bg"))
            {
                continue; // Keep background visible
            }
            
            // Hide all other sprites
            sprite.gameObject.SetActive(false);
        }
        
        // Hide Canvas children except background and win screen
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            foreach (Transform child in canvas.transform)
            {
                // Keep win screen and background
                if (child.name.Contains("WinScreen") || 
                    child.name.Contains("Background") || 
                    child.name.Contains("BG") ||
                    child.name.Contains("bg"))
                {
                    continue; // Keep visible
                }
                
                // Hide everything else
                child.gameObject.SetActive(false);
            }
        }
    }
    
    private void RestartGame()
    {
        // Reset assigned characters list for new round
        assignedCharactersThisRound.Clear();
        
        // Literally restart the scene for a fresh start
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    private void UpdateScoreDisplay(int slotIndex, bool isPlayer1, int scoreAmount)
    {
        TextMeshProUGUI scoreText = isPlayer1 ? player1ScoreTexts[slotIndex] : player2ScoreTexts[slotIndex];
        
        // Create score display if it doesn't exist yet
        if (scoreText == null)
        {
            SpriteRenderer slotRenderer = isPlayer1 ? player1Slots[slotIndex] : player2Slots[slotIndex];
            if (slotRenderer != null)
            {
                Color squareColor = new Color(0xFF / 255f, 0x7C / 255f, 0x00 / 255f, 1f);
                Color textColor = new Color(0xFF / 255f, 0x7C / 255f, 0x00 / 255f, 1f);
                CreateScoreDisplayForSlot(slotRenderer, slotIndex, isPlayer1, squareColor, textColor);
                scoreText = isPlayer1 ? player1ScoreTexts[slotIndex] : player2ScoreTexts[slotIndex];
            }
        }
        
        // Activate the parent if scoreText exists
        if (scoreText != null && scoreText.transform.parent != null)
        {
            scoreText.transform.parent.gameObject.SetActive(true);
        }
        
        if (scoreText != null)
        {
            scoreText.text = scoreAmount.ToString();
            
            // Update all outline text objects too
            string[] outlineNames = { "Outline1", "Outline2", "Outline3", "Outline4", "Outline5", "Outline6", "Outline7", "Outline8" };
            foreach (string outlineName in outlineNames)
            {
                Transform outlineTransform = scoreText.transform.parent.Find(outlineName);
                if (outlineTransform != null)
                {
                    TextMeshProUGUI outlineText = outlineTransform.GetComponent<TextMeshProUGUI>();
                    if (outlineText != null)
                    {
                        outlineText.text = scoreAmount.ToString();
                    }
                }
            }
        }
    }
    
    private void UpdateCreatureDescription(CharacterData character)
    {
        if (creatureDescriptionText != null && character != null)
        {
            if (!string.IsNullOrEmpty(character.creatureDescription))
            {
                string description = character.creatureDescription;
                
                // Replace first "Its " with "{name}'s " or first "It " with "{name} "
                if (!string.IsNullOrEmpty(character.name))
                {
                    // Check if it starts with "Its " (with space)
                    if (description.StartsWith("Its ", StringComparison.OrdinalIgnoreCase))
                    {
                        description = character.name + "'s " + description.Substring(4); // Remove "Its " (4 chars)
                    }
                    // Check if it starts with "It " (with space)
                    else if (description.StartsWith("It ", StringComparison.OrdinalIgnoreCase))
                    {
                        description = character.name + " " + description.Substring(3); // Remove "It " (3 chars)
                    }
                }
                
                creatureDescriptionText.text = description;
            }
            else
            {
                creatureDescriptionText.text = "";
            }
        }
    }
    
    private void PlayChipSound(CharacterData character = null)
    {
        if (audioSource == null) return;
        
        // Play the base chip placement sound
        if (chipPlaceSound != null)
        {
            // Random pitch/speed between 1x and 3x
            float randomPitch = UnityEngine.Random.Range(1f, 3f);
            
            // Store original pitch
            float originalPitch = audioSource.pitch;
            
            // Set random pitch
            audioSource.pitch = randomPitch;
            
            // Play the sound
            audioSource.PlayOneShot(chipPlaceSound);
            
            // Restore original pitch (PlayOneShot doesn't block, so we restore immediately)
            audioSource.pitch = originalPitch;
        }
        
        // Also play a random character audio if available
        if (character != null && character.audioClips != null && character.audioClips.Length > 0)
        {
            // Pick a random audio clip from the character's audio clips
            int randomAudioIndex = UnityEngine.Random.Range(0, character.audioClips.Length);
            AudioClip characterAudio = character.audioClips[randomAudioIndex];
            
            if (characterAudio != null)
            {
                // Play at normal pitch (or you could randomize this too)
                audioSource.PlayOneShot(characterAudio);
            }
        }
    }
    
    private IEnumerator AnimateChipBounce(SpriteRenderer spriteRenderer, Vector3 finalScale)
    {
        if (spriteRenderer == null) yield break;
        
        const float animationDuration = 0.3f;
        const float startScale = 1.5f;
        const float endScale = 1.0f;
        const float startAlpha = 0.0f;
        const float endAlpha = 1.0f;
        
        // Create drop shadow
        GameObject shadowObject = new GameObject("DropShadow");
        shadowObject.transform.SetParent(spriteRenderer.transform);
        shadowObject.transform.localPosition = new Vector3(0.1f, -0.1f, 0.1f); // Offset down and right
        shadowObject.transform.localRotation = Quaternion.identity;
        shadowObject.transform.localScale = Vector3.one;
        
        SpriteRenderer shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = spriteRenderer.sprite;
        shadowRenderer.color = new Color(0, 0, 0, 0.5f); // Black with 50% opacity
        shadowRenderer.sortingOrder = spriteRenderer.sortingOrder - 1; // Render behind main sprite
        
        // Set initial state
        Vector3 startScaleVector = finalScale * startScale;
        spriteRenderer.transform.localScale = startScaleVector;
        shadowObject.transform.localScale = startScaleVector; // Match initial scale
        Color startColor = spriteRenderer.color;
        startColor.a = startAlpha;
        spriteRenderer.color = startColor;
        
        // Shadow starts visible, fades to invisible
        const float shadowStartAlpha = 0.5f;
        const float shadowEndAlpha = 0.0f;
        Color shadowColor = shadowRenderer.color;
        shadowColor.a = shadowStartAlpha;
        shadowRenderer.color = shadowColor;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / animationDuration);
            
            // Apply ease-in for scale, ease-out for opacity
            float scaleT = EaseIn(t);
            float opacityT = EaseOut(t);
            
            // Interpolate scale with ease-in (both main sprite and shadow)
            float currentScale = Mathf.Lerp(startScale, endScale, scaleT);
            spriteRenderer.transform.localScale = finalScale * currentScale;
            shadowObject.transform.localScale = finalScale * currentScale;
            
            // Interpolate opacity with ease-out
            float currentAlpha = Mathf.Lerp(startAlpha, endAlpha, opacityT);
            Color currentColor = spriteRenderer.color;
            currentColor.a = currentAlpha;
            spriteRenderer.color = currentColor;
            
            // Animate shadow opacity with ease-in (same as scale) - fades away
            float shadowAlpha = Mathf.Lerp(shadowStartAlpha, shadowEndAlpha, scaleT);
            Color currentShadowColor = shadowRenderer.color;
            currentShadowColor.a = shadowAlpha;
            shadowRenderer.color = currentShadowColor;
            
            yield return null;
        }
        
        // Ensure final state
        spriteRenderer.transform.localScale = finalScale;
        Color finalColor = spriteRenderer.color;
        finalColor.a = endAlpha;
        spriteRenderer.color = finalColor;
        
        // Remove shadow after animation
        if (shadowObject != null)
        {
            UnityEngine.Object.Destroy(shadowObject);
        }
    }
    
    private float EaseIn(float t)
    {
        // Cubic ease-in: t^3 for smooth acceleration
        return t * t * t;
    }
    
    private float EaseOut(float t)
    {
        // Cubic ease-out: 1 - (1-t)^3 for smooth deceleration
        float invT = 1f - t;
        return 1f - (invT * invT * invT);
    }
    
    private void PlayGongSound()
    {
        if (audioSource != null && gongSound != null)
        {
            // Play gong sound at normal pitch
            audioSource.PlayOneShot(gongSound);
        }
    }
    
    private void PlayClickSound()
    {
        if (audioSource != null && clickSound != null)
        {
            // Play click sound at normal pitch
            audioSource.PlayOneShot(clickSound);
        }
    }
    
    private void ShowPreFirstMoveScreen()
    {
        // Hide all inventory items
        HideInventoryItemsForPreFirstMove();
        
        // Clear previous tile images
        preFirstMoveTileImages.Clear();
        
        // Create or find canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("PreFirstMoveCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        
        // Create pre-first move screen container
        preFirstMoveScreen = new GameObject("PreFirstMoveScreen");
        preFirstMoveScreen.transform.SetParent(canvas.transform, false);
        RectTransform screenRect = preFirstMoveScreen.AddComponent<RectTransform>();
        screenRect.anchorMin = Vector2.zero;
        screenRect.anchorMax = Vector2.one;
        screenRect.sizeDelta = Vector2.zero;
        screenRect.anchoredPosition = Vector2.zero;
        
        // Get first 40 characters
        int tileCount = Mathf.Min(40, availableCharacters.Count);
        const int columns = 10;
        const float tileSize = 80f; // Size of each tile image
        const float spacing = 10f; // Spacing between tiles
        const float textRowHeight = 60f; // Height of the text row
        const float edgeOffset = 4f; // Distance from screen edges (reduced to make room for middle row)
        
        // Get screen height in canvas space
        float screenHeight = Screen.height;
        
        // Calculate grid dimensions
        float gridWidth = (columns * tileSize) + ((columns - 1) * spacing);
        
        // X position calculation (centered horizontally)
        float startX = -(gridWidth / 2f) + (tileSize / 2f);
        
        // Absolute Y positions from screen edges
        // Top rows: positioned from top of screen downwards
        // Bottom rows: positioned from bottom of screen upwards
        
        // Create tiles grid: 2 rows, text row, 2 rows
        int tileIndex = 0;
        Color orangeColor = new Color(0xFF / 255f, 0x7C / 255f, 0x00 / 255f, 1f);
        
        // First 2 rows (top 20 tiles) - absolute from top
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < columns && tileIndex < tileCount; col++)
            {
                float x = startX + (col * (tileSize + spacing));
                CreateTileImageAbsolute(preFirstMoveScreen.transform, availableCharacters[tileIndex], x, row, true, tileSize, spacing, edgeOffset);
                tileIndex++;
            }
        }
        
        // Middle row - 3 tiles on each side (always CardBack), 4 empty spots in middle for text
        // Left side tiles (columns 0, 1, 2)
        for (int col = 0; col < 3; col++)
        {
            float x = startX + (col * (tileSize + spacing));
            CreateStaticCardBackTile(preFirstMoveScreen.transform, x, tileSize);
        }
        
        // Right side tiles (columns 7, 8, 9)
        for (int col = 7; col < 10; col++)
        {
            float x = startX + (col * (tileSize + spacing));
            CreateStaticCardBackTile(preFirstMoveScreen.transform, x, tileSize);
        }
        
        // Text row (middle) - centered vertically
        GameObject textObj = new GameObject("PreFirstMoveText");
        textObj.transform.SetParent(preFirstMoveScreen.transform, false);
        preFirstMoveText = textObj.AddComponent<TextMeshProUGUI>();
        RectTransform textRect = preFirstMoveText.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0, 0); // Center vertically
        textRect.sizeDelta = new Vector2(gridWidth, textRowHeight);
        preFirstMoveText.fontSize = 36f; // Smaller than before (was 72)
        preFirstMoveText.alignment = TextAlignmentOptions.Center;
        preFirstMoveText.verticalAlignment = VerticalAlignmentOptions.Middle;
        preFirstMoveText.fontStyle = FontStyles.Bold;
        preFirstMoveText.textWrappingMode = TextWrappingModes.NoWrap;
        preFirstMoveText.overflowMode = TextOverflowModes.Overflow;
        preFirstMoveText.color = orangeColor;
        preFirstMoveText.text = "Select Your Tiles";
        
        // Last 2 rows (bottom 20 tiles) - absolute from bottom
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < columns && tileIndex < tileCount; col++)
            {
                float x = startX + (col * (tileSize + spacing));
                CreateTileImageAbsolute(preFirstMoveScreen.transform, availableCharacters[tileIndex], x, row, false, tileSize, spacing, edgeOffset);
                tileIndex++;
            }
        }
    }
    
    private void CreateTileImageAbsolute(Transform parent, CharacterData character, float x, int row, bool isTopSection, float tileSize, float spacing, float edgeOffset)
    {
        GameObject tileObj = new GameObject($"Tile_{character.name}");
        tileObj.transform.SetParent(parent, false);
        RectTransform tileRect = tileObj.AddComponent<RectTransform>();
        
        if (isTopSection)
        {
            // Anchor to top center
            tileRect.anchorMin = new Vector2(0.5f, 1f);
            tileRect.anchorMax = new Vector2(0.5f, 1f);
            tileRect.pivot = new Vector2(0.5f, 1f); // Pivot at top of tile
            
            // Position from top downwards
            float y = -(edgeOffset + (row * (tileSize + spacing)));
            tileRect.anchoredPosition = new Vector2(x, y);
        }
        else
        {
            // Anchor to bottom center
            tileRect.anchorMin = new Vector2(0.5f, 0f);
            tileRect.anchorMax = new Vector2(0.5f, 0f);
            tileRect.pivot = new Vector2(0.5f, 0f); // Pivot at bottom of tile
            
            // Position from bottom upwards (row 0 is closest to bottom, row 1 is above it)
            float y = edgeOffset + ((1 - row) * (tileSize + spacing));
            tileRect.anchoredPosition = new Vector2(x, y);
        }
        
        tileRect.sizeDelta = new Vector2(tileSize, tileSize);
        
        Image tileImage = tileObj.AddComponent<Image>();
        tileImage.preserveAspect = true;
        
        // Initialize flip data
        TileFlipData flipData = new TileFlipData
        {
            isFlipped = false, // Start with card back showing
            characterSprite = character.sprite,
            imageComponent = tileImage
        };
        
        // Set initial sprite to card back
        tileImage.sprite = cardBackSprite;
        
        tileFlipDataMap[tileObj] = flipData;
        preFirstMoveTileImages.Add(tileObj);
    }
    
    private void CreateStaticCardBackTile(Transform parent, float x, float tileSize)
    {
        GameObject tileObj = new GameObject("StaticCardBack");
        tileObj.transform.SetParent(parent, false);
        RectTransform tileRect = tileObj.AddComponent<RectTransform>();
        
        // Anchor to center (middle row)
        tileRect.anchorMin = new Vector2(0.5f, 0.5f);
        tileRect.anchorMax = new Vector2(0.5f, 0.5f);
        tileRect.pivot = new Vector2(0.5f, 0.5f);
        
        // Position centered vertically, at the x position
        tileRect.anchoredPosition = new Vector2(x, 0);
        tileRect.sizeDelta = new Vector2(tileSize, tileSize);
        
        Image tileImage = tileObj.AddComponent<Image>();
        tileImage.preserveAspect = true;
        tileImage.sprite = cardBackSprite;
        
        // Note: Do NOT add this to preFirstMoveTileImages or tileFlipDataMap
        // These tiles are static and should never flip
    }
    
    private IEnumerator StartTileFlipping()
    {
        // Group tiles randomly for synchronized flipping
        // Create 4 groups of 10 tiles each
        List<GameObject> tilesToFlip = new List<GameObject>(preFirstMoveTileImages);
        
        // Shuffle tiles
        for (int i = 0; i < tilesToFlip.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, tilesToFlip.Count);
            GameObject temp = tilesToFlip[i];
            tilesToFlip[i] = tilesToFlip[randomIndex];
            tilesToFlip[randomIndex] = temp;
        }
        
        // Create 4 groups with 10 tiles each
        List<List<GameObject>> groups = new List<List<GameObject>>();
        const int numGroups = 4;
        int tilesPerGroup = tilesToFlip.Count / numGroups;
        
        for (int groupIndex = 0; groupIndex < numGroups; groupIndex++)
        {
            List<GameObject> group = new List<GameObject>();
            int startIndex = groupIndex * tilesPerGroup;
            int endIndex = (groupIndex == numGroups - 1) ? tilesToFlip.Count : startIndex + tilesPerGroup;
            
            for (int i = startIndex; i < endIndex; i++)
            {
                group.Add(tilesToFlip[i]);
            }
            groups.Add(group);
        }
        
        // Coordinate flipping so only one group is flipped at a time
        const float showDuration = 4f; // 4 seconds showing faces
        const float flipDuration = 0.5f; // 0.5 seconds for flip transition
        
        // Start with first group flipping to faces
        int currentGroupIndex = 0;
        
        // Initial flip of first group
        PlayClickSound();
        foreach (GameObject tile in groups[currentGroupIndex])
        {
            if (tile != null && tileFlipDataMap.ContainsKey(tile))
            {
                TileFlipData flipData = tileFlipDataMap[tile];
                if (!flipData.isFlipped)
                {
                    StartCoroutine(AnimateFlip(tile, flipData, flipDuration, true));
                }
            }
        }
        
        // Wait for initial flip to complete
        yield return new WaitForSeconds(flipDuration);
        
        while (true)
        {
            // Check if we should stop flipping (first tile placed or game over)
            if (!prefirstMove || gameOver)
            {
                yield break; // Exit the coroutine
            }
            
            // Show faces for 4 seconds
            yield return new WaitForSeconds(showDuration);
            
            // Check again after waiting
            if (!prefirstMove || gameOver)
            {
                yield break;
            }
            
            // Get next group index (loop back to 0 after last group)
            int nextGroupIndex = (currentGroupIndex + 1) % groups.Count;
            
            // Play click sound for the simultaneous flip
            PlayClickSound();
            
            // Simultaneously: flip current group back to card backs AND flip next group to faces
            // Flip current group back
            foreach (GameObject tile in groups[currentGroupIndex])
            {
                if (tile != null && tileFlipDataMap.ContainsKey(tile))
                {
                    TileFlipData flipData = tileFlipDataMap[tile];
                    if (flipData.isFlipped)
                    {
                        StartCoroutine(AnimateFlip(tile, flipData, flipDuration, false));
                    }
                }
            }
            
            // Flip next group to faces (at the same time)
            foreach (GameObject tile in groups[nextGroupIndex])
            {
                if (tile != null && tileFlipDataMap.ContainsKey(tile))
                {
                    TileFlipData flipData = tileFlipDataMap[tile];
                    if (!flipData.isFlipped)
                    {
                        StartCoroutine(AnimateFlip(tile, flipData, flipDuration, true));
                    }
                }
            }
            
            // Wait for flip animations to complete
            yield return new WaitForSeconds(flipDuration);
            
            // Move to next group
            currentGroupIndex = nextGroupIndex;
        }
    }
    
    private IEnumerator AnimateFlip(GameObject tile, TileFlipData flipData, float duration, bool flipToCharacter)
    {
        if (tile == null || flipData.imageComponent == null) yield break;
        
        RectTransform rectTransform = tile.GetComponent<RectTransform>();
        Vector3 originalScale = rectTransform.localScale;
        
        // First half: scale X from 1 to 0 (flip away)
        float elapsed = 0f;
        while (elapsed < duration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration / 2f);
            float scaleX = Mathf.Lerp(1f, 0f, t);
            rectTransform.localScale = new Vector3(scaleX * Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
            yield return null;
        }
        
        // Change sprite at midpoint based on parameter
        if (flipToCharacter)
        {
            // Flip to show character face
            flipData.imageComponent.sprite = flipData.characterSprite;
            flipData.isFlipped = true;
        }
        else
        {
            // Flip to show card back
            flipData.imageComponent.sprite = cardBackSprite;
            flipData.isFlipped = false;
        }
        
        // Second half: scale X from 0 to 1 (flip towards)
        elapsed = 0f;
        while (elapsed < duration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration / 2f);
            float scaleX = Mathf.Lerp(0f, 1f, t);
            rectTransform.localScale = new Vector3(scaleX * Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
            yield return null;
        }
        
        // Ensure final scale is correct
        rectTransform.localScale = originalScale;
    }
    
    private void HidePreFirstMoveScreen()
    {
        // Stop the tile flipping coroutine if it's running
        if (tileFlippingCoroutine != null)
        {
            StopCoroutine(tileFlippingCoroutine);
            tileFlippingCoroutine = null;
        }
        
        // Clean up tile images
        foreach (GameObject tileImage in preFirstMoveTileImages)
        {
            if (tileImage != null)
            {
                UnityEngine.Object.Destroy(tileImage);
            }
        }
        preFirstMoveTileImages.Clear();
        
        if (preFirstMoveScreen != null)
        {
            UnityEngine.Object.Destroy(preFirstMoveScreen);
            preFirstMoveScreen = null;
            preFirstMoveText = null;
        }
    }
    
    private void HideInventoryItemsForPreFirstMove()
    {
        // Hide all player slots
        foreach (SpriteRenderer slot in player1Slots)
        {
            if (slot != null) slot.gameObject.SetActive(false);
        }
        foreach (SpriteRenderer slot in player2Slots)
        {
            if (slot != null) slot.gameObject.SetActive(false);
        }
        
        // Hide all score displays
        for (int i = 0; i < 5; i++)
        {
            if (player1ScoreTexts[i] != null && player1ScoreTexts[i].transform.parent != null)
            {
                player1ScoreTexts[i].transform.parent.gameObject.SetActive(false);
            }
            if (player2ScoreTexts[i] != null && player2ScoreTexts[i].transform.parent != null)
            {
                player2ScoreTexts[i].transform.parent.gameObject.SetActive(false);
            }
        }
        
        // Hide all UI text elements except background and pre-first move text
        TextMeshProUGUI[] allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
        foreach (TextMeshProUGUI text in allTexts)
        {
            // Don't hide our pre-first move text
            if (text != preFirstMoveText && text.gameObject.name != "PreFirstMoveText")
            {
                text.gameObject.SetActive(false);
            }
        }
        
        // Hide all SpriteRenderers except background
        SpriteRenderer[] allSprites = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (SpriteRenderer sprite in allSprites)
        {
            // Skip if it's a background
            if (sprite.gameObject.name.Contains("Background") || 
                sprite.gameObject.name.Contains("BG") ||
                sprite.gameObject.name.Contains("bg"))
            {
                continue; // Keep background visible
            }
            
            // Hide all other sprites
            sprite.gameObject.SetActive(false);
        }
        
        // Hide Canvas children except background and pre-first move screen
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            foreach (Transform child in canvas.transform)
            {
                // Keep pre-first move screen and background
                if (child.name.Contains("PreFirstMoveScreen") || 
                    child.name.Contains("Background") || 
                    child.name.Contains("BG") ||
                    child.name.Contains("bg"))
                {
                    continue; // Keep visible
                }
                
                // Hide everything else
                child.gameObject.SetActive(false);
            }
        }
    }
    
    private void ShowAllGameElements()
    {
        // First, show Canvas children (parent objects) except pre-first move screen
        // This ensures parent GameObjects are active, which makes their child text elements accessible
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            foreach (Transform child in canvas.transform)
            {
                // Hide pre-first move screen
                if (child.name.Contains("PreFirstMoveScreen"))
                {
                    child.gameObject.SetActive(false);
                    continue;
                }
                
                // Show everything else (this includes Player 1, Player 2, and creature description text containers)
                child.gameObject.SetActive(true);
                
                // Also activate all TextMeshProUGUI components in this child (to ensure text elements are visible)
                TextMeshProUGUI[] textComponents = child.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (TextMeshProUGUI text in textComponents)
                {
                    if (text != null && text.gameObject != null)
                    {
                        text.gameObject.SetActive(true);
                    }
                }
            }
        }
        
        // Show all player slots
        foreach (SpriteRenderer slot in player1Slots)
        {
            if (slot != null) slot.gameObject.SetActive(true);
        }
        foreach (SpriteRenderer slot in player2Slots)
        {
            if (slot != null) slot.gameObject.SetActive(true);
        }
        
        // Show all UI text elements - including Player 1, Player 2, and creature description text
        TextMeshProUGUI[] allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
        foreach (TextMeshProUGUI text in allTexts)
        {
            // Don't show pre-first move text
            if (text != preFirstMoveText && text.gameObject != null && text.gameObject.name != "PreFirstMoveText")
            {
                text.gameObject.SetActive(true);
            }
        }
        
        // Show all SpriteRenderers (background is already visible)
        SpriteRenderer[] allSprites = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (SpriteRenderer sprite in allSprites)
        {
            if (sprite != null && sprite.gameObject != null)
            {
                sprite.gameObject.SetActive(true);
            }
        }
    }
}



