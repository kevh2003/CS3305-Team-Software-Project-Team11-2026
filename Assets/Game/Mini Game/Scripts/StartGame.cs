using UnityEngine;

public class StartGame : MonoBehaviour
{
    private Canvas miniGameCanvas;
    private float lookSensitivity;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Canvas[] canvas = other.gameObject.GetComponentsInChildren<Canvas>(true); // need to the correct canvas
            Debug.Log("Found " + canvas.Length + " canvases in player children.");

            foreach (Canvas c in canvas)
            {
                Debug.Log(c.name);
                if (c.name == "Mini Game Canvas")
                {
                    miniGameCanvas = c;
                }
            }

          
           miniGameCanvas.gameObject.SetActive(true);

            // show the cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // lock the player camera
            NetworkPlayer player = other.gameObject.GetComponent<NetworkPlayer>();
            lookSensitivity = player.lookSensitivity; // keep track of the original look sensitivity to reset it later
            player.lookSensitivity = 0;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            miniGameCanvas.gameObject.SetActive(false);

            // show the cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // lock the player camera
            NetworkPlayer player = other.gameObject.GetComponent<NetworkPlayer>();
            player.lookSensitivity = lookSensitivity;

        }
    }
}
