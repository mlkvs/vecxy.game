using Vecxy.Animations;
using Vecxy.Rendering;

namespace AnimationGame;

public static class CharacterAnimation
{
    public static readonly AnimatorFloat Speed = new("Speed");
    public static readonly AnimatorBool Moving = new("Moving");
    public static readonly AnimatorInt Gait = new("Gait");
    public static readonly AnimatorTrigger Survey = new("Survey");

    public static AnimatorController Create(Model model)
    {
        var rest = model.GetAnimation("Survey");
        var walk = model.GetAnimation("Walk");
        var run = model.GetAnimation("Run");

        return AnimatorController.Build("Fox Controller", graph =>
        {
            graph.Parameter(Speed);
            graph.Parameter(Moving);
            graph.Parameter(Gait);
            graph.Parameter(Survey);

            graph.Layer("Base", layer =>
            {
                layer.DefaultState("Rest", rest);
                layer.State("Locomotion", new BlendTree1D(Speed)
                    .At(0.0f, walk)
                    .At(1.0f, walk)
                    .At(3.0f, run));
                layer.State("Survey", rest, loop: false)
                    .Event(0.5f, "SurveyPeak");

                layer.From("Rest")
                    .To("Locomotion")
                    .When(Moving, true)
                    .Duration(0.25f);

                layer.From("Locomotion")
                    .To("Rest")
                    .When(Moving, false)
                    .Duration(0.25f);

                layer.AnyState()
                    .To("Survey")
                    .When(Survey)
                    .When(Gait, EAnimatorComparison.Equal, 0)
                    .Priority(100)
                    .Duration(0.15f);

                layer.From("Survey")
                    .To("Locomotion")
                    .When(Moving, true)
                    .AfterNormalizedTime(0.95f)
                    .Duration(0.2f);

                layer.From("Survey")
                    .To("Rest")
                    .When(Moving, false)
                    .AfterNormalizedTime(0.95f)
                    .Duration(0.2f);
            });
        });
    }
}
