# USD scenes and prototype spawning

The optional engine module lives in
[Vecxy.Usd](../Engine/Vecxy/Code/Vecxy.Usd/README.md).
Its README documents supported schemas, preparation, packaging, markers and tests.

Horror is a small authoring example: a floor and two instances of one washer.
`Assets/Prototypes/Sandbox.prototype` creates the USD scene through `IPrototypes`.
`MainScene` uses the `spawn` marker to place the player and adds independent rain.
Geometry, component colliders and lights come from USD.

Open [Sandbox.blend](../Horror/Authoring/Sandbox.blend). Follow the
[authoring guide](../Horror/Authoring/README.md) for the hierarchy, shared models,
instance overrides, C# component attributes and the edit/export loop.
