using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.IO;
public class NPC : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField]private string text;

    private int message_index = 0;
    private bool display_text = false;
    private TextMeshProUGUI text_box;

  

    // Update is called once per frame
    void FixedUpdate()
    {

        // looping over the text message 
        if(display_text && message_index < text.Length)
        {
            text_box.text = text[..message_index];
            message_index ++;
        }
        
    }

    private void OnTriggerEnter(Collider other)
    
    {
        NetworkObject net_obj = other.GetComponent<NetworkObject>();
        if(!net_obj.IsOwner) return;
        
        
    
        if(other.GetComponent<CharacterController>() != null)
        {

            Canvas canvas = other.GetComponentInChildren<Canvas>(true);
            canvas.gameObject.SetActive(true);
            text_box = canvas.GetComponentInChildren<TextMeshProUGUI>();
            display_text = true;

        }
    }

    private void OnTriggerExit(Collider other)
    {

        NetworkObject net_obj = other.GetComponent<NetworkObject>();
        if(!net_obj.IsOwner) return;
        
        
        if(other.GetComponent<CharacterController>() != null)
        {
            Canvas canvas = other.GetComponentInChildren<Canvas>(true);
            canvas.gameObject.SetActive(false);
            text_box.text = "";
            display_text = false;
            message_index = 0;



        }


    }


}
