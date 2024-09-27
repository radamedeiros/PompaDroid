using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public LifeBar enemylifeBar;
    public GameObject goIndicator;

    public Hero player1;
    public Hero player2;
    // Changed from single prefab to a list of prefabs
    public List<GameObject> heroPrefabs;
    public bool cameraFollows = true;
    public CameraBounds cameraBounds;

    public LevelData currentLevelData;
    private BattleEvent currentBattleEvent;
    private int nextEventIndex;
    public bool hasRemainingEvents;

    public List<GameObject> activeEnemies;
    public Transform[] spawnPositions;

    public GameObject currentLevelBackground;

    public GameObject robotPrefab;
    public GameObject bossPrefab;

    // References to prefabs of the level and game over texts.
    public GameObject levelNamePrefab;
    public GameObject gameOverPrefab;

    // Parent transform of all UI elements
    public RectTransform uiTransform;

    public GameObject loadingScreen;

    // For hero entrance
    public Transform walkInStartTarget;
    public Transform walkInTarget;

    // For exit on level completion
    public Transform walkOutTarget;

    // For loading and keeping track of levels
    public LevelData[] levels;
    public static int CurrentLevel = 0;

    // Variable to determine if the game is multiplayer or singleplayer
    public bool isMultiplayer = true;

    // Variables to define control schemes for each player
    public enum ControlScheme
    {
        Keyboard,
        Gamepad
    }

    public ControlScheme player1ControlScheme = ControlScheme.Keyboard;
    public ControlScheme player2ControlScheme = ControlScheme.Gamepad;


    public ControlScheme Player1ControlScheme
    {
        get { return player1ControlScheme; }
        set
        {
            if (player1ControlScheme != value)
            {
                player1ControlScheme = value;
                if (player1 != null)
                    AssignControlScheme(player1, player1ControlScheme);
            }
        }
    }

    public ControlScheme Player2ControlScheme
    {
        get { return player2ControlScheme; }
        set
        {
            if (player2ControlScheme != value)
            {
               player2ControlScheme = value;
                if (player2 != null)
                    AssignControlScheme(player2, player2ControlScheme);
            }
        }
    }

    private void Awake()
    {
        loadingScreen.SetActive(true);
    }

    void Start()
    {
        // Randomly select hero prefabs for player1
        GameObject player1Prefab = null;
        GameObject player2Prefab = null;

        // Copy heroPrefabs to a new list to avoid modifying the original
        List<GameObject> availablePrefabs = new List<GameObject>(heroPrefabs);
        int player1Index = Random.Range(0, availablePrefabs.Count);
        player1Prefab = availablePrefabs[player1Index];
        availablePrefabs.RemoveAt(player1Index);

        // Instantiate player 1
        player1 = Instantiate(player1Prefab).GetComponent<Hero>();
        player1.playerId = 1;
        player1.name = "Player1";
        // Assign control scheme to player1
        AssignControlScheme(player1, Player1ControlScheme);

        if (isMultiplayer)
        {
            // Ensure that there is at least one prefab left
            if (availablePrefabs.Count == 0)
            {
                availablePrefabs = new List<GameObject>(heroPrefabs);
                availablePrefabs.Remove(player1Prefab);
            }

            int player2Index = Random.Range(0, availablePrefabs.Count);
            player2Prefab = availablePrefabs[player2Index];

            // Instantiate player 2
            player2 = Instantiate(player2Prefab).GetComponent<Hero>();
            player2.playerId = 2;
            player2.name = "Player2";
            // Assign control scheme to player2
            AssignControlScheme(player2, Player2ControlScheme);
        }

        nextEventIndex = 0;
        StartCoroutine(LoadLevelData(levels[CurrentLevel]));
        cameraBounds.SetXPosition(cameraBounds.minVisibleX);
    }

    private void AssignControlScheme(Hero player, ControlScheme controlScheme)
    {
        string schemeName = controlScheme == ControlScheme.Keyboard ? "Keyboard" : "Gamepad";
        player.SetControlScheme(schemeName);

        // Assign device
        InputDevice device = null;
        if (controlScheme == ControlScheme.Keyboard)
        {
            device = Keyboard.current;
        }
        else if (controlScheme == ControlScheme.Gamepad)
        {
            device = Gamepad.current;
        }

        player.SetDevice(device);
    }

    void Update()
    {
        if (currentBattleEvent == null && hasRemainingEvents)
        {
            if (Mathf.Abs(currentLevelData.battleData[nextEventIndex].column -
                          cameraBounds.activeCamera.transform.position.x) < 0.2f)
            {
                PlayBattleEvent(currentLevelData.battleData[nextEventIndex]);
            }
        }

        if (currentBattleEvent != null)
        {
            // Has event, check if enemies are alive
            if (Robot.TotalEnemies == 0)
                CompleteCurrentEvent();
        }

        if (cameraFollows)
        {
            float averageX = player1.transform.position.x;
            if (isMultiplayer && player2 != null)
            {
                // Follow the average position between player1 and player2
                averageX = (player1.transform.position.x + player2.transform.position.x) / 2;
            }
            cameraBounds.SetXPosition(averageX);
        }
    }

    private GameObject SpawnEnemy(EnemyData data)
    {
        // Create a new GameObject from the prefab.
        GameObject enemyObj;
        if (data.type == EnemyType.Boss)
            enemyObj = Instantiate(bossPrefab);
        else
            enemyObj = Instantiate(robotPrefab);

        Vector3 position = spawnPositions[data.row].position;
        position.x = cameraBounds.activeCamera.transform.position.x +
            (data.offset * (cameraBounds.cameraHalfWidth + 1));
        enemyObj.transform.position = position;

        if (data.type == EnemyType.Robot)
            enemyObj.GetComponent<Robot>().SetColor(data.color);

        enemyObj.GetComponent<Enemy>().RegisterEnemy();

        return enemyObj;
    }

    private void PlayBattleEvent(BattleEvent battleEventData)
    {
        currentBattleEvent = battleEventData;
        nextEventIndex++;

        cameraFollows = false;
        cameraBounds.SetXPosition(battleEventData.column);

        // Clear previous enemies
        activeEnemies.Clear();
        Enemy.TotalEnemies = 0;

        foreach (EnemyData enemyData in currentBattleEvent.enemies)
            activeEnemies.Add(SpawnEnemy(enemyData));
    }

    private void CompleteCurrentEvent()
    {
        currentBattleEvent = null;

        cameraFollows = true;
        float cameraTargetX = player1.transform.position.x;
        if (isMultiplayer && player2 != null)
        {
            cameraTargetX = (player1.transform.position.x + player2.transform.position.x) / 2;
        }
        cameraBounds.CalculateOffset(cameraTargetX);
        hasRemainingEvents = currentLevelData.battleData.Count > nextEventIndex;

        enemylifeBar.EnableLifeBar(false);

        // With no more battle events, heroes will walk off screen
        if (!hasRemainingEvents)
            StartCoroutine(HeroesWalkout());
        else
            ShowGoIndicator();
    }

    private IEnumerator LoadLevelData(LevelData data)
    {
        cameraFollows = false;
        currentLevelData = data;

        hasRemainingEvents = currentLevelData.battleData.Count > 0;
        activeEnemies = new List<GameObject>();

        // Pauses the method for one frame
        yield return null;
        cameraBounds.SetXPosition(cameraBounds.minVisibleX);

        // Destroys old level before loading new level
        if (currentLevelBackground != null)
            Destroy(currentLevelBackground);
        currentLevelBackground = Instantiate(currentLevelData.levelPrefab);

        cameraBounds.EnableBounds(false);
        player1.transform.position = walkInStartTarget.transform.position;
        if (isMultiplayer && player2 != null)
        {
            player1.transform.position += Vector3.left * 1.0f;
            player2.transform.position = walkInStartTarget.transform.position + Vector3.right * 1.0f;
        }

        yield return new WaitForSeconds(0.1f);

        player1.UseAutopilot(true);
        if (isMultiplayer && player2 != null)
        {
            player1.AnimateTo(walkInTarget.transform.position + Vector3.left * 1.0f, false, DidFinishIntro);
            player2.UseAutopilot(true);
            player2.AnimateTo(walkInTarget.transform.position + Vector3.right * 1.0f, false, null);
        }
        else
        {
            player1.AnimateTo(walkInTarget.transform.position, false, DidFinishIntro);
        }

        cameraFollows = true;

        ShowTextBanner(currentLevelData.levelName);

        loadingScreen.SetActive(false);
    }

    private void DidFinishIntro()
    {
        player1.UseAutopilot(false);
        player1.controllable = true;

        if (isMultiplayer && player2 != null)
        {
            player2.UseAutopilot(false);
            player2.controllable = true;
        }

        cameraBounds.EnableBounds(true);
        ShowGoIndicator();
    }

    private IEnumerator HeroesWalkout()
    {
        cameraBounds.EnableBounds(false);
        cameraFollows = false;
        player1.UseAutopilot(true);
        player1.controllable = false;
        player1.AnimateTo(walkOutTarget.transform.position, true, DidFinishWalkout);

        if (isMultiplayer && player2 != null)
        {
            player2.UseAutopilot(true);
            player2.controllable = false;
            player2.AnimateTo(walkOutTarget.transform.position + Vector3.right * 1.0f, true, null);
        }

        yield return null;
    }

    private void DidFinishWalkout()
    {
        CurrentLevel++;
        if (CurrentLevel >= levels.Length)
        {
            Victory();
        }
        else
            StartCoroutine(AnimateNextLevel());
        cameraBounds.EnableBounds(true);
        cameraFollows = false;
        player1.UseAutopilot(false);
        player1.controllable = false;
        if (isMultiplayer && player2 != null)
        {
            player2.UseAutopilot(false);
            player2.controllable = false;
        }
    }

    private IEnumerator AnimateNextLevel()
    {
        ShowTextBanner(currentLevelData.levelName + " COMPLETED");
        yield return new WaitForSeconds(3.0f);
        SceneManager.LoadScene("Game");
    }

    private void ShowGoIndicator()
    {
        StartCoroutine(FlickerGoIndicator(4));
    }

    private IEnumerator FlickerGoIndicator(int count = 4)
    {
        while (count > 0)
        {
            goIndicator.SetActive(true);
            yield return new WaitForSeconds(0.2f);
            goIndicator.SetActive(false);
            yield return new WaitForSeconds(0.2f);
            count--;
        }
    }

    private void ShowBanner(string bannerText, GameObject prefab)
    {
        GameObject obj = Instantiate(prefab);
        obj.GetComponent<Text>().text = bannerText;
        RectTransform rectTransform = obj.transform as RectTransform;
        rectTransform.SetParent(uiTransform);
        rectTransform.localScale = Vector3.one;
        rectTransform.anchoredPosition = Vector2.zero;
    }

    public void GameOver()
    {
        ShowBanner("GAME OVER", gameOverPrefab);
    }

    public void Victory()
    {
        ShowBanner("YOU WON", gameOverPrefab);
    }

    public void ShowTextBanner(string levelName)
    {
        ShowBanner(levelName, levelNamePrefab);
    }
}

