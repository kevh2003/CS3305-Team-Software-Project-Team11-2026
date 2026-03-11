using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class StartGame : NetworkBehaviour, IWifiInteractable
{
    [Header("Player Vars")]
    private CharacterController _player;
    private Canvas _miniGameCanvas = null;
    private float _lookSensitivity;

    [Header("Wifi Game Status")]
    public bool completed = false;
    private const int _numberOfParts = 4; // the number of wifi components that need to be locked


    [Header("Interactable")]
    private string text = "Press E";
    public string InteractText => text;


    public void Interact(CharacterController player)
    {
        if (_miniGameCanvas) return;

        // getting the mini game canvas from the player prefab
        Canvas[] canvas = player.GetComponentsInChildren<Canvas>(true); 
        foreach (Canvas can in canvas)
        {
            if (can.name == "Mini Game Canvas")
            {
                _miniGameCanvas = can;
                break;
            }
        }

        if (_miniGameCanvas == null)
            return;

        _miniGameCanvas.gameObject.SetActive(true);

        // show the cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // lock the player camera
        NetworkPlayer nPlayer = player.gameObject.GetComponent<NetworkPlayer>();
        if (nPlayer != null)
        {
            _lookSensitivity = nPlayer.LookSensitivity;
            nPlayer.SetLookSensitivity(0f);
        }

        // reset the player string
        text = "Press Q to quit";

        _player = player;

    }

    private void Update()
    {

        // checking if the player quits
         if (_miniGameCanvas != null
            && Keyboard.current != null
            && Keyboard.current.qKey.wasPressedThisFrame)
        {
            Quit();
        }


        // checking if it is complete
        if (_miniGameCanvas)
        {
            int counter = 0;

            Image[] images = _miniGameCanvas.GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                DraggingBehaviour drag = image.GetComponent<DraggingBehaviour>();
                if (!drag)
                {
                    continue;
                } 
                else if (drag.locked)
                {
                    counter++;
                }
            }


            if (counter >= _numberOfParts)
            {
                completed = true;
            }
        }

    }
    

    
    private void Quit()
    {
        if (_miniGameCanvas == null)
            return;

        // hide the canvas
        ResetCanvas();
        _miniGameCanvas.gameObject.SetActive(false);
        _miniGameCanvas = null;

        // show the cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // reset the camera sensitivity
        if (_player != null)
        {
            NetworkPlayer networkPlayer = _player.gameObject.GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
                networkPlayer.SetLookSensitivity(_lookSensitivity);
        }

        // update the interact text
        text = "Press E";


        if (completed)
        {
            // disable the floor collision
            GetComponent<Collider>().enabled = false;

            // change the model
            Transform[] objects = transform.parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform obj in objects)
            { 
                Debug.Log("Object: " + obj.name);
                if (obj.name == "mini-game-complete")
                {
                    Debug.Log("Activating complete model");
                    obj.gameObject.SetActive(true);
                }
                else if (obj.name == "mini-game-incomplete")
                {  
                    Debug.Log("Deactivating incomplete model");
                    obj.gameObject.SetActive(false);
                }
            }
        }

    }


    private void ResetCanvas()
    {
        // method to reset the player canvas
        float width = _miniGameCanvas.gameObject.GetComponent<RectTransform>().rect.width;
        float height = _miniGameCanvas.gameObject.GetComponent<RectTransform>().rect.height;

        Image[] images = _miniGameCanvas.GetComponentsInChildren<Image>(true);
        foreach (Image img in images)
        {
            DraggingBehaviour drag = img.GetComponent<DraggingBehaviour>();
            if (drag == null)
            {
                continue;
            }
            else if (drag.locked)
            {
                drag.locked = false;

                float x = Random.Range(0, (int)width) / 2;
                float y = Random.Range(0, (int)height) / 2;
             
                img.gameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
                img.gameObject.GetComponent<CanvasGroup>().blocksRaycasts = true;

            }
        }
    }
}
