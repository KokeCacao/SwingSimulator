using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
public enum GameState
{
  SwingState,
  LaunchState,
  DeadState,
  ReturnState,
}

public class GameManager : MonoBehaviour
{

  // constants
  private const float GRAVITY = 9.8f;
  private const float SCREEN_SIZE = 20.0f;
  private const float WAIT_TIME = 15.0f;
  private const float TIME_POW = 1.15f;
  private const float END_GAME_SCORE = 300.0f;
  private const int SMALL_FONT_SIZE = 24;
  private const int BIG_FONT_SIZE = 48;

  [SerializeField] private Camera MainCamera;
  [SerializeField] private Camera ViewportCamera;
  [SerializeField] private GameObject PlayerGameObject;
  [SerializeField] private GameObject SwingGameObject;
  [SerializeField] private GameObject Hand;
  [SerializeField] private GameObject String;
  [SerializeField] private GameObject HeadBody;
  [SerializeField] private GameObject Sit;
  [SerializeField] private GameObject Earth;
  [SerializeField] private GameObject Tmp; // TODO: remove
  [SerializeField] private Text ScoreText;
  [SerializeField] private Text RightScoreText;
  [SerializeField] private PostProcessVolume postProcessVolume;
  [SerializeField] private List<MultiSprite> multiSprites;
  [SerializeField] private AudioSource officeAmbiance;
  [SerializeField] private AudioSource spaceAmbiance;

  private List<GameObject> PlayerChildren;
  private List<GameObject> SwingChildren;
  private List<Rigidbody2D> PlayerRigidbodies;
  private List<Joint2D> PlayerJoints;
  private List<Vector3> PlayerInitialPositions;
  private List<Quaternion> PlayerInitialRotations;
  private List<Vector3> SwingInitialPositions;
  private List<Quaternion> SwingInitialRotations;
  private Vector3 TmpInitialPosition;

  // joints
  private Joint2D HandStringJoint;
  private Joint2D HeadBodySitJoint;

  // internal variables
  [System.NonSerialized] public GameState gameState;
  private float score = 0.0f;
  private float waitTime = 0.0f;
  private int deathCount = 0;

  void Awake()
  {
    // init lists
    PlayerChildren = new List<GameObject>();
    SwingChildren = new List<GameObject>();
    PlayerRigidbodies = new List<Rigidbody2D>();
    PlayerJoints = new List<Joint2D>();
    PlayerInitialPositions = new List<Vector3>();
    PlayerInitialRotations = new List<Quaternion>();
    SwingInitialPositions = new List<Vector3>();
    SwingInitialRotations = new List<Quaternion>();

    // Store Tmp initial position
    TmpInitialPosition = Tmp.transform.position;

    // assert that the PlayerGameObject is not null
    Debug.Assert(PlayerGameObject != null, "PlayerGameObject is null");

    // children of the PlayerGameObject
    foreach (Transform child in PlayerGameObject.transform)
    {
      PlayerChildren.Add(child.gameObject);

      // assert that the child has a rigidbody
      Debug.Assert(child.GetComponent<Rigidbody2D>() != null, "child does not have a rigidbody");
      PlayerRigidbodies.Add(child.GetComponent<Rigidbody2D>());
    }

    // children of the SwingGameObject
    foreach (Transform child in SwingGameObject.transform)
    {
      SwingChildren.Add(child.gameObject);
    }

    // find critical joints
    foreach (Joint2D joint in Hand.GetComponents<Joint2D>())
    {
      if (joint.connectedBody.gameObject == String)
      {
        HandStringJoint = joint;
        break;
      }
    }
    foreach (Joint2D joint in HeadBody.GetComponents<Joint2D>())
    {
      if (joint.connectedBody.gameObject == Sit)
      {
        HeadBodySitJoint = joint;
        break;
      }
    }

    // find all joints
    foreach (GameObject child in PlayerChildren)
    {
      // add all player joints to list
      foreach (Joint2D joint in child.GetComponents<Joint2D>())
      {
        PlayerJoints.Add(joint);
      }
      // store all position and quaternion
      PlayerInitialPositions.Add(child.transform.position);
      PlayerInitialRotations.Add(child.transform.rotation);
    }

    foreach (GameObject child in SwingChildren)
    {
      // store all position and quaternion
      SwingInitialPositions.Add(child.transform.position);
      SwingInitialRotations.Add(child.transform.rotation);
    }

    // set rigidbody properties
    // reset gravity to zero for all rigidbodies
    foreach (Rigidbody2D rigidbody in PlayerRigidbodies)
    {
      rigidbody.gravityScale = 0.0f;
    }
  }

  // Start is called before the first frame update
  void Start()
  {
    gameState = GameState.SwingState;
    score = 0.0f;
    ScoreText.gameObject.SetActive(false);
    RightScoreText.gameObject.SetActive(true);
    RightScoreText.text = "Press [Space] to Release Swing.\n[AWSD]/[Gamepad] to Move.";
    RightScoreText.fontSize = SMALL_FONT_SIZE;
    Time.timeScale = 1.0f;

    // rebind all joints
    foreach (Joint2D joint in PlayerJoints)
    {
      joint.enabled = true;
    }

    // also restore all positions and rotations for swing
    // no need ridigbodies though, since we only care about right bounding
    for (int i = 0; i < SwingChildren.Count; i++)
    {
      SwingChildren[i].transform.position = SwingInitialPositions[i];
      SwingChildren[i].transform.rotation = SwingInitialRotations[i];
    }

    // adjust post processing based on deathCount
    // postProcessVolume.profile.GetSetting<ColorGrading>().saturation.value = Mathf.Max(0.0f, 100.0f - deathCount * 10.0f);
    postProcessVolume.profile.GetSetting<ChromaticAberration>().intensity.value = Mathf.Min(1.0f, 0.5f * SigmoidFromZero(deathCount * 0.2f));
  }

  public float Sigmoid(float x)
  {
    return 1.0f / (1.0f + Mathf.Exp(-x));
  }

  public float SigmoidFromZero(float x)
  {
    return (Sigmoid(x) - 0.5f) * 2.0f;
  }

  public float SigmoidFromZeroShifted(float x, float shift)
  {
    float sig = 1.0f / (1.0f + Mathf.Exp(-(x-shift) * (1.0f/shift)));
    return (sig - 0.5f) * 2.0f;
  }
  void UpdateWhenSwingState()
  {
    // read gamepad data, or keyboard data
    float horizontal = Input.GetAxis("Horizontal") + (Input.GetKey(KeyCode.A) ? -1.0f : 0.0f) + (Input.GetKey(KeyCode.D) ? 1.0f : 0.0f);
    float vertical = Input.GetAxis("Vertical") + (Input.GetKey(KeyCode.S) ? -1.0f : 0.0f) + (Input.GetKey(KeyCode.W) ? 1.0f : 0.0f);
    // process gamepad data
    Vector2 gamepadVec = new Vector2(horizontal, vertical);
    if (Mathf.Abs(gamepadVec.magnitude) > 0.0f)
    {
      gamepadVec.Normalize();
    }
    Tmp.transform.position = new Vector3(gamepadVec.x, gamepadVec.y, 0)*0.5f + TmpInitialPosition;
    gamepadVec.Set(gamepadVec.x, -Mathf.Abs(gamepadVec.y));
    if (Mathf.Abs(gamepadVec.magnitude) < 0.1f)
    {
      gamepadVec.Set(0.0f, -1.0f);
    }
    // visualize gamepad data with tmp

    // apply force to each rigidbody
    foreach (Rigidbody2D rigidbody in PlayerRigidbodies)
    {
      // controller is horizontal -1 to 1
      rigidbody.AddForce(gamepadVec * GRAVITY, ForceMode2D.Force);
    }
  }

  void UpdateWhenLaunchState()
  {
    // apply force to each rigidbody
    foreach (Rigidbody2D rigidbody in PlayerRigidbodies)
    {
      // g = 4/3 (pi * p * R * G)
      float height = rigidbody.transform.position.y + Earth.transform.localScale.x;
      float gravityDivider = 3.0f;
      float earthGravity = (1.0f / (deathCount / gravityDivider + 1.0f)) * GRAVITY / Mathf.Pow((1 + height / Earth.transform.localScale.x), 2);
      Vector2 earthVec = Earth.transform.position - rigidbody.transform.position;
      earthVec.Normalize();

      rigidbody.AddForce(earthVec * earthGravity, ForceMode2D.Force);
    }
  }

  // Update is called once per frame
  void Update()
  {

    if (Input.GetKey(KeyCode.LeftControl))
    {
      if (Input.GetKeyUp(KeyCode.Z))
      {
        OnControlZ();
      }
    }

    // let MainCamera follow HeadBody's position
    MainCamera.transform.position = new Vector3(HeadBody.transform.position.x, HeadBody.transform.position.y, -(HeadBody.transform.position.magnitude + 10.0f));
    // MainCamera.orthographicSize = SCREEN_SIZE + HeadBody.transform.position.magnitude;
    MainCamera.transform.rotation = Quaternion.Euler(0, Mathf.Clamp(-HeadBody.transform.position.x * 0.1f, -45, 45), 0);

    // set ViewportCamera to follow MainCamera
    ViewportCamera.transform.position = MainCamera.transform.position;
    // ViewportCamera.transform.rotation = MainCamera.transform.rotation; // No rotation though

    // calculate distance between earth and player
    score = Mathf.Max(score, HeadBody.transform.position.magnitude);

    // change audio volume
    officeAmbiance.volume = SigmoidFromZeroShifted(HeadBody.transform.position.magnitude, END_GAME_SCORE);
    spaceAmbiance.volume = 1.0f - officeAmbiance.volume;

    switch (gameState)
    {
      case GameState.SwingState:
        UpdateWhenSwingState();
        break;
      case GameState.LaunchState:
        UpdateWhenLaunchState();
        RightScoreText.text = "Score: " + ((int)score).ToString();
        RightScoreText.fontSize = BIG_FONT_SIZE;
        if (score > END_GAME_SCORE) {
          RightScoreText.text += "\nSometimes, [Space] travel in vast [Space] needs some [Space]";
        }

        if (waitTime > 0.0f)
        {
          waitTime -= Time.deltaTime;
          Time.timeScale = Mathf.Pow(TIME_POW, WAIT_TIME - waitTime);
        }
        break;
      case GameState.DeadState:
        UpdateWhenLaunchState();
        break;
      case GameState.ReturnState:
        bool updated = false;
        // reduce waitTime
        waitTime -= Time.deltaTime;
        Time.timeScale = Mathf.Pow(TIME_POW, WAIT_TIME - waitTime);
        for (int i = 0; i < PlayerChildren.Count; i++)
        {
          if (Vector2.Distance(PlayerChildren[i].transform.position, PlayerInitialPositions[i]) < 3.0f || waitTime < 0.0f)
          {
            // reset position and rotation
            PlayerChildren[i].transform.position = PlayerInitialPositions[i];
            PlayerChildren[i].transform.rotation = PlayerInitialRotations[i];
            PlayerRigidbodies[i].velocity = Vector2.zero;
            PlayerRigidbodies[i].angularVelocity = 0.0f;
            PlayerRigidbodies[i].drag = 0.0f;
            PlayerRigidbodies[i].angularDrag = 0.0f;

            // attach joint i
            PlayerJoints[i].enabled = true;
            continue;
          }
          // PlayerChildren[i].transform.position = Vector3.Lerp(PlayerChildren[i].transform.position, PlayerInitialPositions[i], 0.1f);
          // PlayerChildren[i].transform.rotation = Quaternion.Lerp(PlayerChildren[i].transform.rotation, PlayerInitialRotations[i], 0.1f);
          Vector3 forceDir = (PlayerInitialPositions[i] - PlayerChildren[i].transform.position).normalized;
          float forceMag = (PlayerInitialPositions[i] - PlayerChildren[i].transform.position).magnitude;
          PlayerRigidbodies[i].AddForce(forceDir * forceMag * 1.0f * PlayerRigidbodies[i].mass + forceDir * 2.0f * PlayerRigidbodies[i].mass, ForceMode2D.Force);
          // PlayerRigidbodies[i].AddTorque((PlayerInitialRotations[i].eulerAngles.z - PlayerChildren[i].transform.rotation.eulerAngles.z) * 1.0f, ForceMode2D.Force);
          PlayerRigidbodies[i].drag = 0.1f;
          // PlayerRigidbodies[i].angularDrag = 0.1f;
          updated = true;
        }

        // if all player children are back to initial position, then restart game
        if (!updated)
        {
          Start();
        }
        break;
      default:
        Debug.LogException(new System.Exception("Invalid GameState"));
        break;
    }
  }

  void UpdateSprite() {
    int multiSpriteLength = multiSprites.Count;
    int spriteIndex = Random.Range(0, multiSpriteLength);
    bool success = multiSprites[spriteIndex].NextSprite();

    // set all parents boundingbox to false
    for (int i = 0; i < multiSpriteLength; i++) {
      multiSprites[i].ParentBoundingSet(false);
    }

    multiSprites[spriteIndex].ParentBoundingSet(true);
    multiSprites[spriteIndex].RandomColor();
  }

  void PlayerRelease()
  {
    // release joints that connect to swing
    HandStringJoint.enabled = false;
    HeadBodySitJoint.enabled = false;

    gameState = GameState.LaunchState;
    RightScoreText.gameObject.SetActive(true);

    waitTime = WAIT_TIME; // trigger speed up by default
  }

  public void PlayerDie()
  {
    gameState = GameState.DeadState;
    Time.timeScale = 1.0f;
    waitTime = 0;
    deathCount++;

    // release all player joints
    foreach (Joint2D joint in PlayerJoints)
    {
      joint.enabled = false;
    }

    // end game
    ScoreText.text = "Score: " + ((int)score).ToString() + "\nDeaths: " + deathCount.ToString() + "\nPress Ctrl+Z to restart";
    ScoreText.gameObject.SetActive(true);
    RightScoreText.gameObject.SetActive(false);
  }

  void OnControlZ()
  {
    switch (gameState)
    {
      case GameState.SwingState:
        // do nothing
        break;
      case GameState.LaunchState:
        // do nothing
        break;
      case GameState.DeadState:
        gameState = GameState.ReturnState;
        ScoreText.gameObject.SetActive(false);
        RightScoreText.gameObject.SetActive(false);
        waitTime = WAIT_TIME;
        // and change sprite
        UpdateSprite();
        break;
      case GameState.ReturnState:
        // do nothing
        break;
      default:
        Debug.LogException(new System.Exception("Invalid GameState"));
        break;
    }
  }

  void OnGUI()
  {
    Event e = Event.current;
    if (e.isKey && e.type == EventType.KeyDown && e.keyCode == KeyCode.Space) // if space pressed
    {
      switch (gameState)
      {
        case GameState.SwingState:
          PlayerRelease();
          break;
        case GameState.LaunchState:
          HeadBody.GetComponent<Rigidbody2D>().AddForce(new Vector2(0, -1000.0f), ForceMode2D.Impulse);
          break;
        case GameState.DeadState:
          OnControlZ();
          break;
        case GameState.ReturnState:
          Start();
          RightScoreText.gameObject.SetActive(true);
          RightScoreText.text = "You summoned your bodies too quickly. Look at you!\nYou know that you only need to press Ctrl+Z once right?";
          RightScoreText.fontSize = SMALL_FONT_SIZE;
          // do nothing
          break;
        default:
          Debug.LogException(new System.Exception("Invalid GameState"));
          break;
      }
    }
  }
}
