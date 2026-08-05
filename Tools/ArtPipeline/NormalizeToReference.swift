import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

func fail(_ message: String) -> Never {
    FileHandle.standardError.write(Data(("NormalizeToReference: " + message + "\n").utf8))
    exit(1)
}

guard CommandLine.arguments.count == 4 else {
    fail("usage: swift NormalizeToReference.swift <input-256.png> <reference-256.png> <output.png>")
}

func load(_ path: String) -> CGImage {
    guard let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil),
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil),
          image.width == 256, image.height == 256 else { fail("expected a 256x256 PNG: \(path)") }
    return image
}

func alphaBounds(_ image: CGImage) -> CGRect {
    var pixels = [UInt8](repeating: 0, count: 256 * 256 * 4)
    let ok = pixels.withUnsafeMutableBytes { bytes -> Bool in
        guard let context = CGContext(data: bytes.baseAddress, width: 256, height: 256,
                                      bitsPerComponent: 8, bytesPerRow: 256 * 4,
                                      space: CGColorSpaceCreateDeviceRGB(),
                                      bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { return false }
        context.draw(image, in: CGRect(x: 0, y: 0, width: 256, height: 256))
        return true
    }
    guard ok else { fail("cannot read alpha") }
    var minX = 256, minY = 256, maxX = -1, maxY = -1
    for y in 0..<256 { for x in 0..<256 where pixels[(y * 256 + x) * 4 + 3] > 8 {
        minX = min(minX, x); minY = min(minY, y); maxX = max(maxX, x); maxY = max(maxY, y)
    }}
    guard maxX >= minX, maxY >= minY else { fail("sprite has no visible pixels") }
    return CGRect(x: minX, y: minY, width: maxX - minX + 1, height: maxY - minY + 1)
}

let input = load(CommandLine.arguments[1])
let reference = load(CommandLine.arguments[2])
let sourceBounds = alphaBounds(input)
let referenceBounds = alphaBounds(reference)
let scale = referenceBounds.height / sourceBounds.height
let destinationX = referenceBounds.midX - sourceBounds.midX * scale
// Pixel buffers count Y from the top, while CoreGraphics destinations count from the bottom.
// Align the visible foot baseline in bottom-origin coordinates.
let sourceBottom = 256 - sourceBounds.maxY
let referenceBottom = 256 - referenceBounds.maxY
let destinationY = referenceBottom - sourceBottom * scale

guard let context = CGContext(data: nil, width: 256, height: 256, bitsPerComponent: 8,
                              bytesPerRow: 256 * 4, space: CGColorSpaceCreateDeviceRGB(),
                              bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { fail("cannot create canvas") }
context.clear(CGRect(x: 0, y: 0, width: 256, height: 256))
context.interpolationQuality = .none
context.draw(input, in: CGRect(x: destinationX, y: destinationY,
                               width: 256 * scale, height: 256 * scale))
guard let result = context.makeImage(),
      let destination = CGImageDestinationCreateWithURL(URL(fileURLWithPath: CommandLine.arguments[3]) as CFURL,
                                                        UTType.png.identifier as CFString, 1, nil) else {
    fail("cannot create output")
}
CGImageDestinationAddImage(destination, result, nil)
guard CGImageDestinationFinalize(destination) else { fail("cannot write output") }
print("Normalized to reference height \(Int(referenceBounds.height)) px, scale \(String(format: "%.3f", scale)).")
