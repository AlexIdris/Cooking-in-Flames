using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CustomerOrderDisplay : MonoBehaviour
{
    public TextMeshPro orderText;
    public Image orderIcon;

    public Image angerFill;
    float anger = 1f;
    public float drainSpeed = 0.2f;

    public GameObject speechBubble;

    public float typingSpeed = 0.05f;
    private Coroutine typingCoroutine;

    private CustomerMover2 mover;
    private CustomerSpawner2 spawner;

    public void Init(CustomerSpawner2 s, CustomerMover2 m)
    {
        spawner = s;
        mover = m;

        anger = 1f;
        if (angerFill != null)
            angerFill.fillAmount = 1f;

        // Subscribe to event
        mover.OnReachPoint += DisplayOrderTextLetterByLetter;
    }

    void Update()
    {
        if (spawner == null || mover == null) return;

        bool isFront = (spawner.customers.Count > 0 && spawner.customers[0] == mover);
        bool showUI = isFront && mover.hasReachedPoint;

        // ✅ Show UI ONLY when customer reached front
        orderText.gameObject.SetActive(showUI);
        if (orderIcon != null) orderIcon.gameObject.SetActive(showUI);
        if (speechBubble != null) speechBubble.SetActive(showUI);

        if (showUI)
        {
            // ✅ Set icon ONLY once when needed
            if (orderIcon != null && orderIcon.sprite == null)
            {
                orderIcon.sprite = spawner.GetFoodIcon(mover.orderedFood);
            }

            // ✅ Anger draining
            anger -= drainSpeed * Time.deltaTime;
            anger = Mathf.Clamp01(anger);

            if (angerFill != null)
                angerFill.fillAmount = anger;

            if (anger <= 0f)
            {
                mover.FailOrder();
            }
        }
        else
        {
            // reset typing if needed
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
                orderText.text = "";
            }

            // optional: reset icon so next customer updates correctly
            if (orderIcon != null)
                orderIcon.sprite = null;
        }
    }

    public void DisplayOrderTextLetterByLetter(CustomerMover2 mover)
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeText(mover.orderedFood.ToString()));
    }

    private IEnumerator TypeText(string fullText)
    {
        string readable = System.Text.RegularExpressions.Regex.Replace(fullText, "(\\B[A-Z])", " $1");

        orderText.text = "";
        foreach (char c in readable)
        {
            orderText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }
        typingCoroutine = null;
    }

    public void ClearDisplay()
    {
        if (orderText != null) orderText.text = "";
        if (orderIcon != null) orderIcon.enabled = false;
    }
}