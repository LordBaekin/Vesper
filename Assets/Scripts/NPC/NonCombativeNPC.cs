// Assets/Scripts/NPC/NonCombativeNPC.cs
using UnityEngine;

public class NonCombativeNPC : BaseNPC
{
    private int nextLine = 0;

    protected override void NonCombativeTick()
    {
        // TODO: idle/wander or wait for player interaction
    }

    protected override void CombativeTick()
    {
        // never called for non-combative NPCs
    }

    public void Speak()
    {
        if (data.dialogueLines == null || data.dialogueLines.Length == 0)
            return;

        Debug.Log($"{data.displayName}: {data.dialogueLines[nextLine]}");
        nextLine = (nextLine + 1) % data.dialogueLines.Length;
    }
}