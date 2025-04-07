using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;

public class REDCapFileUploader : MonoBehaviour
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

    // Call this function to upload a WAV file
    public void UploadWavFile(string recordID, string fieldName, string filePath)
    {
        StartCoroutine(UploadFileToREDCap(recordID, fieldName, filePath));
    }

    private IEnumerator UploadFileToREDCap(string recordID, string fieldName, string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"File not found: {filePath}");
            yield break;
        }

        byte[] fileData = File.ReadAllBytes(filePath);
        string fileName = Path.GetFileName(filePath);

        WWWForm form = new WWWForm();
        form.AddField("token", apiToken);
        form.AddField("content", "file");
        form.AddField("action", "import");
        form.AddField("record", recordID);
        form.AddField("field", fieldName);
        form.AddBinaryData("file", fileData, fileName, "audio/wav"); // WAV file format

        using (UnityWebRequest www = UnityWebRequest.Post(apiUrl, form))
        {
            www.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error uploading file {fileName}: {www.error}");
            }
            else
            {
                Debug.Log($"File {fileName} uploaded successfully to REDCap field: {fieldName}");
            }
        }
    }
}
