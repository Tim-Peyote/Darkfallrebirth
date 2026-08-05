import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

func fail(_ message: String) -> Never {
    FileHandle.standardError.write(Data(("RemoveLightChecker: " + message + "\n").utf8))
    exit(1)
}

guard CommandLine.arguments.count == 3 else {
    fail("usage: swift RemoveLightChecker.swift <source.png> <alpha-output.png>")
}
let input = CommandLine.arguments[1]
let output = CommandLine.arguments[2]
guard let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: input) as CFURL, nil),
      let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else { fail("cannot decode input") }

let width = image.width
let height = image.height
var pixels = [UInt8](repeating: 0, count: width * height * 4)
guard pixels.withUnsafeMutableBytes({ bytes in
    guard let context = CGContext(data: bytes.baseAddress, width: width, height: height,
                                  bitsPerComponent: 8, bytesPerRow: width * 4,
                                  space: CGColorSpaceCreateDeviceRGB(),
                                  bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { return false }
    context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
    return true
}) else { fail("cannot render input") }

for index in stride(from: 0, to: pixels.count, by: 4) {
    let red = Int(pixels[index])
    let green = Int(pixels[index + 1])
    let blue = Int(pixels[index + 2])
    let spread = max(red, green, blue) - min(red, green, blue)
    // Image generation preview alpha is represented by two very light neutral checker tones.
    // The character has no near-white neutral fills; keep all coloured highlights intact.
    if min(red, green, blue) >= 180 && spread <= 14 {
        pixels[index] = 0
        pixels[index + 1] = 0
        pixels[index + 2] = 0
        pixels[index + 3] = 0
    }
}

guard let context = CGContext(data: &pixels, width: width, height: height,
                              bitsPerComponent: 8, bytesPerRow: width * 4,
                              space: CGColorSpaceCreateDeviceRGB(),
                              bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue),
      let result = context.makeImage() else { fail("cannot create output") }
guard let destination = CGImageDestinationCreateWithURL(URL(fileURLWithPath: output) as CFURL,
                                                        UTType.png.identifier as CFString, 1, nil) else {
    fail("cannot create output")
}
CGImageDestinationAddImage(destination, result, nil)
guard CGImageDestinationFinalize(destination) else { fail("cannot write output") }
