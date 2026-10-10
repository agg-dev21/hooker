using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// One line: who is speaking and what they say
[Serializable]
public class DialogueLine
{
    public string speaker;
    [TextArea(2, 5)] public string text;
}

// There should be only ONE DialogueManager in a scene. It shows the dialogue box.
// Press E for the next line. The box closes after the last line.
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] GameObject panel;     // the whole dialogue box
    [SerializeField] TMP_Text speakerText; // speaker's name
    [SerializeField] TMP_Text bodyText;    // dialogue text

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
