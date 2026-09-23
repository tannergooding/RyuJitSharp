# RyuJitSharp

A port of the dotnet/runtime RyuJIT compiler to C#.

![ci](https://github.com/tannergooding/ryujitsharp/workflows/ci/badge.svg?branch=main&event=push)

## Table of Contents

* [Code of Conduct](#code-of-conduct)
* [License](#license)
* [Contributing](#contributing)
* [Goals](#goals)
* [Porting RyuJIT](#porting-ryujit)
* [Languages and Frameworks](#languages-and-frameworks)

### Code of Conduct

RyuJitSharp and everyone contributing (this includes issues, pull requests, the
wiki, etc) must abide by the .NET Foundation Code of Conduct:
https://dotnetfoundation.org/about/code-of-conduct.

Instances of abusive, harassing, or otherwise unacceptable behavior may be
reported by contacting the project team.

### License

Copyright © Tanner Gooding and Contributors. Licensed under the MIT License
(MIT). See [LICENSE](../LICENSE.md) in the repository root for more information.

Based on the RyuJIT compiler from dotnet/runtime.
Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

### Contributing

If you are looking to contribute you should read our
[Contributing](CONTRIBUTING.md) documentation.

### Goals

RyuJitSharp is an experiement around writing a .NET JIT in .NET itself. It is initially achieving this by
porting RyuJIT from dotnet/runtime to C#.

### Porting RyuJIT

See the [porting workflow](porting/README.md), [continuation plan](porting/PLAN.md),
and [deviation register](porting/DEVIATIONS.md). Pinned upstream revisions and the
current checkpoint are recorded in [state.json](porting/state.json).
Encountered bugs and deferred improvements are tracked in the
[port findings backlog](porting/BACKLOG.md).

### Languages and Frameworks

RyuJitSharp uses C# as its primary development language and .NET as our primary
target framework.
