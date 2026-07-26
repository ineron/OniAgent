Vendored reference assemblies go here — not committed, copied once per machine
from the Mac install:

    OxygenNotIncluded.app/Contents/Resources/Data/Managed/

Required for `dotnet build` to resolve types:

- `Assembly-CSharp.dll`
- `0Harmony.dll`
- `UnityEngine.dll`
- `UnityEngine.CoreModule.dll`

If build errors mention missing types outside these (e.g. `UnityEngine.InputModule`,
`UnityEngine.TextRenderingModule`), copy the corresponding `UnityEngine.*.dll`
from the same folder and add a matching `<Reference>` to `OniAgent.csproj`.
