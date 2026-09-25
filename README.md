# Voxta Game Engine SDK

The official Unity package for connecting games to a [Voxta](https://voxta.ai)
server. It provides runtime components for device authorization, chat,
microphone input, Unity-owned reply audio, and server-triggered game actions.

Requires Unity 2022.3 LTS or later. CI currently validates Unity 2022.3.62f3. The current package version is
`0.1.0-beta.1`.

## Install

In Unity, open **Window > Package Manager**, choose **+ > Add package from git
URL**, and enter:

```
https://github.com/voxta-ai/voxta-game-engine-sdk.git?path=/unity
```

For a local checkout, use **Add package from disk** and select
`unity/package.json`.

## Get started

Import the **Basic Chat Integration** sample from the package's **Samples** tab.
It demonstrates device authorization, text chat, microphone streaming, spatial
reply audio, and a `wave` game-action callback.

Read the package [quickstart](unity/README.md), the
[public API migration guide](unity/Documentation~/runtime-model-public-api-migration.md),
and the [runtime dependency notes](unity/Documentation~/third-party-dependencies.md).

## Contributing

Maintainer plans, dependency-closure records, and protocol implementation notes
are in [docs/maintainers/unity-sdk](docs/maintainers/unity-sdk/). The Unity
package itself is under [unity](unity/).

## License

See the package [license](unity/LICENSE.md). `Voxta.Model` is shipped as a
runtime dependency under its own license notice in
`unity/Runtime/Plugins/Voxta/licenses/`.
