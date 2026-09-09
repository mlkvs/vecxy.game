# Vecxy.Animations demo

This project demonstrates a code-first animation controller without an editor.
The Fox GLB contains one skeleton and three clips. `CharacterAnimation.cs` builds
the state machine; `AnimationDemoDriver.cs` drives float, bool, int, and trigger
parameters.

The demo intentionally uses in-place visual animation and leaves root motion
disabled. It also subscribes to an animation event placed halfway through the
Survey state.

Build without launching:

```powershell
dotnet build AnimationGame/AnimationGame.csproj
```
