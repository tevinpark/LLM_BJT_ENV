using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;

public class REDCapFileUploader : MonoBehaviour
{
    private string apiUrl = "https://redcap.vumc.org/api/";
    private string apiToken = "279376F21084ED4F7EEB1F40BDFD1A03";

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
