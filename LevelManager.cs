using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public partial class LevelManager : MonoBehaviour
{
	//static variables
	private static LevelManager instance;
	public static bool exists { get { return instance != null; } }
	public static Dictionary<CharacterState.Faction, List<ICharacterTarget>> allTargetCharacters = new Dictionary<CharacterState.Faction, List<ICharacterTarget>>();
	public static List<Character> playerCharacters = new List<Character>();
    public delegate void TransitionHandler(float progress);
	public static event TransitionHandler OnTransition;
	public static float transitionProgress { get; private set; }
    public MultiplayerBufferZone MBZ;

    public static CharacterState.Faction PlayerFaction { get { return instance.playerFaction; } set { instance.playerFaction = value; } }
	public static float RoundLength { get { return instance.roundLength; } }
	public static float TransitionLength { get { return instance.transitionLength; } }

	public CharacterState.Faction playerFaction;
	public float roundLength;
	public float transitionLength;
    private static bool noRepetitionPlease,noRepetitionPlease2;

    //commit turn variables
    public float commitTurnHoldTime;
	private float commitProgress;
	private float commitTimer;
	private Coroutine turnRoutine;


    //=== INITIALIZATION ===


	private void ResetStatics()
	{
		ObjectSelection.Reset();
		transitionProgress = 0;

		allTargetCharacters.Clear();
		playerCharacters.Clear();
	}

	public static void Initialize(List<CharacterState> selectedCharacters) { instance.InitializeLevel(selectedCharacters); }
	private void InitializeLevel(List<CharacterState> selectedCharacters)
    {
        allTargetCharacters.Clear();
        allTargetCharacters.Add(CharacterState.Faction.Player, new List<ICharacterTarget>());
        allTargetCharacters.Add(CharacterState.Faction.Neutral, new List<ICharacterTarget>());
        allTargetCharacters.Add(CharacterState.Faction.Enemy, new List<ICharacterTarget>());

        OnStateChange = null;

        GameManager.QueueSceneReset(gameObject.scene, typeof(ObjectSelection), () =>
        {
            ResetStatics();
            Time.timeScale = 1;
        });

        Databases.PreLoadDatabases();

        InputActions.LoadActions();

        playerCharacters.Clear();
        SpawnPoint.SpawnCharacters(selectedCharacters);

        FogOfWar.Initialize();

        StartCoroutine(InitializeCoverPositions());

        InputManager.SelectSoldier(PlayerCharacterByIndex(0));
        CameraMovement.FocusTarget(true);

        ChangeState(States.Planning);
        if (OnTransition != null) OnTransition(1);
    }


    //=== PLAYER CHARACTERS ===
    public static Character PlayerCharacterByIndex(int index)
	{
		index = Mathf.Clamp(index, 0, playerCharacters.Count - 1);

		return playerCharacters[index];
	}

	public static Character CharacterByCharacterState(CharacterState characterState)
	{
		Character character = playerCharacters.Find(x => x.state == characterState);
		if (character == null) return null;

		return character;
	}

	public static int IndexByPlayerCharacter(CharacterState characterState) { return IndexByPlayerCharacter(CharacterByCharacterState(characterState)); }
	public static int IndexByPlayerCharacter(Character character)
	{
		if (!playerCharacters.Contains(character)) return -1;

		return playerCharacters.IndexOf(character);
	}

	public static Character GetNextCharacter()
	{
		int index = IndexByPlayerCharacter(ObjectSelection.parentCharacter) + 1;
		if (index == playerCharacters.Count) index = 0;
		return playerCharacters[index];
	}


	//=== COVER POSITIONS ===
	private static List<CoverPosition> coverPositions;
	private const int coversToProcessPerFrame = 15;
	public static IEnumerator InitializeCoverPositions()
	{
		Debug.Log("Initialize cover positions");
		coverPositions = new List<CoverPosition>(GameObject.FindObjectsOfType<CoverPosition>());
		yield return null;

		for (int i = 0; i < coverPositions.Count; i++)
		{
			CoverPosition coverPosition = coverPositions[i];
			coverPosition.Initialize();
			if ((i + 1) % coversToProcessPerFrame == 0) yield return null;
		}

		coverPositions.RemoveAll(x => !x.validPosition);

		//HACK - remove the loading scene
		Scene loading = SceneManager.GetSceneByName("Loading");
		if (loading.isLoaded) SceneManager.UnloadSceneAsync(loading);
		LevelAudio.Trigger(LevelAudio.Triggers.Level_Loaded);
	}

	public static void GetNearbyCoverPositions(ref List<CoverPosition> positions, Vector3 position, float maxDistance, Character c)
	{
		positions.Clear();
		UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
		foreach (CoverPosition coverPos in coverPositions)
		{
			if (coverPos.gameObject.activeInHierarchy &&
			(coverPos.transform.position - position).magnitude < maxDistance &&
			(coverPos.reservedBy == null || coverPos.reservedBy == c) &&
			(coverPos.occupiedBy == null || coverPos.occupiedBy == c))
			{
				UnityEngine.AI.NavMesh.CalculatePath(c.GetModule<CharacterAspect>().basePosition, coverPos.transform.position, UnityEngine.AI.NavMesh.AllAreas, path);
				if (path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
				{
					positions.Add(coverPos);
				}
			}
		}
	}


	//=== UPDATE FUNCTIONS ===
	void Update()
	{
        string lycamobile="";
        if (PhotonNetwork.isMasterClient) lycamobile = "Master";
        if (PhotonNetwork.isClient) lycamobile = "Client";
        PhotonNetwork.RaiseEvent(0, lycamobile, true, null);

    }
	void LateUpdate()
	{
		if(MultiplayerBufferZone.playersReady>0)
        Debug.LogError("PlayerReady: " + MultiplayerBufferZone.playerReady +" OppReady: "+MultiplayerBufferZone.opponentReady + " PlayersReady: "+MultiplayerBufferZone.playersReady);

        if (Time.time > commitTimer + 1)
        {
            commitProgress = 0;
        }
        CommitTurnIndicator.SetFill(commitProgress);

        if (MultiplayerBufferZone.playersReady==2)
        {
            // StartCoroutine(MPCommitTurnRoutine());
            if (PhotonNetwork.isClient) MBZ.ClientSend();
            if (PhotonNetwork.isMasterClient) MBZ.MasterSend();
            MultiplayerBufferZone.playersReady++;
            Debug.LogError("playersReady==2 ++ preparing send");
        }
        else if (MultiplayerBufferZone.playersReady == 3)
        {
            StartCoroutine(MPExecuteTurnRoutine());
            MultiplayerBufferZone.playersReady = 0;
            Debug.LogError("playersReady reset");

        }
        else if (!MultiplayerBufferZone.opponentReady&&!MultiplayerBufferZone.playerReady)
		{
            Reset();
        }

        else if (MultiplayerBufferZone.playerReady)
        {
            //StartCoroutine(MPUpdateRoutine());
			//master can't send events for some reason
              if (PhotonNetwork.isClient) PhotonNetwork.RaiseEvent(0, true, true, null);
            MultiplayerBufferZone.playersReady++;
            Debug.LogError("playerReady ++");

            Wait();
            MultiplayerBufferZone.playerReady = false;
        }
		else if (MultiplayerBufferZone.opponentReady)
        {
			//the event code 0 from the MultiplayerBufferZone already adds playersready, this is kinda emulating it
			if(!noRepetitionPlease2&&PhotonNetwork.isClient){
                Debug.LogError("0: Emulating ready signal");
				MultiplayerBufferZone.playersReady++;
            } 

            Ready();
            MultiplayerBufferZone.opponentReady = false;
            noRepetitionPlease2 = true;

        }


	}

    // private IEnumerator MPCommitTurnRoutine()
    // {
    //     if (PhotonNetwork.isClient) MBZ.ClientSend();
    //     if (PhotonNetwork.isMasterClient) MBZ.MasterSend();
    //     MultiplayerBufferZone.playersReady++;
    //     yield return null;


    // }
    private IEnumerator MPExecuteTurnRoutine()
    {
        yield return CommitTurnRoutine();
        noRepetitionPlease = false;
        noRepetitionPlease2 = false;
        MultiplayerBufferZone.playerReady = false;
        MultiplayerBufferZone.opponentReady = false;
    }

	//=== COMMIT TURN ===
	public static void CommitTurnHold()
	{
		if (instance.turnRoutine != null)
		{
			return;
		}

		instance.commitTimer = Time.time;
		instance.commitProgress += 1 / instance.commitTurnHoldTime * Time.unscaledDeltaTime;
		
		if (instance.commitProgress >= 1 && !noRepetitionPlease)
		{
            //debugging
            Debug.LogError("I am ready");
            MultiplayerBufferZone.playerReady = true;
            noRepetitionPlease = true;

        }
    }
	public static void CommitTurn() { instance.turnRoutine = instance.StartCoroutine(instance.CommitTurnRoutine()); }
	private IEnumerator CommitTurnRoutine()
	{
		//fade to execution phase
		LevelAudio.Trigger(LevelAudio.Triggers.Level_PhaseExecution);
		StartCoroutine(TransitionEventRoutine(false));
		yield return TimeManager.FadeTimeRoutine(0, transitionLength * .5f);
		yield return ChangeStateRoutine(States.Executing);
		yield return TimeManager.FadeTimeRoutine(1, transitionLength * .5f);


		//wait for turn to run
		yield return new WaitForSeconds(roundLength - transitionLength);


		//phase back to planning phase
		LevelAudio.Trigger(LevelAudio.Triggers.Level_PhasePlanning);
		StartCoroutine(TransitionEventRoutine(true));
		yield return TimeManager.FadeTimeRoutine(0, transitionLength * .5f);
		yield return ChangeStateRoutine(States.Planning);
		yield return TimeManager.FadeTimeRoutine(1, transitionLength * .5f);

        //end routine
        turnRoutine = null;
	}


    private IEnumerator TransitionEventRoutine(bool active)
	{
		float startTime = Time.unscaledTime;
		float endTime = startTime + transitionLength * .5f;
		float progress = 0;
		do
		{
			progress = Mathf.InverseLerp(startTime, endTime, Time.unscaledTime);
			transitionProgress = active ? progress : 1 - progress;

			if (OnTransition != null) OnTransition(transitionProgress);
			yield return null;
		}
		while (progress < 1);
	}


	//=== GAME OVER CHECK ===
	public static void CheckGameOver()
	{
		bool gameOver = true;
		for (int i = 0; i < allTargetCharacters[PlayerFaction].Count; i++)
		{
			if (allTargetCharacters[PlayerFaction][i].aspect.ch.isOperational) gameOver = false;
		}

		if (gameOver)
		{
			Lose();
		}
	}

    public static void Lose()
    {
        ChangeState(States.LevelEnded);
        EndLevelBox.ShowBox(false);
		LevelAudio.Trigger (LevelAudio.Triggers.Level_Lost);
    }

    public static void Win()
    {
        ChangeState(States.LevelEnded);
        EndLevelBox.ShowBox(true);
		LevelAudio.Trigger (LevelAudio.Triggers.Level_Won);
    }


    public void Ready()
    {
        //ChangeState(States.);
        waitingText.gameObject.SetActive(true);
        LevelAudio.Trigger(LevelAudio.Triggers.Level_Lost);
    }
    public void Reset()
    {
        //ChangeState(States.);

    }
 }
