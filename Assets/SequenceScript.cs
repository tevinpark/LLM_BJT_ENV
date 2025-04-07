using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using System.Collections;
using UnityEngine.XR.Hands.Samples.GestureSample;
using TMPro;
using System;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;


public class SequenceScript : MonoBehaviour
{
    //Keeps track of current function
    private List<IEnumerator> sequentialSteps;
    private int currentStepIndex = 0;
    public int participantNum = 0;
    private int recordingNum = 0;
    [Header("External Scripts")]
    //Other scripts called
    public GameObject scriptHolderObj;

    [Header("Video")]
    public VideoPlayer videoPlayer;
    public Material videoMaterial;
    public VideoClip introClip;
    public List<VideoClip> videoClips;

    [Header("Audio")]
    public AudioSource soundPlayer;

    [Header("Fog")]
    public GameObject fogParent;
    private bool updateFogScale = false;
    private float fogElapsedTime = 0f;
    private Vector3 fogNewScale = new Vector3(1f, 1f, 1f);
    public float fogLerpDuration = 1f;
    public Vector3 fogSmallScale;
    public Vector3 fogLargeScale;
    private List<Transform> childTransforms = new List<Transform>();
    private List<float> fogChildOriginalSizes = new List<float>();
    public float fogChildScaleFactor;

    [Header("Environment")]
    private int envType = 1;
    public bool correct_environment;
    public GameObject startEnvironment;
    public List<GameObject> environments;
    public List<GameObject> environments_wrong;

    [Header("Gestures")]
    private string currentGesture = "";
    public GameObject rightThumbsUpDetector;
    public GameObject rightThumbsDownDetector;
    public GameObject leftThumbsUpDetector;
    public GameObject leftThumbsDownDetector;
    private StaticHandGesture thumbsUpGestureTracker;
    private StaticHandGesture thumbsDownGestureTracker;
    private StaticHandGesture thumbsUpGestureTracker2;
    private StaticHandGesture thumbsDownGestureTracker2;

    [Header("HUD Elements")]
    public GameObject ProgramSetupCanvas;
    public TMP_Dropdown ParticipantNumDropdown;
    public TextMeshProUGUI ProgramStartText;
    public TextMeshProUGUI MicStatusText;
    public TextMeshProUGUI StoryModalityText;
    public TextMeshProUGUI playerInstructionsText;
    public GameObject blackScreen;
    public GameObject SelectParticipantNumScreen;
    public GameObject participantNumScrollContent;
    public GameObject SelectStoryScreen;
    public GameObject StoryScrollContent;
    public List<Button> StoryEnvButtons;
    private UnityEngine.Color[] StoryEnvButtonColors = { new Color32(255, 153, 153, 255), new Color32(153, 255, 153, 255), new Color32(153, 204, 255, 255) };

    [Header("Testing Variables")]

    public TextMeshProUGUI gestureText;
    public int thumbsUpCount = 0;
    public TextMeshProUGUI functionCompleteText;
    public TextMeshProUGUI micActiveText;
    public TextMeshProUGUI storyTypeText;
    public List<int> storyType = new List<int> { 0, 1, 2, 0, 1, 2 }; //storytype : 0 - audio, 1 - visual, 2 = audiovisual
    public List<String> storyTitles;

    //Modes
    public GameObject redcapObj;
    public enum SeqMode { Headset, Redcap }
    SeqMode currentSeqMode = SeqMode.Redcap;


    // Start is called before the first frame update
    void Start()
    {
        playerInstructionsText.gameObject.SetActive(false);
        scriptHolderObj.GetComponent<ScrollViewScript>().PopulateParticipantScrollView();

        //Ensure videoMaterial is assigned to videoObject
        if (videoMaterial != null && videoPlayer != null)
        {
            videoPlayer.GetComponent<Renderer>().material = videoMaterial;
        }

        //Connect all fog children to fog parent
        foreach (Transform child in fogParent.transform)
        {
            childTransforms.Add(child);
            fogChildOriginalSizes.Add(child.localScale.x);
        }
        //Set the initial radius of fog
        fogParent.transform.localScale = fogLargeScale;

        //Show starting environment
        startEnvironment.SetActive(true);
        //Hide all story environments
        for (int i = 0; i < environments.Count; i++)
        {
            environments[i].SetActive(false);
        }
        for (int i = 0; i < environments_wrong.Count; i++)
        {
            environments_wrong[i].SetActive(false);
        }

        //Reset videplayer
        videoPlayer.targetTexture.Release();

        //Assign gesture detectors
        thumbsUpGestureTracker = rightThumbsUpDetector.GetComponent<StaticHandGesture>();
        thumbsDownGestureTracker = rightThumbsDownDetector.GetComponent<StaticHandGesture>();
        thumbsUpGestureTracker2 = leftThumbsUpDetector.GetComponent<StaticHandGesture>();
        thumbsDownGestureTracker2 = leftThumbsDownDetector.GetComponent<StaticHandGesture>();
        InitializeGestureTracker(thumbsUpGestureTracker, OnThumbsUpPerformed, "Thumbs Up");
        InitializeGestureTracker(thumbsDownGestureTracker, OnThumbsDownPerformed, "Thumbs Down");
        InitializeGestureTracker(thumbsUpGestureTracker2, OnThumbsUpPerformed, "Thumbs Up 2");
        InitializeGestureTracker(thumbsDownGestureTracker2, OnThumbsDownPerformed, "Thumbs Down 2");

        // Initialize the steps to execute
        sequentialSteps = new List<IEnumerator>
        {
            ProgramStarting(),
            Introduction(),
            ShowStories(),
            ProgramEnding()
        };

        recordingNum = 0;

        playerInstructionsText.text = "Loading...";
        playerInstructionsText.gameObject.SetActive(true);

        GetComponent<REDCapAPI>().GetSelectedParticipant((participantID, environmentType) =>
            {
                if (participantID == -1)
                {
                    currentSeqMode = SeqMode.Headset;
                }
                else
                {
                    currentSeqMode = SeqMode.Redcap;
                    Debug.Log(participantID);
                    participantNum = participantID;
                    envType = environmentType;
                    StartCoroutine(StartProgram(1));
                }

                blackScreen.gameObject.SetActive(false);
                playerInstructionsText.gameObject.SetActive(false);
            });


    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.U))
        {
            OnThumbsUpPerformed();
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            OnThumbsDownPerformed();
        }
        if (updateFogScale)
        {
            fogElapsedTime += Time.deltaTime;
            //converting to normalized time (0 to 1)
            float t;
            if (fogNewScale == fogLargeScale)
            {
                t = Mathf.Clamp01(fogElapsedTime / (fogLerpDuration * 240f * Time.deltaTime));
            }
            else
            {
                t = Mathf.Clamp01(fogElapsedTime / (fogLerpDuration * 12f * Time.deltaTime));
            }
            fogParent.transform.localScale = Vector3.Lerp(fogParent.transform.localScale, fogNewScale, t);

            if (t >= 1f)
            {
                updateFogScale = false;
                fogElapsedTime = 0f;
            }
        }
        Vector3 parentScale = fogParent.transform.localScale;
        for (int i = 0; i < childTransforms.Count; i++)
        {
            Transform child = childTransforms[i];
            float originalSize = fogChildOriginalSizes[i];
            if (child != null)
            {
                // Invert the parent's scale for the child
                child.localScale = new Vector3(
                    originalSize / (parentScale.x / fogLargeScale.x) * Mathf.Lerp(1.0f, fogChildScaleFactor, 6 - parentScale.x / fogLargeScale.x),
                    originalSize / (parentScale.y / fogLargeScale.y) * Mathf.Lerp(1.0f, fogChildScaleFactor, 6 - parentScale.y / fogLargeScale.y),
                    originalSize / (parentScale.z / fogLargeScale.z) * Mathf.Lerp(1.0f, fogChildScaleFactor, 6 - parentScale.z / fogLargeScale.z)
                );
            }
        }
    }

    public void setParticipantNum()
    {
        participantNum = participantNumScrollContent.GetComponent<SnapToGridScript>().getNum();
        SelectParticipantNumScreen.gameObject.SetActive(false);
        SelectStoryScreen.gameObject.SetActive(true);
        Debug.Log("Participant #: " + participantNum);
    }

    //Called by experimenter dashboard button
    //Shows the correct environments to the participants
    public void StartProgram_CorrectEnv()
    {
        String particpantNumString = ParticipantNumDropdown.options[ParticipantNumDropdown.value].text;
        if (!string.IsNullOrEmpty(particpantNumString) && int.TryParse(particpantNumString, out int result))
        {
            participantNum = Int32.Parse(particpantNumString);
        }
        correct_environment = true;
        ProgramSetupCanvas.gameObject.SetActive(false);
        ProgramStartText.gameObject.SetActive(true);
        // Start the sequential process
        StartCoroutine(ExecuteSequentialSteps());
    }

    //Called by experimenter dashboard button
    //Shows the incorrect environments to the participants
    public void StartProgram_IncorrectEnv()
    {
        String particpantNumString = ParticipantNumDropdown.options[ParticipantNumDropdown.value].text;
        if (!string.IsNullOrEmpty(particpantNumString) && int.TryParse(particpantNumString, out int result))
        {
            participantNum = Int32.Parse(particpantNumString);
        }
        correct_environment = false;
        ProgramSetupCanvas.gameObject.SetActive(false);
        ProgramStartText.gameObject.SetActive(true);
        // Start the sequential process
        StartCoroutine(ExecuteSequentialSteps());
    }

    public void SetEnvType(int eType)
    {
        envType = eType;
        for (int i = 0; i < StoryEnvButtons.Count; i++)
        {
            StoryEnvButtons[i].GetComponent<UnityEngine.UI.Image>().color = (i == envType) ? StoryEnvButtonColors[envType] : UnityEngine.Color.white;
        }
    }

    public void SetSequence()
    {
        int i = StoryScrollContent.GetComponent<SnapToGridScript>().getNum();
        SelectStoryScreen.gameObject.SetActive(false);
        StartCoroutine(StartProgram(i));
    }

    public IEnumerator StartProgram(int i)
    {
        if (envType == 0)
        {
            correct_environment = false;
        }
        else if (envType == 1)
        {
            correct_environment = true;
        }
        else if (envType == 2)
        {
            correct_environment = UnityEngine.Random.value > 0.5f ? true : false;
        }
        switch (i)
        {
            case 1:
                {
                    ProgramSetupCanvas.gameObject.SetActive(false);
                    ProgramStartText.gameObject.SetActive(true);
                    // Start the sequential process
                    StartCoroutine(ExecuteSequentialSteps());
                    break;
                }
            case 2:
                {
                    int randomSingleStoryNum = UnityEngine.Random.Range(0, 7);
                    yield return WaitForGesture(new List<string> { "ThumbsUp" });
                    yield return StartCoroutine(ShowStory(randomSingleStoryNum));
                    yield return StartCoroutine(MicStart());
                    yield return StartCoroutine(MicEnd(randomSingleStoryNum));
                    yield return ProgramEnding();
                    break;
                }
            case 3:
                {
                    yield return WaitForGesture(new List<string> { "ThumbsUp" });
                    yield return StartCoroutine(ShowStory(0));
                    yield return StartCoroutine(MicStart());
                    yield return StartCoroutine(MicEnd(0));
                    yield return ProgramEnding();
                    break;
                }
            case 4:
                {
                    yield return WaitForGesture(new List<string> { "ThumbsUp" });
                    yield return StartCoroutine(ShowStory(1));
                    yield return StartCoroutine(MicStart());
                    yield return StartCoroutine(MicEnd(1));
                    yield return ProgramEnding();
                    break;
                }
            case 5:
                {
                    yield return WaitForGesture(new List<string> { "ThumbsUp" });
                    yield return StartCoroutine(ShowStory(2));
                    yield return StartCoroutine(MicStart());
                    yield return StartCoroutine(MicEnd(2));
                    yield return ProgramEnding();
                    break;
                }
            case 6:
                {
                    yield return WaitForGesture(new List<string> { "ThumbsUp" });
                    yield return StartCoroutine(ShowStory(3));
                    yield return StartCoroutine(MicStart());
                    yield return StartCoroutine(MicEnd(3));
                    yield return ProgramEnding();
                    break;
                }
            case 7:
                {
                    yield return WaitForGesture(new List<string> { "ThumbsUp" });
                    yield return StartCoroutine(ShowStory(4));
                    yield return StartCoroutine(MicStart());
                    yield return StartCoroutine(MicEnd(4));
                    yield return ProgramEnding();
                    break;
                }
            case 8:
                {
                    yield return WaitForGesture(new List<string> { "ThumbsUp" });
                    yield return StartCoroutine(ShowStory(5));
                    yield return StartCoroutine(MicStart());
                    yield return StartCoroutine(MicEnd(5));
                    yield return ProgramEnding();
                    break;
                }
            default:
                {
                    Debug.Log("Default case");
                    break;
                }
        }
    }

    //Shows text to experimenter and debug that the program has started
    private IEnumerator ProgramStarting()
    {
        videoPlayer.GetComponent<Renderer>().enabled = true;
        Debug.Log("Program Starting...");
        functionCompleteText.text = "ready";
        yield return WaitForGesture(new List<string> { "ThumbsUp" });
        ProgramStartText.gameObject.SetActive(false);
    }

    //Plays instructions to the participant
    //Introduces hand gesture responses to the participant
    private IEnumerator Introduction()
    {
        // StartVideo(introClip);
        Debug.Log("Introduction...");
        functionCompleteText.text = "Introduction...";
        Debug.Log((float)introClip.length);
        blackScreen.gameObject.SetActive(true);
        playerInstructionsText.text = "The experiment is about to begin.\nYou will be told a story in various ways: audio, visual, or audio visual.\nAfter listening to each story, try to retell it to the best of your ability.\n\nGive a thumbs up to begin.";
        playerInstructionsText.gameObject.SetActive(true);
        // yield return new WaitForSeconds((float)introClip.length);
        functionCompleteText.text = "ready";
        yield return WaitForGesture(new List<string> { "ThumbsUp", "ThumbsDown" });
        // Respond based on the gesture received
        if (currentGesture == "ThumbsUp")
        {
            blackScreen.gameObject.SetActive(false);
            playerInstructionsText.gameObject.SetActive(false);
            Debug.Log("Thumbs Up received in Introduction.");
        }
        else if (currentGesture == "ThumbsDown")
        {
            Debug.Log("Thumbs Down received, repeating Introduction.");
            yield return StartCoroutine(Introduction());
        }
        currentGesture = ""; // Reset for the next iteration
    }

    //Goes through all the stories
    private IEnumerator ShowStories()
    {
        // Random order for the first three
        List<int> firstThree = new List<int> { 0, 1, 2 };  // First three
        ShuffleList(firstThree);  // Shuffle the list
        // Execute first three in random order
        foreach (int i in firstThree)
        {
            yield return StartCoroutine(ShowStory(i));
            yield return StartCoroutine(MicStart());
            yield return StartCoroutine(MicEnd(i));
        }
        for (int i = 3; i < 6; i++)
        {
            yield return StartCoroutine(ShowStory(i));
            yield return StartCoroutine(MicStart());
            yield return StartCoroutine(MicEnd(i));
        }
        yield return new WaitForSeconds(0f);
    }

    //Called for each story
    //Enables environment, starts video/audio, then when story is finished, waits for gesture to continue to next story
    private IEnumerator ShowStory(int iteration)
    {
        Debug.Log($"Showing Story Part {iteration + 1}...");
        functionCompleteText.text = $"Showing Story Part {iteration + 1}...";
        yield return StartCoroutine(EnableEnvironment(iteration));
        yield return new WaitForSeconds(0.5f);
        StoryModalityText.gameObject.SetActive(false);
        yield return new WaitForSeconds(1.5f);
        playerInstructionsText.text = "The story is about to begin. \n Give a thumbs up to start.";
        playerInstructionsText.gameObject.SetActive(true);
        yield return WaitForGesture(new List<string> { "ThumbsUp" });
        playerInstructionsText.gameObject.SetActive(false);
        float storyDuration = 0f;
        if (videoClips[iteration] != null)
        {
            storyDuration = (float)videoClips[iteration].length;
            StartVideo(videoClips[iteration]);
        }
        else
        {
            Debug.Log("No video");
        }
        yield return new WaitForSeconds(storyDuration);
        functionCompleteText.text = "ready";
        playerInstructionsText.text = "When you are ready to retell the story, \ngive a thumbs up.";
        playerInstructionsText.gameObject.SetActive(true);
        yield return WaitForGesture(new List<string> { "ThumbsUp" });
        playerInstructionsText.gameObject.SetActive(false);
    }

    //Disables all environments except the current story environment
    //Uses fog to hide loading
    IEnumerator EnableEnvironment(int envNum)
    {
        int storyIndex = envNum >= 3 ? 3 : envNum;
        ShrinkFog();
        yield return new WaitForSeconds(1.2f);
        String modality = storyType[envNum] == 0 ? "Audio" : storyType[envNum] == 1 ? "Video" : storyType[envNum] == 2 ? "Audiovisual" : "Unknown Type";
        String title = storyTitles[envNum];
        StoryModalityText.text = $"<size=100%>{title}<br><size=75%>{modality}";
        StoryModalityText.gameObject.SetActive(true);
        yield return new WaitForSeconds(3.0f);
        videoPlayer.GetComponent<Renderer>().enabled = (storyType[envNum] != 0);
        videoPlayer.SetDirectAudioMute(0, (storyType[envNum] == 1));
        if (storyType[envNum] == 0)
        {
            storyTypeText.text = "Audio";
        }
        else if (storyType[envNum] == 1)
        {
            storyTypeText.text = "Video";
        }
        else if (storyType[envNum] == 2)
        {
            storyTypeText.text = "AudioVisual";
        }
        startEnvironment.SetActive(false);
        if (correct_environment)
        {
            for (int i = 0; i < environments.Count; i++)
            {
                if (i == storyIndex)
                {
                    environments[i].SetActive(true);
                }
                else
                {
                    environments[i].SetActive(false);
                }
            }
        }
        else
        {
            for (int i = 0; i < environments_wrong.Count; i++)
            {
                if (i == storyIndex)
                {
                    environments_wrong[i].SetActive(true);
                }
                else
                {
                    environments_wrong[i].SetActive(false);
                }
            }
        }
        ExpandFog();
    }

    //Shows text to participant and debug that the program has ended
    private IEnumerator ProgramEnding()
    {
        Debug.Log("Program Ending...");
        playerInstructionsText.text = "This concludes the study.\nThank you for your time and participation!";
        playerInstructionsText.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f); // Simulate some operation
        yield return WaitForGesture(new List<string> { "ThumbsUp" });
        RefreshScene();
    }

    //Starts new story video/audio
    void StartVideo(VideoClip videoClip)
    {
        // Stop current video
        videoPlayer.Stop();
        //Change to new video
        videoPlayer.clip = videoClip;
        //Play new video
        videoPlayer.Play();
    }

    //Pause current video
    void PauseVideo()
    {
        videoPlayer.Pause();
    }

    //Resume current video
    void ResumeVideo()
    {
        videoPlayer.Play();
    }

    //Activates the mic to start recording the participant's retelling
    private IEnumerator MicStart()
    {
        Debug.Log("Mic Starting...");
        functionCompleteText.text = "Mic Starting...";
        micActiveText.text = "Mic On";
        playerInstructionsText.text = "When done with retelling,\ngive a thumbs up";
        playerInstructionsText.gameObject.SetActive(true);
        MicStatusText.gameObject.SetActive(true);
        scriptHolderObj.GetComponent<MicRecorder>().StartRecording();
        yield return new WaitForSeconds(0.5f);
        functionCompleteText.text = "ready";
        yield return WaitForGesture(new List<string> { "ThumbsUp" });
    }

    //Deactivates the mic to end recording the participant's retelling
    private IEnumerator MicEnd(int iteration)
    {
        Debug.Log("Mic Ending...");
        functionCompleteText.text = "Mic Ending...";
        micActiveText.text = "Mic Off";
        playerInstructionsText.gameObject.SetActive(false);
        scriptHolderObj.GetComponent<MicRecorder>().StopRecording(participantNum, Math.Min(iteration + 1, 3), storyType[iteration], correct_environment, recordingNum, currentSeqMode);
        MicStatusText.gameObject.SetActive(false);
        recordingNum++;
        yield return new WaitForSeconds(0.5f);
    }

    //Exapnds the fog to reveal story environment
    void ExpandFog()
    {
        fogNewScale = fogLargeScale;
        fogElapsedTime = 0f;
        updateFogScale = true;
    }

    //Shrinks the fog to hide story environment
    void ShrinkFog()
    {
        fogNewScale = fogSmallScale;
        fogElapsedTime = 0f;
        updateFogScale = true;
    }

    //Initializes the gesture tracker by adding the given action as a listener
    //If failed, logs error in debug
    private void InitializeGestureTracker(StaticHandGesture gestureTracker, UnityAction gestureAction, string gestureName)
    {
        if (gestureTracker != null)
        {
            gestureTracker.gesturePerformed.AddListener(gestureAction);
        }
        else
        {
            Debug.LogError($"{gestureName} component not found on the assigned GameObject.");
        }
    }

    //Does nothing until specific gesture is called
    private IEnumerator WaitForGesture(List<string> validGestures)
    {
        currentGesture = ""; // Reset gesture
        // Wait until a gesture is detected that matches the valid gestures
        while (!validGestures.Contains(currentGesture))
        {
            yield return null; // Keep waiting until a valid gesture is set
        }
    }

    // Thumbs up gesture detection
    public void OnThumbsUpPerformed()
    {
        currentGesture = "ThumbsUp"; // Set the current gesture to Thumbs Up
    }

    // Thumbs down gesture detection
    public void OnThumbsDownPerformed()
    {
        currentGesture = "ThumbsDown"; // Set the current gesture to Thumbs Down
    }

    // private void HandleGestureResponse()
    // {
    //     switch (currentGesture)
    //     {
    //         case "ThumbsUp":
    //             Debug.Log("Thumbs Up received.");
    //             break;
    //         case "ThumbsDown":
    //             Debug.Log("Thumbs Down received.");
    //             break;
    //         default:
    //             Debug.Log("Unknown gesture.");
    //             break;
    //     }
    //     currentGesture = ""; // Reset for the next gesture
    // }

    // Shuffle the list using Fisher-Yates algorithm
    private void ShuffleList(List<int> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = UnityEngine.Random.Range(0, n + 1);
            int value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    private IEnumerator ExecuteSequentialSteps()
    {
        while (currentStepIndex < sequentialSteps.Count)
        {
            // Execute the current step
            yield return StartCoroutine(sequentialSteps[currentStepIndex]);
            currentStepIndex++;
        }
    }

    // Call this function to refresh the scene
    public void RefreshScene()
    {
        // Get the active scene's name
        string sceneName = SceneManager.GetActiveScene().name;

        // Reload the scene
        SceneManager.LoadScene(sceneName);
    }


}
