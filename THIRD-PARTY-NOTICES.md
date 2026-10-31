# Third-party rendering code

The active shader native/singularity.hlsl is an HLSL adaptation of the
Schwarzschild path and disk shading in GreenScreen410/singularity:
https://github.com/GreenScreen410/singularity
Upstream commit: d5703c233ce3481915784b321454e5f572cccfa1
Original file: src/singularity.wgsl. MIT license, copyright 2026 GreenScreen410.

That shader credits s0xDk/ghostty-blackhole:
https://github.com/s0xDk/ghostty-blackhole
MIT license, copyright 2026 s13k <s13k@pm.me>.

Both projects credit Eric Bruneton's black-hole rendering research:
https://ebruneton.github.io/black_hole_shader/

Adaptations: WGSL to HLSL syntax; existing Direct3D 11 bindings; fixed widget
position and footprint fade; bottom-left to top-right roll; legacy theme tint;
2x2 supersampling. Kerr tracing, procedural stars, drift and network checking
are not included. The desktop capture and presentation pipeline remains the
BlackHoleBin implementation; it is not the upstream zero-copy Rust pipeline.

MIT License

Copyright (c) 2026 GreenScreen410
Copyright (c) 2026 s13k <s13k@pm.me>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
