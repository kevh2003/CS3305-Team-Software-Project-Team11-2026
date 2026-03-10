using UnityEngine;
using UnityEngine.UI;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;

public class StartGame : MonoBehaviour
{
    private Canvas miniGameCanvas;
    private float lookSensitivity;
    private bool completed = false;

    private void Update()
    {
        // need to check if the all the children components are locked, if they are we can disable the collision and set completed to true
        if(miniGameCanvas)
        {
            int counter = 0;
            int numberOfParts = 4; // the number of wifi components that need to be locked
            Image[] images = miniGameCanvas.GetComponentsInChildren<Image>(true);
            


            foreach (Image image in images)
            {
                DraggingBehaviour drag = image.GetComponent<DraggingBehaviour>();
                if (drag == null)
                {
                    continue;
                }
                else if(drag.locked)
                {
                    counter++;
                }
            }

            if (counter >= numberOfParts)
            {
                completed = true;
               
            }
        }

        Debug.Log("Completed: " + completed);



    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Canvas[] canvas = other.gameObject.GetComponentsInChildren<Canvas>(true); // need to the correct canvas
            Debug.Log("Found " + canvas.Length + " canvases in player children.");

            foreach (Canvas c in canvas)
            {
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

            if(completed)
            {
                // disable the collision so the player can walk through
                GetComponent<Collider>().enabled = false;

                // change the model
                Transform[] objects = GetComponentsInChildren<Transform>(true);
                Debug.Log("Found " + objects.Length + " game objects in mini game children.");
                foreach (Transform obj in objects)
                {
                    Debug.Log("Found object: " + obj.name);

                    if (obj.name == "mini-game-complete")
                    {
                        obj.gameObject.SetActive(true);
                    }
                    else if (obj.name == "mini-game-incomplete")
                    {
                        obj.gameObject.SetActive(false);
                    }
                }

                float width = miniGameCanvas.GetComponent<RectTransform>().rect.width;
                Debug.Log("Canvas width: " + width);
                // reset_canvas();


            }

        }
    }


    private void reset_canvas()
    {
        // method to reset the player canvas
        // ublock all the components in the canvas;
        Image[] images = miniGameCanvas.GetComponentsInChildren<Image>(true);
        foreach(Image img in images)
        {
            DraggingBehaviour drag = img.GetComponent<DraggingBehaviour>();
            if (drag == null)
            {
                continue;
            }
            else if (drag.locked)
            {
                drag.locked = false;
            }
        }



    }
}
