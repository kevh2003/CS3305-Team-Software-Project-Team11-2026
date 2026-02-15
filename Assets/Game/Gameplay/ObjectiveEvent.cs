using System;

/*
 * ObjectiveEvent
 * 
 * This is simply stating that an objective has been completed
 * There is no state involved yet, only the fact that the objective has been completed.
 */

public static class ObjectiveEvent
{
    public static Action OnObjectiveCompleted;
}