using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;

public class ApiTokenConfig
{
    public string apiToken;
}
public class REDCapAPI : MonoBehaviour
{

    private string apiUrl = "https://redcap.vumc.org/api/";

    private string apiToken;


    public void CheckAndLoadApiToken()
    {
        string configPath = Path.Combine(Application.persistentDataPath, "api_config.json");

        if (File.Exists(configPath))
        {
            string json = File.ReadAllText(configPath);
            ApiTokenConfig config = JsonUtility.FromJson<ApiTokenConfig>(json);

            if (!string.IsNullOrEmpty(config.apiToken))
            {
                apiToken = config.apiToken;
                Debug.Log("API token loaded: " + apiToken);
            }
            else
            {
                Debug.LogWarning("API token file found but token is empty.");
            }
        }
        else
        {
            Debug.Log("No API token config file found at: " + configPath);
        }
    }

    public void GetSelectedParticipant(Action<int, int> onComplete)
    {
        CheckAndLoadApiToken();
        StartCoroutine(FetchSelectedParticipant(onComplete));
    }


    private IEnumerator FetchSelectedParticipant(Action<int, int> onResult)
    {
        WWWForm form = new WWWForm();
        form.AddField("token", apiToken);
        form.AddField("content", "record");
        form.AddField("format", "json");
        form.AddField("type", "flat");

        form.AddField("filterLogic", "[record_id] = 1");
        form.AddField("fields", "selected_participant_1,environment_type_1,ready_1,selected_participant_2,environment_type_2,ready_2");

        using (UnityWebRequest www = UnityWebRequest.Post(apiUrl, form))
        {
            www.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log($"Request failed: {www.error}");
                onResult?.Invoke(-1, -1);
                yield break;
            }

            string jsonResponse = www.downloadHandler.text;
            Debug.Log($"Received response: {jsonResponse}");

            RecordSelectionForm[] records = null;
            int selectedParticipant = -1;
            int environmentType = -1;

            int participantType = -1;

            try
            {
                records = JsonHelper.FromJson<RecordSelectionForm>(jsonResponse);

                if (records.Length > 0)
                {
                    if (records[0].ready_1 == "0" && !string.IsNullOrEmpty(records[0].selected_participant_1) && !string.IsNullOrEmpty(records[0].environment_type_1))
                    {
                        int.TryParse(records[0].selected_participant_1, out selectedParticipant);
                        int.TryParse(records[0].environment_type_1, out environmentType);
                        participantType = 1;
                    }
                    else if (records[0].ready_2 == "0" && !string.IsNullOrEmpty(records[0].selected_participant_2) && !string.IsNullOrEmpty(records[0].environment_type_2))
                    {
                        int.TryParse(records[0].selected_participant_2, out selectedParticipant);
                        int.TryParse(records[0].environment_type_2, out environmentType);
                        participantType = 2;
                    }
                    else
                    {
                        Debug.Log($"No participant selections available");
                        onResult?.Invoke(-1, -1);
                        yield break;
                    }
                }
                else
                {
                    Debug.Log("No valid data found in response.");
                    onResult?.Invoke(-1, -1);
                    yield break;
                }
            }
            catch (System.Exception e)
            {
                Debug.Log($"JSON parsing error: {e.Message}");
                onResult?.Invoke(-1, -1);
                yield break;
            }

            bool found = false;
            
            Debug.Log($"Verifying if participant {selectedParticipant} exists...");

            yield return StartCoroutine(FetchParticipantData(selectedParticipant, exists =>
            {
                found = exists;
            }));

            if (found)
            {
                Debug.Log($"Participant {selectedParticipant} found in REDCap.");
                onResult?.Invoke(selectedParticipant, environmentType);
            }
            else
            {
                Debug.Log($"Participant {selectedParticipant} not found in REDCap.");
                onResult?.Invoke(-1, environmentType);
            }
        }
    }




    private IEnumerator FetchParticipantData(int participantID, Action<bool> onResult)
    {
        WWWForm form = new WWWForm();
        form.AddField("token", apiToken);
        form.AddField("content", "record");
        form.AddField("format", "json");
        form.AddField("type", "flat");

        // Fetch only data for the selected participant
        form.AddField("filterLogic", "[participant_id] = '" + participantID + "'");
        form.AddField("fields", "participant_id,name,age,gender");

        using (UnityWebRequest www = UnityWebRequest.Post(apiUrl, form))
        {
            www.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error fetching participant data: " + www.error);
                onResult?.Invoke(false);
                yield break;
            }

            string jsonResponse = www.downloadHandler.text;

            if (string.IsNullOrEmpty(jsonResponse) || jsonResponse == "[]")
            {
                Debug.Log($"Participant with ID {participantID} does not exist in REDCap.");
                onResult?.Invoke(false);
                yield break;
            }

            try
            {
                RecordParticipantForm[] participantRecords = JsonHelper.FromJson<RecordParticipantForm>(jsonResponse);

                if (participantRecords.Length > 0)
                {
                    RecordParticipantForm participant = participantRecords[0];

                    Debug.Log($"Loaded Data for Participant {participant.participant_id}:");
                    Debug.Log($"Name: {participant.name}, Age: {participant.age}, Gender: {participant.gender}");

                    onResult?.Invoke(true);
                }
                else
                {
                    Debug.Log($"Participant with ID {participantID} does not exist.");
                    onResult?.Invoke(false);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Failed to parse JSON. Raw response: " + jsonResponse);
                Debug.LogError("Parsing error: " + e.Message);
                onResult?.Invoke(false);
            }
        }
    }


    public void UploadAudioClip(int participantID, int recordingNum, string fileName, AudioClip audioClip)
    {
        StartCoroutine(UploadAudioToREDCap(participantID, recordingNum, fileName, audioClip));
    }

    private IEnumerator UploadAudioToREDCap(int participantID, int recordingNum, string fileName, AudioClip audioClip)
    {
        if (audioClip == null)
        {
            Debug.LogError("AudioClip is null. Cannot upload.");
            yield break;
        }

        // Convert AudioClip to WAV format
        float[] samples = new float[audioClip.samples * audioClip.channels];
        audioClip.GetData(samples, 0);
        byte[] wavData = MicRecorder.ConvertAudioClipToWav(samples, audioClip.channels, audioClip.frequency);

        if (wavData == null)
        {
            Debug.LogError("Failed to convert AudioClip to WAV format.");
            yield break;
        }

        string fieldName = $"recording_{recordingNum}";

        // Add 1 to the participant ID before uploading
        int recordID = participantID;

        WWWForm form = new WWWForm();
        form.AddField("token", apiToken);
        form.AddField("content", "file");
        form.AddField("action", "import");
        form.AddField("record", recordID.ToString());
        form.AddField("field", fieldName);
        form.AddBinaryData("file", wavData, fileName, "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post(apiUrl, form))
        {
            yield return www.SendWebRequest();

            // Print Full Response
            string responseText = www.downloadHandler.text;
            Debug.Log($"REDCap Upload Response: {responseText}");

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error uploading {fileName} to REDCap field {fieldName}: {www.error}");
            }
            else
            {
                Debug.Log($"Successfully uploaded {fileName} to REDCap field {fieldName} (record {recordID}).");
            }
        }
    }







}

public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        string wrappedJson = "{\"items\":" + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrappedJson);
        return wrapper.items;
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public T[] items;
    }
}

// Define model to match REDCap JSON structure
[System.Serializable]
public class RecordSelectionForm // For Unity Control Form (Only in Record ID 1)
{
    public string record_id;
    public string selected_participant_1; // selected participant ID 1
    public string environment_type_1;
    public string ready_1;
    public string selected_participant_2; // selected participant ID 2
    public string environment_type_2;
    public string ready_2;
}

[System.Serializable]
public class RecordParticipantForm // For Participant Data Form (One per participant)
{
    public string record_id;
    public string participant_id;
    public string name;
    public string age;
    public string gender;
    public string recording_0;
    public string recording_1;
    public string recording_2;
    public string recording_3;
    public string recording_4;
    public string recording_5;
    public string recording_6;
    public string recording_7;
    public string recording_8;
}
