using System;

public interface IInteractionPromptSource
{
    string CurrentPrompt { get; }

    event Action<string> PromptChanged;
}
