using System.Collections.Generic;

// Part of EndingGrave (see EndingGrave.cs for its settings and the main loop).
// Everything that is said in the ending: Mom's letter and every dialogue line.
public partial class EndingGrave
{
    // ---------------- The words ----------------

    static readonly string[] Letter =
    {
        "My little Raphael,\n\n" +
        "If you're reading this, you found me. I told you that you'd understand someday. " +
        "I hope you don't understand all of it.\n\n" +
        "I wasn't a good mother. I left because I was scared. Not of you. Of staying. " +
        "Everyone else, I could sail away from. You, I couldn't. So I ran from myself and called it freedom.",

        "They'll have told you stories about me. Most of them are true.\n\n" +
        "Hardy wrote me terrible poems. Oscar kept everything I ever left behind. " +
        "Madame O loved me more than I deserved.\n\n" +
        "And KK... KK made me laugh, and he never once asked me to stay. " +
        "I should have asked him to.",

        "They call you the Flaw of the Seven Seas. Don't you dare believe them.\n\n" +
        "I followed you from far away, more than you know. Every port you sailed into, " +
        "I heard about the boy with the hook who wouldn't give up.\n\n" +
        "You were never my mistake, sweetheart. You were the only thing I ever got right.",

        "The ring is my wedding ring. Nobody ever put it on my finger. " +
        "I bought it myself, the day you were born, and promised I'd give it to the one who stays.\n\n" +
        "That's you. You stayed, even after I didn't.\n\n" +
        "Don't keep it in a drawer. Give it to someone who will stay with you. " +
        "And when you find them, stay too. Don't be like me.",

        "I'm sorry. I love you. I always did.\n\n" +
        "Mom\n\n\n" +
        "P.S. Your father still owes me eleven fish. Collect them."
    };

    List<DialogueLine> Opening() => L(
        R, "The cliff. Just like KK said.",
        R, "...");

    List<DialogueLine> AtGrave() => L(
        R, "Hi, Mom.",
        R, "You said, find me when you're ready.",
        R, "I don't think I was ever going to be ready. I came anyway.",
        R, "There's a letter. It has my name on it.");

    List<DialogueLine> AfterLetter() => L(
        R, "...",
        R, "Eleven fish. Of course.",
        R, "And her ring. She kept it all these years.");

    List<DialogueLine> ArielArrives() => L(
        A, "Raphael?",
        A, "I waited at the bottom of the path. I thought you'd want to be alone. But you've been up here a long time.",
        R, "She left me a letter. And this.",
        A, "...Her ring.",
        A, "Are you okay?",
        R, "No. But I think I'm going to be.");

    List<DialogueLine> BeforeProposal() => L(
        R, "She said to give it to someone who will stay.",
        R, "You followed me across Hardy's ship, a poison tower, a sunken prison, and a hallway full of water. You never once asked me to turn back.",
        A, "You never once let me fall behind.",
        R, "I spent my whole life looking for where I came from.",
        R, "Turns out I was looking in the wrong direction. The answer was walking right next to me.");

    // After the player presses E to propose
    List<DialogueLine> Proposal() => L(
        "", "(Raphael kneels in the grass, the ring held out in his open hand.)",
        R, "Ariel. I don't know how to be anyone's family. Nobody ever showed me how.",
        R, "But I want to learn it. With you.",
        R, "Will you marry me?",
        A, "...",
        A, "You absolute idiot. You proposed at your mother's grave.",
        R, "She'd have wanted a front-row seat.",
        A, "I have only know you for a few days.",
        A, "Fine... I'll marry you.",
        A, "Now get up. The ground is wet, and you're crying on your mother's flowers.",
        R, "...Worth it.");

    List<DialogueLine> Alone() => L(
        R, "Someone who will stay...",
        R, "I'll keep it safe, Mom. Not in a drawer. Someday.",
        R, "I promise.");

    List<DialogueLine> Goodbye(bool withAriel) => withAriel
        ? L(A, "Come on. Let's go home.", R, "Home?", A, "Wherever we're going. Together. That's home now.", R, "Bye, Mom.")
        : L(R, "Bye, Mom.", R, "Thanks for waiting for me.");

    static List<DialogueLine> L(params string[] pairs)
    {
        var list = new List<DialogueLine>();
        for (int i = 0; i + 1 < pairs.Length; i += 2) list.Add(new DialogueLine { speaker = pairs[i], text = pairs[i + 1] });
        return list;
    }
}