"""Check the running development host's retained APIs and retired routes."""

import argparse
import json
import struct
import urllib.error
import urllib.parse
import urllib.request
import zlib
from collections.abc import Callable


def fetch(base_url: str, path: str) -> tuple[int, bytes]:
    try:
        with urllib.request.urlopen(urllib.parse.urljoin(base_url, path), timeout=30) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()


def verify_png(data: bytes) -> None:
    assert data.startswith(b"\x89PNG\r\n\x1a\n")
    chunks: list[tuple[bytes, bytes]] = []
    offset = 8
    while offset < len(data):
        size = struct.unpack_from(">I", data, offset)[0]
        kind = data[offset + 4 : offset + 8]
        content = data[offset + 8 : offset + 8 + size]
        checksum = struct.unpack_from(">I", data, offset + 8 + size)[0]
        assert zlib.crc32(kind + content) == checksum
        chunks.append((kind, content))
        offset += size + 12

    assert chunks[-1][0] == b"IEND" and offset == len(data)
    width, height, depth, color, compression, filtering, interlace = struct.unpack(
        ">IIBBBBB", chunks[0][1]
    )
    assert (width, height, depth, color, compression, filtering, interlace) == (64, 48, 8, 6, 0, 0, 0)
    pixels = zlib.decompress(b"".join(content for kind, content in chunks if kind == b"IDAT"))
    stride = width * 4
    assert len(pixels) == (stride + 1) * height
    previous = bytearray(stride)
    colors: set[bytes] = set()
    for y in range(height):
        start = y * (stride + 1)
        mode = pixels[start]
        row = bytearray(pixels[start + 1 : start + 1 + stride])
        assert mode in range(5)
        for x in range(stride):
            left = row[x - 4] if x >= 4 else 0
            above = previous[x]
            upper_left = previous[x - 4] if x >= 4 else 0
            predict = left + above - upper_left
            distances = (abs(predict - left), abs(predict - above), abs(predict - upper_left))
            paeth = (left, above, upper_left)[distances.index(min(distances))]
            row[x] = (row[x] + (0, left, above, (left + above) // 2, paeth)[mode]) & 255
        colors.update(bytes(row[x : x + 4]) for x in range(0, stride, 4))
        previous = row
    assert len(colors) > 1, "GPU produced an empty/uniform frame"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("base_url", help="Running development host URL, e.g. http://127.0.0.1:58637")
    args = parser.parse_args()

    retained: dict[str, Callable[[bytes], bool]] = {
        "/health": lambda body: body == b"Healthy",
        "/": lambda body: b"Welcome" in body,
        "/api/Mesh/Cube/meta": lambda body: json.loads(body)["indexCount"] == 36,
        "/api/Mesh/Cube/vertex": lambda body: len(body) > 0,
        "/api/ApiGen/webgpu/native/enum/name": lambda body: len(json.loads(body)) > 0,
        "/api/DotnetReflection/entry-type": lambda body: len(json.loads(body)) > 0,
        "/ILSL/compile/MinimumTriangle/wgsl": lambda body: b"@fragment" in body,
        "/ILSL": lambda body: b"ILSL Development" in body and b"/js/dist/client.css" in body,
        "/js/dist/client.css": lambda body: b".monaco-editor" in body,
        "/render/repl": lambda body: b"REPL-Style Renderer" in body,
        "/home/volume": lambda body: b"Volume Rendering" in body,
        "/swagger/v1/swagger.json": lambda body: b"openapi" in body,
    }
    for path, check in retained.items():
        status, body = fetch(args.base_url, path)
        assert status == 200 and check(body), f"{path}: HTTP {status}, {body[:100]!r}"
        print(f"{path}: 200")

    path = "/render/cube?width=64&height=48"
    status, body = fetch(args.base_url, path)
    assert status == 200, f"{path}: HTTP {status}, {body[:100]!r}"
    verify_png(body)
    print(f"{path}: 200, decoded 64x48 non-uniform PNG")

    retired = (
        "/home/desktop",
        "/home/webview2",
        "/api/SignalConnection/server",
        "/api/ServerConnection/client",
        "/api/WebViewInterop",
        "/hub/signal-connection",
        "/ws/signal-connection/00000000-0000-0000-0000-000000000000",
        "/js/browserclient-interop.js",
    )
    for path in retired:
        status, _ = fetch(args.base_url, path)
        assert status == 404, f"{path}: expected 404, got {status}"
        print(f"{path}: 404")


if __name__ == "__main__":
    main()
