﻿using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts;
using System.Collections;
using System;

public class GameManager : MonoBehaviour
{

    //sprites can be found here: 
    //http://www.gameartguppy.com/shop/top-tower-defense-bunny-badgers-game-art-set/

    //enemies on screen
    public static List<GameObject> Enemies;
    //prefabs
    public GameObject EnemyPrefab;
    public GameObject PathPrefab;
    public GameObject TowerPrefab;
    //list of waypoints in the current level
    public static Transform[] Waypoints;
    private GameObject PathPiecesParent;
    private GameObject WaypointsParent;
    //file pulled from resources
    private LevelStuffFromXML levelStuffFromXML;
    //will spawn carrots on screen
    public CarrotSpawner CarrotSpawner;

    //helpful variables for our player
    public static int MoneyAvailable;
    public static float MinCarrotSpawnTime, MaxCarrotSpawnTime;
    public static int Lives = 10;
    private int currentRoundIndex = 0;
    public static GameState CurrentGameState;

    public static bool FinalEnemyRound;
    public AudioManager audioManager;
    public GUIText infoText;

    private object lockerObject = new object();

    // Use this for initialization
    void Start()
    {

        IgnoreLayerCollisions();

        Enemies = new List<GameObject>();
        PathPiecesParent = GameObject.Find("PathPieces");
        WaypointsParent = GameObject.Find("Waypoints");
        levelStuffFromXML = Utilities.ReadXMLFile();

        CreateLevelFromXML();

        CurrentGameState = GameState.Start;

        FinalEnemyRound = false;
    }

    /// <summary>
    /// Will create necessary stuff from the object that has the XML stuff
    /// </summary>
    private void CreateLevelFromXML()
    {
        foreach (var position in levelStuffFromXML.Paths)
        {
            GameObject go = Instantiate(PathPrefab, position, Quaternion.identity) as GameObject;
            go.GetComponent<SpriteRenderer>().sortingLayerName = "Path";
            go.transform.parent = PathPiecesParent.transform;
        }

        for (int i = 0; i < levelStuffFromXML.Waypoints.Count; i++)
        {
            GameObject go = new GameObject();
            go.transform.position = levelStuffFromXML.Waypoints[i];
            go.transform.parent = WaypointsParent.transform;
            go.tag = "Waypoint";
            go.name = "Waypoints" + i.ToString();
        }

        GameObject tower = Instantiate(TowerPrefab, levelStuffFromXML.Tower, Quaternion.identity) as GameObject;
        tower.GetComponent<SpriteRenderer>().sortingLayerName = "Foreground";

        Waypoints = GameObject.FindGameObjectsWithTag("Waypoint")
            .OrderBy(x => x.name).Select(x => x.transform).ToArray();

        MoneyAvailable = levelStuffFromXML.InitialMoney;
        MinCarrotSpawnTime = levelStuffFromXML.MinCarrotSpawnTime;
        MaxCarrotSpawnTime = levelStuffFromXML.MaxCarrotSpawnTime;
    }

    /// <summary>
    /// Will make the arrow collide only with enemies!
    /// </summary>
    private static void IgnoreLayerCollisions()
    {
        Physics2D.IgnoreLayerCollision(12, 15); //Bunny and Enemy (when dragging the bunny)
        Physics2D.IgnoreLayerCollision(9, 8); //Arrow and BunnyGenerator
        Physics2D.IgnoreLayerCollision(9, 10); //Arrow and Background
        Physics2D.IgnoreLayerCollision(9, 11); //Arrow and Path
        Physics2D.IgnoreLayerCollision(9, 12); //Arrow and Bunny
        Physics2D.IgnoreLayerCollision(9, 13); //Arrow and Tower
        Physics2D.IgnoreLayerCollision(9, 14); //Arrow and Carrot
    }



    IEnumerator NextRound()
    {
        yield return new WaitForSeconds(2f);
        Round currentRound = levelStuffFromXML.Rounds[currentRoundIndex];
        for (int i = 0; i < currentRound.NoOfEnemies; i++)
        {
            GameObject enemy = Instantiate(EnemyPrefab, Waypoints[0].position, Quaternion.identity) as GameObject;
            enemy.GetComponent<Enemy>().Speed += currentRoundIndex;
            enemy.GetComponent<Enemy>().EnemyKilled += OnEnemyKilled;
            enemy.GetComponent<Enemy>().audioManager = audioManager;
            Enemies.Add(enemy);
            yield return new WaitForSeconds(1f / (currentRoundIndex == 0 ? 1 : currentRoundIndex));
        }

    }

    void OnEnemyKilled(object sender, EventArgs e)
    {
        bool startNewRound = false;
        lock (lockerObject)
        {
            if (Enemies.Where(x => x != null).Count() == 0 && CurrentGameState == GameState.Playing)
            {
                startNewRound = true;
            }
        }
        if (startNewRound)
            CheckAndStartNewRound();
    }

    private void CheckAndStartNewRound()
    {
        if (currentRoundIndex < levelStuffFromXML.Rounds.Count - 1)
        {
            currentRoundIndex++;
            StartCoroutine(NextRound());
        }
        else
        {
            FinalEnemyRound = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        switch (CurrentGameState)
        {
            case GameState.Start:
                if (Input.GetMouseButtonUp(0))
                {
                    CurrentGameState = GameState.Playing;
                    StartCoroutine(NextRound());
                    CarrotSpawner.StartCarrotSpawn();
                }
                break;
            case GameState.Playing:
                if (Lives == 0) //we lost
                {
                    StopCoroutine(NextRound());
                    DestroyExistingEnemiesAndCarrots();
                    CarrotSpawner.StopCarrotSpawn();
                    CurrentGameState = GameState.Lost;
                }
                else if (FinalEnemyRound && Enemies.Where(x => x != null).Count() == 0)
                {
                    DestroyExistingEnemiesAndCarrots();
                    CarrotSpawner.StopCarrotSpawn();
                    CurrentGameState = GameState.Won;
                }
                break;
            case GameState.Won:
                if (Input.GetMouseButtonUp(0))
                {
                    Application.LoadLevel(Application.loadedLevel);
                }
                break;
            case GameState.Lost:
                if (Input.GetMouseButtonUp(0))
                {
                    Application.LoadLevel(Application.loadedLevel);
                }
                break;
            default:
                break;
        }
    }

    private void DestroyExistingEnemiesAndCarrots()
    {
        foreach (var item in Enemies)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        var carrots = GameObject.FindGameObjectsWithTag("Carrot");
        foreach (var item in carrots)
        {
            Destroy(item);
        }
    }

    void OnGUI()
    {
        Utilities.AutoResize(800, 480);
        switch (CurrentGameState)
        {
            case GameState.Start:
                infoText.text = "Tap to start!";
                break;
            case GameState.Playing:
                infoText.text = "Money: " + MoneyAvailable.ToString() + "\n"
                    + "Life: " + Lives.ToString() + "\n" +
                    string.Format("round {0} of {1}", currentRoundIndex + 1, levelStuffFromXML.Rounds.Count);
                break;
            case GameState.Won:
                infoText.text = "Won :( Tap to restart!";
                break;
            case GameState.Lost:
                infoText.text = "Lost :( Tap to restart!";
                break;
            default:
                break;
        }


    }
}
