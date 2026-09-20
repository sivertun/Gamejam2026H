using UnityEngine;

public interface ISuckable
{
    void OnSuck(LampSuck lamp);
}

// Stays where it is while the beam is on it and gets drained over time (dead enemies)
public interface IDrainable
{
    void OnDrain(LampSuck lamp, float deltaTime);
}
