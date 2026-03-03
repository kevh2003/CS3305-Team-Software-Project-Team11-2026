//used for 'hold e' interactions

public interface IHoldInteractable
{
    // called every frame while the player is looking at it (owner only)
    void HoldTick(Interactor interactor, bool isHolding, float dt);

    // force reset if interactor looks away
    void HoldCancel(Interactor interactor);
}