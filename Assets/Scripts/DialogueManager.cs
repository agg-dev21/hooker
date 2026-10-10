using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Ek line: kaun bol raha hai, aur kya bol raha hai
[Serializable]
public class DialogueLine
{
    public string speaker;
    [TextArea(2, 5)] public string text;
}

// Scene mein sirf EK DialogueManager hona chahiye. Ye dialogue box dikhata hai.
// E dabao = agli line. Last line ke baad box band.
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] GameObject panel;     // poora dialogue box
    [SerializeField] TMP_Text speakerText; // bolne wale ka naam
    [SerializeField] TMP_Text bodyText;    // dialogue ka text

    public bool IsOpen { get; private set; }

    List<DialogueLine> lines;
    int index;
    int openedFrame;

    void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);
    }

    public void StartDialogue(List<DialogueLine> newLines)
    {
        if (IsOpen || newLines == null || newLines.Count == 0) return;

        lines = newLines;
        index = 0;
        IsOpen = true;
        openedFrame = Time.frameCount;

        if (panel) panel.SetActive(true);
        ShowLine();
    }

    void Update()
    {
        if (!IsOpen || Time.frameCount == openedFrame) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            index++;
            if (index >= lines.Count)
            {
                IsOpen = false;
                if (panel) panel.SetActive(false);
            }
            else
            {
                ShowLine();
            }
        }
    }

    void ShowLine()
    {
        if (speakerText) speakerText.text = lines[index].speaker;
        if (bodyText) bodyText.text = lines[index].text;
    }
}
