import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

func fail(_ message: String) -> Never {
    FileHandle.standardError.write(Data(("RemoveChroma: " + message + "\n").utf8))
    exit(1)
}

guard CommandLine.arguments.count == 3 else {
    fail("usage: swift RemoveChroma.swift <magenta-source.png> <alpha-output.png>")
}
let input = CommandLine.arguments[1]
let output = CommandLine.arguments[2]
guard let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: input) as CFURL, nil),
      let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else { fail("cannot decode input") }

let width = image.width
let height = image.height
var pixels = [UInt8](repeating: 0, count: width * height * 4)
let rendered = pixels.withUnsafeMutableBytes { bytes -> Bool in
    guard let context = CGContext(data: bytes.baseAddress, width: width, height: height,
                                  bitsPerComponent: 8, bytesPerRow: width * 4,
                                  space: CGColorSpaceCreateDeviceRGB(),
                                  bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { return false }
    context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
    return true
}
guard rendered else { fail("cannot render input") }

let transparentDistance = 22.0
let opaqueDistance = 150.0
for index in stride(from: 0, to: pixels.count, by: 4) {
    let red = Double(pixels[index])
    let green = Double(pixels[index + 1])
    let blue = Double(pixels[index + 2])
    let distance = sqrt(pow(red - 255.0, 2) + pow(green, 2) + pow(blue - 255.0, 2))
    let linear = max(0.0, min(1.0, (distance - transparentDistance) / (opaqueDistance - transparentDistance)))
    let distanceAlpha = linear * linear * (3.0 - 2.0 * linear)
    // Generated antialiasing can contain dark magenta pixels far from the bright key in RGB distance.
    // Detect the key hue as well, while preserving the hero's muted low-saturation purple cloth.
    let magentaDominance = min(red, blue) - green
    let redBlueBalance = abs(red - blue)
    let hueAlpha = redBlueBalance < 72.0
        ? max(0.0, min(1.0, (72.0 - magentaDominance) / 28.0))
        : 1.0
    let alpha = min(distanceAlpha, hueAlpha)
    if alpha <= 0.001 {
        pixels[index] = 0; pixels[index + 1] = 0; pixels[index + 2] = 0; pixels[index + 3] = 0
        continue
    }
    // Suppress magenta spill only on semi-transparent matte pixels.
    let edge = 1.0 - alpha
    let neutral = max(green, min(red, blue) * 0.42)
    let cleanRed = red * (1.0 - edge * 0.72) + neutral * edge * 0.72
    let cleanBlue = blue * (1.0 - edge * 0.72) + neutral * edge * 0.72
    pixels[index] = UInt8(max(0, min(255, cleanRed * alpha)))
    pixels[index + 1] = UInt8(max(0, min(255, green * alpha)))
    pixels[index + 2] = UInt8(max(0, min(255, cleanBlue * alpha)))
    pixels[index + 3] = UInt8(max(0, min(255, 255.0 * alpha)))
}

guard let context = CGContext(data: &pixels, width: width, height: height,
                              bitsPerComponent: 8, bytesPerRow: width * 4,
                              space: CGColorSpaceCreateDeviceRGB(),
                              bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue),
      let result = context.makeImage() else { fail("cannot create alpha image") }
let outputURL = URL(fileURLWithPath: output)
guard let destination = CGImageDestinationCreateWithURL(outputURL as CFURL, UTType.png.identifier as CFString, 1, nil) else {
    fail("cannot create output")
}
CGImageDestinationAddImage(destination, result, nil)
guard CGImageDestinationFinalize(destination) else { fail("cannot write output") }
