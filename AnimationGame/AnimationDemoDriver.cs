using Vecxy.Animations;
using Vecxy.Scene;

namespace AnimationGame;

public sealed class AnimationDemoDriver(Animator animator) : AComponent
{
    private float _elapsed;
    private int _lastSurveyCycle = -1;

    public override void Update(float deltaTime)
    {
        _elapsed += deltaTime;

        // Rest for two seconds, accelerate through Walk to Run, then slow down.
        var cycle = _elapsed % 12.0f;
        var speed = cycle switch
        {
            < 2.0f => 0.0f,
            < 6.0f => (cycle - 2.0f) * 0.75f,
            < 10.0f => (10.0f - cycle) * 0.75f,
            _ => 0.0f
        };

        animator.SetFloat(CharacterAnimation.Speed, speed);
        animator.SetBool(CharacterAnimation.Moving, speed > 0.05f);
        animator.SetInt(CharacterAnimation.Gait, speed > 2.0f ? 2 : speed > 0.05f ? 1 : 0);

        var surveyCycle = (int)(_elapsed / 12.0f);
        if (surveyCycle > _lastSurveyCycle && cycle < deltaTime * 2.0f)
        {
            _lastSurveyCycle = surveyCycle;
            animator.SetTrigger(CharacterAnimation.Survey);
        }
    }
}
