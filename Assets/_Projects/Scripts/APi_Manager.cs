using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class APi_Manager : MonoBehaviour
{
    public string getLatestUrl = "http://localhost:5297/robot/get-latest";
    public string moveResetUrl = "http://localhost:5297/robot/move?direction=none";
    void Start()
    {
        StartCoroutine(PollRobotMovement());
    }

    IEnumerator PollRobotMovement()
    {
        while (true)
        {
            // 1. Send GET request to get latest direction
            UnityWebRequest getRequest = UnityWebRequest.Get(getLatestUrl);
            yield return getRequest.SendWebRequest();

            if (getRequest.result == UnityWebRequest.Result.Success)
            {
          
                string json = getRequest.downloadHandler.text;
                string direction = JsonUtility.FromJson<DirectionResponse>(json).direction;
                Debug.Log($"Received command: {direction}");
                if (!string.IsNullOrEmpty(direction) && direction != "none")
                {
                    MoveRobot(direction);

                    // 2. After moving, reset the command
                    UnityWebRequest resetRequest = UnityWebRequest.PostWwwForm(moveResetUrl, "");
                    yield return resetRequest.SendWebRequest();
                }
            }
            else
            {
                Debug.LogError("Failed to get latest move: " + getRequest.error);
            }

            yield return new WaitForSeconds(0.05f); // Poll every 0.1 second
        }
    }


    public float moveSpeed = 2f;
    void MoveRobot(string direction)
    {   
        Vector3 move = Vector3.zero;

        if (direction == "forward") move = Vector3.back;
        else if (direction == "backward") move = Vector3.forward;
        else if (direction == "left") move = Vector3.left;
        else if (direction == "right") move = Vector3.right;
        else if (direction == "raise") move = Vector3.up; 
        else if (direction == "down") move = Vector3.down;

        transform.Translate(move * moveSpeed * Time.deltaTime);
    }

    [System.Serializable]
    public class DirectionResponse
    {
        public string direction;
    }
}
