Vendored reference assemblies go here — not committed, copied once per machine
from the Mac install:

    OxygenNotIncluded.app/Contents/Resources/Data/Managed/

Required for `dotnet build` to resolve types:

- `Assembly-CSharp.dll`
- `Assembly-CSharp-firstpass.dll` — core Klei framework types (`KMonoBehaviour`,
  `KPrefabID`, `Resource`, `ResourceSet<T>`, `Tag`, etc.) live here, not in
  `Assembly-CSharp.dll`. Confirmed by decompiling the vendored DLL with
  `ilspycmd` (`dotnet tool install -g ilspycmd`) and finding no top-level
  definition for these names — anything touching a duplicant/building
  component needs this assembly too.
- `Newtonsoft.Json.dll` — already bundled with the game itself (Unity's JSON
  package), used here for HTTP response serialization instead of a hand-rolled
  writer.
- `0Harmony.dll`
- `UnityEngine.dll`
- `UnityEngine.CoreModule.dll`

If build errors mention missing types outside these (e.g. `UnityEngine.InputModule`,
`UnityEngine.TextRenderingModule`), copy the corresponding `UnityEngine.*.dll`
from the same folder and add a matching `<Reference>` to `OniAgent.csproj`.
