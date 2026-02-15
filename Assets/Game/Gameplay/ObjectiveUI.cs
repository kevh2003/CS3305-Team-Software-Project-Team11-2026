using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ObjectiveUI : MonoBehaviour
{
    [Header("Ducks Objective")]
    [SerializeField] private Toggle ducksToggle;
    [SerializeField] private TMP_Text ducksLabel;
    [SerializeField] private TMP_Text ducksCountText;

    [Header("Ducks Settings")]
    [SerializeField] private int ducksTotal = 6;

    private int ducksFound = 0;
    private bool completed = false;

    private void Awake()
    {
        if (ducksToggle) ducksToggle.interactable = false;
        if (ducksLabel) ducksLabel.text = "Find rubber ducks";

        Refresh();
    }

    private void Refresh()
    {
        bool ducksComplete = ducksFound >= ducksTotal;

        if (ducksToggle)
            ducksToggle.isOn = ducksComplete;

        if (ducksCountText)
            ducksCountText.text = ducksComplete ? $"{ducksTotal}/{ducksTotal}" : $"{ducksFound}/{ducksTotal}";
    }

    public void AddDuck()
    {
        if (completed) return;

        ducksFound++;
        Refresh();

        if (ducksFound >= ducksTotal)
        {
            completed = true;
            StartCoroutine(HideAfterDelay());
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        gameObject.SetActive(false);
    }
}
